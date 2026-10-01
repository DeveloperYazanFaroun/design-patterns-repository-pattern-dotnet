using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PatternsLab.Application.Abstractions;
using PatternsLab.Domain.Abstractions;

namespace PatternsLab.Infrastructure.External;

/// <summary>
/// Injected everywhere instead of <c>DateTimeOffset.UtcNow</c>. Tests replace it with a
/// fixed clock, which is what makes "outstanding for 30 days" assertable without waiting.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Test double for the payer's eligibility API.</summary>
public sealed class StubEligibilityChecker(ILogger<StubEligibilityChecker> logger) : IEligibilityChecker
{
    public Task<EligibilityResult> VerifyAsync(
        Guid patientId, DateOnly serviceDate, CancellationToken ct = default)
    {
        // Deterministic rule so the sample behaves the same on every machine:
        // patients whose id ends in an even byte are on the tiered plan.
        var planType = patientId.ToByteArray()[^1] % 2 == 0 ? "tiered" : "contracted";

        logger.LogDebug("Eligibility for {PatientId} on {ServiceDate}: {Plan}", patientId, serviceDate, planType);

        return Task.FromResult(new EligibilityResult(true, null, planType));
    }
}

/// <summary>Test double for the clearing house.</summary>
public sealed class StubClearingHouseClient(ILogger<StubClearingHouseClient> logger) : IClearingHouseClient
{
    public Task<string> SendAsync(Guid claimId, string payerCode, decimal amount, CancellationToken ct = default)
    {
        var tracking = $"TRK-{payerCode}-{claimId.ToString()[..8].ToUpperInvariant()}";
        logger.LogInformation("Transmitted claim {ClaimId} to {Payer}, tracking {Tracking}",
            claimId, payerCode, tracking);

        return Task.FromResult(tracking);
    }
}

/// <summary>
/// In-memory audit trail, registered as a SINGLETON so entries survive across requests.
///
/// Note what it does NOT do: it never injects <see cref="IUnitOfWork"/> or a DbContext.
/// A singleton holding a scoped dependency is the captive-dependency bug from
/// handbook §1.4 and Table 3.1 — the single most common way a correct repository
/// implementation still fails in production.
/// </summary>
public sealed class InMemoryAuditTrail : IAuditTrail
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();

    public sealed record AuditEntry(DateTimeOffset At, string Subject, string Action, string Detail);

    public IReadOnlyCollection<AuditEntry> Entries => _entries.ToArray();

    public Task RecordAsync(string subject, string action, string detail, CancellationToken ct = default)
    {
        _entries.Enqueue(new AuditEntry(DateTimeOffset.UtcNow, subject, action, detail));

        // Keep the demo's memory bounded.
        while (_entries.Count > 500 && _entries.TryDequeue(out _)) { }

        return Task.CompletedTask;
    }
}

/// <summary>Anonymous fallback so the audit stamper always has something to write.</summary>
public sealed class AnonymousUser : ICurrentUser
{
    public string? UserId => "anonymous";
    public string? DisplayName => "Anonymous";
}
