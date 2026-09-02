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

    /// <summary>Whether the resource implements <see cref="ISoftDeletable"/>.</summary>
    public bool IsSoftDeletable => typeof(ISoftDeletable).IsAssignableFrom(ResourceType);

    private PropertyInfo? _deletedAtProperty;

    /// <summary>
    /// The property that stores the soft-delete timestamp, resolved from the configured field name.
    /// Returns <c>null</c> if the resource does not implement <see cref="ISoftDeletable"/>.
    /// </summary>
    public PropertyInfo? DeletedAtProperty => _deletedAtProperty ??= FindDeletedAtProperty(ResourceType, Options.SoftDelete.DeletedAtFieldName);

    /// <summary>Whether soft delete is actively enabled for this resource.</summary>
    public bool SoftDeleteEnabled => Options.SoftDelete.Enabled && IsSoftDeletable;

    /// <summary>Whether the resource implements <see cref="IAuditable"/>.</summary>
    public bool IsAuditable => typeof(IAuditable).IsAssignableFrom(ResourceType);

    /// <summary>Whether auditing is actively enabled for this resource.</summary>
    public bool AuditingEnabled => Options.Auditing.Enabled && IsAuditable;

    private PropertyInfo? _createdAtProperty;

    /// <summary>
    /// The property that stores the creation timestamp, resolved from the configured field name.
    /// Returns <c>null</c> if the resource does not implement <see cref="IAuditable"/>.
    /// </summary>
    public PropertyInfo? CreatedAtProperty => _createdAtProperty ??= FindAuditProperty(ResourceType, Options.Auditing.CreatedAtFieldName);

    private PropertyInfo? _updatedAtProperty;

    /// <summary>
    /// The property that stores the last update timestamp, resolved from the configured field name.
    /// Returns <c>null</c> if the resource does not implement <see cref="IAuditable"/>.
    /// </summary>
    public PropertyInfo? UpdatedAtProperty => _updatedAtProperty ??= FindAuditProperty(ResourceType, Options.Auditing.UpdatedAtFieldName);

    private PropertyInfo? _createdByProperty;

    /// <summary>
    /// The property that stores the creator's identity, resolved from the configured field name.
    /// Returns <c>null</c> if the resource does not implement <see cref="IAuditable"/>.
    /// </summary>
    public PropertyInfo? CreatedByProperty => _createdByProperty ??= FindAuditProperty(ResourceType, Options.Auditing.CreatedByFieldName);

    private PropertyInfo? _updatedByProperty;

    /// <summary>
    /// The property that stores the last updater's identity, resolved from the configured field name.
    /// Returns <c>null</c> if the resource does not implement <see cref="IAuditable"/>.
    /// </summary>
    public PropertyInfo? UpdatedByProperty => _updatedByProperty ??= FindAuditProperty(ResourceType, Options.Auditing.UpdatedByFieldName);

    /// <summary>Sets the creation timestamp on a resource instance.</summary>
    public void SetCreatedAt(object instance, DateTimeOffset value)
    {
        if (AuditingEnabled)
        {
            CreatedAtProperty!.SetValue(instance, value);
        }
    }

    /// <summary>Reads the creation timestamp from a resource instance.</summary>
    public DateTimeOffset? GetCreatedAt(object instance)
        => AuditingEnabled ? (DateTimeOffset?)CreatedAtProperty!.GetValue(instance) : null;

    /// <summary>Sets the last update timestamp on a resource instance.</summary>
    public void SetUpdatedAt(object instance, DateTimeOffset value)
    {
        if (AuditingEnabled)
        {
            UpdatedAtProperty!.SetValue(instance, value);
        }
    }

    /// <summary>Sets the creator's identity on a resource instance.</summary>
    public void SetCreatedBy(object instance, string? value)
    {
        if (AuditingEnabled)
        {
            CreatedByProperty!.SetValue(instance, value);
        }
    }

    /// <summary>Reads the creator's identity from a resource instance.</summary>
    public string? GetCreatedBy(object instance)
        => AuditingEnabled ? (string?)CreatedByProperty!.GetValue(instance) : null;

    /// <summary>Sets the last updater's identity on a resource instance.</summary>
    public void SetUpdatedBy(object instance, string? value)
    {
        if (AuditingEnabled)
        {
            UpdatedByProperty!.SetValue(instance, value);
        }
    }

    /// <summary>Reads the soft-delete timestamp from a resource instance.</summary>
    public DateTimeOffset? GetDeletedAt(object instance)
        => SoftDeleteEnabled ? (DateTimeOffset?)DeletedAtProperty!.GetValue(instance) : null;

    /// <summary>Sets the soft-delete timestamp on a resource instance.</summary>
    public void SetDeletedAt(object instance, DateTimeOffset? value)
    {
        if (SoftDeleteEnabled)
        {
            DeletedAtProperty!.SetValue(instance, value);
        }
    }

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

    private static PropertyInfo? FindDeletedAtProperty(Type resourceType, string fieldName)
    {
        return resourceType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
    }

    private static PropertyInfo? FindAuditProperty(Type resourceType, string fieldName)
    {
        return resourceType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
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
