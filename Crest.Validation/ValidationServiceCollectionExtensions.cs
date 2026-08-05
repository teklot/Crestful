using System.Reflection;
using System.Runtime.CompilerServices;
using Crest;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crest.Validation;

/// <summary>
/// Registers automatic request validation: Data Annotations by default, plus any
/// FluentValidation validators discovered in the configured assemblies.
/// </summary>
public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Enables automatic request validation. Data Annotations are enabled by default; every
    /// FluentValidation <c>IValidator&lt;T&gt;</c> where <c>T</c> is an <see cref="IResource"/>
    /// found in the calling or entry assemblies is wired in as well.
    /// </summary>
    /// <remarks>
    /// Named <c>AddResourceValidation</c> (rather than <c>AddValidation</c>) to avoid colliding
    /// with the built-in <c>Microsoft.Extensions.Validation</c> service registration on .NET 9+.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddResourceValidation(this IServiceCollection services, Action<ValidationOptions>? configure = null)
    {
        var options = new ValidationOptions();
        configure?.Invoke(options);

        AddDiscoveryAssemblies(options, Assembly.GetCallingAssembly());
        services.AddSingleton(options);

        if (options.UseDataAnnotations)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IResourceValidator, DataAnnotationsResourceValidator>());
        }

        foreach (var assembly in options.Assemblies.Distinct())
        {
            foreach (var (resourceType, validatorType) in FindFluentValidators(assembly))
            {
                services.TryAddEnumerable(ServiceDescriptor.Scoped(
                    typeof(IValidator<>).MakeGenericType(resourceType),
                    validatorType));
                services.TryAddEnumerable(ServiceDescriptor.Scoped(
                    typeof(IResourceValidator),
                    typeof(FluentValidationResourceValidator<>).MakeGenericType(resourceType)));
            }
        }

        return services;
    }

    private static void AddDiscoveryAssemblies(ValidationOptions options, Assembly caller)
    {
        var entry = Assembly.GetEntryAssembly();

        AddAssembly(options, caller);
        if (entry is not null && entry != caller)
        {
            AddAssembly(options, entry);
        }
    }

    private static void AddAssembly(ValidationOptions options, Assembly assembly)
    {
        if (!options.Assemblies.Contains(assembly))
        {
            options.Assemblies.Add(assembly);
        }
    }

    private static IEnumerable<(Type ResourceType, Type ValidatorType)> FindFluentValidators(Assembly assembly)
    {
        foreach (var type in GetCandidateTypes(assembly))
        {
            if (type.IsAbstract || type.IsGenericTypeDefinition)
            {
                continue;
            }

            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IValidator<>))
                {
                    continue;
                }

                var resourceType = iface.GetGenericArguments()[0];
                if (typeof(IResource).IsAssignableFrom(resourceType))
                {
                    yield return (resourceType, type);
                }
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
