using Microsoft.EntityFrameworkCore;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Claims.Specifications;
using PatternsLab.Infrastructure.Persistence;

namespace PatternsLab.Infrastructure.Repositories;

/// <summary>
/// Inherits the mechanics from <see cref="Repository{T}"/> and adds the vocabulary
/// that only makes sense for claims — handbook §3.9.
/// </summary>
public sealed class EfClaimRepository(AppDbContext db, IClock clock)
    : Repository<Claim>(db), IClaimRepository
{
    public async Task<Claim?> GetWithLinesAsync(Guid id, CancellationToken ct = default) =>
        await Set.Include(c => c.Lines)
                 .Include(c => c.Patient)
                 .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Claim?> GetByNumberAsync(string number, CancellationToken ct = default)
    {
        var normalised = number.Trim().ToUpperInvariant();
        return await Set.Include(c => c.Lines)
                        .FirstOrDefaultAsync(c => c.Number == normalised, ct);
    }

    public async Task<bool> NumberExistsAsync(string number, CancellationToken ct = default)
    {
        var normalised = number.Trim().ToUpperInvariant();

        // IgnoreQueryFilters: a soft-deleted claim still occupies its number, because the
        // unique index does not know about the filter. Miss this and the insert throws
        // a constraint violation instead of returning a friendly conflict.
        return await Set.IgnoreQueryFilters()
                        .AnyAsync(c => c.Number == normalised, ct);
    }

    public Task<IReadOnlyList<Claim>> GetOutstandingAsync(string payerCode, CancellationToken ct = default) =>
        ListAsync(new OutstandingClaimsSpec(payerCode, clock.UtcNow, page: 1, pageSize: 500), ct);

    public async Task<IReadOnlyList<PayerTotal>> TotalsByPayerAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await Set.AsNoTracking()
                 .Where(c => c.ServiceDate >= from
                             && c.ServiceDate <= to
                             && c.Status != ClaimStatus.Voided)
                 .GroupBy(c => c.PayerCode)
                 .Select(g => new PayerTotal(g.Key, g.Count(), g.Sum(c => c.Total.Amount)))
                 .OrderByDescending(t => t.TotalAmount)
                 .ToListAsync(ct);
}
