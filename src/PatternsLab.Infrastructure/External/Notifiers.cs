using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PatternsLab.Application.Abstractions;

namespace PatternsLab.Infrastructure.External;

/// <summary>
/// ADAPTER (handbook §1.5).
///
/// Pretend <c>LegacySmtpGateway</c> is a vendor SDK you do not own and cannot change:
/// it speaks its own vocabulary and throws its own exceptions. This class translates
/// both into ours, so nothing above the infrastructure layer ever sees it.
/// </summary>
public sealed class EmailNotifierAdapter(LegacySmtpGateway gateway, ILogger<EmailNotifierAdapter> logger)
    : INotifier
{
    public string Channel => "email";

    public async Task SendAsync(string recipient, string message, CancellationToken ct = default)
    {
        try
        {
            // Their vocabulary: "envelope", "deliver", an int status code.
            var status = await gateway.DeliverAsync(
                new LegacySmtpGateway.Envelope("no-reply@patternslab.local", recipient, "PatternsLab", message), ct);

            if (status != 0)
                throw new NotificationFailedException($"SMTP gateway returned status {status}.");

            logger.LogInformation("email -> {Recipient}: {Message}", recipient, message);
        }
        catch (LegacySmtpGateway.GatewayException ex)
        {
            // Their failure becomes our failure. Callers never catch a vendor exception.
            throw new NotificationFailedException("Email delivery failed.", ex);
        }
    }
}

public sealed class SmsNotifier(ILogger<SmsNotifier> logger) : INotifier
{
    public string Channel => "sms";

    public Task SendAsync(string recipient, string message, CancellationToken ct = default)
    {
        logger.LogInformation("sms -> {Recipient}: {Message}", recipient, message);
        return Task.CompletedTask;
    }
}

public sealed class PushNotifier(ILogger<PushNotifier> logger) : INotifier
{
    public string Channel => "push";

    public Task SendAsync(string recipient, string message, CancellationToken ct = default)
    {
        logger.LogInformation("push -> {Recipient}: {Message}", recipient, message);
        return Task.CompletedTask;
    }
}

public sealed class NotificationFailedException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// FACTORY METHOD (handbook §1.4, Figure 1.2).
///
/// The handbook's "C# reality check" applies here: the DI container already is an
/// abstract factory, so this class is thin on purpose. Its job is to give the decision
/// a <em>name</em> and one home, not to reimplement the container.
/// </summary>
public sealed class NotifierFactory(IServiceProvider services) : INotifierFactory
{
    public INotifier Create(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Email => services.GetRequiredService<EmailNotifierAdapter>(),
        NotificationChannel.Sms => services.GetRequiredService<SmsNotifier>(),
        NotificationChannel.Push => services.GetRequiredService<PushNotifier>(),
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown notification channel.")
    };
}

/// <summary>A stand-in for the third-party SDK the adapter above wraps.</summary>
public sealed class LegacySmtpGateway
{
    public sealed record Envelope(string From, string To, string Subject, string Body);

    public sealed class GatewayException(string message) : Exception(message);

    public Task<int> DeliverAsync(Envelope envelope, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(envelope.To))
            throw new GatewayException("Recipient is required.");

        return Task.FromResult(0);   // 0 == accepted, in their dialect
    }
}
