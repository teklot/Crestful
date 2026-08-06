using System.Collections.Concurrent;

namespace Crestful;

/// <summary>
/// Holds the metadata and options for every discovered resource, and guards against
/// mapping a resource more than once.
/// </summary>
public sealed class ResourceRegistry
{
    private readonly CrestfulOptions _options;
    private readonly ConcurrentDictionary<Type, ResourceInfo> _resources = new();
    private readonly object _mapLock = new();
    private readonly HashSet<Type> _mapped = new();

    internal ResourceRegistry(CrestfulOptions options)
    {
        _options = options;
    }

    /// <summary>All resources that have been discovered or explicitly requested.</summary>
    public IReadOnlyList<ResourceInfo> Resources => _resources.Values.ToList();

    /// <summary>Gets the metadata for a resource type, throwing if it was never discovered.</summary>
    public ResourceInfo Get(Type resourceType)
    {
        if (_resources.TryGetValue(resourceType, out var info))
        {
            return info;
        }

        throw new InvalidOperationException(
            $"Resource '{resourceType.Name}' has not been discovered. Ensure its assembly is added to " +
            $"CrestfulOptions.Assemblies (via AddResources) or map it with MapResource<{resourceType.Name}>().");
    }

    /// <summary>Gets the strongly typed metadata for a resource type.</summary>
    public ResourceInfo<TResource> Get<TResource>() where TResource : class, IResource
        => (ResourceInfo<TResource>)Get(typeof(TResource));

    /// <summary>Gets or creates the metadata for a resource type.</summary>
    public ResourceInfo<TResource> GetOrAdd<TResource>() where TResource : class, IResource
        => (ResourceInfo<TResource>)_resources.GetOrAdd(typeof(TResource), _ => CreateInfo<TResource>());

    internal void Register(Type resourceType)
    {
        if (resourceType.IsAbstract || _resources.ContainsKey(resourceType))
        {
            return;
        }

        typeof(ResourceRegistry)
            .GetMethod(nameof(GetOrAdd), Type.EmptyTypes)!
            .MakeGenericMethod(resourceType)
            .Invoke(this, null);
    }

    internal void MarkMapped(Type resourceType)
    {
        lock (_mapLock)
        {
            if (!_mapped.Add(resourceType))
            {
                throw new InvalidOperationException(
                    $"Resource '{resourceType.Name}' has already been mapped. Map each resource only once, " +
                    $"either via MapResource<{resourceType.Name}>() or MapResources().");
            }
        }
    }

    private ResourceInfo<TResource> CreateInfo<TResource>() where TResource : class, IResource
    {
        var options = new ResourceOptions<TResource>();
        _options.DefaultResourceOptions?.Invoke(options);
        return new ResourceInfo<TResource>(typeof(TResource), _options.DefaultRoutePrefix, options);
    }
}
