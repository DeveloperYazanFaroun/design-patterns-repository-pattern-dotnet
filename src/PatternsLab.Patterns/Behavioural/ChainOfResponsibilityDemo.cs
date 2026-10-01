namespace PatternsLab.Patterns.Behavioural;

// ---------------------------------------------------------------------------
// CHAIN OF RESPONSIBILITY — handbook §1.3
//
// Pass a request along a chain until someone handles it. Every ASP.NET Core
// request you have ever served went through one of these.
// ---------------------------------------------------------------------------

public sealed class ClaimRequest(string number, decimal amount, string submittedBy)
{
    public string Number { get; } = number;
    public decimal Amount { get; } = amount;
    public string SubmittedBy { get; } = submittedBy;

    public List<string> Trail { get; } = [];
    public bool IsApproved { get; private set; }
    public string? Rejection { get; private set; }

    public void Approve(string by)
    {
        IsApproved = true;
        Trail.Add($"approved by {by}");
    }

    public void Reject(string by, string reason)
    {
        IsApproved = false;
        Rejection = reason;
        Trail.Add($"rejected by {by}: {reason}");
    }
}

public abstract class ApprovalHandler
{
    private ApprovalHandler? _next;

    public ApprovalHandler Then(ApprovalHandler next)
    {
        _next = next;
        return next;          // returns the LINK so the chain reads left to right
    }

    public void Handle(ClaimRequest request)
    {
        request.Trail.Add($"-> {GetType().Name}");

        if (TryHandle(request)) return;      // stop: this link owned the request

        _next?.Handle(request);              // otherwise pass it on
    }

    /// <returns>true when this handler has dealt with the request and the chain should stop.</returns>
    protected abstract bool TryHandle(ClaimRequest request);
}

public sealed class ValidationHandler : ApprovalHandler
{
    protected override bool TryHandle(ClaimRequest request)
    {
        if (request.Amount > 0) return false;

        request.Reject(nameof(ValidationHandler), "Amount must be positive.");
        return true;
    }
}

public sealed class AutoApprovalHandler : ApprovalHandler
{
    protected override bool TryHandle(ClaimRequest request)
    {
        if (request.Amount >= 500m) return false;

        request.Approve("auto");
        return true;
    }
}

public sealed class SupervisorHandler : ApprovalHandler
{
    protected override bool TryHandle(ClaimRequest request)
    {
        if (request.Amount >= 5_000m) return false;

        request.Approve("supervisor");
        return true;
    }
}

public sealed class DirectorHandler : ApprovalHandler
{
    protected override bool TryHandle(ClaimRequest request)
    {
        request.Approve("director");
        return true;      // the terminal link always handles
    }
}

public sealed class ChainOfResponsibilityDemo : PatternDemo
{
    public override string Key => "chain-of-responsibility";
    public override string Name => "Chain of Responsibility";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Pass a request along a chain until someone handles it.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "ASP.NET Core middleware pipeline, DelegatingHandler";

    protected override void Execute(Action<string> log)
    {
        foreach (var request in new[]
                 {
                     new ClaimRequest("CLM-001", 120m, "yazan"),
                     new ClaimRequest("CLM-002", 3_400m, "yazan"),
                     new ClaimRequest("CLM-003", 48_000m, "yazan"),
                     new ClaimRequest("CLM-004", 0m, "yazan")
                 })
        {
            // Rebuilt per request so the demo is order-explicit.
            var chain = new ValidationHandler();
            chain.Then(new AutoApprovalHandler()).Then(new SupervisorHandler()).Then(new DirectorHandler());

            chain.Handle(request);

            log($"{request.Number} ({request.Amount,9:0.00}) : {string.Join(" ", request.Trail)}");
        }

        log("Inserting a ComplianceHandler is one `Then` call — no existing handler changes.");
    }
}
