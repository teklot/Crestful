namespace Crest;

/// <summary>
/// Options shared by every resource, including the route name and which CRUD operations
/// the framework should generate.
/// </summary>
public class ResourceOptions
{
    /// <summary>
    /// Route name override, e.g. <c>"widgets"</c>. Defaults to a pluralized, lower-cased
    /// version of the resource type name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Whether GET on the collection is generated. Defaults to <c>true</c>.</summary>
    public bool ListEnabled { get; set; } = true;

    /// <summary>Whether GET by id is generated. Defaults to <c>true</c>.</summary>
    public bool GetEnabled { get; set; } = true;

    /// <summary>Whether POST is generated. Defaults to <c>true</c>.</summary>
    public bool CreateEnabled { get; set; } = true;

    /// <summary>Whether PUT and PATCH are generated. Defaults to <c>true</c>.</summary>
    public bool UpdateEnabled { get; set; } = true;

    /// <summary>Whether DELETE is generated. Defaults to <c>true</c>.</summary>
    public bool DeleteEnabled { get; set; } = true;
}

/// <summary>
/// Options for a specific resource type.
/// </summary>
public sealed class ResourceOptions<TResource> : ResourceOptions where TResource : class, IResource
{
    /// <summary>Runs before a new resource is persisted.</summary>
    public Func<CreateContext<TResource>, Task>? BeforeCreate { get; set; }

    /// <summary>Runs after a new resource is persisted.</summary>
    public Func<CreateContext<TResource>, Task>? AfterCreate { get; set; }

    /// <summary>Runs before an existing resource is updated.</summary>
    public Func<UpdateContext<TResource>, Task>? BeforeUpdate { get; set; }

    /// <summary>Runs after an existing resource is updated.</summary>
    public Func<UpdateContext<TResource>, Task>? AfterUpdate { get; set; }

    /// <summary>Runs before an existing resource is deleted.</summary>
    public Func<DeleteContext<TResource>, Task>? BeforeDelete { get; set; }

    /// <summary>Runs after an existing resource is deleted.</summary>
    public Func<DeleteContext<TResource>, Task>? AfterDelete { get; set; }

    /// <summary>Runs immediately before a change is persisted (saved).</summary>
    public Func<ResourceHookContext, Task>? BeforeSave { get; set; }

    /// <summary>Runs immediately after a change is persisted (saved).</summary>
    public Func<ResourceHookContext, Task>? AfterSave { get; set; }
}
