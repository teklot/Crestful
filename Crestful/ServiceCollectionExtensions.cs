using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crestful;

/// <summary>
/// Registers the Crestful framework with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers resource discovery, ProblemDetails, in-memory data sources for every
    /// discovered resource, and the framework services. Resources are discovered from the
    /// calling and entry assemblies, plus any assembly added to <c>CrestfulOptions.Assemblies</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddResources(this IServiceCollection services, Action<CrestfulOptions>? configure = null)
    {
        var options = new CrestfulOptions();
        configure?.Invoke(options);

        AddDiscoveryAssemblies(options, Assembly.GetCallingAssembly());
        var registry = new ResourceRegistry(options);
        RegisterDiscoveredResources(services, options, registry);

        services.AddProblemDetails();
        services.AddSingleton(options);
        services.AddSingleton(registry);
        services.AddSingleton<ResourceValidationService>();
        return services;
    }

    private static void AddDiscoveryAssemblies(CrestfulOptions options, Assembly caller)
    {
        var entry = Assembly.GetEntryAssembly();

        AddAssembly(options, caller);
        if (entry is not null && entry != caller)
        {
            AddAssembly(options, entry);
        }
    }

    private static void AddAssembly(CrestfulOptions options, Assembly assembly)
    {
        if (!options.Assemblies.Contains(assembly))
        {
            options.Assemblies.Add(assembly);
        }
    }

    private static void RegisterDiscoveredResources(IServiceCollection services, CrestfulOptions options, ResourceRegistry registry)
    {
        foreach (var assembly in options.Assemblies.Distinct())
        {
            foreach (var type in GetCandidateTypes(assembly))
            {
                if (!typeof(IResource).IsAssignableFrom(type) || type.IsAbstract || !type.IsClass)
                {
                    continue;
                }

                registry.Register(type);
                services.TryAddSingleton(
                    typeof(IResourceDataSource<>).MakeGenericType(type),
                    typeof(InMemoryResourceDataSource<>).MakeGenericType(type));
            }
        }
    }

    private static IEnumerable<Type> GetCandidateTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Select(t => t!);
        }
    }
}
