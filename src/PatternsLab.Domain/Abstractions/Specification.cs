using System.Linq.Expressions;

namespace PatternsLab.Domain.Abstractions;

/// <summary>
/// Base class every concrete specification derives from. Subclasses configure
/// themselves in their constructor, which is what makes a rule <em>nameable</em>:
/// <c>new OutstandingClaimsSpec("PBM-01")</c> reads as the rule, not as a query.
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    private readonly List<Expression<Func<T, object>>> _includes = [];
    private readonly List<string> _includeStrings = [];

    protected Specification() { }

    protected Specification(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    public Expression<Func<T, bool>>? Criteria { get; private set; }

    public IReadOnlyList<Expression<Func<T, object>>> Includes => _includes;
    public IReadOnlyList<string> IncludeStrings => _includeStrings;

    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    public int? Skip { get; private set; }
    public int? Take { get; private set; }

    public bool AsNoTracking { get; private set; }
    public bool IgnoreQueryFilters { get; private set; }
    public bool AsSplitQuery { get; private set; }

    // ---- configuration API, available to subclasses only ---------------------

    protected void Where(Expression<Func<T, bool>> criteria) =>
        Criteria = Criteria is null ? criteria : Criteria.AndAlso(criteria);

    protected void AddInclude(Expression<Func<T, object>> include) => _includes.Add(include);

    protected void AddInclude(string includeString) => _includeStrings.Add(includeString);

    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderBy) => OrderByDescending = orderBy;

    /// <summary>1-based page number, so callers do not have to compute the skip themselves.</summary>
    protected void ApplyPaging(int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        Skip = (page - 1) * pageSize;
        Take = pageSize;
    }

    protected void ReadOnly() => AsNoTracking = true;
    protected void IncludeDeleted() => IgnoreQueryFilters = true;
    protected void SplitQuery() => AsSplitQuery = true;
}

/// <inheritdoc cref="ISpecification{T, TResult}"/>
public abstract class Specification<T, TResult> : Specification<T>, ISpecification<T, TResult>
{
    protected Specification(Expression<Func<T, TResult>> selector) => Selector = selector;

    protected Specification(Expression<Func<T, bool>> criteria, Expression<Func<T, TResult>> selector)
        : base(criteria) => Selector = selector;

    public Expression<Func<T, TResult>> Selector { get; }
}

/// <summary>
/// Combining two lambdas is not as simple as <c>Expression.AndAlso(a.Body, b.Body)</c>:
/// each lambda has its own parameter instance, and EF Core will refuse the result.
/// This rebinds both bodies onto a single parameter first.
/// </summary>
public static class SpecificationExpressionExtensions
{
    public static Expression<Func<T, bool>> AndAlso<T>(
        this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right) =>
        Combine(left, right, Expression.AndAlso);

    public static Expression<Func<T, bool>> OrElse<T>(
        this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right) =>
        Combine(left, right, Expression.OrElse);

    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expression) =>
        Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters);

    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        var parameter = Expression.Parameter(typeof(T), "e");

        var leftBody = new ParameterRebinder(parameter).Visit(left.Body)!;
        var rightBody = new ParameterRebinder(parameter).Visit(right.Body)!;

        return Expression.Lambda<Func<T, bool>>(merge(leftBody, rightBody), parameter);
    }

    /// <summary>Visitor pattern (handbook §1.3, GoF Visitor) — the BCL ships one for expression trees.</summary>
    private sealed class ParameterRebinder(ParameterExpression parameter) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => parameter;
    }
}
