using System.Text.Json;
using Crestful.Query;
using Microsoft.AspNetCore.Http;

namespace Crestful;

/// <summary>
/// Parses Eve-style query parameters from the HTTP request into a <see cref="ResourceQueryContext"/>.
/// </summary>
internal static class QueryParameterParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ResourceQueryContext Parse(HttpContext http, ResourceQueryOptions options)
    {
        var query = http.Request.Query;

        return new ResourceQueryContext
        {
            Filters = ParseWhere(query, options),
            Sort = ParseSort(query, options),
            Search = ParseSearch(query, options),
            Page = ParseInt(query, "page"),
            MaxResults = ParseMaxResults(query, options),
            Fields = ParseCommaSeparated(query, "field"),
            Embedded = options.EmbeddingEnabled ? ParseCommaSeparated(query, "embedded") : null,
        };
    }

    private static IReadOnlyList<QueryFilter> ParseWhere(IQueryCollection query, ResourceQueryOptions options)
    {
        if (!query.TryGetValue("where", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var rawValue = raw.ToString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        try
        {
            var filterDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rawValue, JsonOptions);
            if (filterDict is null)
            {
                return [];
            }

            var filters = new List<QueryFilter>();
            foreach (var kvp in filterDict)
            {
                var propertyName = kvp.Key;
                if (options.AllowedFilterFields.Count > 0 && !options.AllowedFilterFields.Contains(propertyName))
                {
                    continue;
                }

                var value = ParseFilterValue(kvp.Value);
                filters.Add(new QueryFilter
                {
                    PropertyName = propertyName,
                    Operator = QueryFilterOperator.Equals,
                    Value = value,
                });
            }

            return filters;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static object? ParseFilterValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ParseFilterValue).ToList(),
            _ => element.ToString(),
        };
    }

    private static IReadOnlyList<SortDescriptor> ParseSort(IQueryCollection query, ResourceQueryOptions options)
    {
        if (!query.TryGetValue("sort", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var descriptors = new List<SortDescriptor>();
        foreach (var part in raw.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var descending = trimmed.StartsWith('-');
            var fieldName = descending ? trimmed[1..] : trimmed;

            if (options.AllowedSortFields.Count > 0 && !options.AllowedSortFields.Contains(fieldName))
            {
                continue;
            }

            descriptors.Add(new SortDescriptor { PropertyName = fieldName, Descending = descending });
        }

        return descriptors;
    }

    private static string? ParseSearch(IQueryCollection query, ResourceQueryOptions options)
    {
        if (!options.SearchEnabled)
        {
            return null;
        }

        return query.TryGetValue("search", out var raw) && !string.IsNullOrWhiteSpace(raw)
            ? raw.ToString()
            : null;
    }

    private static int? ParseMaxResults(IQueryCollection query, ResourceQueryOptions options)
    {
        var value = ParseInt(query, "max_results");
        if (value is null)
        {
            return null;
        }

        if (options.MaxPageSize > 0 && value > options.MaxPageSize)
        {
            return options.MaxPageSize;
        }

        return Math.Max(1, value.Value);
    }

    private static int? ParseInt(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (int.TryParse(raw, out var value) && value > 0)
        {
            return value;
        }

        return null;
    }

    private static IReadOnlyList<string>? ParseCommaSeparated(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var items = raw.ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return items.Length > 0 ? items : null;
    }
}
