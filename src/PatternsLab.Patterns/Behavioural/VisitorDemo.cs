namespace PatternsLab.Patterns.Behavioural;

// ---------------------------------------------------------------------------
// VISITOR — handbook §1.3
//
// Add a NEW OPERATION to a fixed class hierarchy without editing the classes.
//
// The trade is explicit: operations become cheap to add, new node TYPES become
// expensive (every visitor must change). Use it when the hierarchy is stable and
// the operations are not — a compiler's AST, or LINQ's ExpressionVisitor.
// ---------------------------------------------------------------------------

public interface IClaimComponentVisitor<out TResult>
{
    TResult VisitLine(ClaimLineNode node);
    TResult VisitDiscount(DiscountNode node);
    TResult VisitTax(TaxNode node);
}

public abstract class ClaimComponent
{
    public abstract TResult Accept<TResult>(IClaimComponentVisitor<TResult> visitor);
}

public sealed class ClaimLineNode(string code, int quantity, decimal unitPrice) : ClaimComponent
{
    public string Code { get; } = code;
    public int Quantity { get; } = quantity;
    public decimal UnitPrice { get; } = unitPrice;

    public override TResult Accept<TResult>(IClaimComponentVisitor<TResult> visitor) => visitor.VisitLine(this);
}

public sealed class DiscountNode(string reason, decimal percentage) : ClaimComponent
{
    public string Reason { get; } = reason;
    public decimal Percentage { get; } = percentage;

    public override TResult Accept<TResult>(IClaimComponentVisitor<TResult> visitor) => visitor.VisitDiscount(this);
}

public sealed class TaxNode(string name, decimal rate) : ClaimComponent
{
    public string Name { get; } = name;
    public decimal Rate { get; } = rate;

    public override TResult Accept<TResult>(IClaimComponentVisitor<TResult> visitor) => visitor.VisitTax(this);
}

/// <summary>Operation 1 — written without touching a single node class.</summary>
public sealed class TotallingVisitor : IClaimComponentVisitor<decimal>
{
    private decimal _runningTotal;

    public decimal VisitLine(ClaimLineNode node) => _runningTotal += node.Quantity * node.UnitPrice;

    public decimal VisitDiscount(DiscountNode node) => _runningTotal -= _runningTotal * node.Percentage;

    public decimal VisitTax(TaxNode node) => _runningTotal += _runningTotal * node.Rate;
}

/// <summary>Operation 2 — added later, again without touching a node class.</summary>
public sealed class DescribingVisitor : IClaimComponentVisitor<string>
{
    public string VisitLine(ClaimLineNode node) => $"{node.Code} x{node.Quantity} @ {node.UnitPrice:0.00}";
    public string VisitDiscount(DiscountNode node) => $"discount {node.Percentage:P0} ({node.Reason})";
    public string VisitTax(TaxNode node) => $"tax {node.Name} {node.Rate:P0}";
}

public sealed class VisitorDemo : PatternDemo
{
    public override string Key => "visitor";
    public override string Name => "Visitor";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Add an operation to a class hierarchy without editing the classes.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "ExpressionVisitor — used by Specification.AndAlso in this solution";

    protected override void Execute(Action<string> log)
    {
        ClaimComponent[] components =
        [
            new ClaimLineNode("A4253", 100, 0.42m),
            new ClaimLineNode("E0607", 1, 38.00m),
            new DiscountNode("contracted rate", 0.10m),
            new TaxNode("VAT", 0.16m)
        ];

        var describe = new DescribingVisitor();
        foreach (var component in components)
            log("  " + component.Accept(describe));

        var total = new TotallingVisitor();
        decimal running = 0;
        foreach (var component in components)
            running = component.Accept(total);

        log($"total: {running:0.00}");
        log("Two operations, zero edits to ClaimLineNode / DiscountNode / TaxNode.");
        log("The cost: adding a 4th node type would force BOTH visitors to change. Know the trade.");
    }
}
