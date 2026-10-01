using Microsoft.EntityFrameworkCore;
using PatternsLab.Api;
using PatternsLab.Api.Endpoints;
using PatternsLab.Api.Workers;
using PatternsLab.Infrastructure.DependencyInjection;
using PatternsLab.Infrastructure.Persistence;
using PatternsLab.Patterns;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// COMPOSITION ROOT — the only place in the solution that knows every layer.
// Handbook §3.8: lifetimes are the point. See InfrastructureServiceCollectionExtensions.
// ---------------------------------------------------------------------------
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPatternCatalogue();

// BackgroundService is a Template Method (handbook §1.6) AND the place the
// captive-dependency rule bites: it is a singleton, so it takes IServiceScopeFactory,
// never IUnitOfWork.
builder.Services.AddHostedService<OutstandingClaimChaser>();

// The OpenAPI document. Every endpoint's .WithTags()/.WithSummary() ends up here, which
// is what makes the Swagger page below readable rather than a wall of untitled routes.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info = new()
    {
        Title = "PatternsLab API",
        Version = "v1",
        Description = "Reference application for Design Patterns & the Repository Pattern. "
                      + "Start with GET /api/patients to get an id you can POST a claim against."
    };
    return Task.CompletedTask;
}));

builder.Services.AddProblemDetails();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database bootstrap. EnsureCreated is for the sample only — a real project uses
// `dotnet ef migrations add` and `Migrate()`. See README, "Database".
// ---------------------------------------------------------------------------
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.EnsureSeededAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    // /openapi/v1.json is the machine-readable contract; /swagger renders it with a
    // "Try it out" button on every route. Development only — a public API does not
    // hand strangers an executable map of itself.
    app.MapOpenApi();

    app.UseSwaggerUI(ui =>
    {
        ui.SwaggerEndpoint("/openapi/v1.json", "PatternsLab API v1");
        ui.RoutePrefix = "swagger";
        ui.DocumentTitle = "PatternsLab API";
        ui.DisplayRequestDuration();          // makes the CachingClaimRepository decorator visible
        ui.EnableTryItOutByDefault();
    });
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/", () => Results.Ok(new
{
    name = "Design Patterns & the Repository Pattern — reference application",
    swagger = "/swagger",
    openApiDocument = "/openapi/v1.json",
    handbook = "docs/Design-Patterns-and-the-Repository-Pattern.pdf",
    exercises = "docs/EXERCISES.md",
    endpoints = new[]
    {
        "GET  /api/patterns",
        "GET  /api/patterns/{key}",
        "POST /api/patterns/{key}/run",
        "GET  /api/claims",
        "GET  /api/claims/{id}",
        "POST /api/claims",
        "GET  /api/claims/outstanding/{payerCode}",
        "GET  /api/reports/totals-by-payer",
        "GET  /api/audit"
    }
})).WithSummary("Where to start.");

app.MapPatternEndpoints();
app.MapClaimEndpoints();
app.MapDiagnosticsEndpoints();

app.Run();

/// <summary>Exposed so the integration test project can spin the API up in-process.</summary>
public partial class Program;
