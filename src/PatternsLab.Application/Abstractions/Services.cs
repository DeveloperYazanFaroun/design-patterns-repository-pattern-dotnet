namespace PatternsLab.Application.Abstractions;

/// <summary>
/// The application's own contract for "tell someone something". Third-party SDKs are
/// bent to fit this shape by an ADAPTER in the infrastructure layer (handbook §1.5) —
/// the application never sees the vendor's types.
/// </summary>
public interface INotifier
{
    string Channel { get; }

    Task SendAsync(string recipient, string message, CancellationToken ct = default);
}

public enum NotificationChannel
{
    Email = 0,
    Sms = 1,
    Push = 2
}

/// <summary>
/// FACTORY METHOD (handbook §1.4, Figure 1.2). The caller asks for a channel and gets
/// an <see cref="INotifier"/>; adding a channel is a new class plus one registration.
/// </summary>
public interface INotifierFactory
{
    INotifier Create(NotificationChannel channel);
}

public sealed record EligibilityResult(bool IsEligible, string? Reason, string? PlanType);

/// <summary>Stands in for a payer's eligibility API.</summary>
public interface IEligibilityChecker
{
    Task<EligibilityResult> VerifyAsync(Guid patientId, DateOnly serviceDate, CancellationToken ct = default);
}

/// <summary>Stands in for the clearing house a submitted claim is transmitted to.</summary>
public interface IClearingHouseClient
{
    Task<string> SendAsync(Guid claimId, string payerCode, decimal amount, CancellationToken ct = default);
}

/// <summary>Append-only record of what happened, for compliance.</summary>
public interface IAuditTrail
{
    Task RecordAsync(string subject, string action, string detail, CancellationToken ct = default);
}
