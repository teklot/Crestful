using System.Reflection;

namespace Crest;

/// <summary>
/// Copies scalar (non-navigation) property values from one resource instance to another,
/// skipping the key property. Shared by the in-memory and EF Core data sources.
/// </summary>
internal static class ResourceValueCopier
{
    public static void Copy<TResource>(ResourceInfo info, TResource source, TResource target)
    {
        foreach (var property in typeof(TResource).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property == info.KeyProperty)
            {
                continue;
            }

            if (!property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (!IsScalar(property.PropertyType))
            {
                continue;
            }

            property.SetValue(target, property.GetValue(source));
        }
    }

    internal static bool IsScalar(Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        return target.IsPrimitive
            || target.IsEnum
            || target == typeof(string)
            || target == typeof(decimal)
            || target == typeof(DateTime)
            || target == typeof(DateTimeOffset)
            || target == typeof(TimeSpan)
            || target == typeof(DateOnly)
            || target == typeof(TimeOnly)
            || target == typeof(Guid)
            || target == typeof(Uri);
    }
}
