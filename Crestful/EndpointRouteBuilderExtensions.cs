using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crestful;

/// <summary>
/// Maps discovered resources to minimal API endpoints.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a single resource to CRUD endpoints. <paramref name="configure"/> can customize
    /// the resource's options before the endpoints are generated.
    /// </summary>
    public static ResourceRouteGroup MapResource<TResource>(this IEndpointRouteBuilder endpoints, Action<ResourceOptions<TResource>>? configure = null)
        where TResource : class, IResource
    {
        var registry = endpoints.ServiceProvider.GetRequiredService<ResourceRegistry>();
        var info = registry.GetOrAdd<TResource>();
        configure?.Invoke(info.Options);
        return MapCore(endpoints, info);
    }

    /// <summary>Maps every discovered resource to CRUD endpoints.</summary>
    public static IEndpointRouteBuilder MapResources(this IEndpointRouteBuilder endpoints)
    {
        var registry = endpoints.ServiceProvider.GetRequiredService<ResourceRegistry>();
        foreach (var info in registry.Resources)
        {
            MapCore(endpoints, info);
        }

        return endpoints;
    }

    internal static ResourceRouteGroup MapCore(IEndpointRouteBuilder endpoints, ResourceInfo info)
    {
        var method = typeof(EndpointRouteBuilderExtensions)
            .GetMethod(nameof(MapGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(info.ResourceType);
        try
        {
            return (ResourceRouteGroup)method.Invoke(null, new object[] { endpoints, info })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static ResourceRouteGroup MapGeneric<TResource>(IEndpointRouteBuilder endpoints, ResourceInfo info)
        where TResource : class, IResource
    {
        var registry = endpoints.ServiceProvider.GetRequiredService<ResourceRegistry>();
        registry.MarkMapped(typeof(TResource));

        var typed = (ResourceInfo<TResource>)info;
        var group = endpoints.MapGroup(typed.RoutePattern).WithTags(typed.Name);
        var routeGroup = new ResourceRouteGroup(group, typed);
        var endpoint = new ResourceEndpoint<TResource>(typed);

        // Cross-cutting HTTP concerns live in an endpoint filter (roadmap Decision 2), so authorization
        // and rate limiting in later releases can reuse the same pipeline.
        RouteHandlerBuilder WithConcurrency(RouteHandlerBuilder builder, ConcurrencyRole role)
            => typed.ConditionalRequestsEnabled
                ? builder.AddEndpointFilter(new ResourceConcurrencyFilter<TResource>(typed, role))
                : builder;

        if (typed.Options.ListEnabled)
        {
            // No filter on the collection: a list response has no single entity tag to validate against.
            group.MapGet("", (Delegate)((HttpContext http) => endpoint.ListAsync(http)));
        }

        if (typed.Options.GetEnabled)
        {
            WithConcurrency(
                group.MapGet("{id}", (Delegate)((HttpContext http) => endpoint.GetAsync(http))),
                ConcurrencyRole.Get).WithName($"crest:{typed.Name}:get");
        }

        if (typed.Options.CreateEnabled)
        {
            WithConcurrency(
                group.MapPost("", (Delegate)((HttpContext http) => endpoint.CreateAsync(http))),
                ConcurrencyRole.Create);
        }

        if (typed.Options.UpdateEnabled)
        {
            WithConcurrency(
                group.MapPut("{id}", (Delegate)((HttpContext http) => endpoint.UpdateAsync(http))),
                ConcurrencyRole.Update);
            WithConcurrency(
                group.MapPatch("{id}", (Delegate)((HttpContext http) => endpoint.PatchAsync(http))),
                ConcurrencyRole.Patch);
        }

        if (typed.Options.DeleteEnabled)
        {
            WithConcurrency(
                group.MapDelete("{id}", (Delegate)((HttpContext http) => endpoint.DeleteAsync(http))),
                ConcurrencyRole.Delete);
        }

        return routeGroup;
    }
}
