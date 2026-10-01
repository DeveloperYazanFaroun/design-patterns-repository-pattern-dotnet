using PatternsLab.Domain.Common;

namespace PatternsLab.Domain.Claims;

/// <summary>
/// Part of the Claim aggregate — NOT an aggregate root.
///
/// This is why there is no <c>IClaimLineRepository</c> anywhere in this solution:
/// a line has no life outside its claim, so giving it a repository would be the
/// "repository per table" anti-pattern from handbook §4.3.
/// </summary>
public sealed class ClaimLine : Entity
{
    private ClaimLine() { }

    private ClaimLine(string procedureCode, int quantity, Money unitPrice)
    {
        ProcedureCode = procedureCode;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public string ProcedureCode { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = Money.Zero();

    public Money LineTotal => UnitPrice * Quantity;

    // 'internal' so only the aggregate root in this assembly can construct one.
    internal static ClaimLine Create(string procedureCode, int quantity, Money unitPrice) =>
        new(procedureCode, quantity, unitPrice);

    internal void OverridePrice(Money unitPrice) => UnitPrice = unitPrice;

    internal void ChangeQuantity(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        Quantity = quantity;
    }
}
