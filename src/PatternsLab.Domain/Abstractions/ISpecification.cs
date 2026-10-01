using System.Linq.Expressions;

namespace PatternsLab.Domain.Abstractions;

/// <summary>
/// A query described as data rather than issued as code — handbook §3.4.
///
/// This is what lets a repository stay closed (no <c>IQueryable&lt;T&gt;</c> escapes,
/// see §2.8) without exploding into a method per query (§4.3, anti-pattern 3).
/// </summary>
public interface ISpecification<T>
{
    Expression<Func<T, bool>>? Criteria { get; }

    IReadOnlyList<Expression<Func<T, object>>> Includes { get; }
    IReadOnlyList<string> IncludeStrings { get; }

    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }

    int? Skip { get; }
    int? Take { get; }

    /// <summary>Read-only path: skip change tracking entirely.</summary>
    bool AsNoTracking { get; }

    /// <summary>Opt out of the global soft-delete filter.</summary>
    bool IgnoreQueryFilters { get; }

    /// <summary>Use <c>AsSplitQuery()</c> — worth setting when two or more collection includes are present.</summary>
    bool AsSplitQuery { get; }
}

/// <summary>
/// Projection variant: lets a read-only screen select a DTO instead of materialising
/// whole aggregates. Handbook §4.2 — the repository is not automatically fast.
/// </summary>
public interface ISpecification<T, TResult> : ISpecification<T>
{
    Expression<Func<T, TResult>> Selector { get; }
}
