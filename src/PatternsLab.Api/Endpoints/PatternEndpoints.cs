using PatternsLab.Patterns;

namespace PatternsLab.Api.Endpoints;

public static class PatternEndpoints
{
    public static IEndpointRouteBuilder MapPatternEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/patterns").WithTags("Patterns");

        group.MapGet("/", (PatternCatalogue catalogue, string? category) =>
        {
            if (string.IsNullOrWhiteSpace(category))
                return Results.Ok(new { count = catalogue.Count, patterns = catalogue.All() });

            return Enum.TryParse<PatternCategory>(category, ignoreCase: true, out var parsed)
                ? Results.Ok(new { category = parsed.ToString(), patterns = catalogue.ByCategory(parsed) })
                : Results.BadRequest(new
                {
                    error = $"Unknown category '{category}'.",
                    valid = Enum.GetNames<PatternCategory>()
                });
        })
        .WithSummary("The Gang of Four catalogue — Table 1.1 of the handbook, live.");

        group.MapGet("/{key}", (PatternCatalogue catalogue, string key) =>
            catalogue.Find(key) is { } demo
                ? Results.Ok(new
                {
                    demo.Key,
                    demo.Name,
                    category = demo.Category.ToString(),
                    demo.Intent,
                    handbookSection = demo.HandbookSection,
                    seenInDotNet = demo.SeenInDotNet
                })
                : Results.NotFound(new { error = $"No pattern demo registered under '{key}'." }))
        .WithSummary("One pattern's card.");

        group.MapPost("/{key}/run", (PatternCatalogue catalogue, string key) =>
        {
            var demo = catalogue.Find(key);
            if (demo is null)
                return Results.NotFound(new { error = $"No pattern demo registered under '{key}'." });

            return Results.Ok(new
            {
                demo.Key,
                demo.Name,
                transcript = demo.Run()
            });
        })
        .WithSummary("Run the demo and return a narrated transcript of what happened.");

        group.MapPost("/run-all", (PatternCatalogue catalogue) => Results.Ok(
                catalogue.All().Select(p => new
                {
                    p.Key,
                    p.Name,
                    p.Category,
                    transcript = catalogue.Run(p.Key)
                })))
        .WithSummary("Run every pattern demo at once — useful as a smoke test.");

        return app;
    }
}
