using Crestful.Query;

namespace Crestful;

/// <summary>
/// The persistence abstraction Crestful uses to serve CRUD requests. Crestful ships an in-memory
/// implementation and <c>Crestful.EFCore</c> ships an EF Core implementation; applications can
/// also register their own.
/// </summary>
public interface IResourceDataSource<TResource> where TResource : class, IResource
{
    /// <summary>Returns every resource in the collection.</summary>
    Task<IReadOnlyList<TResource>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Returns resources matching the given query.</summary>
    Task<IReadOnlyList<TResource>> ListAsync(ResourceQueryContext query, CancellationToken cancellationToken)
        => ListAsync(cancellationToken);

    /// <summary>Returns the resource with the given key, or <c>null</c> if absent.</summary>
    Task<TResource?> GetAsync(object key, CancellationToken cancellationToken);

    /// <summary>Persists a new resource and returns it (the key may be assigned).</summary>
    Task<TResource> CreateAsync(TResource resource, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the values of <paramref name="resource"/> to the existing <paramref name="original"/>
    /// and persists the change. Returns <c>null</c> if the original no longer exists.
    /// </summary>
    Task<TResource?> UpdateAsync(TResource resource, TResource original, CancellationToken cancellationToken);

    /// <summary>Deletes the resource with the given key. Returns <c>false</c> if absent.</summary>
    Task<bool> DeleteAsync(object key, CancellationToken cancellationToken);
}

/// <summary>Raised by a data source when a create collides with an existing key.</summary>
public sealed class ResourceConflictException : Exception
{
    /// <summary>Creates a conflict exception with the given <paramref name="message"/>.</summary>
    public ResourceConflictException(string message)
        : base(message)
    {
    }
}
