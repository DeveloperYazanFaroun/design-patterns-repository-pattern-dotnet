namespace PatternsLab.Patterns.Creational;

// ---------------------------------------------------------------------------
// BUILDER — handbook §1.4
//
// Problem: a constructor with eleven parameters, half of them optional, and an
// object that can exist in a half-built invalid state.
//
// Solution: assemble step by step, validate once, hand back a finished object.
// ---------------------------------------------------------------------------

public sealed record DemoClaimLine(string ProcedureCode, int Quantity, decimal UnitPrice)
{
    public decimal Total => Quantity * UnitPrice;
}

public sealed record DemoClaim(
    Guid PatientId,
    DateOnly ServiceDate,
    string PayerCode,
    IReadOnlyList<DemoClaimLine> Lines)
{
    public decimal Total => Lines.Sum(l => l.Total);
}

public sealed class ClaimBuilder
{
    private readonly List<DemoClaimLine> _lines = [];
    private Guid _patientId;
    private DateOnly _serviceDate = DateOnly.FromDateTime(DateTime.UtcNow);
    private string? _payerCode;

    public ClaimBuilder ForPatient(Guid patientId) { _patientId = patientId; return this; }
    public ClaimBuilder OnDate(DateOnly date) { _serviceDate = date; return this; }
    public ClaimBuilder BilledTo(string payerCode) { _payerCode = payerCode; return this; }

    public ClaimBuilder AddLine(string code, int quantity, decimal unitPrice)
    {
        _lines.Add(new DemoClaimLine(code, quantity, unitPrice));
        return this;
    }

    /// <summary>Validation happens ONCE, here — not scattered across setters.</summary>
    public DemoClaim Build()
    {
        if (_patientId == Guid.Empty) throw new InvalidOperationException("Patient is required.");
        if (string.IsNullOrWhiteSpace(_payerCode)) throw new InvalidOperationException("Payer is required.");
        if (_lines.Count == 0) throw new InvalidOperationException("A claim needs at least one line.");

        return new DemoClaim(_patientId, _serviceDate, _payerCode, _lines.AsReadOnly());
    }
}

public sealed class BuilderDemo : PatternDemo
{
    public override string Key => "builder";
    public override string Name => "Builder";
    public override PatternCategory Category => PatternCategory.Creational;
    public override string Intent => "Assemble a complex object step by step; same steps, different results.";
    public override string HandbookSection => "1.4";
    public override string SeenInDotNet => "WebApplicationBuilder, StringBuilder, DbContextOptionsBuilder";

    protected override void Execute(Action<string> log)
    {
        var claim = new ClaimBuilder()
            .ForPatient(Guid.CreateVersion7())
            .BilledTo("PBM-01")
            .OnDate(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)))
            .AddLine("A4253", quantity: 100, unitPrice: 0.42m)
            .AddLine("E0607", quantity: 1, unitPrice: 38.00m)
            .Build();

        log($"Built a claim for {claim.PayerCode} with {claim.Lines.Count} lines, total {claim.Total:0.00}.");
        log("The call site reads like the domain, not like a constructor.");

        try
        {
            _ = new ClaimBuilder().BilledTo("PBM-01").Build();
        }
        catch (InvalidOperationException ex)
        {
            log($"A half-built claim cannot escape the builder: \"{ex.Message}\"");
        }
    }
}
