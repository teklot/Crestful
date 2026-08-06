using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace Crestful;

/// <summary>
/// Describes a discovered resource: its CLR type, derived route, key property, and options.
/// </summary>
public class ResourceInfo
{
    private readonly string _routePrefix;

    internal ResourceInfo(Type resourceType, string routePrefix, ResourceOptions options)
    {
        ResourceType = resourceType;
        _routePrefix = routePrefix ?? string.Empty;
        Options = options;
    }

    /// <summary>The CLR type of the resource.</summary>
    public Type ResourceType { get; }

    /// <summary>The resource options, which may be mutated until the resource is mapped.</summary>
    public ResourceOptions Options { get; }

    private PropertyInfo? _keyProperty;

    /// <summary>
    /// The key property, discovered by convention (<c>[Key]</c>, <c>Id</c>, <c>{TypeName}Id</c>, or
    /// <c>{TypeName}Id</c> with a trailing <c>Resource</c> suffix stripped). Resolved lazily so a
    /// malformed resource does not break discovery of the rest of an assembly.
    /// </summary>
    public PropertyInfo KeyProperty => _keyProperty ??= FindKeyProperty(ResourceType);

    /// <summary>The type of the key property.</summary>
    public Type KeyType => KeyProperty.PropertyType;

    /// <summary>The derived route name, e.g. <c>"devices"</c>.</summary>
    public string Name => Options.Name ?? Pluralizer.Pluralize(ResourceType.Name).ToLowerInvariant();

    /// <summary>The route relative to the application root, e.g. <c>"/devices"</c>.</summary>
    public string RelativeRoute => $"/{Name}";

    /// <summary>The full route pattern, e.g. <c>"/api/devices"</c>.</summary>
    public string RoutePattern => string.IsNullOrEmpty(_routePrefix) ? RelativeRoute : $"/{_routePrefix}{RelativeRoute}";

    /// <summary>Reads the key from a resource instance.</summary>
    public object? GetKey(object instance) => KeyProperty.GetValue(instance);

    /// <summary>Writes the key onto a resource instance.</summary>
    public void SetKey(object instance, object? value) => KeyProperty.SetValue(instance, value);

    /// <summary>Attempts to parse a raw route value into a key of the resource's key type.</summary>
    public bool TryConvertKey(string? raw, out object? key)
    {
        key = null;
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        var target = Nullable.GetUnderlyingType(KeyType) ?? KeyType;

        if (target == typeof(int))
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) { key = value; return true; }
            return false;
        }
        if (target == typeof(long))
        {
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) { key = value; return true; }
            return false;
        }
        if (target == typeof(short))
        {
            if (short.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) { key = value; return true; }
            return false;
        }
        if (target == typeof(byte))
        {
            if (byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) { key = value; return true; }
            return false;
        }
        if (target == typeof(uint))
        {
            if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) { key = value; return true; }
            return false;
        }
        if (target == typeof(ulong))
        {
            if (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) { key = value; return true; }
            return false;
        }
        if (target == typeof(ushort))
        {
            if (ushort.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) { key = value; return true; }
            return false;
        }
        if (target == typeof(Guid))
        {
            if (Guid.TryParse(raw, out var value)) { key = value; return true; }
            return false;
        }
        if (target == typeof(string))
        {
            key = raw;
            return true;
        }

        try
        {
            key = TypeDescriptor.GetConverter(target).ConvertFromInvariantString(raw);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static PropertyInfo FindKeyProperty(Type resourceType)
    {
        var properties = resourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var candidates = new List<string> { "Id", resourceType.Name + "Id" };
        if (resourceType.Name.EndsWith("Resource", StringComparison.Ordinal))
        {
            candidates.Add(resourceType.Name[..^"Resource".Length]);
        }

        var key = properties.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() is not null)
               ?? properties.FirstOrDefault(p => candidates.Any(c => string.Equals(p.Name, c, StringComparison.OrdinalIgnoreCase)));

        if (key is null)
        {
            throw new InvalidOperationException(
                $"Resource '{resourceType.Name}' must define a key property named '{string.Join("', '", candidates)}' or decorated with [Key].");
        }

        if (!key.CanWrite)
        {
            throw new InvalidOperationException(
                $"Key property '{key.Name}' on resource '{resourceType.Name}' must be writable.");
        }

        bool isEnumerable = key.PropertyType.IsArray ||
            (typeof(System.Collections.IEnumerable).IsAssignableFrom(key.PropertyType) && key.PropertyType != typeof(string));
        if (isEnumerable)
        {
            throw new InvalidOperationException(
                $"Key property '{key.Name}' on resource '{resourceType.Name}' must be a scalar type.");
        }

        return key;
    }
}

/// <summary>
/// Describes a discovered resource with strongly typed options and hook access.
/// </summary>
public sealed class ResourceInfo<TResource> : ResourceInfo where TResource : class, IResource
{
    /// <summary>Creates resource metadata with strongly typed options.</summary>
    public ResourceInfo(Type resourceType, string routePrefix, ResourceOptions<TResource> options)
        : base(resourceType, routePrefix, options)
    {
        Options = options;
    }

    /// <summary>The strongly typed options for the resource.</summary>
    public new ResourceOptions<TResource> Options { get; }
}
