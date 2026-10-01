using Microsoft.Extensions.Logging;
using PatternsLab.Application.Abstractions;
using PatternsLab.Domain.Claims;
using PatternsLab.Domain.Common;

namespace PatternsLab.Application.Claims.Handlers;

/// <summary>
/// OBSERVER, listener side (handbook §1.6).
///
/// <c>Claim.Submit()</c> has no idea this class exists. Adding a second reaction to the
/// same event means adding a class — it never means editing the aggregate.
/// </summary>
public sealed class NotifyBillingOnClaimSubmitted(
    INotifierFactory notifiers,
    ILogger<NotifyBillingOnClaimSubmitted> logger) : IDomainEventHandler<ClaimSubmitted>
{
    public async Task HandleAsync(ClaimSubmitted domainEvent, CancellationToken ct = default)
    {
        logger.LogInformation("Claim {Number} submitted to {Payer}.", domainEvent.Number, domainEvent.PayerCode);

        await notifiers.Create(NotificationChannel.Email).SendAsync(
            "billing@example.org",
            $"Claim {domainEvent.Number} ({domainEvent.Amount:0.00} {domainEvent.Currency}) " +
            $"was submitted to {domainEvent.PayerCode}.",
            ct);
    }
}

/// <summary>A second observer of the same event — neither knows about the other.</summary>
public sealed class RecordAuditOnClaimSubmitted(IAuditTrail audit) : IDomainEventHandler<ClaimSubmitted>
{
    public Task HandleAsync(ClaimSubmitted domainEvent, CancellationToken ct = default) =>
        audit.RecordAsync(
            subject: $"claim:{domainEvent.ClaimId}",
            action: "submitted",
            detail: $"payer={domainEvent.PayerCode}; amount={domainEvent.Amount:0.00} {domainEvent.Currency}",
            ct);
}

public sealed class NotifyOnClaimRejected(
    INotifierFactory notifiers,
    ILogger<NotifyOnClaimRejected> logger) : IDomainEventHandler<ClaimRejected>
{
    public async Task HandleAsync(ClaimRejected domainEvent, CancellationToken ct = default)
    {
        logger.LogWarning("Claim {Number} rejected: {Reason}", domainEvent.Number, domainEvent.Reason);

        await notifiers.Create(NotificationChannel.Sms).SendAsync(
            "+970000000000", $"Claim {domainEvent.Number} was rejected: {domainEvent.Reason}", ct);
    }
}

public sealed class RecordAuditOnClaimPaid(IAuditTrail audit) : IDomainEventHandler<ClaimPaid>
{
    public Task HandleAsync(ClaimPaid domainEvent, CancellationToken ct = default) =>
        audit.RecordAsync(
            subject: $"claim:{domainEvent.ClaimId}",
            action: "paid",
            detail: $"amount={domainEvent.Amount:0.00} {domainEvent.Currency}",
            ct);
}
