namespace PatternsLab.Patterns.Creational;

// ---------------------------------------------------------------------------
// PROTOTYPE — handbook §1.3
//
// Create new objects by copying a configured instance rather than rebuilding it
// from scratch. In modern C# a record's `with` expression IS this pattern, which
// is why the hand-written Clone() below exists only for contrast.
// ---------------------------------------------------------------------------

public sealed record ClaimTemplate(
    string PayerCode,
    string PlanType,
    IReadOnlyList<string> DefaultProcedureCodes,
    bool RequiresPriorAuthorisation)
{
    /// <summary>Deep copy: the list is copied too, so the clone shares nothing mutable.</summary>
    public ClaimTemplate DeepClone() => this with { DefaultProcedureCodes = DefaultProcedureCodes.ToList() };
}

public sealed class PrototypeDemo : PatternDemo
{
    public override string Key => "prototype";
    public override string Name => "Prototype";
    public override PatternCategory Category => PatternCategory.Creational;
    public override string Intent => "Create new objects by copying an existing configured instance.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "ICloneable, record `with` expressions, MemberwiseClone";

    protected override void Execute(Action<string> log)
    {
        var standard = new ClaimTemplate(
            PayerCode: "PBM-01",
            PlanType: "tiered",
            DefaultProcedureCodes: ["A4253", "E0607"],
            RequiresPriorAuthorisation: false);

        // Shallow copy via `with`: cheap, and correct when every member is immutable.
        var urgent = standard with { RequiresPriorAuthorisation = true, PlanType = "contracted" };

        log($"prototype : {standard.PayerCode}/{standard.PlanType}, priorAuth={standard.RequiresPriorAuthorisation}");
        log($"clone     : {urgent.PayerCode}/{urgent.PlanType}, priorAuth={urgent.RequiresPriorAuthorisation}");

        var deep = standard.DeepClone();
        log($"shallow `with` shares the list : {ReferenceEquals(standard.DefaultProcedureCodes, urgent.DefaultProcedureCodes)}");
        log($"DeepClone() copies it          : {!ReferenceEquals(standard.DefaultProcedureCodes, deep.DefaultProcedureCodes)}");
        log("Shallow is fine for immutable members; reach for a deep copy the moment one is mutable.");
    }
}
