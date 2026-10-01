namespace PatternsLab.Patterns.Structural;

// ---------------------------------------------------------------------------
// ADAPTER — handbook §1.5
//
// You own the interface your code expects. You do NOT own the third-party SDK.
// The adapter translates one into the other — including its exceptions, which is
// the half people forget.
// ---------------------------------------------------------------------------

/// <summary>Your contract, shaped by your domain.</summary>
public interface IPaymentGateway
{
    PaymentReceipt Charge(decimal amount, string currency, string reference);
}

public sealed record PaymentReceipt(bool Succeeded, string? TransactionId, string? FailureReason);

/// <summary>Their SDK: XML in, XML out, its own exception type. You cannot change it.</summary>
public sealed class LegacyPayXmlClient
{
    public sealed class PayXmlException(string message) : Exception(message);

    public string Execute(string requestXml)
    {
        if (!requestXml.Contains("<amt>", StringComparison.Ordinal))
            throw new PayXmlException("E-4021: <amt> element missing");

        var amount = requestXml.Split("<amt>")[1].Split("</amt>")[0];

        return decimal.Parse(amount) > 10_000m
            ? "<resp><code>52</code><msg>LIMIT_EXCEEDED</msg></resp>"
            : $"<resp><code>00</code><txn>TXN{amount.Replace(".", string.Empty)}</txn></resp>";
    }
}

public sealed class LegacyPayGatewayAdapter(LegacyPayXmlClient client) : IPaymentGateway
{
    public PaymentReceipt Charge(decimal amount, string currency, string reference)
    {
        try
        {
            // 1. our vocabulary -> theirs
            var request = $"<req><ref>{reference}</ref><cur>{currency}</cur><amt>{amount:0.00}</amt></req>";
            var response = client.Execute(request);

            // 2. their result -> ours
            var code = response.Split("<code>")[1].Split("</code>")[0];

            if (code != "00")
            {
                var message = response.Split("<msg>")[1].Split("</msg>")[0];
                return new PaymentReceipt(false, null, message);
            }

            return new PaymentReceipt(true, response.Split("<txn>")[1].Split("</txn>")[0], null);
        }
        catch (LegacyPayXmlClient.PayXmlException ex)
        {
            // 3. their failure -> ours. Callers never catch a vendor exception.
            return new PaymentReceipt(false, null, ex.Message);
        }
    }
}

public sealed class AdapterDemo : PatternDemo
{
    public override string Key => "adapter";
    public override string Name => "Adapter";
    public override PatternCategory Category => PatternCategory.Structural;
    public override string Intent => "Convert one interface into another the client expects.";
    public override string HandbookSection => "1.5";
    public override string SeenInDotNet => "TextReader wrappers, any SDK shim you have ever written";

    protected override void Execute(Action<string> log)
    {
        IPaymentGateway gateway = new LegacyPayGatewayAdapter(new LegacyPayXmlClient());

        var ok = gateway.Charge(42.50m, "USD", "CLM-001");
        log($"Charge(42.50) -> succeeded={ok.Succeeded}, txn={ok.TransactionId}");

        var refused = gateway.Charge(25_000m, "USD", "CLM-002");
        log($"Charge(25000) -> succeeded={refused.Succeeded}, reason={refused.FailureReason}");

        log("The caller sees PaymentReceipt. It never sees XML, and never catches PayXmlException.");
    }
}
