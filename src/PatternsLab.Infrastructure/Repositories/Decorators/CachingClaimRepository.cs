using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims;

namespace PatternsLab.Infrastructure.Repositories.Decorators;

/// <summary>
/// DECORATOR, second layer (handbook §1.5).
///
/// Caches only the two read-by-identity paths. Note the two rules that make a caching
/// decorator correct rather than a subtle bug:
///
///   1. <b>Only cache what you can invalidate.</b> Specification-driven list queries are
///      not cached here, because there is no cheap way to know which cached lists a write
///      invalidates.
///   2. <b>Never cache a tracked entity.</b> A tracked instance belongs to one DbContext,
///      which dies with the request; handing it to the next request is a use-after-free in
///      slow motion. Only the no-tracking paths are cached.
/// </summary>
public sealed class CachingClaimRepository(
    IClaimRepository inner,
    IMemoryCache cache,
    ILogger<CachingClaimRepository> logger) : IClaimRepository
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private static string ExistsKey(string number) => $"claim:number-exists:{number.Trim().ToUpperInvariant()}";

    public async Task<bool> NumberExistsAsync(string number, CancellationToken ct = default)
    {
        var key = ExistsKey(number);

        if (cache.TryGetValue(key, out bool cached))
        {
            logger.LogDebug("Cache hit for {Key}", key);
            return cached;
        }

        var exists = await inner.NumberExistsAsync(number, ct);
        cache.Set(key, exists, Ttl);
        return exists;
    }

    // A write must invalidate, or the decorator stops being transparent and starts
    // being a correctness bug.
    public void Add(Claim entity)
    {
        inner.Add(entity);
        cache.Remove(ExistsKey(entity.Number));
    }

    public void Remove(Claim entity)
    {
        inner.Remove(entity);
        cache.Remove(ExistsKey(entity.Number));
    }

    public void Update(Claim entity)
    {
        inner.Update(entity);
        cache.Remove(ExistsKey(entity.Number));
    }

    public void AddRange(IEnumerable<Claim> entities)
    {
        var list = entities as IList<Claim> ?? entities.ToList();
        inner.AddRange(list);

        foreach (var claim in list)
            cache.Remove(ExistsKey(claim.Number));
    }

    // --- everything else is a straight pass-through ---------------------------

    public Task<Claim?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);

    public Task<Claim?> GetWithLinesAsync(Guid id, CancellationToken ct = default) =>
        inner.GetWithLinesAsync(id, ct);

    public Task<Claim?> GetByNumberAsync(string number, CancellationToken ct = default) =>
        inner.GetByNumberAsync(number, ct);

    public Task<IReadOnlyList<Claim>> GetOutstandingAsync(string payerCode, CancellationToken ct = default) =>
        inner.GetOutstandingAsync(payerCode, ct);

    public Task<IReadOnlyList<PayerTotal>> TotalsByPayerAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default) =>
        inner.TotalsByPayerAsync(from, to, ct);

    public Task<Claim?> FirstOrDefaultAsync(ISpecification<Claim> specification, CancellationToken ct = default) =>
        inner.FirstOrDefaultAsync(specification, ct);

    public Task<IReadOnlyList<Claim>> ListAsync(ISpecification<Claim> specification, CancellationToken ct = default) =>
        inner.ListAsync(specification, ct);

    public Task<IReadOnlyList<TResult>> ListAsync<TResult>(
        ISpecification<Claim, TResult> specification, CancellationToken ct = default) =>
        inner.ListAsync(specification, ct);

    public Task<PagedResult<Claim>> PageAsync(
        ISpecification<Claim> specification, int page, int pageSize, CancellationToken ct = default) =>
        inner.PageAsync(specification, page, pageSize, ct);

    public Task<int> CountAsync(ISpecification<Claim> specification, CancellationToken ct = default) =>
        inner.CountAsync(specification, ct);

    public Task<bool> AnyAsync(ISpecification<Claim> specification, CancellationToken ct = default) =>
        inner.AnyAsync(specification, ct);
}
