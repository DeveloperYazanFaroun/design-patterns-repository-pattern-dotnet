using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Claims.Specifications;
using Shouldly;

namespace PatternsLab.UnitTests.Domain;

/// <summary>
/// A specification is an object, so its rule can be compiled and asserted directly —
/// no database, no EF Core, milliseconds (handbook §3.4).
/// </summary>
public class SpecificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static Claim ClaimWith(string payer, ClaimStatus status, int submittedDaysAgo)
    {
        var claim = Claim.Create(Guid.CreateVersion7(), $"CLM-{Guid.NewGuid():N}"[..12],
            DateOnly.FromDateTime(Now.UtcDateTime.AddDays(-submittedDaysAgo - 1)), payer);

        claim.AddLine("A4253", 1, new Money(1m));

        if (status != ClaimStatus.Draft)
            claim.Submit(Now.AddDays(-submittedDaysAgo));

        switch (status)
        {
            case ClaimStatus.Paid:
                claim.MarkPaid(Now.AddDays(-1));
                break;
            case ClaimStatus.Voided:
                claim.Void("test");
                break;
        }

        return claim;
    }

    [Theory]
    [InlineData("PBM-01", ClaimStatus.Submitted, 45, true)]   // the one we want
    [InlineData("PBM-01", ClaimStatus.Paid, 45, false)]        // already settled
    [InlineData("PBM-01", ClaimStatus.Submitted, 5, false)]    // too recent
    [InlineData("PBM-01", ClaimStatus.Voided, 45, false)]      // voided
    [InlineData("PBM-07", ClaimStatus.Submitted, 45, false)]   // different payer
    [InlineData("PBM-01", ClaimStatus.Draft, 45, false)]       // never submitted
    public void OutstandingClaimsSpec_matches_only_genuinely_outstanding_claims(
        string payer, ClaimStatus status, int daysAgo, bool expected)
    {
        var spec = new OutstandingClaimsSpec("PBM-01", Now);
        var predicate = spec.Criteria!.Compile();

        predicate(ClaimWith(payer, status, daysAgo)).ShouldBe(expected);
    }

    [Fact]
    public void OutstandingClaimsSpec_is_read_only_and_paged()
    {
        var spec = new OutstandingClaimsSpec("PBM-01", Now, page: 3, pageSize: 20);

        spec.AsNoTracking.ShouldBeTrue();
        spec.Skip.ShouldBe(40);
        spec.Take.ShouldBe(20);
        spec.Includes.Count.ShouldBe(2);
    }

    [Fact]
    public void OutstandingClaimsSpec_can_opt_into_tracking_for_callers_that_will_mutate()
    {
        new OutstandingClaimsSpec("PBM-01", Now, tracked: true).AsNoTracking.ShouldBeFalse();
    }

    [Fact]
    public void ClaimSearchSpec_with_no_filters_has_no_criteria()
    {
        new ClaimSearchSpec().Criteria.ShouldBeNull();
    }

    [Fact]
    public void ClaimSearchSpec_combines_every_supplied_filter_with_AND()
    {
        var spec = new ClaimSearchSpec(payerCode: "pbm-01", status: ClaimStatus.Submitted, numberStartsWith: "clm");
        var predicate = spec.Criteria!.Compile();

        predicate(ClaimWith("PBM-01", ClaimStatus.Submitted, 10)).ShouldBeTrue();
        predicate(ClaimWith("PBM-07", ClaimStatus.Submitted, 10)).ShouldBeFalse();
        predicate(ClaimWith("PBM-01", ClaimStatus.Paid, 10)).ShouldBeFalse();
    }

    [Fact]
    public void AndAlso_rebinds_both_lambdas_onto_one_parameter()
    {
        System.Linq.Expressions.Expression<Func<Claim, bool>> a = c => c.PayerCode == "PBM-01";
        System.Linq.Expressions.Expression<Func<Claim, bool>> b = c => c.Status == ClaimStatus.Draft;

        var combined = a.AndAlso(b);

        combined.Parameters.Count.ShouldBe(1);
        combined.Compile()(ClaimWith("PBM-01", ClaimStatus.Draft, 1)).ShouldBeTrue();
        combined.Compile()(ClaimWith("PBM-01", ClaimStatus.Submitted, 1)).ShouldBeFalse();
    }

    [Fact]
    public void ApplyPaging_rejects_a_page_below_one()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ClaimSearchSpec(page: 0));
    }

    [Fact]
    public void DeletedClaimsSpec_opts_out_of_the_global_query_filter()
    {
        new DeletedClaimsSpec().IgnoreQueryFilters.ShouldBeTrue();
    }
}
