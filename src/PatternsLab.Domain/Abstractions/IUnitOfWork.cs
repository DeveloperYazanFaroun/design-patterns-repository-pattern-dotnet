using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Common;
using PatternsLab.Domain.Patients;

namespace PatternsLab.Domain.Abstractions;

/// <summary>
/// One business transaction — handbook §3.6.
///
/// The repositories it hands out all share a single change tracker, which is the
/// only reason a use case that touches two aggregates can commit once.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Aggregates that earned their own vocabulary.</summary>
    IClaimRepository Claims { get; }

    IPatientRepository Patients { get; }

    /// <summary>Generic escape hatch for aggregates that have not (yet) earned a named repository.</summary>
    IRepository<T> Repository<T>() where T : Entity;

    /// <summary>Commits everything accumulated in this unit, then dispatches domain events.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside one database transaction, with the
    /// provider's execution strategy so a transient failure is retried as a whole.
    /// A nested call joins the transaction already in flight rather than opening a second.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken ct = default);

    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation, CancellationToken ct = default);
}

/// <summary>Supplies the acting user for audit stamping. Implemented in the API layer.</summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? DisplayName { get; }
}

/// <summary>
/// Injected instead of <c>DateTimeOffset.UtcNow</c> so tests can pin the clock.
/// .NET ships <c>TimeProvider</c> for exactly this; the interface keeps the domain
/// free of even that dependency and makes the intent obvious to a trainee.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
