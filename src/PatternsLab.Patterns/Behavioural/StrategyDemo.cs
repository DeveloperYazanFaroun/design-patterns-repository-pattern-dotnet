namespace PatternsLab.Patterns.Behavioural;

// ---------------------------------------------------------------------------
// STRATEGY — handbook §1.6, Figure 1.4
//
// The cure for the switch that grows a new arm every sprint. A new algorithm is a
// new CLASS, never an edit to the service — the Open/Closed Principle, concretely.
//
// The production version lives in PatternsLab.Domain/Claims/IPricingRule.cs.
// ---------------------------------------------------------------------------

public interface IPricingStrategy
{
    string PlanType { get; }
    decimal Price(string procedureCode, int quantity);
}

public sealed class FlatRate : IPricingStrategy
{
    public string PlanType => "flat";
    public decimal Price(string procedureCode, int quantity) => quantity * 10m;
}

public sealed class Tiered : IPricingStrategy
{
    public string PlanType => "tiered";

    public decimal Price(string procedureCode, int quantity) => quantity * (quantity switch
    {
        >= 100 => 7.00m,
        >= 50 => 8.50m,
        >= 10 => 9.50m,
        _ => 10.00m
    });
}

public sealed class Contracted : IPricingStrategy
{
    private static readonly Dictionary<string, decimal> Rates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A4253"] = 0.42m,
        ["E0607"] = 38.00m
    };

    public string PlanType => "contracted";

    public decimal Price(string procedureCode, int quantity) =>
        quantity * (Rates.TryGetValue(procedureCode, out var rate) ? rate : 15m);
}

/// <summary>Receives every strategy from DI and picks one. This file never changes again.</summary>
public sealed class PricingContext(IEnumerable<IPricingStrategy> strategies)
{
    private readonly IReadOnlyDictionary<string, IPricingStrategy> _byPlan =
        strategies.ToDictionary(s => s.PlanType, StringComparer.OrdinalIgnoreCase);

    public decimal Price(string planType, string procedureCode, int quantity) =>
        _byPlan.TryGetValue(planType, out var strategy)
            ? strategy.Price(procedureCode, quantity)
            : throw new NotSupportedException($"No pricing strategy for plan '{planType}'.");
}

public sealed class StrategyDemo : PatternDemo
{
    public override string Key => "strategy";
    public override string Name => "Strategy";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Make a family of algorithms interchangeable at run time.";
    public override string HandbookSection => "1.6";
    public override string SeenInDotNet => "IComparer<T>, IPricingRule in this solution's domain";

    protected override void Execute(Action<string> log)
    {
        var context = new PricingContext([new FlatRate(), new Tiered(), new Contracted()]);

        foreach (var plan in new[] { "flat", "tiered", "contracted" })
            log($"{plan,-11} A4253 x100 -> {context.Price(plan, "A4253", 100),10:0.00}");

        try
        {
            context.Price("capitation", "A4253", 100);
        }
        catch (NotSupportedException ex)
        {
            log($"Unknown plan fails in one place: {ex.Message}");
        }

        log("Adding 'capitation' = 1 new class + 1 registration. PricingContext is never touched.");
    }
}
