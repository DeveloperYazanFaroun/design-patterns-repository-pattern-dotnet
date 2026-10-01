using Microsoft.EntityFrameworkCore;
using PatternsLab.Domain.Abstractions;

namespace PatternsLab.Infrastructure.Persistence;

/// <summary>
/// Turns a <see cref="ISpecification{T}"/> into an <c>IQueryable&lt;T&gt;</c>.
///
/// This is the single place in the whole solution where specification meets EF Core.
/// Everything above it speaks specifications; everything below speaks SQL.
/// </summary>
public static class SpecificationEvaluator
{
    /// <param name="forCount">
    /// When true, ordering, includes and paging are skipped. A paged screen issues two
    /// queries from one specification — the page and the total — and applying Skip/Take
    /// to the count would make every page report "50 of 50" (handbook §3.5, §3.12).
    /// </param>
    public static IQueryable<T> Apply<T>(
        IQueryable<T> source, ISpecification<T> specification, bool forCount = false) where T : class
    {
        ArgumentNullException.ThrowIfNull(specification);

        var query = source;

        if (specification.IgnoreQueryFilters)
            query = query.IgnoreQueryFilters();

        if (specification.AsNoTracking)
            query = query.AsNoTracking();

        if (specification.Criteria is not null)
            query = query.Where(specification.Criteria);

        if (forCount)
            return query;

        query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));
        query = specification.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

        if (specification.AsSplitQuery)
            query = query.AsSplitQuery();

        if (specification.OrderBy is not null)
            query = query.OrderBy(specification.OrderBy);
        else if (specification.OrderByDescending is not null)
            query = query.OrderByDescending(specification.OrderByDescending);

        if (specification.Skip is { } skip)
            query = query.Skip(skip);

        if (specification.Take is { } take)
            query = query.Take(take);

        return query;
    }
}
