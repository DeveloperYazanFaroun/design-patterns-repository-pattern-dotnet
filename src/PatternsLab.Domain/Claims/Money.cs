namespace PatternsLab.Domain.Claims;

/// <summary>
/// Value object: no identity, compared by value, immutable — a record gives us all three
/// for free, and <c>with</c> expressions make "the same amount in another currency" cheap.
///
/// It is a record <b>class</b> rather than a <c>record struct</c> on purpose: EF Core
/// owned types must be reference types, and this is mapped with <c>OwnsOne</c> in
/// <c>ClaimConfiguration</c>. (EF Core 8+ complex properties do support structs, but
/// owned types remain the better fit for a value object stored inline.)
/// </summary>
public sealed record Money(decimal Amount, string Currency = "USD")
{
    public static Money Zero(string currency = "USD") => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left with { Amount = left.Amount + right.Amount };
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left with { Amount = left.Amount - right.Amount };
    }

    public static Money operator *(Money money, int quantity) => money with { Amount = money.Amount * quantity };

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot combine {left.Currency} with {right.Currency}. Convert first.");
        }
    }
}
