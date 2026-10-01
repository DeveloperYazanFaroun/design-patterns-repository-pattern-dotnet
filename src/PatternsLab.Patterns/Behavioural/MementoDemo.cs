namespace PatternsLab.Patterns.Behavioural;

// ---------------------------------------------------------------------------
// MEMENTO — handbook §1.3
//
// Capture an object's state so it can be restored later, WITHOUT exposing its
// internals. The memento is opaque to everyone except the originator.
//
// EF Core's change tracker is a memento: it snapshots every entity on load so it
// can work out what changed at SaveChanges time.
// ---------------------------------------------------------------------------

/// <summary>Opaque to the caretaker: it can hold one, but cannot read inside it.</summary>
public sealed class ClaimMemento
{
    internal ClaimMemento(string payerCode, List<(string Code, int Qty)> lines, DateTimeOffset takenAt)
    {
        PayerCode = payerCode;
        Lines = lines;
        TakenAt = takenAt;
    }

    internal string PayerCode { get; }
    internal List<(string Code, int Qty)> Lines { get; }

    /// <summary>The only member the caretaker is allowed to see.</summary>
    public DateTimeOffset TakenAt { get; }
}

/// <summary>The originator — the only type that can create or read a memento.</summary>
public sealed class EditableClaim(string payerCode)
{
    private readonly List<(string Code, int Qty)> _lines = [];

    public string PayerCode { get; private set; } = payerCode;
    public IReadOnlyList<(string Code, int Qty)> Lines => _lines.AsReadOnly();

    public void AddLine(string code, int qty) => _lines.Add((code, qty));
    public void ChangePayer(string payer) => PayerCode = payer;

    public ClaimMemento Save() => new(PayerCode, [.. _lines], DateTimeOffset.UtcNow);

    public void Restore(ClaimMemento memento)
    {
        PayerCode = memento.PayerCode;
        _lines.Clear();
        _lines.AddRange(memento.Lines);
    }

    public override string ToString() =>
        $"payer={PayerCode}, lines=[{string.Join(", ", _lines.Select(l => $"{l.Code}x{l.Qty}"))}]";
}

/// <summary>The caretaker holds the history. It never inspects what it is holding.</summary>
public sealed class ClaimHistory
{
    private readonly Stack<ClaimMemento> _snapshots = new();

    public int Count => _snapshots.Count;

    public void Push(ClaimMemento memento) => _snapshots.Push(memento);

    public ClaimMemento? Pop() => _snapshots.TryPop(out var memento) ? memento : null;
}

public sealed class MementoDemo : PatternDemo
{
    public override string Key => "memento";
    public override string Name => "Memento";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Capture and restore an object's state without breaking encapsulation.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "EF Core change-tracker snapshots, undo stacks";

    protected override void Execute(Action<string> log)
    {
        var claim = new EditableClaim("PBM-01");
        var history = new ClaimHistory();

        claim.AddLine("A4253", 100);
        history.Push(claim.Save());
        log($"snapshot 1 taken : {claim}");

        claim.AddLine("E0607", 1);
        claim.ChangePayer("PBM-07");
        history.Push(claim.Save());
        log($"snapshot 2 taken : {claim}");

        claim.AddLine("J1815", 30);
        log($"after more edits : {claim}");

        claim.Restore(history.Pop()!);
        log($"restore snapshot2: {claim}");

        claim.Restore(history.Pop()!);
        log($"restore snapshot1: {claim}");

        log("ClaimHistory stored both snapshots and could not read a single field of either.");
    }
}
