using Microsoft.Extensions.Logging;
using PatternsLab.Application.Abstractions;
using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Common;

namespace PatternsLab.Application.Claims;

public sealed record SubmissionOutcome(bool Accepted, string? TrackingNumber, string? Reason);

/// <summary>
/// FACADE (handbook §1.5).
///
/// A caller that wants "submit this claim end to end" makes one call instead of five.
/// Note that this type <em>delegates</em> and holds no rules of its own — the moment a
/// facade starts computing prices it has become the god class nobody wants to edit.
/// </summary>
public sealed class ClaimSubmissionFacade(
    ClaimSubmissionService submission,
    IUnitOfWork uow,
    IClearingHouseClient clearingHouse,
    IAuditTrail audit,
    INotifierFactory notifiers,
    ILogger<ClaimSubmissionFacade> logger)
{
    public async Task<SubmissionOutcome> SubmitEndToEndAsync(
        SubmitClaimCommand command, CancellationToken ct = default)
    {
        var result = await submission.SubmitAsync(command, ct);

        if (!result.IsSuccess)
        {
            logger.LogInformation("Claim {Number} was not submitted: {Reason}", command.Number, result.Error);
            return new SubmissionOutcome(false, null, result.Error);
        }

        var claimId = result.Value;

        // Re-read so the clearing house is told the real amount. The alternative — having
        // SubmitAsync return the total as well as the id — is also fine; what is NOT fine is
        // the facade inventing a value, because a facade delegates and computes nothing.
        var claim = await uow.Claims.GetWithLinesAsync(claimId, ct)
            ?? throw new InvalidOperationException($"Claim {claimId} vanished immediately after submission.");

        var tracking = await clearingHouse.SendAsync(claimId, claim.PayerCode, claim.Total.Amount, ct);

        await audit.RecordAsync(
            subject: $"claim:{claimId}",
            action: "submitted",
            detail: $"tracking={tracking}; amount={claim.Total}", ct);

        await notifiers.Create(NotificationChannel.Email)
                       .SendAsync("billing@example.org", $"Claim {command.Number} submitted ({tracking}).", ct);

        return new SubmissionOutcome(true, tracking, null);
    }
}
