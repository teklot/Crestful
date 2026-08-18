namespace Crestful.Query;

/// <summary>
/// Represents a single filter condition parsed from the <c>?where=</c> query parameter.
/// Supports operators: eq (equals), ne (not equals), gt, gte, lt, lte, like, in.
/// </summary>
public sealed class QueryFilter
{
    /// <summary>The property name to filter on.</summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>The filter operator.</summary>
    public QueryFilterOperator Operator { get; init; } = QueryFilterOperator.Equals;

    /// <summary>The value to compare against (will be coerced to the property type at execution time).</summary>
    public object? Value { get; init; }

    /// <summary>Multiple values for the <c>in</c> operator.</summary>
    public IReadOnlyList<object?>? Values { get; init; }
}

/// <summary>
/// Supported filter operators.
/// </summary>
public enum QueryFilterOperator
{
    /// <summary>Equal to.</summary>
    Equals,

    /// <summary>Not equal to.</summary>
    NotEquals,

    /// <summary>Greater than.</summary>
    GreaterThan,

    /// <summary>Greater than or equal to.</summary>
    GreaterThanOrEqual,

    /// <summary>Less than.</summary>
    LessThan,

    /// <summary>Less than or equal to.</summary>
    LessThanOrEqual,

    /// <summary>Wildcard pattern match (supports * and ?).</summary>
    Like,

    /// <summary>Value is in a list of allowed values.</summary>
    In
}
