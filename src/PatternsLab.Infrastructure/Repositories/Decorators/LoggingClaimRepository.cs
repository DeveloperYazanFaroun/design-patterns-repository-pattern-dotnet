using System.Diagnostics;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims;
using Microsoft.Extensions.Logging;

namespace PatternsLab.Infrastructure.Repositories.Decorators;

/// <summary>
/// DECORATOR (handbook §1.5, Figure 1.3).
///
/// Adds timing to every read without touching <see cref="EfClaimRepository"/>. It
/// implements the same interface and holds an inner instance of it, so the caller
/// cannot tell how deep the wrapping goes.
///
/// This is only possible because <see cref="IClaimRepository"/> exists. Against a raw
/// DbContext there is no seam to wrap — one of the concrete benefits in §4.1.
/// </summary>
public sealed class LoggingClaimRepository(
    IClaimRepository inner,
    ILogger<LoggingClaimRepository> logger) : IClaimRepository
{
    private const int SlowQueryMs = 200;

    public Task<Claim?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Timed(nameof(GetByIdAsync), () => inner.GetByIdAsync(id, ct));

    public Task<Claim?> GetWithLinesAsync(Guid id, CancellationToken ct = default) =>
        Timed(nameof(GetWithLinesAsync), () => inner.GetWithLinesAsync(id, ct));

    public Task<Claim?> GetByNumberAsync(string number, CancellationToken ct = default) =>
        Timed(nameof(GetByNumberAsync), () => inner.GetByNumberAsync(number, ct));

    public Task<bool> NumberExistsAsync(string number, CancellationToken ct = default) =>
        Timed(nameof(NumberExistsAsync), () => inner.NumberExistsAsync(number, ct));

    public Task<IReadOnlyList<Claim>> GetOutstandingAsync(string payerCode, CancellationToken ct = default) =>
        Timed(nameof(GetOutstandingAsync), () => inner.GetOutstandingAsync(payerCode, ct));

    public Task<IReadOnlyList<PayerTotal>> TotalsByPayerAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Timed(nameof(TotalsByPayerAsync), () => inner.TotalsByPayerAsync(from, to, ct));

    public Task<Claim?> FirstOrDefaultAsync(ISpecification<Claim> specification, CancellationToken ct = default) =>
        Timed($"{nameof(FirstOrDefaultAsync)}<{specification.GetType().Name}>",
            () => inner.FirstOrDefaultAsync(specification, ct));

    public Task<IReadOnlyList<Claim>> ListAsync(ISpecification<Claim> specification, CancellationToken ct = default) =>
        Timed($"{nameof(ListAsync)}<{specification.GetType().Name}>",
            () => inner.ListAsync(specification, ct));

    public Task<IReadOnlyList<TResult>> ListAsync<TResult>(
        ISpecification<Claim, TResult> specification, CancellationToken ct = default) =>
        Timed($"{nameof(ListAsync)}<{specification.GetType().Name}>",
            () => inner.ListAsync(specification, ct));

    public Task<PagedResult<Claim>> PageAsync(
        ISpecification<Claim> specification, int page, int pageSize, CancellationToken ct = default) =>
        Timed($"{nameof(PageAsync)}<{specification.GetType().Name}>",
            () => inner.PageAsync(specification, page, pageSize, ct));

    public Task<int> CountAsync(ISpecification<Claim> specification, CancellationToken ct = default) =>
        Timed(nameof(CountAsync), () => inner.CountAsync(specification, ct));

    public Task<bool> AnyAsync(ISpecification<Claim> specification, CancellationToken ct = default) =>
        Timed(nameof(AnyAsync), () => inner.AnyAsync(specification, ct));

    // Writes are pass-through: there is nothing to time until SaveChanges runs.
    public void Add(Claim entity) => inner.Add(entity);
    public void AddRange(IEnumerable<Claim> entities) => inner.AddRange(entities);
    public void Update(Claim entity) => inner.Update(entity);
    public void Remove(Claim entity) => inner.Remove(entity);

    private async Task<TResult> Timed<TResult>(string operation, Func<Task<TResult>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await action();
        }
        finally
        {
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds >= SlowQueryMs)
                logger.LogWarning("SLOW  claims.{Operation} took {Elapsed} ms", operation, stopwatch.ElapsedMilliseconds);
            else
                logger.LogDebug("claims.{Operation} took {Elapsed} ms", operation, stopwatch.ElapsedMilliseconds);
        }
    }
}
