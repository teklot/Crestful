using System.Collections.Concurrent;

namespace Crestful;

/// <summary>
/// A thread-safe, process-local data source backed by a dictionary. Registered automatically
/// by <c>AddResources()</c> for every discovered resource, so a resource can be served with no
/// persistence layer at all.
/// </summary>
public sealed class InMemoryResourceDataSource<TResource> : IResourceDataSource<TResource>
    where TResource : class, IResource
{
    private readonly ResourceInfo<TResource> _info;
    private readonly ConcurrentDictionary<object, TResource> _store = new();

    public InMemoryResourceDataSource(ResourceRegistry registry)
    {
        _info = registry.Get<TResource>();
    }

    /// <summary>Number of items currently stored.</summary>
    public int Count => _store.Count;

    public Task<IReadOnlyList<TResource>> ListAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TResource>>(_store.Values.ToList());

    public Task<TResource?> GetAsync(object key, CancellationToken cancellationToken)
        => Task.FromResult(_store.TryGetValue(key, out var item) ? item : null);

    public Task<TResource> CreateAsync(TResource resource, CancellationToken cancellationToken)
    {
        var key = _info.GetKey(resource);
        if (IsDefaultKey(key))
        {
            key = GenerateKey();
            _info.SetKey(resource, key);
        }

        if (key is null)
        {
            throw new InvalidOperationException(
                $"The key of resource '{typeof(TResource).Name}' is null and cannot be used.");
        }

        if (!_store.TryAdd(key, resource))
        {
            throw new ResourceConflictException(
                $"A resource of type '{typeof(TResource).Name}' with key '{key}' already exists.");
        }

        return Task.FromResult(resource);
    }

    public Task<TResource?> UpdateAsync(TResource resource, TResource original, CancellationToken cancellationToken)
    {
        var key = _info.GetKey(original);
        if (key is null)
        {
            return Task.FromResult<TResource?>(null);
        }

        if (!_store.ContainsKey(key))
        {
            return Task.FromResult<TResource?>(null);
        }

        ResourceValueCopier.Copy(_info, resource, original);
        _store[key] = original;
        return Task.FromResult<TResource?>(original);
    }

    public Task<bool> DeleteAsync(object key, CancellationToken cancellationToken)
        => Task.FromResult(_store.TryRemove(key, out _));

    private bool IsDefaultKey(object? key)
    {
        var type = Nullable.GetUnderlyingType(_info.KeyType) ?? _info.KeyType;
        if (type == typeof(int)) return key is int i && i == 0;
        if (type == typeof(long)) return key is long l && l == 0;
        if (type == typeof(short)) return key is short s && s == 0;
        if (type == typeof(byte)) return key is byte b && b == 0;
        if (type == typeof(Guid)) return key is Guid g && g == Guid.Empty;
        if (type == typeof(string)) return string.IsNullOrWhiteSpace(key as string);
        return key is null;
    }

    private object GenerateKey()
    {
        var type = Nullable.GetUnderlyingType(_info.KeyType) ?? _info.KeyType;
        if (type == typeof(int))
        {
            return _store.Keys.OfType<int>().DefaultIfEmpty(0).Max() + 1;
        }
        if (type == typeof(long))
        {
            return _store.Keys.OfType<long>().DefaultIfEmpty(0).Max() + 1;
        }
        if (type == typeof(short))
        {
            return (short)(_store.Keys.OfType<short>().DefaultIfEmpty((short)0).Max() + 1);
        }
        if (type == typeof(byte))
        {
            return (byte)(_store.Keys.OfType<byte>().DefaultIfEmpty((byte)0).Max() + 1);
        }
        if (type == typeof(Guid))
        {
            return Guid.NewGuid();
        }
        if (type == typeof(string))
        {
            return Guid.NewGuid().ToString("N");
        }

        throw new NotSupportedException(
            $"InMemoryResourceDataSource cannot auto-generate keys of type '{_info.KeyType.Name}' for " +
            $"resource '{typeof(TResource).Name}'. Provide a key when creating the resource.");
    }
}
