using PatternsLab.Domain.Abstractions;

namespace PatternsLab.Domain.Claims;

/// <summary>Projection returned by the totals report — never a whole aggregate.</summary>
public sealed record PayerTotal(string PayerCode, int ClaimCount, decimal TotalAmount);

/// <summary>
/// The aggregate-specific repository — handbook §3.9.
///
/// It inherits the mechanics from <see cref="IRepository{T}"/> and adds
/// <em>vocabulary</em>. Read these names out loud with "EF Core" removed: they still
/// describe things the business does. That is the §4.6 test for whether the layer
/// is earning its place.
/// </summary>
public interface IClaimRepository : IRepository<Claim>
{
    Task<Claim?> GetWithLinesAsync(Guid id, CancellationToken ct = default);

    Task<Claim?> GetByNumberAsync(string number, CancellationToken ct = default);

    Task<bool> NumberExistsAsync(string number, CancellationToken ct = default);

    /// <summary>Claims this payer has not settled within the outstanding threshold.</summary>
    Task<IReadOnlyList<Claim>> GetOutstandingAsync(string payerCode, CancellationToken ct = default);

    /// <summary>Aggregation: belongs here, not in a generic contract everyone inherits.</summary>
    Task<IReadOnlyList<PayerTotal>> TotalsByPayerAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default);
}
