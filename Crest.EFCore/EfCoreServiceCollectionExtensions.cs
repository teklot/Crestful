using System.Reflection;
using Crest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crest.EFCore;

/// <summary>
/// Registers EF Core backed data sources for resources exposed by the application's
/// <see cref="DbContext"/> types.
/// </summary>
public static class EfCoreServiceCollectionExtensions
{
    /// <summary>
    /// Scans every registered <see cref="DbContext"/> for <c>DbSet&lt;T&gt;</c> properties whose
    /// entity type implements <see cref="IResource"/> and registers an EF Core data source for each.
    /// Call this after registering your <see cref="DbContext"/> with <c>AddDbContext</c>.
    /// </summary>
    public static IServiceCollection AddEfCore(this IServiceCollection services, Action<EfCoreOptions>? configure = null)
    {
        var options = new EfCoreOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        var contextTypes = services
            .Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>))
            .Select(d => d.ServiceType.GetGenericArguments()[0])
            .Distinct()
            .ToList();

        foreach (var contextType in contextTypes)
        {
            foreach (var entityType in GetDbSetEntityTypes(contextType))
            {
                if (typeof(IResource).IsAssignableFrom(entityType))
                {
                    RegisterEfDataSource(services, entityType, contextType);
                }
            }
        }

        return services;
    }

    /// <summary>
    /// Explicitly registers an EF Core data source for a resource backed by a specific context.
    /// </summary>
    public static IServiceCollection AddEfCoreResource<TResource, TDbContext>(this IServiceCollection services)
        where TResource : class, IResource
        where TDbContext : DbContext
    {
        RegisterEfDataSource(services, typeof(TResource), typeof(TDbContext));
        return services;
    }

    internal static IEnumerable<Type> GetDbSetEntityTypes(Type contextType)
    {
        return contextType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .Distinct();
    }

    private static void RegisterEfDataSource(IServiceCollection services, Type resourceType, Type contextType)
    {
        var dataSourceInterface = typeof(IResourceDataSource<>).MakeGenericType(resourceType);
        RemoveInMemoryDataSource(services, dataSourceInterface);

        var implementationType = typeof(EfCoreResourceDataSource<,>).MakeGenericType(resourceType, contextType);
        services.AddScoped(dataSourceInterface, implementationType);
    }

    private static void RemoveInMemoryDataSource(IServiceCollection services, Type dataSourceInterface)
    {
        var toRemove = services
            .Where(d => d.ServiceType == dataSourceInterface
                && d.ImplementationType is { IsGenericType: true } implementation
                && implementation.GetGenericTypeDefinition() == typeof(InMemoryResourceDataSource<>))
            .ToList();

        foreach (var descriptor in toRemove)
        {
            services.Remove(descriptor);
        }
    }
}
