namespace PatternsLab.Patterns.Behavioural;

// ---------------------------------------------------------------------------
// COMMAND — handbook §1.6
//
// Wrap a request as an object. Once a request IS an object it can be queued,
// logged, validated, retried — and, as here, UNDONE.
// ---------------------------------------------------------------------------

public sealed class ClaimDraft
{
    private readonly List<(string Code, int Qty)> _lines = [];

    public string PayerCode { get; internal set; } = "PBM-01";
    public IReadOnlyList<(string Code, int Qty)> Lines => _lines.AsReadOnly();

    internal void AddLine(string code, int qty) => _lines.Add((code, qty));
    internal bool RemoveLine(string code) => _lines.RemoveAll(l => l.Code == code) > 0;

    public override string ToString() =>
        $"payer={PayerCode}, lines=[{string.Join(", ", _lines.Select(l => $"{l.Code}x{l.Qty}"))}]";
}

public interface IClaimCommand
{
    string Describe();
    void Execute(ClaimDraft draft);
    void Undo(ClaimDraft draft);
}

public sealed class AddLineCommand(string code, int quantity) : IClaimCommand
{
    public string Describe() => $"add {code} x{quantity}";
    public void Execute(ClaimDraft draft) => draft.AddLine(code, quantity);
    public void Undo(ClaimDraft draft) => draft.RemoveLine(code);
}

public sealed class ChangePayerCommand(string newPayer) : IClaimCommand
{
    private string? _previous;

    public string Describe() => $"change payer to {newPayer}";

    public void Execute(ClaimDraft draft)
    {
        _previous = draft.PayerCode;
        draft.PayerCode = newPayer;
    }

    public void Undo(ClaimDraft draft) => draft.PayerCode = _previous ?? draft.PayerCode;
}

/// <summary>The invoker: it keeps history, so undo is free for every command ever written.</summary>
public sealed class ClaimEditor(ClaimDraft draft)
{
    private readonly Stack<IClaimCommand> _history = new();

    public IReadOnlyCollection<string> History => _history.Select(c => c.Describe()).ToArray();

    public void Do(IClaimCommand command)
    {
        command.Execute(draft);
        _history.Push(command);
    }

    public bool Undo()
    {
        if (!_history.TryPop(out var command)) return false;

        command.Undo(draft);
        return true;
    }
}

public sealed class CommandDemo : PatternDemo
{
    public override string Key => "command";
    public override string Name => "Command";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Wrap a request as an object so it can be queued, logged or undone.";
    public override string HandbookSection => "1.6";
    public override string SeenInDotNet => "MediatR requests, ICommand in WPF, SubmitClaimCommand in this solution";

    protected override void Execute(Action<string> log)
    {
        var draft = new ClaimDraft();
        var editor = new ClaimEditor(draft);

        editor.Do(new AddLineCommand("A4253", 100));
        editor.Do(new AddLineCommand("E0607", 1));
        editor.Do(new ChangePayerCommand("PBM-07"));

        log($"after 3 commands : {draft}");
        log($"history          : {string.Join(" | ", editor.History)}");

        editor.Undo();
        log($"after undo       : {draft}");

        editor.Undo();
        log($"after undo       : {draft}");

        log("Undo works for EVERY command, including ones written next year — the invoker never changes.");
    }
}
