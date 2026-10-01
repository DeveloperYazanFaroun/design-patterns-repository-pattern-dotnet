namespace PatternsLab.Patterns.Behavioural;

// ---------------------------------------------------------------------------
// STATE — handbook §1.3
//
// An object whose BEHAVIOUR changes with its state. The alternative — a status
// enum plus an if-ladder in every method — is the thing this replaces.
//
// The Claim aggregate in this solution deliberately uses the enum-plus-guard form
// instead, because it has four transitions and one guard clause each. Reach for
// State when the transition table gets big enough that you cannot hold it in your
// head — knowing when NOT to is handbook Part 4 in miniature.
// ---------------------------------------------------------------------------

public sealed class ClaimContext(string number)
{
    public string Number { get; } = number;
    public IClaimState State { get; private set; } = new DraftState();
    public List<string> Log { get; } = [];

    public void TransitionTo(IClaimState next)
    {
        Log.Add($"{State.Name} -> {next.Name}");
        State = next;
    }

    public void Submit() => State.Submit(this);
    public void Pay() => State.Pay(this);
    public void Void() => State.Void(this);
}

public interface IClaimState
{
    string Name { get; }
    void Submit(ClaimContext context);
    void Pay(ClaimContext context);
    void Void(ClaimContext context);
}

/// <summary>Default behaviour: every transition is refused unless a state allows it.</summary>
public abstract class ClaimStateBase : IClaimState
{
    public abstract string Name { get; }

    public virtual void Submit(ClaimContext context) => Refuse(context, nameof(Submit));
    public virtual void Pay(ClaimContext context) => Refuse(context, nameof(Pay));
    public virtual void Void(ClaimContext context) => Refuse(context, nameof(Void));

    private void Refuse(ClaimContext context, string action) =>
        context.Log.Add($"{action}() refused — claim is {Name}");
}

public sealed class DraftState : ClaimStateBase
{
    public override string Name => "Draft";
    public override void Submit(ClaimContext context) => context.TransitionTo(new SubmittedState());
    public override void Void(ClaimContext context) => context.TransitionTo(new VoidedState());
}

public sealed class SubmittedState : ClaimStateBase
{
    public override string Name => "Submitted";
    public override void Pay(ClaimContext context) => context.TransitionTo(new PaidState());
    public override void Void(ClaimContext context) => context.TransitionTo(new VoidedState());
}

public sealed class PaidState : ClaimStateBase
{
    public override string Name => "Paid";
    // Everything is refused: a paid claim is terminal.
}

public sealed class VoidedState : ClaimStateBase
{
    public override string Name => "Voided";
}

public sealed class StateDemo : PatternDemo
{
    public override string Key => "state";
    public override string Name => "State";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Change an object's behaviour when its internal state changes.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "Workflow engines, TCP connection state machines";

    protected override void Execute(Action<string> log)
    {
        var claim = new ClaimContext("CLM-001");

        claim.Pay();        // refused — still a draft
        claim.Submit();     // Draft -> Submitted
        claim.Pay();        // Submitted -> Paid
        claim.Void();       // refused — paid is terminal

        foreach (var line in claim.Log) log("  " + line);

        log($"final state: {claim.State.Name}");
        log("No method on ClaimContext contains a switch on status — each state owns its own rules.");
    }
}
