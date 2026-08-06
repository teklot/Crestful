using System.Reflection;

namespace Crestful;

/// <summary>
/// Global configuration for the Crestful framework.
/// </summary>
public sealed class CrestfulOptions
{
    /// <summary>Assemblies scanned for <see cref="IResource"/> types. Defaults to the
    /// calling and entry assemblies.</summary>
    public IList<Assembly> Assemblies { get; } = new List<Assembly>();

    /// <summary>Route prefix shared by every resource, e.g. <c>"api"</c> turns
    /// a <c>Device</c> resource into <c>/api/devices</c>. Defaults to <c>"api"</c>.</summary>
    public string DefaultRoutePrefix { get; set; } = "api";

    /// <summary>Applied to every discovered resource's options before the application
    /// maps it, so global conventions (naming, disabling operations) can be set once.</summary>
    public Action<ResourceOptions>? DefaultResourceOptions { get; set; }

    /// <summary>Adds the assembly containing <typeparamref name="T"/> to discovery.</summary>
    public void DiscoverFromAssemblyContaining<T>() => Assemblies.Add(typeof(T).Assembly);
}
