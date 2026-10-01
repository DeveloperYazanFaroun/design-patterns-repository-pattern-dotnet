namespace PatternsLab.Patterns.Behavioural;

// ---------------------------------------------------------------------------
// TEMPLATE METHOD — handbook §1.6
//
// The ORDER of the steps is fixed in one place; the steps themselves are supplied
// by subclasses. Contrast with Strategy: Strategy swaps a whole algorithm through
// composition, Template Method varies steps of a fixed one through inheritance.
// ---------------------------------------------------------------------------

public sealed record ImportReport(string Source, int Read, int Valid, int Persisted);

public abstract class ImportJob<TRow>
{
    /// <summary>The template. This method is not virtual — the order is not negotiable.</summary>
    public ImportReport Run(string payload, Action<string> log)
    {
        log($"[{Name}] parse");
        var rows = Parse(payload);

        log($"[{Name}] validate");
        var valid = rows.Where(Validate).ToList();

        log($"[{Name}] persist");
        var persisted = Persist(valid);

        var report = new ImportReport(Name, rows.Count, valid.Count, persisted);

        OnCompleted(report, log);      // optional hook with a default
        return report;
    }

    protected abstract string Name { get; }
    protected abstract IReadOnlyList<TRow> Parse(string payload);
    protected abstract bool Validate(TRow row);
    protected abstract int Persist(IReadOnlyList<TRow> rows);

    /// <summary>A HOOK: virtual with a do-nothing default, so subclasses opt in.</summary>
    protected virtual void OnCompleted(ImportReport report, Action<string> log) { }
}

public sealed class CsvClaimImport : ImportJob<string[]>
{
    protected override string Name => "csv";

    protected override IReadOnlyList<string[]> Parse(string payload) =>
        payload.Split('\n', StringSplitOptions.RemoveEmptyEntries)
               .Select(line => line.Split(','))
               .ToList();

    protected override bool Validate(string[] row) => row.Length == 2 && int.TryParse(row[1], out var q) && q > 0;

    protected override int Persist(IReadOnlyList<string[]> rows) => rows.Count;

    protected override void OnCompleted(ImportReport report, Action<string> log) =>
        log($"[csv] emailed a summary: {report.Valid}/{report.Read} rows accepted");
}

public sealed class PipeDelimitedImport : ImportJob<string[]>
{
    protected override string Name => "pipe";

    protected override IReadOnlyList<string[]> Parse(string payload) =>
        payload.Split('\n', StringSplitOptions.RemoveEmptyEntries)
               .Select(line => line.Split('|'))
               .ToList();

    protected override bool Validate(string[] row) => row.Length >= 2;

    protected override int Persist(IReadOnlyList<string[]> rows) => rows.Count;
    // No OnCompleted override — the default hook does nothing, and that is fine.
}

public sealed class TemplateMethodDemo : PatternDemo
{
    public override string Key => "template-method";
    public override string Name => "Template Method";
    public override PatternCategory Category => PatternCategory.Behavioural;
    public override string Intent => "Fix the skeleton of an algorithm, let subclasses fill in steps.";
    public override string HandbookSection => "1.6";
    public override string SeenInDotNet => "BackgroundService.ExecuteAsync, ControllerBase filters";

    protected override void Execute(Action<string> log)
    {
        var csv = new CsvClaimImport().Run("A4253,100\nE0607,1\nBAD,0\nJ1815,30", log);
        log($"  -> {csv}");

        var pipe = new PipeDelimitedImport().Run("A4253|100|note\nE0607|1", log);
        log($"  -> {pipe}");

        log("parse -> validate -> persist -> notify is written ONCE. Neither subclass can reorder it.");
    }
}
