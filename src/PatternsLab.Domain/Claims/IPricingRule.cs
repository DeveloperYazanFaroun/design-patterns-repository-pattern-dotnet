namespace PatternsLab.Domain.Claims;

/// <summary>
/// STRATEGY (handbook §1.6, Figure 1.4).
///
/// Adding a new pricing scheme is a new class implementing this interface plus one
/// DI registration. It is never an edit to <c>PricingService</c> or to <c>Claim</c> —
/// that is the Open/Closed Principle made concrete.
/// </summary>
public interface IPricingRule
{
    /// <summary>Matches the payer's plan type, e.g. "flat", "tiered", "contracted".</summary>
    string PlanType { get; }

    Money PriceFor(string procedureCode, int quantity);
}

/// <summary>Flat rate: every procedure costs the same, regardless of code or volume.</summary>
public sealed class FlatRatePricing(decimal unitPrice = 10m) : IPricingRule
{
    public string PlanType => "flat";

    public Money PriceFor(string procedureCode, int quantity) => new(unitPrice);
}

/// <summary>Volume discount: the more you buy, the cheaper the unit.</summary>
public sealed class TieredPricing(decimal basePrice = 12m) : IPricingRule
{
    public string PlanType => "tiered";

    public Money PriceFor(string procedureCode, int quantity)
    {
        var multiplier = quantity switch
        {
            >= 100 => 0.70m,
            >= 50 => 0.85m,
            >= 10 => 0.95m,
            _ => 1.00m
        };

        return new Money(decimal.Round(basePrice * multiplier, 2));
    }
}

/// <summary>Negotiated rates per procedure code, with a fallback for anything unlisted.</summary>
public sealed class ContractedRatePricing : IPricingRule
{
    private readonly Dictionary<string, decimal> _rates;
    private readonly decimal _fallback;

    public ContractedRatePricing(IReadOnlyDictionary<string, decimal>? rates = null, decimal fallback = 15m)
    {
        _rates = new Dictionary<string, decimal>(
            rates ?? new Dictionary<string, decimal>
            {
                ["A4253"] = 0.42m,
                ["E0607"] = 38.00m,
                ["J1815"] = 6.25m
            },
            StringComparer.OrdinalIgnoreCase);

        _fallback = fallback;
    }

    public string PlanType => "contracted";

    public Money PriceFor(string procedureCode, int quantity) =>
        new(_rates.TryGetValue(procedureCode, out var rate) ? rate : _fallback);
}
