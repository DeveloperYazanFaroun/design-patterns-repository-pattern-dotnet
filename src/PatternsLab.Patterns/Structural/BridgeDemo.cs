namespace PatternsLab.Patterns.Structural;

// ---------------------------------------------------------------------------
// BRIDGE — handbook §1.3
//
// Two things vary independently: WHAT you are reporting (summary, detailed) and
// HOW you render it (text, HTML, CSV). Inheritance would need 2 x 3 = 6 classes,
// and 3 x 4 = 12 the moment either side grows.
//
// Bridge splits the abstraction from the implementation, so you write 2 + 3.
// ---------------------------------------------------------------------------

/// <summary>The implementation side: HOW output is rendered.</summary>
public interface IReportRenderer
{
    string Heading(string text);
    string Row(string label, string value);
    string Finish();
}

public sealed class PlainTextRenderer : IReportRenderer
{
    public string Heading(string text) => $"== {text} ==";
    public string Row(string label, string value) => $"{label,-22}: {value}";
    public string Finish() => "-- end --";
}

public sealed class HtmlRenderer : IReportRenderer
{
    public string Heading(string text) => $"<h2>{text}</h2>";
    public string Row(string label, string value) => $"<tr><th>{label}</th><td>{value}</td></tr>";
    public string Finish() => "</table>";
}

public sealed class CsvRenderer : IReportRenderer
{
    public string Heading(string text) => $"# {text}";
    public string Row(string label, string value) => $"{label},{value}";
    public string Finish() => string.Empty;
}

/// <summary>The abstraction side: WHAT is being reported. Holds a renderer, is not one.</summary>
public abstract class ClaimReport(IReportRenderer renderer)
{
    protected IReportRenderer Renderer { get; } = renderer;

    public abstract IEnumerable<string> Render(int claimCount, decimal total, string payer);
}

public sealed class SummaryReport(IReportRenderer renderer) : ClaimReport(renderer)
{
    public override IEnumerable<string> Render(int claimCount, decimal total, string payer)
    {
        yield return Renderer.Heading($"Summary — {payer}");
        yield return Renderer.Row("Claims", claimCount.ToString());
        yield return Renderer.Row("Total", total.ToString("0.00"));
        yield return Renderer.Finish();
    }
}

public sealed class DetailedReport(IReportRenderer renderer) : ClaimReport(renderer)
{
    public override IEnumerable<string> Render(int claimCount, decimal total, string payer)
    {
        yield return Renderer.Heading($"Detailed — {payer}");
        yield return Renderer.Row("Claims", claimCount.ToString());
        yield return Renderer.Row("Total", total.ToString("0.00"));
        yield return Renderer.Row("Average", claimCount == 0 ? "n/a" : (total / claimCount).ToString("0.00"));
        yield return Renderer.Row("Generated", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
        yield return Renderer.Finish();
    }
}

public sealed class BridgeDemo : PatternDemo
{
    public override string Key => "bridge";
    public override string Name => "Bridge";
    public override PatternCategory Category => PatternCategory.Structural;
    public override string Intent => "Split an abstraction from its implementation so both vary independently.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "ILogger vs. logging providers";

    protected override void Execute(Action<string> log)
    {
        (string Label, ClaimReport Report)[] combinations =
        [
            ("SummaryReport  + PlainTextRenderer", new SummaryReport(new PlainTextRenderer())),
            ("DetailedReport + CsvRenderer", new DetailedReport(new CsvRenderer())),
            ("SummaryReport  + HtmlRenderer", new SummaryReport(new HtmlRenderer()))
        ];

        foreach (var (label, report) in combinations)
        {
            log($"{label}:");
            foreach (var line in report.Render(12, 4_812.75m, "PBM-01").Where(l => l.Length > 0))
                log("  " + line);
        }

        log("2 report types x 3 renderers = 5 classes, not 6. Add a 4th renderer: 1 class, not 2.");
    }
}
