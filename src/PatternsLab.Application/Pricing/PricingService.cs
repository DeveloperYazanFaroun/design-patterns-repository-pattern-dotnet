using PatternsLab.Domain.Claims;

namespace PatternsLab.Application.Pricing;

/// <summary>
/// STRATEGY, consumer side (handbook §1.6).
///
/// Compare with the version this replaces:
/// <code>
/// public decimal Price(Claim c) => c.PlanType switch { "flat" => …, "tiered" => …, … };
/// </code>
/// That switch grew an arm every sprint, in one file, forever. Here a new plan type is
/// a new class implementing <see cref="IPricingRule"/> plus one DI registration, and
/// this file never changes again.
/// </summary>
public sealed class PricingService(IEnumerable<IPricingRule> rules)
{
    private readonly IReadOnlyDictionary<string, IPricingRule> _rules =
        rules.ToDictionary(r => r.PlanType, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> AvailablePlanTypes => _rules.Keys.ToArray();

    public IPricingRule RuleFor(string planType) =>
        _rules.TryGetValue(planType, out var rule)
            ? rule
            : throw new NotSupportedException(
                $"No pricing rule is registered for plan type '{planType}'. " +
                $"Known plan types: {string.Join(", ", _rules.Keys)}.");

    public Money PriceLine(string planType, string procedureCode, int quantity) =>
        RuleFor(planType).PriceFor(procedureCode, quantity);
}
