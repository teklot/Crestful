namespace Crestful;

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

    /// <summary>Query engine configuration for the list endpoint.</summary>
    public ResourceQueryOptions Query { get; set; } = new();

    /// <summary>Soft delete configuration for the resource.</summary>
    public ResourceSoftDeleteOptions SoftDelete { get; set; } = new();
}

/// <summary>
/// Configuration for the query engine on a per-resource basis.
/// </summary>
public class ResourceQueryOptions
{
    /// <summary>
    /// Fields allowed for filtering via <c>?where=</c>. Defaults to all fields.
    /// Set to restrict which properties clients can query.
    /// </summary>
    public HashSet<string> AllowedFilterFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Fields allowed for sorting via <c>?sort=</c>. Defaults to all fields.
    /// Set to restrict which properties clients can sort by.
    /// </summary>
    public HashSet<string> AllowedSortFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Default page size when <c>?page=</c> is provided without <c>?max_results=</c>. Defaults to 25.</summary>
    public int DefaultPageSize { get; set; } = 25;

    /// <summary>Maximum page size allowed for <c>?max_results=</c>. Defaults to 100. Set to 0 for unlimited.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>Whether full-text search via <c>?search=</c> is enabled. Defaults to <c>true</c>.</summary>
    public bool SearchEnabled { get; set; } = true;

    /// <summary>Whether field selection via <c>?field=</c> is enabled. Defaults to <c>true</c>.</summary>
    public bool FieldSelectionEnabled { get; set; } = true;

    /// <summary>Whether relationship embedding via <c>?embedded=</c> is enabled. Defaults to <c>true</c>.</summary>
    public bool EmbeddingEnabled { get; set; } = true;
}

/// <summary>
/// Configuration for soft delete on a per-resource basis.
/// </summary>
public class ResourceSoftDeleteOptions
{
    /// <summary>Whether soft delete is enabled for this resource. Defaults to <c>false</c>.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The property name on the resource that stores the soft-delete timestamp.
    /// Defaults to <c>"DeletedAt"</c>.
    /// </summary>
    public string DeletedAtFieldName { get; set; } = "DeletedAt";
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
