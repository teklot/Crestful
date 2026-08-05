using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Crest;

/// <summary>
/// A group of endpoints mapped for a single resource. Generated CRUD endpoints are mapped
/// first; custom handlers can be added with the <c>Map*</c> methods and are grouped under
/// the same route prefix.
/// </summary>
public sealed class ResourceRouteGroup
{
    private readonly RouteGroupBuilder _builder;

    internal ResourceRouteGroup(RouteGroupBuilder builder, ResourceInfo resource)
    {
        _builder = builder;
        Resource = resource;
    }

    /// <summary>Metadata for the resource this group serves.</summary>
    public ResourceInfo Resource { get; }

    /// <summary>The underlying <see cref="RouteGroupBuilder"/> for advanced scenarios.</summary>
    public RouteGroupBuilder Builder => _builder;

    /// <summary>Adds a custom GET handler under the resource's route prefix.</summary>
    public ResourceRouteGroup MapGet(string pattern, Delegate handler)
    {
        _builder.MapGet(pattern, handler);
        return this;
    }

    /// <summary>Adds a custom POST handler under the resource's route prefix.</summary>
    public ResourceRouteGroup MapPost(string pattern, Delegate handler)
    {
        _builder.MapPost(pattern, handler);
        return this;
    }

    /// <summary>Adds a custom PUT handler under the resource's route prefix.</summary>
    public ResourceRouteGroup MapPut(string pattern, Delegate handler)
    {
        _builder.MapPut(pattern, handler);
        return this;
    }

    /// <summary>Adds a custom PATCH handler under the resource's route prefix.</summary>
    public ResourceRouteGroup MapPatch(string pattern, Delegate handler)
    {
        _builder.MapPatch(pattern, handler);
        return this;
    }

    /// <summary>Adds a custom DELETE handler under the resource's route prefix.</summary>
    public ResourceRouteGroup MapDelete(string pattern, Delegate handler)
    {
        _builder.MapDelete(pattern, handler);
        return this;
    }
}
