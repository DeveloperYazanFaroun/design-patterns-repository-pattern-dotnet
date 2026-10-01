using Microsoft.Extensions.Logging;
using PatternsLab.Application.Abstractions;
using PatternsLab.Application.Pricing;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Common;

namespace PatternsLab.Application.Claims;

/// <summary>
/// The payoff — handbook §3.9.
///
/// Read this class and notice what is NOT in it: no DbContext, no Include, no LINQ, no
/// SaveChanges on a repository, no connection string, no SQL. Every line is either a
/// business rule or a transaction boundary.
///
/// It is also fully unit-testable with a substituted <see cref="IUnitOfWork"/> and no
/// database at all — see <c>ClaimSubmissionServiceTests</c>.
/// </summary>
public sealed class ClaimSubmissionService(
    IUnitOfWork uow,
    PricingService pricing,
    IEligibilityChecker eligibility,
    IClock clock,
    ILogger<ClaimSubmissionService> logger)
{
    public async Task<Result<Guid>> SubmitAsync(SubmitClaimCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Lines.Count == 0)
            return Result<Guid>.Invalid("A claim needs at least one line.");

        // Guard clauses first — cheap checks before we open a transaction.
        if (await uow.Claims.NumberExistsAsync(command.Number, ct))
        {
            return Result<Guid>.Conflict(
                $"Claim number '{command.Number}' already exists. Claim numbers must be unique.");
        }

        var patient = await uow.Patients.GetByIdAsync(command.PatientId, ct);
        if (patient is null)
            return Result<Guid>.NotFound($"Patient {command.PatientId} was not found.");

        var check = await eligibility.VerifyAsync(command.PatientId, command.ServiceDate, ct);
        if (!check.IsEligible)
            return Result<Guid>.Invalid($"Patient is not eligible on {command.ServiceDate:yyyy-MM-dd}: {check.Reason}");

        var rule = pricing.RuleFor(check.PlanType ?? "flat");

        // TWO aggregates change below. One transaction covers both, or neither happens —
        // this is the property the Unit of Work is named after (Figure 3.2).
        return await uow.ExecuteInTransactionAsync(async token =>
        {
            var claim = Claim.Create(patient.Id, command.Number, command.ServiceDate, command.PayerCode);

            foreach (var line in command.Lines)
                claim.AddLine(line.ProcedureCode, line.Quantity, rule.PriceFor(line.ProcedureCode, line.Quantity));

            claim.Submit(clock.UtcNow);          // raises ClaimSubmitted
            patient.RecordSubmission(clock.UtcNow);  // second aggregate, same transaction

            uow.Claims.Add(claim);

            await uow.SaveChangesAsync(token);

            logger.LogInformation(
                "Claim {Number} submitted for patient {PatientId} — {Total}", claim.Number, patient.Id, claim.Total);

            return Result<Guid>.Success(claim.Id);
        }, ct);
    }

    public async Task<Result> RepriceAsync(RepriceClaimCommand command, CancellationToken ct = default)
    {
        var claim = await uow.Claims.GetWithLinesAsync(command.ClaimId, ct);
        if (claim is null)
            return Result.NotFound($"Claim {command.ClaimId} was not found.");

        try
        {
            claim.Reprice(pricing.RuleFor(command.PlanType));
        }
        catch (NotSupportedException ex)
        {
            return Result.Invalid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Conflict(ex.Message);
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> VoidAsync(VoidClaimCommand command, CancellationToken ct = default)
    {
        var claim = await uow.Claims.GetByIdAsync(command.ClaimId, ct);
        if (claim is null)
            return Result.NotFound($"Claim {command.ClaimId} was not found.");

        try
        {
            claim.Void(command.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Conflict(ex.Message);
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
