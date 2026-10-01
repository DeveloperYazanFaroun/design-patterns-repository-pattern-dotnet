namespace PatternsLab.Patterns.Behavioural;

// ---------------------------------------------------------------------------
// MEDIATOR — handbook §1.3
//
// N components that each know each other = N(N-1) links. Route everything through
// one mediator and it becomes N. The trade: the mediator itself can grow into a
// god object, so keep it a ROUTER, not a rules engine.
// ---------------------------------------------------------------------------

public interface IClaimModule
{
    string Name { get; }
    void Receive(string from, string message);
    void Attach(IClaimMediator mediator);
}

public interface IClaimMediator
{
    void Register(IClaimModule module);
    void Send(string from, string to, string message);
    void Broadcast(string from, string message);
}

public sealed class ClaimMediator : IClaimMediator
{
    private readonly Dictionary<string, IClaimModule> _modules = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Routed { get; } = [];

    public void Register(IClaimModule module)
    {
        _modules[module.Name] = module;
        module.Attach(this);
    }

    public void Send(string from, string to, string message)
    {
        Routed.Add($"{from} -> {to}: {message}");

        if (_modules.TryGetValue(to, out var target)) target.Receive(from, message);
        else Routed.Add($"  (no module named '{to}')");
    }

    public void Broadcast(string from, string message)
    {
        foreach (var module in _modules.Values.Where(m => m.Name != from))
            Send(from, module.Name, message);
    }
}

public abstract class ClaimModuleBase(string name) : IClaimModule
{
    private IClaimMediator? _mediator;

    public string Name { get; } = name;
    public List<string> Inbox { get; } = [];

    public void Attach(IClaimMediator mediator) => _mediator = mediator;

    protected void Send(string to, string message) => _mediator?.Send(Name, to, message);
    protected void Broadcast(string message) => _mediator?.Broadcast(Name, message);

    public virtual void Receive(string from, string message) => Inbox.Add($"from {from}: {message}");
}

public sealed class IntakeModule() : ClaimModuleBase("intake")
{
    public void Accept(string claimNumber) => Broadcast($"claim {claimNumber} accepted");
}

public sealed class PricingModule() : ClaimModuleBase("pricing")
{
    public override void Receive(string from, string message)
    {
        base.Receive(from, message);

        if (message.Contains("accepted", StringComparison.Ordinal))
            Send("billing", message.Replace("accepted", "priced at 42.50"));
    }
}

public sealed class BillingModule() : ClaimModuleBase("billing");

public sealed class MediatorDemo : PatternDemo
{
    public override string Key => "mediator";
    public override string Name => "Mediator";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Route communication through one object instead of many direct links.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "MediatR, the ASP.NET Core endpoint routing table";

    protected override void Execute(Action<string> log)
    {
        var mediator = new ClaimMediator();
        var intake = new IntakeModule();
        var pricing = new PricingModule();
        var billing = new BillingModule();

        mediator.Register(intake);
        mediator.Register(pricing);
        mediator.Register(billing);

        intake.Accept("CLM-001");

        foreach (var line in mediator.Routed) log("  " + line);

        log($"billing inbox: {string.Join(" | ", billing.Inbox)}");
        log("IntakeModule never references PricingModule or BillingModule — 3 links instead of 6.");
        log("Keep the mediator a router. Once it holds business rules it becomes the god object.");
    }
}
