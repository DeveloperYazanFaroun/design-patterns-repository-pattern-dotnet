using PatternsLab.Domain.Abstractions;
using PatternsLab.Domain.Common;

namespace PatternsLab.Application.Claims;

/// <summary>
/// Thrown by the infrastructure layer when the database reports a concurrency conflict.
/// It exists so the application can react to "someone else edited this" without
/// referencing <c>DbUpdateConcurrencyException</c> — handbook §3.10, §2.5.
/// </summary>
public sealed class ConcurrencyConflictException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed class ClaimPaymentService(IUnitOfWork uow, IClock clock)
{
    public async Task<Result> MarkPaidAsync(MarkClaimPaidCommand command, CancellationToken ct = default)
    {
        var claim = await uow.Claims.GetByIdAsync(command.ClaimId, ct);
        if (claim is null)
            return Result.NotFound($"Claim {command.ClaimId} was not found.");

        // Optimistic concurrency: the caller tells us which version it was looking at.
        if (command.ExpectedRowVersion is { Length: > 0 } expected &&
            claim.RowVersion is { } actual &&
            !expected.SequenceEqual(actual))
        {
            return Result.Conflict("The claim was modified by another user. Reload it and try again.");
        }

        try
        {
            claim.MarkPaid(clock.UtcNow);
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Conflict(ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            // The check above is a courtesy; this is the one the database enforces.
            return Result.Conflict(ex.Message);
        }
    }
}
