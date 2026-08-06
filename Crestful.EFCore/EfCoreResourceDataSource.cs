using System.Reflection;
using Crestful;
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

    public EfCoreResourceDataSource(ResourceRegistry registry, TDbContext db)
    {
        _info = registry.Get(typeof(TResource));
        _db = db;
    }

    public async Task<IReadOnlyList<TResource>> ListAsync(CancellationToken cancellationToken)
        => await _db.Set<TResource>().AsNoTracking().ToListAsync(cancellationToken);

    public Task<TResource?> GetAsync(object key, CancellationToken cancellationToken)
        => _db.Set<TResource>().FindAsync(new[] { key }, cancellationToken).AsTask();

    public async Task<TResource> CreateAsync(TResource resource, CancellationToken cancellationToken)
    {
        _db.Set<TResource>().Add(resource);
        await _db.SaveChangesAsync(cancellationToken);
        return resource;
    }

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
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        ResourceValueCopier.Copy(_info, resource, original);
        await _db.SaveChangesAsync(cancellationToken);
        return original;
    }

    public async Task<bool> DeleteAsync(object key, CancellationToken cancellationToken)
    {
        var existing = await _db.Set<TResource>().FindAsync(new[] { key }, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        _db.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
