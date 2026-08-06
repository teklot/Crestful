using Microsoft.AspNetCore.Http;

namespace Crestful;

/// <summary>
/// Base context handed to lifecycle hooks.
/// </summary>
public class ResourceHookContext
{
    /// <summary>The current HTTP request.</summary>
    public required HttpContext HttpContext { get; init; }

    /// <summary>Metadata for the resource being operated on.</summary>
    public required ResourceInfo ResourceInfo { get; init; }

    /// <summary>Cancellation token tied to the request.</summary>
    public CancellationToken CancellationToken => HttpContext.RequestAborted;
}

/// <summary>Context for create hooks.</summary>
public sealed class CreateContext<TResource> : ResourceHookContext where TResource : class, IResource
{
    /// <summary>The resource being created.</summary>
    public required TResource Resource { get; init; }
}

/// <summary>Context for update hooks.</summary>
public sealed class UpdateContext<TResource> : ResourceHookContext where TResource : class, IResource
{
    /// <summary>The incoming (PUT or PATCH) values.</summary>
    public required TResource Resource { get; init; }

    /// <summary>The existing resource being updated.</summary>
    public required TResource Original { get; init; }
}

/// <summary>Context for delete hooks.</summary>
public sealed class DeleteContext<TResource> : ResourceHookContext where TResource : class, IResource
{
    /// <summary>The resource being deleted.</summary>
    public required TResource Resource { get; init; }
}

/// <summary>
/// DI-based lifecycle hooks for a resource. Register implementations with the container
/// (e.g. <c>services.AddScoped&lt;IResourceHook&lt;Device&gt;, DeviceHook&gt;()</c>); they run
/// after any hooks configured on the resource options.
/// </summary>
public interface IResourceHook<TResource> where TResource : class, IResource
{
    Task BeforeCreateAsync(CreateContext<TResource> context) => Task.CompletedTask;
    Task AfterCreateAsync(CreateContext<TResource> context) => Task.CompletedTask;
    Task BeforeUpdateAsync(UpdateContext<TResource> context) => Task.CompletedTask;
    Task AfterUpdateAsync(UpdateContext<TResource> context) => Task.CompletedTask;
    Task BeforeDeleteAsync(DeleteContext<TResource> context) => Task.CompletedTask;
    Task AfterDeleteAsync(DeleteContext<TResource> context) => Task.CompletedTask;
    Task BeforeSaveAsync(ResourceHookContext context) => Task.CompletedTask;
    Task AfterSaveAsync(ResourceHookContext context) => Task.CompletedTask;
}
