namespace PatternsLab.Domain.Common;

/// <summary>
/// Something business-significant that has already happened. Past tense, always.
/// Handbook §1.6 (Observer) and §3.6.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}

/// <summary>Convenience base so every event does not repeat the two boilerplate members.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Observer for a single event type. Implementations are discovered by DI, so an
/// aggregate never learns the names of the things that react to it.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}

/// <summary>
/// Fans a batch of events out to their handlers. Called by the Unit of Work
/// <em>after</em> SaveChangesAsync returns — never before (handbook §3.12).
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default);
}
