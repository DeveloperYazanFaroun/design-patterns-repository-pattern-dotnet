namespace PatternsLab.Patterns.Structural;

// ---------------------------------------------------------------------------
// FLYWEIGHT — handbook §1.3
//
// Split state into INTRINSIC (shared, immutable — the procedure's description and
// unit price) and EXTRINSIC (per-instance — the quantity on this claim). Share the
// intrinsic part across every instance instead of storing 100 000 copies of it.
// ---------------------------------------------------------------------------

/// <summary>Intrinsic state — immutable, shared by every line that uses this code.</summary>
public sealed record ProcedureCode(string Code, string Description, decimal ListPrice);

public sealed class ProcedureCodeFactory
{
    private readonly Dictionary<string, ProcedureCode> _pool = new(StringComparer.OrdinalIgnoreCase);

    public int DistinctObjects => _pool.Count;
    public int Requests { get; private set; }

    public ProcedureCode Get(string code)
    {
        Requests++;

        if (_pool.TryGetValue(code, out var existing)) return existing;

        return _pool[code] = new ProcedureCode(
            code,
            Description: $"Procedure {code.ToUpperInvariant()}",
            ListPrice: 0.10m * code.Sum(c => c));
    }
}

/// <summary>Extrinsic state lives here; the shared flyweight is referenced, not copied.</summary>
public sealed record BillingLine(ProcedureCode Procedure, int Quantity)
{
    public decimal Total => Procedure.ListPrice * Quantity;
}

public sealed class FlyweightDemo : PatternDemo
{
    public override string Key => "flyweight";
    public override string Name => "Flyweight";
    public override PatternCategory Category => PatternCategory.Structural;
    public override string Intent => "Share immutable state across many objects to save memory.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "String interning, ArrayPool<T>, Encoding.UTF8";

    protected override void Execute(Action<string> log)
    {
        var factory = new ProcedureCodeFactory();
        string[] codes = ["A4253", "E0607", "J1815"];

        var lines = Enumerable.Range(0, 30_000)
                              .Select(i => new BillingLine(factory.Get(codes[i % codes.Length]), i % 7 + 1))
                              .ToList();

        log($"{lines.Count:N0} billing lines requested a procedure code {factory.Requests:N0} times.");
        log($"Distinct ProcedureCode objects actually allocated: {factory.DistinctObjects}.");
        log($"Every line for A4253 points at the SAME instance: " +
            $"{ReferenceEquals(lines[0].Procedure, lines[3].Procedure)}");
        log($"Total billed: {lines.Sum(l => l.Total):N2}");
        log("Use it only when profiling says memory is the problem — it is an optimisation, not a default.");
    }
}
