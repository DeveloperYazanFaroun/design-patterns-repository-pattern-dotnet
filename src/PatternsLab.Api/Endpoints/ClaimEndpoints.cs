using PatternsLab.Application.Claims;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Common;

namespace PatternsLab.Api.Endpoints;

public sealed record CreateClaimRequest(
    Guid PatientId,
    string Number,
    DateOnly ServiceDate,
    string PayerCode,
    List<CreateClaimLine> Lines);

public sealed record CreateClaimLine(string ProcedureCode, int Quantity);

public static class ClaimEndpoints
{
    public static IEndpointRouteBuilder MapClaimEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/claims").WithTags("Claims");

        group.MapGet("/", async (
            ClaimQueryService queries,
            string? payer, ClaimStatus? status, string? number,
            int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        {
            var result = await queries.SearchAsync(payer, status, number, page: page, pageSize: pageSize, ct: ct);

            return Results.Ok(new
            {
                result.TotalCount,
                result.Page,
                result.PageSize,
                result.TotalPages,
                items = result.Items.Select(ToDto)
            });
        })
        .WithSummary("Search claims through a Specification — never through a leaked IQueryable.");

        group.MapGet("/summaries", async (
            ClaimQueryService queries, string? payer, int page = 1, int pageSize = 25,
            CancellationToken ct = default) =>
            Results.Ok(await queries.SummariesAsync(payer, page, pageSize, ct)))
        .WithSummary("Projection: the database returns six columns, not whole aggregates.");

        group.MapGet("/{id:guid}", async (ClaimQueryService queries, Guid id, CancellationToken ct) =>
            await queries.GetAsync(id, ct) is { } claim
                ? Results.Ok(ToDto(claim))
                : Results.NotFound(new { error = $"Claim {id} was not found." }))
        .WithSummary("One claim with its lines.");

        group.MapGet("/outstanding/{payerCode}", async (
            ClaimQueryService queries, string payerCode, CancellationToken ct) =>
            Results.Ok((await queries.OutstandingAsync(payerCode, ct)).Select(ToDto)))
        .WithSummary("The named business rule from handbook §2.1, living in exactly one place.");

        group.MapGet("/deleted", async (ClaimQueryService queries, CancellationToken ct) =>
            Results.Ok((await queries.DeletedAsync(ct)).Select(ToDto)))
        .WithSummary("Soft-deleted claims — shows the global query filter being opted out of.");

        group.MapPost("/", async (
            ClaimSubmissionService submission, CreateClaimRequest request, CancellationToken ct) =>
        {
            var command = new SubmitClaimCommand(
                request.PatientId,
                request.Number,
                request.ServiceDate,
                request.PayerCode,
                request.Lines.Select(l => new ClaimLineInput(l.ProcedureCode, l.Quantity)).ToList());

            var result = await submission.SubmitAsync(command, ct);

            return result.IsSuccess
                ? Results.Created($"/api/claims/{result.Value}", new { id = result.Value })
                : ToProblem(result);
        })
        .WithSummary("Submit a claim. Two aggregates change; one transaction covers both.");

        group.MapPost("/{id:guid}/reprice", async (
            ClaimSubmissionService submission, Guid id, string planType, CancellationToken ct) =>
        {
            var result = await submission.RepriceAsync(new RepriceClaimCommand(id, planType), ct);
            return result.IsSuccess ? Results.NoContent() : ToProblem(result);
        })
        .WithSummary("Re-price through a Strategy chosen at run time.");

        group.MapPost("/{id:guid}/pay", async (ClaimPaymentService payments, Guid id, CancellationToken ct) =>
        {
            var result = await payments.MarkPaidAsync(new MarkClaimPaidCommand(id, null), ct);
            return result.IsSuccess ? Results.NoContent() : ToProblem(result);
        })
        .WithSummary("Mark paid — raises ClaimPaid, dispatched after the commit.");

        group.MapDelete("/{id:guid}", async (
            PatternsLab.Domain.Abstractions.IUnitOfWork uow, Guid id, CancellationToken ct) =>
        {
            var claim = await uow.Claims.GetByIdAsync(id, ct);
            if (claim is null) return Results.NotFound(new { error = $"Claim {id} was not found." });

            uow.Claims.Remove(claim);        // becomes a soft delete inside SaveChangesAsync
            await uow.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithSummary("Delete — converted to a soft delete by the Unit of Work.");

        app.MapGet("/api/reports/totals-by-payer", async (
            ClaimQueryService queries, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        {
            var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
            var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            return Results.Ok(await queries.TotalsByPayerAsync(start, end, ct));
        })
        .WithTags("Reports")
        .WithSummary("Aggregation that belongs on the specific repository, not the generic contract.");

        return app;
    }

    private static object ToDto(Claim claim) => new
    {
        claim.Id,
        claim.Number,
        claim.PayerCode,
        status = claim.Status.ToString(),
        claim.ServiceDate,
        claim.SubmittedOn,
        total = new { claim.Total.Amount, claim.Total.Currency },
        patient = claim.Patient is null ? null : new { claim.Patient.Id, claim.Patient.FullName },
        lines = claim.Lines.Select(l => new
        {
            l.ProcedureCode,
            l.Quantity,
            unitPrice = l.UnitPrice.Amount,
            lineTotal = l.LineTotal.Amount
        })
    };

    /// <summary>Maps a domain Result onto HTTP without the API knowing about EF Core.</summary>
    private static IResult ToProblem(Result result) => result.Status switch
    {
        ResultStatus.NotFound => Results.NotFound(new { error = result.Error }),
        ResultStatus.Conflict => Results.Conflict(new { error = result.Error }),
        ResultStatus.Gone => Results.StatusCode(StatusCodes.Status410Gone),
        _ => Results.BadRequest(new { error = result.Error })
    };
}
