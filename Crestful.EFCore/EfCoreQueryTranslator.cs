using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Crestful.Query;

namespace Crestful.EFCore;

/// <summary>
/// Translates a <see cref="ResourceQueryContext"/> into LINQ expressions that EF Core can translate to SQL.
/// </summary>
internal static class EfCoreQueryTranslator
{
    public static IQueryable<TResource> Apply<TResource>(
        IQueryable<TResource> source,
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

        if (query.Page.HasValue || query.MaxResults.HasValue)
        {
            var pageSize = query.MaxResults ?? info.Options.Query.DefaultPageSize;
            var page = query.Page ?? 1;
            result = result.Skip((page - 1) * pageSize).Take(pageSize);
        }

        return result;
    }

    private static IQueryable<T> ApplyFilters<T>(IQueryable<T> source, IReadOnlyList<QueryFilter> filters) where T : class
    {
        foreach (var filter in filters)
        {
            source = ApplyFilter(source, filter);
        }
        return source;
    }

    private static IQueryable<T> ApplyFilter<T>(IQueryable<T> source, QueryFilter filter) where T : class
    {
        var property = typeof(T).GetProperty(filter.PropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null)
        {
            return source;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var propertyAccess = Expression.Property(parameter, property);

        Expression? condition = filter.Operator switch
        {
            QueryFilterOperator.Equals => CreateEqualsExpression(propertyAccess, filter.Value, property),
            QueryFilterOperator.NotEquals => Expression.Not(CreateEqualsExpression(propertyAccess, filter.Value, property)),
            QueryFilterOperator.GreaterThan => CreateComparisonExpression(propertyAccess, filter.Value, property, Expression.GreaterThan),
            QueryFilterOperator.GreaterThanOrEqual => CreateComparisonExpression(propertyAccess, filter.Value, property, Expression.GreaterThanOrEqual),
            QueryFilterOperator.LessThan => CreateComparisonExpression(propertyAccess, filter.Value, property, Expression.LessThan),
            QueryFilterOperator.LessThanOrEqual => CreateComparisonExpression(propertyAccess, filter.Value, property, Expression.LessThanOrEqual),
            QueryFilterOperator.Like => CreateLikeExpression(propertyAccess, filter.Value, property),
            QueryFilterOperator.In => CreateInExpression(propertyAccess, filter.Values, property),
            _ => null,
        };

        if (condition is null)
        {
            return source;
        }

        var lambda = Expression.Lambda<Func<T, bool>>(condition, parameter);
        return source.Where(lambda);
    }

    private static Expression CreateEqualsExpression(Expression propertyAccess, object? filterValue, PropertyInfo property)
    {
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (filterValue is null)
        {
            return Expression.Equal(propertyAccess, Expression.Constant(null, property.PropertyType));
        }

        var convertedValue = ConvertValue(filterValue, targetType);
        if (convertedValue is null)
        {
            return Expression.Equal(propertyAccess, Expression.Constant(null, property.PropertyType));
        }

        var constant = Expression.Constant(convertedValue, targetType);

        if (property.PropertyType != targetType)
        {
            propertyAccess = Expression.Convert(propertyAccess, targetType);
        }

        return Expression.Equal(propertyAccess, constant);
    }

    private static Expression CreateComparisonExpression(
        Expression propertyAccess,
        object? filterValue,
        PropertyInfo property,
        Func<Expression, Expression, Expression> compare)
    {
        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (filterValue is null)
        {
            return Expression.Constant(false);
        }

        var convertedValue = ConvertValue(filterValue, targetType);
        if (convertedValue is null)
        {
            return Expression.Constant(false);
        }

        var constant = Expression.Constant(convertedValue, targetType);

        if (property.PropertyType != targetType)
        {
            propertyAccess = Expression.Convert(propertyAccess, targetType);
        }

        return compare(propertyAccess, constant);
    }

    private static Expression CreateLikeExpression(Expression propertyAccess, object? filterValue, PropertyInfo property)
    {
        if (filterValue is null)
        {
            return Expression.Constant(false);
        }

        var pattern = Convert.ToString(filterValue, CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(pattern))
        {
            return Expression.Constant(false);
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (targetType != typeof(string))
        {
            return Expression.Constant(false);
        }

        // Convert wildcard pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";

        var method = typeof(Regex).GetMethod(nameof(Regex.IsMatch), [typeof(string), typeof(string), typeof(RegexOptions)])!;
        var stringValue = Expression.Call(propertyAccess, typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes)!);
        var regexConstant = Expression.Constant(regexPattern);
        var optionsConstant = Expression.Constant(RegexOptions.IgnoreCase);

        return Expression.Call(method, stringValue, regexConstant, optionsConstant);
    }

    private static Expression CreateInExpression(Expression propertyAccess, IReadOnlyList<object?>? values, PropertyInfo property)
    {
        if (values is null || values.Count == 0)
        {
            return Expression.Constant(false);
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var listType = typeof(List<>).MakeGenericType(targetType);
        var addMethod = listType.GetMethod("Add")!;

        var list = Activator.CreateInstance(listType)!;
        foreach (var value in values)
        {
            if (value is not null)
            {
                var converted = ConvertValue(value, targetType);
                if (converted is not null)
                {
                    addMethod.Invoke(list, [converted]);
                }
            }
        }

        var containsMethod = typeof(Enumerable)
            .GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(targetType);

        var listExpression = Expression.Constant(list, listType);

        Expression valueAccess = propertyAccess;
        if (property.PropertyType != targetType)
        {
            valueAccess = Expression.Convert(propertyAccess, targetType);
        }

        return Expression.Call(containsMethod, listExpression, valueAccess);
    }

    private static IQueryable<T> ApplySearch<T>(IQueryable<T> source, string search) where T : class
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.CanRead)
            .ToList();

        if (properties.Count == 0)
        {
            return source;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? orExpression = null;

        foreach (var property in properties)
        {
            var propertyAccess = Expression.Property(parameter, property);
            var searchConstant = Expression.Constant(search);
            var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string), typeof(StringComparison)])!;
            var containsCall = Expression.Call(propertyAccess, containsMethod, searchConstant, Expression.Constant(StringComparison.OrdinalIgnoreCase));

            orExpression = orExpression is null ? containsCall : Expression.OrElse(orExpression, containsCall);
        }

        if (orExpression is null)
        {
            return source;
        }

        var lambda = Expression.Lambda<Func<T, bool>>(orExpression, parameter);
        return source.Where(lambda);
    }

    private static IQueryable<T> ApplySort<T>(IQueryable<T> source, IReadOnlyList<SortDescriptor> sort) where T : class
    {
        IOrderedQueryable<T>? ordered = null;

        foreach (var descriptor in sort)
        {
            var property = typeof(T).GetProperty(descriptor.PropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
            {
                continue;
            }

            var parameter = Expression.Parameter(typeof(T), "x");
            var propertyAccess = Expression.Property(parameter, property);
            var lambda = Expression.Lambda(propertyAccess, parameter);

            if (ordered is null)
            {
                ordered = (IOrderedQueryable<T>)(descriptor.Descending
                    ? source.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), "OrderByDescending", [typeof(T), property.PropertyType], source.Expression, lambda))
                    : source.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), "OrderBy", [typeof(T), property.PropertyType], source.Expression, lambda)));
            }
            else
            {
                ordered = (IOrderedQueryable<T>)ordered.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), descriptor.Descending ? "ThenByDescending" : "ThenBy", [typeof(T), property.PropertyType], ordered.Expression, lambda));
            }

            source = ordered;
        }

        return ordered ?? source;
    }

    private static IQueryable<T> ApplyFieldSelection<T>(IQueryable<T> source, IReadOnlyList<string> fields) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var bindings = new List<MemberBinding>();

        foreach (var field in fields)
        {
            var property = typeof(T).GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is not null)
            {
                var propertyAccess = Expression.Property(parameter, property);
                bindings.Add(Expression.Bind(property, propertyAccess));
            }
        }

        if (bindings.Count == 0)
        {
            return source;
        }

        var body = Expression.MemberInit(Expression.New(typeof(T)), bindings);
        var lambda = Expression.Lambda<Func<T, T>>(body, parameter);
        return source.Select(lambda);
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
