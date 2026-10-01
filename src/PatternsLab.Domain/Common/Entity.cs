namespace PatternsLab.Domain.Common;

/// <summary>
/// Base type for anything with an identity that outlives a single request.
/// Handbook §3.2.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    // Identity equality: two instances are the same entity if they share an Id,
    // regardless of which DbContext loaded them or what their fields hold.
    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>
/// Marks the entry point of an aggregate. Only aggregate roots get a repository —
/// handbook §4.3, anti-pattern 4 ("repository per table").
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Events raised but not yet dispatched. The Unit of Work drains this after commit.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Observer pattern, write side: the aggregate announces, it does not call handlers.</summary>
    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>Stamped automatically in <c>UnitOfWork.SaveChangesAsync</c> — never by hand.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedOn { get; set; }
    string? CreatedBy { get; set; }
    DateTimeOffset? ModifiedOn { get; set; }
    string? ModifiedBy { get; set; }
}

/// <summary>
/// Deletes on these types are converted to updates in the Unit of Work, and a global
/// query filter hides them. Opt back in with <c>Specification.IncludeDeleted()</c>.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedOn { get; set; }
}

/// <summary>Carries a concurrency token so two editors cannot silently overwrite each other.</summary>
public interface IHasConcurrencyToken
{
    byte[]? RowVersion { get; set; }
}
