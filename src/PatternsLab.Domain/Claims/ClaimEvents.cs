using PatternsLab.Domain.Common;

namespace PatternsLab.Domain.Claims;

/// <summary>
/// Observer, write side. The claim raises these; it has no idea who listens.
/// The Unit of Work dispatches them after the transaction commits, so no handler
/// can react to something that was rolled back (handbook §3.12).
/// </summary>
public sealed record ClaimSubmitted(Guid ClaimId, string Number, string PayerCode, decimal Amount, string Currency)
    : DomainEvent;

public sealed record ClaimRejected(Guid ClaimId, string Number, string Reason) : DomainEvent;

public sealed record ClaimPaid(Guid ClaimId, string Number, decimal Amount, string Currency) : DomainEvent;
