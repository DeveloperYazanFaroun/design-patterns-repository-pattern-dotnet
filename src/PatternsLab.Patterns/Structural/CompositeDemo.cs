namespace PatternsLab.Patterns.Structural;

// ---------------------------------------------------------------------------
// COMPOSITE — handbook §1.3
//
// Treat "one thing" and "a tree of things" through the same interface, so client
// code contains no `if (isGroup)` branch anywhere.
// ---------------------------------------------------------------------------

public interface IChargeNode
{
    string Name { get; }
    decimal Total();
    IEnumerable<string> Describe(int depth = 0);
}

/// <summary>Leaf.</summary>
public sealed class ChargeLine(string name, decimal amount) : IChargeNode
{
    public string Name => name;

    public decimal Total() => amount;

    public IEnumerable<string> Describe(int depth = 0)
    {
        yield return $"{new string(' ', depth * 2)}- {Name}: {amount:0.00}";
    }
}

/// <summary>Composite. Note it implements the SAME interface as the leaf.</summary>
public sealed class ChargeGroup(string name) : IChargeNode
{
    private readonly List<IChargeNode> _children = [];

    public string Name => name;

    public ChargeGroup Add(IChargeNode child)
    {
        _children.Add(child);
        return this;
    }

    public decimal Total() => _children.Sum(c => c.Total());

    public IEnumerable<string> Describe(int depth = 0)
    {
        yield return $"{new string(' ', depth * 2)}+ {Name} ({Total():0.00})";

        foreach (var line in _children.SelectMany(c => c.Describe(depth + 1)))
            yield return line;
    }
}

public sealed class CompositeDemo : PatternDemo
{
    public override string Key => "composite";
    public override string Name => "Composite";
    public override PatternCategory Category => PatternCategory.Structural;
    public override string Intent => "Treat a tree of objects and a single object uniformly.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "IConfiguration sections, Razor component trees, the file system";

    protected override void Execute(Action<string> log)
    {
        var invoice = new ChargeGroup("Claim CLM-001")
            .Add(new ChargeGroup("Pharmacy")
                .Add(new ChargeLine("A4253 x100", 42.00m))
                .Add(new ChargeLine("J1815 x30", 187.50m)))
            .Add(new ChargeGroup("Equipment")
                .Add(new ChargeLine("E0607", 38.00m)))
            .Add(new ChargeLine("Dispensing fee", 4.50m));

        foreach (var line in invoice.Describe())
            log(line);

        log($"Total(): {invoice.Total():0.00}");
        log("Total() is recursive and the caller never asks whether a node is a leaf or a branch.");
    }
}
