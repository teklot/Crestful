using System.Reflection;
using Crestful;
using Crestful.Query;
using Microsoft.EntityFrameworkCore;

namespace Crestful.EFCore;

/// <summary>
/// An <see cref="IResourceDataSource{TResource}"/> backed by EF Core. Uses the scoped
/// <typeparamref name="TDbContext"/> directly — no repository layer.
/// </summary>
public sealed class EfCoreResourceDataSource<TResource, TDbContext> : IResourceDataSource<TResource>
    where TResource : class, IResource
    where TDbContext : DbContext
{
    private readonly ResourceInfo _info;
    private readonly TDbContext _db;

    /// <summary>Creates an EF Core data source for the resource registered in <paramref name="registry"/>, backed by <paramref name="db"/>.</summary>
    public EfCoreResourceDataSource(ResourceRegistry registry, TDbContext db)
    {
        _info = registry.Get(typeof(TResource));
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TResource>> ListAsync(CancellationToken cancellationToken)
        => await _db.Set<TResource>().AsNoTracking().ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TResource>> ListAsync(ResourceQueryContext query, CancellationToken cancellationToken)
    {
        var source = _db.Set<TResource>().AsNoTracking();
        var result = EfCoreQueryTranslator.Apply(source, query, (ResourceInfo<TResource>)_info);
        return await result.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResource?> GetAsync(object key, CancellationToken cancellationToken)
        => _db.Set<TResource>().FindAsync(new[] { key }, cancellationToken).AsTask();

    /// <inheritdoc />
    public async Task<TResource> CreateAsync(TResource resource, CancellationToken cancellationToken)
    {
        _db.Set<TResource>().Add(resource);
        await SaveAsync(cancellationToken);
        return resource;
    }

    /// <inheritdoc />
    public async Task<TResource?> UpdateAsync(TResource resource, TResource original, CancellationToken cancellationToken)
    {
        if (_db.Entry(original).State == EntityState.Detached)
        {
            var existing = await _db.Set<TResource>().FindAsync(new[] { _info.GetKey(original) }, cancellationToken);
            if (existing is null)
            {
                return null;
            }

            ResourceValueCopier.Copy(_info, resource, existing);
            await SaveAsync(cancellationToken);
            return existing;
        }

        ResourceValueCopier.Copy(_info, resource, original);
        await SaveAsync(cancellationToken);
        return original;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(object key, CancellationToken cancellationToken)
    {
        var existing = await _db.Set<TResource>().FindAsync(new[] { key }, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (_info.SoftDeleteEnabled)
        {
            _info.SetDeletedAt(existing, DateTimeOffset.UtcNow);
            await SaveAsync(cancellationToken);
            return true;
        }

        _db.Remove(existing);
        await SaveAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Saves pending changes, translating EF Core's concurrency failure into
    /// <see cref="ResourceConcurrencyException"/> so the endpoints can answer 412. The row version
    /// itself is maintained by the database, not here.
    /// </summary>
    private async Task<int> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ResourceConcurrencyException(
                $"'{typeof(TResource).Name}' was modified by another request before this one could save.", ex);
        }
    }
}
