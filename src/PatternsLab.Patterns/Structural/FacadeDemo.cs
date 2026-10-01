namespace PatternsLab.Patterns.Structural;

// ---------------------------------------------------------------------------
// FACADE — handbook §1.5
//
// One entry point over a complicated subsystem. A facade DELEGATES; the moment it
// starts computing prices itself it has stopped being a facade and become the class
// everyone is afraid to edit.
// ---------------------------------------------------------------------------

public sealed record EligibilityCheck(bool IsEligible, string PlanId, string? Reason);

public interface IEligibilityService { EligibilityCheck Verify(Guid patientId, DateOnly date); }
public interface IPricingEngine { decimal Price(IReadOnlyList<(string Code, int Qty)> lines, string planId); }
public interface IClearingHouse { string Send(Guid claimId, decimal amount); }
public interface IAuditLog { void Record(string message); }

public sealed class EligibilityService : IEligibilityService
{
    public EligibilityCheck Verify(Guid patientId, DateOnly date) =>
        date.Year >= 2020
            ? new EligibilityCheck(true, "PLAN-GOLD", null)
            : new EligibilityCheck(false, string.Empty, "Coverage had not started.");
}

public sealed class PricingEngine : IPricingEngine
{
    public decimal Price(IReadOnlyList<(string Code, int Qty)> lines, string planId) =>
        lines.Sum(l => l.Qty * (planId == "PLAN-GOLD" ? 0.42m : 0.65m));
}

public sealed class ClearingHouse : IClearingHouse
{
    public string Send(Guid claimId, decimal amount) => $"TRK-{claimId.ToString()[..6].ToUpperInvariant()}";
}

public sealed class AuditLog : IAuditLog
{
    public List<string> Entries { get; } = [];
    public void Record(string message) => Entries.Add(message);
}

public sealed record SubmissionResult(bool Accepted, string? Tracking, string? Reason);

/// <summary>Four collaborators behind one method.</summary>
public sealed class ClaimSubmissionFacade(
    IEligibilityService eligibility,
    IPricingEngine pricing,
    IClearingHouse clearingHouse,
    IAuditLog audit)
{
    public SubmissionResult Submit(Guid patientId, DateOnly serviceDate, IReadOnlyList<(string, int)> lines)
    {
        var check = eligibility.Verify(patientId, serviceDate);
        if (!check.IsEligible) return new SubmissionResult(false, null, check.Reason);

        var amount = pricing.Price(lines, check.PlanId);
        var tracking = clearingHouse.Send(patientId, amount);

        audit.Record($"patient={patientId:N} amount={amount:0.00} tracking={tracking}");

        return new SubmissionResult(true, tracking, null);
    }
}

public sealed class FacadeDemo : PatternDemo
{
    public override string Key => "facade";
    public override string Name => "Facade";
    public override PatternCategory Category => PatternCategory.Structural;
    public override string Intent => "Offer one simple entry point over a complicated subsystem.";
    public override string HandbookSection => "1.5";
    public override string SeenInDotNet => "HttpClient over sockets/TLS/DNS; WebApplication over the whole host";

    protected override void Execute(Action<string> log)
    {
        var audit = new AuditLog();
        var facade = new ClaimSubmissionFacade(new EligibilityService(), new PricingEngine(), new ClearingHouse(), audit);

        var ok = facade.Submit(Guid.CreateVersion7(), new DateOnly(2026, 3, 1), [("A4253", 100), ("E0607", 1)]);
        log($"Submit(2026) -> accepted={ok.Accepted}, tracking={ok.Tracking}");

        var rejected = facade.Submit(Guid.CreateVersion7(), new DateOnly(2015, 3, 1), [("A4253", 10)]);
        log($"Submit(2015) -> accepted={rejected.Accepted}, reason={rejected.Reason}");

        log($"audit entries: {audit.Entries.Count}");
        log("One call instead of four. The subsystem is still there for anyone who needs it directly.");
    }
}
