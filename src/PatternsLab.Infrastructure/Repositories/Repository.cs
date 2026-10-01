using Microsoft.EntityFrameworkCore;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Common;
using PatternsLab.Infrastructure.Persistence;

namespace PatternsLab.Infrastructure.Repositories;

/// <summary>
/// The generic repository — handbook §3.5.
///
/// It supplies the <em>mechanics</em> every aggregate needs. Vocabulary specific to one
/// aggregate goes in a derived class such as <see cref="EfClaimRepository"/>; that split
/// is what keeps this type from turning into the god repository of §4.3.
///
/// Every member is virtual so a derived repository can override one query without
/// reimplementing the rest.
/// </summary>
public class Repository<T>(AppDbContext db) : IRepository<T> where T : Entity
{
    protected AppDbContext Db { get; } = db;

    protected DbSet<T> Set => Db.Set<T>();

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public virtual async Task<T?> FirstOrDefaultAsync(
        ISpecification<T> specification, CancellationToken ct = default) =>
        await SpecificationEvaluator.Apply(Set, specification).FirstOrDefaultAsync(ct);

    public virtual async Task<IReadOnlyList<T>> ListAsync(
        ISpecification<T> specification, CancellationToken ct = default) =>
        await SpecificationEvaluator.Apply(Set, specification).ToListAsync(ct);

    public virtual async Task<IReadOnlyList<TResult>> ListAsync<TResult>(
        ISpecification<T, TResult> specification, CancellationToken ct = default) =>
        await SpecificationEvaluator.Apply(Set, specification)
                                    .Select(specification.Selector)
                                    .ToListAsync(ct);

    public virtual async Task<PagedResult<T>> PageAsync(
        ISpecification<T> specification, int page, int pageSize, CancellationToken ct = default)
    {
        // Two queries, one specification. The count deliberately ignores Skip/Take.
        var total = await CountAsync(specification, ct);
        var items = await ListAsync(specification, ct);

        return new PagedResult<T>(items, total, page, pageSize);
    }

    public virtual async Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default) =>
        await SpecificationEvaluator.Apply(Set, specification, forCount: true).CountAsync(ct);

    public virtual async Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken ct = default) =>
        await SpecificationEvaluator.Apply(Set, specification, forCount: true).AnyAsync(ct);

    // Writes only stage the change. Committing is the Unit of Work's job — putting a
    // SaveChanges here is anti-pattern 2 in handbook §4.3.
    public virtual void Add(T entity) => Set.Add(entity);

    public virtual void AddRange(IEnumerable<T> entities) => Set.AddRange(entities);

    public virtual void Update(T entity) => Set.Update(entity);

    public virtual void Remove(T entity) => Set.Remove(entity);
}
