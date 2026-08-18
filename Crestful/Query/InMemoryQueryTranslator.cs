using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Crestful.Query;

namespace Crestful;

/// <summary>
/// Translates a <see cref="ResourceQueryContext"/> into LINQ operations on an in-memory collection.
/// Used by <see cref="InMemoryResourceDataSource{TResource}"/>.
/// </summary>
internal static class InMemoryQueryTranslator
{
    public static IReadOnlyList<TResource> Apply<TResource>(
        IEnumerable<TResource> source,
        ResourceQueryContext query,
        ResourceInfo<TResource> info) where TResource : class, IResource
    {
        var result = source;

        if (query.Filters.Count > 0)
        {
            result = ApplyFilters(result, query.Filters);
        }

        if (!string.IsNullOrEmpty(query.Search))
        {
            result = ApplySearch(result, query.Search);
        }

        if (query.Sort.Count > 0)
        {
            result = ApplySort(result, query.Sort);
        }

        var items = result.ToList();

        if (query.Page.HasValue || query.MaxResults.HasValue)
        {
            var pageSize = query.MaxResults ?? info.Options.Query.DefaultPageSize;
            var page = query.Page ?? 1;
            items = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        return items;
    }

    private static IEnumerable<T> ApplyFilters<T>(IEnumerable<T> source, IReadOnlyList<QueryFilter> filters) where T : class
    {
        foreach (var filter in filters)
        {
            source = ApplyFilter(source, filter);
        }
        return source;
    }

    private static IEnumerable<T> ApplyFilter<T>(IEnumerable<T> source, QueryFilter filter) where T : class
    {
        var property = typeof(T).GetProperty(filter.PropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null)
        {
            return source;
        }

        return filter.Operator switch
        {
            QueryFilterOperator.Equals => source.Where(x => MatchesEquals(property, x, filter.Value)),
            QueryFilterOperator.NotEquals => source.Where(x => !MatchesEquals(property, x, filter.Value)),
            QueryFilterOperator.GreaterThan => source.Where(x => MatchesComparison(property, x, filter.Value, (a, b) => a > b)),
            QueryFilterOperator.GreaterThanOrEqual => source.Where(x => MatchesComparison(property, x, filter.Value, (a, b) => a >= b)),
            QueryFilterOperator.LessThan => source.Where(x => MatchesComparison(property, x, filter.Value, (a, b) => a < b)),
            QueryFilterOperator.LessThanOrEqual => source.Where(x => MatchesComparison(property, x, filter.Value, (a, b) => a <= b)),
            QueryFilterOperator.Like => source.Where(x => MatchesLike(property, x, filter.Value)),
            QueryFilterOperator.In => source.Where(x => MatchesIn(property, x, filter.Values)),
            _ => source,
        };
    }

    private static bool MatchesEquals(PropertyInfo property, object instance, object? filterValue)
    {
        var actual = property.GetValue(instance);
        if (filterValue is null)
        {
            return actual is null;
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var converted = ConvertValue(filterValue, targetType);
        if (converted is null)
        {
            return actual is null;
        }

        return Equals(actual, converted);
    }

    private static bool MatchesComparison(PropertyInfo property, object instance, object? filterValue, Func<int, int, bool> compare)
    {
        var actual = property.GetValue(instance);
        if (actual is null || filterValue is null)
        {
            return false;
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var converted = ConvertValue(filterValue, targetType);
        if (converted is null)
        {
            return false;
        }

        if (actual is IComparable a && converted is IComparable c)
        {
            return compare(a.CompareTo(c), 0);
        }

        return false;
    }

    private static bool MatchesLike(PropertyInfo property, object instance, object? filterValue)
    {
        var actual = property.GetValue(instance);
        if (actual is null || filterValue is null)
        {
            return false;
        }

        var pattern = Convert.ToString(filterValue, CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        var actualStr = Convert.ToString(actual, CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(actualStr))
        {
            return false;
        }

        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(actualStr, regexPattern, RegexOptions.IgnoreCase);
    }

    private static bool MatchesIn(PropertyInfo property, object instance, IReadOnlyList<object?>? values)
    {
        if (values is null || values.Count == 0)
        {
            return false;
        }

        var actual = property.GetValue(instance);
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        return values.Any(v =>
        {
            if (v is null)
            {
                return actual is null;
            }

            var converted = ConvertValue(v, targetType);
            if (converted is null)
            {
                return actual is null;
            }

            return Equals(actual, converted);
        });
    }

    private static IEnumerable<T> ApplySearch<T>(IEnumerable<T> source, string search) where T : class
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.CanRead)
            .ToList();

        if (properties.Count == 0)
        {
            return source;
        }

        return source.Where(item =>
            properties.Any(p =>
            {
                var value = p.GetValue(item) as string;
                return value is not null && value.Contains(search, StringComparison.OrdinalIgnoreCase);
            }));
    }

    private static IEnumerable<T> ApplySort<T>(IEnumerable<T> source, IReadOnlyList<SortDescriptor> sort) where T : class
    {
        IOrderedEnumerable<T>? ordered = null;

        foreach (var descriptor in sort)
        {
            var property = typeof(T).GetProperty(descriptor.PropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
            {
                continue;
            }

            var keySelector = CreateKeySelector<T>(property);

            ordered = ordered is null
                ? (descriptor.Descending
                    ? source.OrderByDescending(keySelector)
                    : source.OrderBy(keySelector))
                : (descriptor.Descending
                    ? ordered.ThenByDescending(keySelector)
                    : ordered.ThenBy(keySelector));

            source = ordered;
        }

        return ordered ?? source;
    }

    private static Func<T, object?> CreateKeySelector<T>(PropertyInfo property) where T : class
    {
        return instance =>
        {
            var value = property.GetValue(instance);
            if (value is null)
            {
                return null;
            }

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (targetType.IsValueType)
            {
                return value;
            }

            return value;
        };
    }

    private static List<T> ApplyFieldSelection<T>(List<T> items, IReadOnlyList<string> fields) where T : class
    {
        if (items.Count == 0)
        {
            return items;
        }

        return items.Select(item => ProjectItem(item, fields)).ToList();
    }

    private static T ProjectItem<T>(T item, IReadOnlyList<string> fields) where T : class
    {
        var result = Activator.CreateInstance<T>();
        var sourceProperties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            var sourceProp = sourceProperties.FirstOrDefault(p =>
                string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));

            if (sourceProp is not null && sourceProp.CanRead)
            {
                var targetProp = typeof(T).GetProperty(sourceProp.Name, BindingFlags.Public | BindingFlags.Instance);
                if (targetProp is not null && targetProp.CanWrite && targetProp.PropertyType == sourceProp.PropertyType)
                {
                    targetProp.SetValue(result, sourceProp.GetValue(item));
                }
            }
        }

        return result;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }
}
