namespace PatternsLab.Patterns.Creational;

// ---------------------------------------------------------------------------
// FACTORY METHOD — handbook §1.4, Figure 1.2
//
// Problem: `new SmsNotifier(...)` inside AlertService welds the service to one
// channel forever. It cannot be tested without a real gateway, and every new
// channel is an edit to a class that has nothing to do with channels.
//
// Solution: the service asks a factory for an INotifier. Adding a PushNotifier
// touches the factory and one new class — never the service.
// ---------------------------------------------------------------------------

public interface INotifier
{
    string Channel { get; }
    string Send(string message);
}

public sealed class EmailNotifier : INotifier
{
    public string Channel => "email";
    public string Send(string message) => $"[smtp] {message}";
}

public sealed class SmsNotifier : INotifier
{
    public string Channel => "sms";
    public string Send(string message) => $"[sms] {message}";
}

public sealed class PushNotifier : INotifier
{
    public string Channel => "push";
    public string Send(string message) => $"[push] {message}";
}

public interface INotifierFactory
{
    INotifier Create(string channel);
}

/// <summary>The factory method itself. One switch, in one place, with a name.</summary>
public sealed class NotifierFactory : INotifierFactory
{
    public INotifier Create(string channel) => channel.ToLowerInvariant() switch
    {
        "email" => new EmailNotifier(),
        "sms" => new SmsNotifier(),
        "push" => new PushNotifier(),
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown channel.")
    };
}

/// <summary>The client. It depends on two interfaces and knows no concrete notifier.</summary>
public sealed class AlertService(INotifierFactory factory)
{
    public string Raise(string channel, string text) => factory.Create(channel).Send(text);
}

public sealed class FactoryMethodDemo : PatternDemo
{
    public override string Key => "factory-method";
    public override string Name => "Factory Method";
    public override PatternCategory Category => PatternCategory.Creational;
    public override string Intent => "Let a subclass or delegate decide which concrete type to instantiate.";
    public override string HandbookSection => "1.4";
    public override string SeenInDotNet => "ILoggerFactory.CreateLogger, DbProviderFactory";

    protected override void Execute(Action<string> log)
    {
        var service = new AlertService(new NotifierFactory());

        log("AlertService depends on INotifierFactory + INotifier only.");
        foreach (var channel in new[] { "email", "sms", "push" })
            log($"  Raise(\"{channel}\") -> {service.Raise(channel, "Claim CLM-001 is overdue.")}");

        try
        {
            service.Raise("carrier-pigeon", "…");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            log($"  Unknown channel fails loudly, in ONE place: {ex.ParamName} = 'carrier-pigeon'");
        }

        log("Adding a 4th channel = 1 new class + 1 line in the factory. AlertService never changes.");
    }
}
