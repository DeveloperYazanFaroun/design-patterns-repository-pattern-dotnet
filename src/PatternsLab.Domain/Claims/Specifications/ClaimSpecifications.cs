using PatternsLab.Domain.Abstractions;

namespace PatternsLab.Domain.Claims.Specifications;

/// <summary>
/// "Claims this payer has not settled in thirty days."
///
/// The rule now has a name, one home, and can be unit-tested with no database:
/// <code>
/// var spec = new OutstandingClaimsSpec("PBM-01", clock.UtcNow);
/// spec.Criteria!.Compile()(paidClaim).ShouldBeFalse();
/// </code>
/// </summary>
public sealed class OutstandingClaimsSpec : Specification<Claim>
{
    public static readonly TimeSpan Threshold = TimeSpan.FromDays(30);

    /// <param name="tracked">
    /// Read screens want <c>false</c> (no change tracking, faster). A caller that intends to
    /// MODIFY what comes back must pass <c>true</c> — an untracked entity can be mutated all
    /// day and SaveChanges will write nothing. That is a silent no-op, and one of the most
    /// common bugs in a repository-based codebase.
    /// </param>
    public OutstandingClaimsSpec(
        string payerCode, DateTimeOffset now, int page = 1, int pageSize = 50, bool tracked = false)
    {
        var cutoff = now - Threshold;

        Where(c => c.PayerCode == payerCode
                   && c.Status != ClaimStatus.Paid
                   && c.Status != ClaimStatus.Voided
                   && c.Status != ClaimStatus.Draft
                   && c.SubmittedOn != null
                   && c.SubmittedOn < cutoff);

        AddInclude(c => c.Lines);
        AddInclude(c => c.Patient!);
        ApplyOrderByDescending(c => c.SubmittedOn!);
        ApplyPaging(page, pageSize);

        if (!tracked) ReadOnly();
    }
}

/// <summary>Everything a claim-detail screen needs, in one round trip.</summary>
public sealed class ClaimWithLinesSpec : Specification<Claim>
{
    public ClaimWithLinesSpec(Guid claimId, bool tracked = true)
    {
        Where(c => c.Id == claimId);
        AddInclude(c => c.Lines);
        AddInclude(c => c.Patient!);

        if (!tracked) ReadOnly();
    }
}

/// <summary>
/// The search screen. Every filter is optional, which is precisely the case that used to
/// justify leaking <c>IQueryable</c> — and does not need to (handbook §2.8).
/// </summary>
public sealed class ClaimSearchSpec : Specification<Claim>
{
    public ClaimSearchSpec(
        string? payerCode = null,
        ClaimStatus? status = null,
        string? numberStartsWith = null,
        DateOnly? serviceFrom = null,
        DateOnly? serviceTo = null,
        int page = 1,
        int pageSize = 25)
    {
        if (!string.IsNullOrWhiteSpace(payerCode))
        {
            var code = payerCode.Trim().ToUpperInvariant();
            Where(c => c.PayerCode == code);
        }

        if (status is { } s)
            Where(c => c.Status == s);

        if (!string.IsNullOrWhiteSpace(numberStartsWith))
        {
            var prefix = numberStartsWith.Trim().ToUpperInvariant();
            Where(c => c.Number.StartsWith(prefix));
        }

        if (serviceFrom is { } from)
            Where(c => c.ServiceDate >= from);

        if (serviceTo is { } to)
            Where(c => c.ServiceDate <= to);

        AddInclude(c => c.Patient!);
        ApplyOrderByDescending(c => c.ServiceDate);
        ApplyPaging(page, pageSize);
        ReadOnly();
    }
}

/// <summary>Projection: a list screen that never materialises a whole aggregate.</summary>
public sealed record ClaimSummary(
    Guid Id, string Number, string PayerCode, ClaimStatus Status, DateOnly ServiceDate, decimal Total);

public sealed class ClaimSummarySpec : Specification<Claim, ClaimSummary>
{
    public ClaimSummarySpec(string? payerCode, int page = 1, int pageSize = 25)
        : base(c => new ClaimSummary(c.Id, c.Number, c.PayerCode, c.Status, c.ServiceDate, c.Total.Amount))
    {
        if (!string.IsNullOrWhiteSpace(payerCode))
        {
            var code = payerCode.Trim().ToUpperInvariant();
            Where(c => c.PayerCode == code);
        }

        ApplyOrderByDescending(c => c.ServiceDate);
        ApplyPaging(page, pageSize);
        ReadOnly();
    }
}

/// <summary>Demonstrates the opt-out from the global soft-delete query filter.</summary>
public sealed class DeletedClaimsSpec : Specification<Claim>
{
    public DeletedClaimsSpec()
    {
        Where(c => c.IsDeleted);
        IncludeDeleted();
        ApplyOrderByDescending(c => c.DeletedOn!);
        ReadOnly();
    }
}
