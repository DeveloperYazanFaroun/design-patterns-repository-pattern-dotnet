using PatternsLab.Domain.Common;

namespace PatternsLab.Domain.Abstractions;

/// <summary>
/// A page of results plus the unpaged total — returned instead of an
/// <c>IQueryable&lt;T&gt;</c> so paging cannot be got wrong by a caller (handbook §2.8).
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// The generic repository contract — handbook §3.3.
///
/// Deliberately small. Every method added here is inherited by every aggregate
/// whether it makes sense for it or not. Note what is NOT here:
///   • no SaveChangesAsync — that belongs to <see cref="IUnitOfWork"/>;
///   • no GetAllAsync     — that is a production incident with a friendly name;
///   • no IQueryable      — see handbook §2.8.
/// </summary>
public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken ct = default);

    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken ct = default);

    Task<IReadOnlyList<TResult>> ListAsync<TResult>(
        ISpecification<T, TResult> specification, CancellationToken ct = default);

    Task<PagedResult<T>> PageAsync(ISpecification<T> specification, int page, int pageSize,
        CancellationToken ct = default);

    Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default);

    Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken ct = default);

    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    void Update(T entity);
    void Remove(T entity);
}
