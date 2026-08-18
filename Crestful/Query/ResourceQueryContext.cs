namespace Crestful.Query;

/// <summary>
/// Holds the parsed query parameters for a list request. Data sources translate this
/// into their native query mechanism (LINQ for in-memory, Expression trees for EF Core).
/// </summary>
public sealed class ResourceQueryContext
{
    /// <summary>Filters parsed from <c>?where=</c>.</summary>
    public IReadOnlyList<QueryFilter> Filters { get; init; } = [];

    /// <summary>Sort descriptors parsed from <c>?sort=</c>.</summary>
    public IReadOnlyList<SortDescriptor> Sort { get; init; } = [];

    /// <summary>Full-text search term parsed from <c>?search=</c>.</summary>
    public string? Search { get; init; }

    /// <summary>Page number (1-indexed) parsed from <c>?page=</c>.</summary>
    public int? Page { get; init; }

    /// <summary>Maximum items per page parsed from <c>?max_results=</c>.</summary>
    public int? MaxResults { get; init; }

    /// <summary>Fields to include in the response parsed from <c>?field=</c>.</summary>
    public IReadOnlyList<string>? Fields { get; init; }

    /// <summary>Related resources to embed parsed from <c>?embedded=</c>.</summary>
    public IReadOnlyList<string>? Embedded { get; init; }

    /// <summary>Whether any query parameters were provided (skip defaulting to "return everything").</summary>
    public bool HasFilters => Filters.Count > 0 || Sort.Count > 0 || Search is not null
        || Page.HasValue || MaxResults.HasValue || Fields is { Count: > 0 } || Embedded is { Count: > 0 };
}
