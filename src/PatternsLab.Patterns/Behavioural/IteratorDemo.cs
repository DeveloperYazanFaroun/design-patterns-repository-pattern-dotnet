namespace PatternsLab.Patterns.Behavioural;

// ---------------------------------------------------------------------------
// ITERATOR — handbook §1.3
//
// Walk a collection without exposing how it is stored. C# has this built in:
// IEnumerable<T> is the iterator interface and `yield return` writes the iterator
// for you, which is why you almost never implement this by hand.
// ---------------------------------------------------------------------------

public sealed record Batch(string Name, IReadOnlyList<string> ClaimNumbers);

/// <summary>Storage is a list of batches; iteration is a flat sequence of claim numbers.</summary>
public sealed class ClaimBatchFile(IEnumerable<Batch> batches) : IEnumerable<string>
{
    private readonly List<Batch> _batches = batches.ToList();

    /// <summary>The compiler turns this into a full state machine implementing IEnumerator.</summary>
    public IEnumerator<string> GetEnumerator()
    {
        foreach (var batch in _batches)
            foreach (var number in batch.ClaimNumbers)
                yield return $"{batch.Name}/{number}";
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Lazy: nothing is computed until the caller asks for the next item.</summary>
    public IEnumerable<string> Streaming(Action<string> trace)
    {
        foreach (var batch in _batches)
        {
            trace($"opening batch {batch.Name}");

            foreach (var number in batch.ClaimNumbers)
                yield return number;
        }
    }
}

public sealed class IteratorDemo : PatternDemo
{
    public override string Key => "iterator";
    public override string Name => "Iterator";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Walk a collection without exposing its internals.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "IEnumerable<T>, yield return, IAsyncEnumerable<T>";

    protected override void Execute(Action<string> log)
    {
        var file = new ClaimBatchFile(
        [
            new Batch("2026-03-01", ["CLM-001", "CLM-002"]),
            new Batch("2026-03-02", ["CLM-003"]),
            new Batch("2026-03-03", ["CLM-004", "CLM-005", "CLM-006"])
        ]);

        log("foreach over the file — the caller never sees the batch list:");
        foreach (var entry in file) log("  " + entry);

        log("Laziness: Take(2) only ever opens the first batch.");
        foreach (var number in file.Streaming(log).Take(2)) log("  yielded " + number);

        log($"LINQ composes over the same iterator: {file.Count()} entries, " +
            $"{file.Count(e => e.StartsWith("2026-03-03"))} on the third.");
    }
}
