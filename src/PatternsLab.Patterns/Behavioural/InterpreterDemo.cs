namespace PatternsLab.Patterns.Behavioural;

// ---------------------------------------------------------------------------
// INTERPRETER — handbook §1.3
//
// Represent a small grammar as a class hierarchy and evaluate sentences in it.
// Rare in application code, but you use one every day: a LINQ expression tree is
// exactly this, and so is the Specification in this solution's domain layer.
//
// Grammar implemented below (claim filter rules):
//   expression := term (AND term | OR term)*
//   term       := "payer=" VALUE | "amount>" NUMBER | "status=" VALUE | "NOT" term
// ---------------------------------------------------------------------------

public sealed record ClaimFacts(string PayerCode, string Status, decimal Amount);

public interface IFilterExpression
{
    bool Interpret(ClaimFacts facts);
    string Describe();
}

public sealed class PayerIs(string payerCode) : IFilterExpression
{
    public bool Interpret(ClaimFacts facts) =>
        string.Equals(facts.PayerCode, payerCode, StringComparison.OrdinalIgnoreCase);

    public string Describe() => $"payer={payerCode}";
}

public sealed class StatusIs(string status) : IFilterExpression
{
    public bool Interpret(ClaimFacts facts) =>
        string.Equals(facts.Status, status, StringComparison.OrdinalIgnoreCase);

    public string Describe() => $"status={status}";
}

public sealed class AmountOver(decimal threshold) : IFilterExpression
{
    public bool Interpret(ClaimFacts facts) => facts.Amount > threshold;
    public string Describe() => $"amount>{threshold:0.##}";
}

public sealed class And(IFilterExpression left, IFilterExpression right) : IFilterExpression
{
    public bool Interpret(ClaimFacts facts) => left.Interpret(facts) && right.Interpret(facts);
    public string Describe() => $"({left.Describe()} AND {right.Describe()})";
}

public sealed class Or(IFilterExpression left, IFilterExpression right) : IFilterExpression
{
    public bool Interpret(ClaimFacts facts) => left.Interpret(facts) || right.Interpret(facts);
    public string Describe() => $"({left.Describe()} OR {right.Describe()})";
}

public sealed class Not(IFilterExpression inner) : IFilterExpression
{
    public bool Interpret(ClaimFacts facts) => !inner.Interpret(facts);
    public string Describe() => $"NOT {inner.Describe()}";
}

/// <summary>A deliberately tiny parser — the pattern is about the tree, not the parsing.</summary>
public static class FilterParser
{
    public static IFilterExpression Parse(string input)
    {
        var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var index = 0;
        var expression = ParseTerm(tokens, ref index);

        while (index < tokens.Length)
        {
            var op = tokens[index++];
            var right = ParseTerm(tokens, ref index);

            expression = op.ToUpperInvariant() switch
            {
                "AND" => new And(expression, right),
                "OR" => new Or(expression, right),
                _ => throw new FormatException($"Expected AND or OR, found '{op}'.")
            };
        }

        return expression;
    }

    private static IFilterExpression ParseTerm(string[] tokens, ref int index)
    {
        if (index >= tokens.Length) throw new FormatException("Unexpected end of expression.");

        var token = tokens[index++];

        if (token.Equals("NOT", StringComparison.OrdinalIgnoreCase))
            return new Not(ParseTerm(tokens, ref index));

        if (token.StartsWith("payer=", StringComparison.OrdinalIgnoreCase))
            return new PayerIs(token[6..]);

        if (token.StartsWith("status=", StringComparison.OrdinalIgnoreCase))
            return new StatusIs(token[7..]);

        if (token.StartsWith("amount>", StringComparison.OrdinalIgnoreCase))
            return new AmountOver(decimal.Parse(token[7..]));

        throw new FormatException($"Unrecognised term '{token}'.");
    }
}

public sealed class InterpreterDemo : PatternDemo
{
    public override string Key => "interpreter";
    public override string Name => "Interpreter";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Represent a grammar and evaluate sentences in it.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "LINQ expression trees; the Specification pattern in this solution";

    protected override void Execute(Action<string> log)
    {
        var expression = FilterParser.Parse("payer=PBM-01 AND amount>1000 AND NOT status=Paid");
        log($"parsed: {expression.Describe()}");

        ClaimFacts[] claims =
        [
            new("PBM-01", "Submitted", 4_200m),
            new("PBM-01", "Paid", 4_200m),
            new("PBM-01", "Submitted", 80m),
            new("PBM-07", "Submitted", 9_000m)
        ];

        foreach (var claim in claims)
            log($"  {claim.PayerCode,-7} {claim.Status,-10} {claim.Amount,9:0.00} -> {expression.Interpret(claim)}");

        log("A Specification<T> is this pattern with EF Core, rather than a bool, doing the evaluating.");
    }
}
