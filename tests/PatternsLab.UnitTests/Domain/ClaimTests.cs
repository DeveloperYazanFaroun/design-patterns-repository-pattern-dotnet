using PatternsLab.Domain.Claims;
using Shouldly;

namespace PatternsLab.UnitTests.Domain;

/// <summary>
/// Aggregate rules, tested with no database, no DI and no mocks — because the rules
/// live in the aggregate rather than in a service that needs a DbContext.
/// </summary>
public class ClaimTests
{
    private static Claim NewDraft() =>
        Claim.Create(Guid.CreateVersion7(), "clm-001", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), "pbm-01");

    [Fact]
    public void Create_normalises_number_and_payer_to_upper_case()
    {
        var claim = NewDraft();

        claim.Number.ShouldBe("CLM-001");
        claim.PayerCode.ShouldBe("PBM-01");
        claim.Status.ShouldBe(ClaimStatus.Draft);
    }

    [Fact]
    public void Create_rejects_a_service_date_in_the_future()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        Should.Throw<ArgumentException>(() => Claim.Create(Guid.CreateVersion7(), "CLM-002", tomorrow, "PBM-01"));
    }

    [Fact]
    public void AddLine_recalculates_the_total()
    {
        var claim = NewDraft();

        claim.AddLine("A4253", 100, new Money(0.42m));
        claim.AddLine("E0607", 1, new Money(38.00m));

        claim.Total.Amount.ShouldBe(80.00m);
        claim.Lines.Count.ShouldBe(2);
    }

    [Fact]
    public void AddLine_refuses_a_duplicate_procedure_code_with_a_client_readable_message()
    {
        var claim = NewDraft();
        claim.AddLine("A4253", 10, new Money(1m));

        var ex = Should.Throw<InvalidOperationException>(() => claim.AddLine("a4253", 5, new Money(1m)));

        ex.Message.ShouldContain("A4253");
        ex.Message.ShouldContain("already has a line");
    }

    [Fact]
    public void Submit_raises_exactly_one_ClaimSubmitted_event()
    {
        var claim = NewDraft();
        claim.AddLine("A4253", 10, new Money(1m));

        claim.Submit(DateTimeOffset.UtcNow);

        claim.Status.ShouldBe(ClaimStatus.Submitted);
        claim.DomainEvents.OfType<ClaimSubmitted>().Count().ShouldBe(1);
    }

    [Fact]
    public void Submit_refuses_an_empty_claim()
    {
        var claim = NewDraft();

        Should.Throw<InvalidOperationException>(() => claim.Submit(DateTimeOffset.UtcNow))
              .Message.ShouldContain("no lines");
    }

    [Fact]
    public void A_submitted_claim_cannot_be_edited()
    {
        var claim = NewDraft();
        claim.AddLine("A4253", 10, new Money(1m));
        claim.Submit(DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() => claim.AddLine("E0607", 1, new Money(1m)));
    }

    [Fact]
    public void A_paid_claim_cannot_be_voided()
    {
        var claim = NewDraft();
        claim.AddLine("A4253", 10, new Money(1m));
        claim.Submit(DateTimeOffset.UtcNow);
        claim.MarkPaid(DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() => claim.Void("changed my mind"));
    }

    [Fact]
    public void Reprice_applies_the_strategy_to_every_line()
    {
        var claim = NewDraft();
        claim.AddLine("A4253", 100, new Money(99m));
        claim.AddLine("E0607", 1, new Money(99m));

        claim.Reprice(new ContractedRatePricing());

        claim.Lines.Single(l => l.ProcedureCode == "A4253").UnitPrice.Amount.ShouldBe(0.42m);
        claim.Lines.Single(l => l.ProcedureCode == "E0607").UnitPrice.Amount.ShouldBe(38.00m);
        claim.Total.Amount.ShouldBe(80.00m);
    }

    [Fact]
    public void Money_refuses_to_mix_currencies()
    {
        Should.Throw<InvalidOperationException>(() => _ = new Money(1m, "USD") + new Money(1m, "ILS"));
    }
}
