using Microsoft.EntityFrameworkCore;
using PatternsLab.Application.Pricing;
using PatternsLab.Domain.Patients.Specifications;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Infrastructure.External;
using PatternsLab.Infrastructure.Persistence;

namespace PatternsLab.Api.Endpoints;

public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit", (InMemoryAuditTrail audit) => Results.Ok(audit.Entries.Reverse()))
           .WithTags("Diagnostics")
           .WithSummary("What the domain-event observers have recorded since startup.");

        app.MapGet("/api/pricing/plans", (PricingService pricing) => Results.Ok(new
        {
            plans = pricing.AvailablePlanTypes,
            note = "Each plan is a separate IPricingRule. Adding one is a class, not an edit."
        }))
        .WithTags("Diagnostics")
        .WithSummary("The Strategy registry.");

        // Goes through the repository + a named specification, like everything else.
        // An endpoint that reached for AppDbContext directly would be exactly the leak
        // handbook §2.1 opens with.
        app.MapGet("/api/patients", async (IUnitOfWork uow, string? name, CancellationToken ct) =>
            Results.Ok((await uow.Patients.ListAsync(new PatientDirectorySpec(name), ct))
                       .Select(p => new { p.Id, p.FullName, p.MemberNumber, p.SubmittedClaimCount })))
        .WithTags("Diagnostics")
        .WithSummary("Patient ids, so you have something to POST a claim against.");

        app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct)
                ? Results.Ok(new { status = "healthy", provider = db.Database.ProviderName })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
        .WithTags("Diagnostics");

        return app;
    }
}
