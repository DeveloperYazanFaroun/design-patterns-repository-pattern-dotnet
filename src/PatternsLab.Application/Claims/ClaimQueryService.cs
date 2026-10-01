using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Claims.Specifications;

namespace PatternsLab.Application.Claims;

/// <summary>
/// The read side. It uses specifications, never <c>IQueryable</c> — so a screen cannot
/// invent a query the repository has not agreed to (handbook §2.8, §4.1).
///
/// Handbook §4.5 notes that once a read screen gets heavy, the honest answer is often to
/// move it to a CQRS projection and stop paying for aggregate materialisation. The
/// summary method below is the first step on that road.
/// </summary>
public sealed class ClaimQueryService(IUnitOfWork uow, IClock clock)
{
    public Task<Claim?> GetAsync(Guid id, CancellationToken ct = default) =>
        uow.Claims.FirstOrDefaultAsync(new ClaimWithLinesSpec(id, tracked: false), ct);

    public Task<PagedResult<Claim>> SearchAsync(
        string? payerCode = null,
        ClaimStatus? status = null,
        string? numberStartsWith = null,
        DateOnly? serviceFrom = null,
        DateOnly? serviceTo = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default) =>
        uow.Claims.PageAsync(
            new ClaimSearchSpec(payerCode, status, numberStartsWith, serviceFrom, serviceTo, page, pageSize),
            page, pageSize, ct);

    /// <summary>Projection — the database returns six columns, not whole aggregates.</summary>
    public Task<IReadOnlyList<ClaimSummary>> SummariesAsync(
        string? payerCode, int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        uow.Claims.ListAsync(new ClaimSummarySpec(payerCode, page, pageSize), ct);

    public Task<IReadOnlyList<Claim>> OutstandingAsync(string payerCode, CancellationToken ct = default) =>
        uow.Claims.ListAsync(new OutstandingClaimsSpec(payerCode, clock.UtcNow), ct);

    public Task<IReadOnlyList<PayerTotal>> TotalsByPayerAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default) =>
        uow.Claims.TotalsByPayerAsync(from, to, ct);

    /// <summary>Shows the soft-delete opt-out working end to end.</summary>
    public Task<IReadOnlyList<Claim>> DeletedAsync(CancellationToken ct = default) =>
        uow.Claims.ListAsync(new DeletedClaimsSpec(), ct);
}
