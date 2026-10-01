using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PatternsLab.Domain.Common;

namespace PatternsLab.Infrastructure.Events;

/// <summary>
/// OBSERVER, notification side (handbook §1.6).
///
/// Resolves every <see cref="IDomainEventHandler{TEvent}"/> registered for the runtime
/// type of each event and invokes it. The aggregate that raised the event never learns
/// how many observers there were, or whether any existed at all.
///
/// A failing handler is logged and swallowed on purpose: the business transaction has
/// already committed, so throwing here would fail a request whose work actually succeeded.
/// If a side effect must be guaranteed, it belongs in an outbox, not in a handler.
/// </summary>
public sealed class DomainEventDispatcher(
    IServiceProvider services,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = services.GetServices(handlerType).Where(h => h is not null).ToList();

            if (handlers.Count == 0)
            {
                logger.LogDebug("No handler registered for {Event}.", domainEvent.GetType().Name);
                continue;
            }

            foreach (var handler in handlers)
            {
                try
                {
                    var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
                    await (Task)method.Invoke(handler, [domainEvent, ct])!;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Handler {Handler} failed for {Event} ({EventId}). The transaction is already committed.",
                        handler!.GetType().Name, domainEvent.GetType().Name, domainEvent.EventId);
                }
            }
        }
    }
}
