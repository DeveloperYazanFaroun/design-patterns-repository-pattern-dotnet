namespace PatternsLab.Patterns.Behavioural;

// ---------------------------------------------------------------------------
// OBSERVER — handbook §1.6
//
// One-to-many: when the subject changes, every dependent is told. The subject must
// not know who is listening, or the coupling it was meant to remove comes back.
//
// The production version is the domain-event pipeline:
// AggregateRoot.Raise -> UnitOfWork.SaveChangesAsync -> IDomainEventDispatcher.
// ---------------------------------------------------------------------------

public sealed record ClaimSubmittedEvent(string Number, decimal Amount, DateTimeOffset At);

public interface IClaimObserver
{
    string Name { get; }
    void OnClaimSubmitted(ClaimSubmittedEvent e);
}

/// <summary>The subject. It holds a list of observers and knows nothing else about them.</summary>
public sealed class ClaimWorkflow
{
    private readonly List<IClaimObserver> _observers = [];

    public IDisposable Subscribe(IClaimObserver observer)
    {
        _observers.Add(observer);
        return new Subscription(() => _observers.Remove(observer));
    }

    public void Submit(string number, decimal amount)
    {
        var e = new ClaimSubmittedEvent(number, amount, DateTimeOffset.UtcNow);

        // A snapshot, so an observer that unsubscribes during notification cannot
        // mutate the collection we are iterating.
        foreach (var observer in _observers.ToArray())
            observer.OnClaimSubmitted(e);
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}

public sealed class BillingObserver : IClaimObserver
{
    public List<string> Seen { get; } = [];
    public string Name => "billing";
    public void OnClaimSubmitted(ClaimSubmittedEvent e) => Seen.Add($"invoice queued for {e.Number}");
}

public sealed class AuditObserver : IClaimObserver
{
    public List<string> Seen { get; } = [];
    public string Name => "audit";
    public void OnClaimSubmitted(ClaimSubmittedEvent e) => Seen.Add($"audit row for {e.Number} ({e.Amount:0.00})");
}

public sealed class FraudObserver : IClaimObserver
{
    public List<string> Seen { get; } = [];
    public string Name => "fraud";

    public void OnClaimSubmitted(ClaimSubmittedEvent e)
    {
        if (e.Amount > 1_000m) Seen.Add($"FLAG {e.Number}: {e.Amount:0.00} exceeds the review threshold");
    }
}

public sealed class ObserverDemo : PatternDemo
{
    public override string Key => "observer";
    public override string Name => "Observer";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Notify many dependents automatically when one object changes.";
    public override string HandbookSection => "1.6";
    public override string SeenInDotNet => "events, IObservable<T>, RxJS on the Angular side";

    protected override void Execute(Action<string> log)
    {
        var workflow = new ClaimWorkflow();
        var billing = new BillingObserver();
        var audit = new AuditObserver();
        var fraud = new FraudObserver();

        workflow.Subscribe(billing);
        workflow.Subscribe(audit);
        var fraudSubscription = workflow.Subscribe(fraud);

        workflow.Submit("CLM-001", 42.50m);
        workflow.Submit("CLM-002", 5_400.00m);

        fraudSubscription.Dispose();          // the fraud module is switched off
        workflow.Submit("CLM-003", 9_999.00m);

        foreach (var observer in new IClaimObserver[] { billing, audit, fraud })
        {
            List<string> seen = observer switch
            {
                BillingObserver b => b.Seen,
                AuditObserver a => a.Seen,
                FraudObserver f => f.Seen,
                _ => new List<string>()
            };

            log($"{observer.Name}:");
            foreach (var line in seen) log("  " + line);
        }

        log("CLM-003 was never seen by fraud — subscription, not code, decided that.");
        log("ClaimWorkflow.Submit() never mentions billing, audit or fraud by name.");
    }
}
