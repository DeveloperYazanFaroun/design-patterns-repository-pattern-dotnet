namespace PatternsLab.Application.Claims;

/// <summary>
/// COMMAND (handbook §1.6): a request expressed as an object, so it can be validated,
/// logged, queued or replayed. Here it simply keeps the service signature honest —
/// one parameter instead of nine.
/// </summary>
public sealed record SubmitClaimCommand(
    Guid PatientId,
    string Number,
    DateOnly ServiceDate,
    string PayerCode,
    IReadOnlyList<ClaimLineInput> Lines);

public sealed record ClaimLineInput(string ProcedureCode, int Quantity);

public sealed record RepriceClaimCommand(Guid ClaimId, string PlanType);

public sealed record VoidClaimCommand(Guid ClaimId, string Reason);

public sealed record MarkClaimPaidCommand(Guid ClaimId, byte[]? ExpectedRowVersion);
