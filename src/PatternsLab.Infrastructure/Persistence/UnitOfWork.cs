using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PatternsLab.Application.Claims;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Common;
using PatternsLab.Domain.Patients;
using PatternsLab.Infrastructure.Repositories;

namespace PatternsLab.Infrastructure.Persistence;

/// <summary>
/// One business transaction — handbook §3.6.
///
/// Three things happen here and nowhere else in the application:
///   1. <b>Commit.</b> No repository calls SaveChanges; this type does, once.
///   2. <b>Cross-cutting write concerns.</b> Audit stamps, soft deletes and concurrency
///      tokens are applied to every aggregate, on every write path, forever.
///   3. <b>Domain event dispatch.</b> After the write succeeds — never before.
///
/// Registered <b>scoped</b>: the unit IS the request.
/// </summary>
public sealed class UnitOfWork(
    AppDbContext db,
    IServiceProvider services,
    IDomainEventDispatcher dispatcher,
    IClock clock,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = [];
    private IDbContextTransaction? _transaction;

    public IClaimRepository Claims => Resolve<IClaimRepository>();

    public IPatientRepository Patients => Resolve<IPatientRepository>();

    /// <summary>
    /// Generic repositories are constructed directly against <em>this</em> context, which is
    /// what guarantees every repository in the unit shares one change tracker (§3.1).
    /// </summary>
    public IRepository<T> Repository<T>() where T : Entity
    {
        if (_repositories.TryGetValue(typeof(T), out var cached))
            return (IRepository<T>)cached;

        var repository = new Repository<T>(db);
        _repositories[typeof(T)] = repository;
        return repository;
    }

    private TRepository Resolve<TRepository>() where TRepository : notnull
    {
        if (_repositories.TryGetValue(typeof(TRepository), out var cached))
            return (TRepository)cached;

        // Resolved from DI so a named repository can take its own dependencies — and,
        // because AppDbContext is scoped, it receives the same instance this unit holds.
        var repository = services.GetRequiredService<TRepository>();
        _repositories[typeof(TRepository)] = repository;
        return repository;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        StampAuditFields();
        ConvertDeletesToSoftDeletes();
        StampConcurrencyTokens();

        // Collect BEFORE saving: SaveChanges detaches nothing, but an event raised during
        // the save would be missed, and after dispatch we must not re-dispatch.
        var events = CollectAndClearDomainEvents();

        int affected;
        try
        {
            affected = await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translate the infrastructure failure into something the application layer
            // can react to without referencing EF Core (handbook §2.5, §3.10).
            var entry = ex.Entries.FirstOrDefault();
            var databaseValues = entry is null ? null : await entry.GetDatabaseValuesAsync(ct);

            throw new ConcurrencyConflictException(
                databaseValues is null
                    ? "The record was deleted by another user."
                    : "The record was modified by another user. Reload it and try again.",
                ex);
        }

        if (events.Count > 0)
        {
            logger.LogDebug("Dispatching {Count} domain event(s) after a successful commit.", events.Count);
            await dispatcher.DispatchAsync(events, ct);
        }

        return affected;
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Nested call: join the transaction already in flight instead of opening a second.
        if (_transaction is not null)
            return await operation(ct);

        // The execution strategy must wrap the WHOLE transaction. Without this, a retry
        // would re-run only the failed statement inside a transaction that no longer exists.
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            _transaction = transaction;

            try
            {
                var result = await operation(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            finally
            {
                _transaction = null;
            }
        });
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default) =>
        ExecuteInTransactionAsync<object?>(async token =>
        {
            await operation(token);
            return null;
        }, ct);

    // ---- cross-cutting write concerns, applied once for every aggregate --------

    private void StampAuditFields()
    {
        var now = clock.UtcNow;
        var user = services.GetService<ICurrentUser>()?.UserId ?? "system";

        foreach (var entry in db.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOn = now;
                    entry.Entity.CreatedBy = user;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedOn = now;
                    entry.Entity.ModifiedBy = user;

                    // Never let an update rewrite the creation stamp.
                    entry.Property(nameof(IAuditable.CreatedOn)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    break;
            }
        }
    }

    private void ConvertDeletesToSoftDeletes()
    {
        var now = clock.UtcNow;

        foreach (var entry in db.ChangeTracker.Entries<ISoftDeletable>().ToList())
        {
            if (entry.State != EntityState.Deleted) continue;

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedOn = now;

            // Remove() cascades immediately, so every owned dependent (a Claim's lines,
            // for instance) is ALREADY marked Deleted by the time we get here. Flipping the
            // root back to Modified is not enough — without this, a "soft" delete silently
            // and permanently destroys the aggregate's children.
            ResetCascadedDependents(entry);
        }
    }

    private static void ResetCascadedDependents(EntityEntry entry)
    {
        var dependents = entry.References
            .Select(reference => reference.TargetEntry)
            .Concat(entry.Collections.SelectMany(collection =>
                collection.CurrentValue?.Cast<object>().Select(entry.Context.Entry) ?? []));

        foreach (var dependent in dependents)
        {
            if (dependent is null || dependent.State != EntityState.Deleted) continue;
            if (!dependent.Metadata.IsOwned()) continue;   // only the aggregate's own parts

            dependent.State = EntityState.Unchanged;
            ResetCascadedDependents(dependent);            // owned types can nest
        }
    }

    /// <summary>
    /// SQL Server generates rowversion itself; on SQLite we generate the token so the
    /// same optimistic-concurrency behaviour is available in local development and tests.
    /// </summary>
    private void StampConcurrencyTokens()
    {
        if (db.Database.IsSqlServer()) return;

        foreach (var entry in db.ChangeTracker.Entries<IHasConcurrencyToken>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
        }
    }

    private List<IDomainEvent> CollectAndClearDomainEvents()
    {
        var roots = db.ChangeTracker
                      .Entries<AggregateRoot>()
                      .Select(e => e.Entity)
                      .Where(a => a.DomainEvents.Count > 0)
                      .ToList();

        var events = roots.SelectMany(a => a.DomainEvents).ToList();

        foreach (var root in roots)
            root.ClearDomainEvents();

        return events;
    }
}
