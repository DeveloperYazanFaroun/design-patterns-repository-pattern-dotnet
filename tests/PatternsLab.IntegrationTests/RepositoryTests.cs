using Microsoft.EntityFrameworkCore;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Claims.Specifications;
using Shouldly;

namespace PatternsLab.IntegrationTests;

/// <summary>
/// These assert that the specifications TRANSLATE and return the right rows. A mocked
/// repository returning List&lt;T&gt; proves nothing about that — handbook §4.2.
/// </summary>
public class RepositoryTests(SqliteFixture fx) : IClassFixture<SqliteFixture>
{
    [Fact]
    public async Task OutstandingClaimsSpec_returns_only_the_claim_that_qualifies()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);

        var expected = fx.AddSubmittedClaim(patient.Id, $"OUT-{Guid.NewGuid():N}"[..12], submittedDaysAgo: 45);
        var tooRecent = fx.AddSubmittedClaim(patient.Id, $"NEW-{Guid.NewGuid():N}"[..12], submittedDaysAgo: 5);
        var otherPayer = fx.AddSubmittedClaim(patient.Id, $"OTH-{Guid.NewGuid():N}"[..12], "PBM-99", 45);

        var results = await fx.Uow.Claims.ListAsync(new OutstandingClaimsSpec("PBM-01", fx.Clock.UtcNow));

        results.Select(c => c.Number).ShouldContain(expected.Number);
        results.Select(c => c.Number).ShouldNotContain(tooRecent.Number);
        results.Select(c => c.Number).ShouldNotContain(otherPayer.Number);
    }

    [Fact]
    public async Task Includes_declared_on_the_specification_prevent_an_N_plus_1()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);
        fx.AddSubmittedClaim(patient.Id, $"INC-{Guid.NewGuid():N}"[..12], submittedDaysAgo: 40);

        fx.Db.ChangeTracker.Clear();

        var results = await fx.Uow.Claims.ListAsync(new OutstandingClaimsSpec("PBM-01", fx.Clock.UtcNow));

        // Lines came back in the same round trip, without a second query per claim.
        results.ShouldAllBe(c => c.Lines.Count > 0);
    }

    [Fact]
    public async Task CountAsync_ignores_paging_so_page_totals_are_correct()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);

        for (var i = 0; i < 7; i++)
            fx.AddSubmittedClaim(patient.Id, $"PG{i}-{Guid.NewGuid():N}"[..12], "PBM-42", 60);

        var spec = new OutstandingClaimsSpec("PBM-42", fx.Clock.UtcNow, page: 1, pageSize: 3);

        var page = await fx.Uow.Claims.ListAsync(spec);
        var total = await fx.Uow.Claims.CountAsync(spec);

        page.Count.ShouldBe(3);
        total.ShouldBe(7);            // NOT 3 — this is the bug forCount exists to prevent
    }

    [Fact]
    public async Task PageAsync_reports_the_unpaged_total_alongside_the_page()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);

        for (var i = 0; i < 5; i++)
            fx.AddSubmittedClaim(patient.Id, $"PA{i}-{Guid.NewGuid():N}"[..12], "PBM-55", 60);

        var result = await fx.Uow.Claims.PageAsync(
            new OutstandingClaimsSpec("PBM-55", fx.Clock.UtcNow, page: 2, pageSize: 2), page: 2, pageSize: 2);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(3);
        result.HasNextPage.ShouldBeTrue();
        result.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public async Task Projection_specification_returns_a_dto_not_an_aggregate()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);
        fx.AddSubmittedClaim(patient.Id, $"PRJ-{Guid.NewGuid():N}"[..12], "PBM-77", 10);

        var summaries = await fx.Uow.Claims.ListAsync(new ClaimSummarySpec("PBM-77"));

        summaries.ShouldNotBeEmpty();
        summaries[0].ShouldBeOfType<ClaimSummary>();
        summaries[0].Total.ShouldBe(42.00m);
    }

    [Fact]
    public async Task TotalsByPayer_aggregates_in_the_database()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);

        fx.AddSubmittedClaim(patient.Id, $"AGG1-{Guid.NewGuid():N}"[..12], "PBM-AGG", 30);
        fx.AddSubmittedClaim(patient.Id, $"AGG2-{Guid.NewGuid():N}"[..12], "PBM-AGG", 30);

        var from = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime.AddYears(-1));
        var to = DateOnly.FromDateTime(fx.Clock.UtcNow.UtcDateTime);

        var totals = await fx.Uow.Claims.TotalsByPayerAsync(from, to);

        var row = totals.SingleOrDefault(t => t.PayerCode == "PBM-AGG");
        row.ShouldNotBeNull();
        row!.ClaimCount.ShouldBe(2);
        row.TotalAmount.ShouldBe(84.00m);
    }

    [Fact]
    public async Task NumberExistsAsync_still_sees_a_soft_deleted_claim()
    {
        var patient = fx.AddPatient(memberNumber: $"MEM-{Guid.NewGuid():N}"[..12]);
        var claim = fx.AddSubmittedClaim(patient.Id, "DEL-0001", "PBM-DEL", 10);

        var tracked = await fx.Uow.Claims.GetByIdAsync(claim.Id);
        fx.Uow.Claims.Remove(tracked!);
        await fx.Uow.SaveChangesAsync();

        // Hidden from normal queries…
        (await fx.Uow.Claims.GetByIdAsync(claim.Id)).ShouldBeNull();

        // …but the unique index still holds the number, so the check must ignore the filter.
        (await fx.Uow.Claims.NumberExistsAsync("DEL-0001")).ShouldBeTrue();
    }
}
