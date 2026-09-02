# Getting started

This guide walks through the core concepts of Crestful: defining resources, enabling persistence, validating requests, wiring hooks, querying collections, and extending routes.

- [Prerequisites](#prerequisites)
- [Install](#install)
- [Define a resource](#define-a-resource)
- [Register the framework](#register-the-framework)
- [Key properties](#key-properties)
- [Persistence](#persistence)
- [Validation](#validation)
- [Hooks](#hooks)
- [Custom endpoints](#custom-endpoints)
- [Error handling](#error-handling)
- [Query engine](#query-engine)
- [Soft delete](#soft-delete)
- [Auditing](#auditing)
- [Configuration reference](#configuration-reference)

## Prerequisites

- .NET SDK 8.0 or 10.0 (the packages multi-target `net8.0` and `net10.0`)
- An ASP.NET Core application (empty web, web API, or minimal API template)

## Install

Add the NuGet packages you need:

```
dotnet add package Crestful
dotnet add package Crestful.EFCore       # EF Core persistence
dotnet add package Crestful.Validation   # DataAnnotations + FluentValidation
```

## Define a resource

A resource is any class that implements the marker interface `IResource`.

```csharp
using System.ComponentModel.DataAnnotations;
using Crestful;

public sealed class Device : IResource
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Model { get; set; }
}
```

Crestful derives everything from this type:

- **Route** — the pluralized, lower-cased type name under the `api` prefix: `/api/devices`.
- **Key** — the key property (see [Key properties](#key-properties)), used for `GET/PUT/PATCH/DELETE /{id}`.
- **Validation** — Data Annotation attributes are honored automatically once `AddResourceValidation` is registered.

### Resource naming

| Resource type | Route | Route pattern |
| --- | --- | --- |
| `Device` | `devices` | `/api/devices` |
| `Category` | `categories` | `/api/categories` |
| `GuidResource` | `guidresources` | `/api/guidresources` |

Override the name per resource with `o.Name = "machines"` when mapping (see [Configuration reference](#configuration-reference)).

## Register the framework

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResources(options => options.DiscoverFromAssemblyContaining<Device>());
builder.Services.AddResourceValidation();

var app = builder.Build();
app.UseExceptionHandler();
app.MapResources();

app.Run();
```

- `AddResources` discovers `IResource` types and registers services for them. By default it scans the calling and entry assemblies; add more with `DiscoverFromAssemblyContaining<T>()` or by appending to `CrestfulOptions.Assemblies`.
- `AddResourceValidation` enables automatic request validation.
- `MapResources` generates the CRUD endpoints for every discovered resource.
- `UseExceptionHandler()` is optional but recommended; Crestful surfaces domain errors as `ProblemDetails` and lets the exception handler format unexpected failures.

To map a single resource, use `app.MapResource<Device>()`.

### Route prefix

The default prefix is `api`. Change it globally:

```csharp
builder.Services.AddResources(o =>
{
    o.DiscoverFromAssemblyContaining<Device>();
    o.DefaultRoutePrefix = "";
});
```

With an empty prefix, `Device` is served at `/devices`.

## Key properties

Crestful finds the key property using the first rule that matches, in this order:

1. A property decorated with `[Key]`
2. `Id` (case-insensitive)
3. `{TypeName}Id`, e.g. `DeviceId`
4. The type name with a trailing `Resource` stripped, e.g. `DeviceIdResource` → `DeviceId`

```csharp
public sealed class DeviceIdResource : IResource
{
    public int DeviceId { get; set; }   // matched by the {TypeName}Id convention
}

public sealed class StringKeyResource : IResource
{
    [Key]
    public string Code { get; set; } = string.Empty;   // matched by [Key]
}
```

Supported key types include the numeric types, `Guid`, and `string`. Keys must be writable and scalar. If no key property can be found, mapping the resource throws a descriptive `InvalidOperationException`.

Keys that are left at their default value are generated automatically:

- `int`/`long`/`short`/`byte` — auto-incrementing
- `Guid` — a new `Guid`
- `string` — a generated value

## Persistence

Crestful registers a thread-safe in-memory data source (`InMemoryResourceDataSource<T>`) for every discovered resource, so `AddResources` alone is enough for prototypes and tests.

### EF Core

Register your `DbContext` and call `AddEfCore` to back every matching resource with EF Core:

```csharp
using Crestful.EFCore;
using Microsoft.EntityFrameworkCore;

public sealed class DeviceDbContext : DbContext
{
    public DeviceDbContext(DbContextOptions<DeviceDbContext> options) : base(options) { }
    public DbSet<Device> Devices => Set<Device>();
}

// Program.cs
builder.Services.AddDbContext<DeviceDbContext>(o => o.UseInMemoryDatabase("devices"));
builder.Services.AddEfCore();
```

`AddEfCore` scans every registered `DbContext` for `DbSet<T>` properties whose entity type implements `IResource` and replaces the in-memory data source for those resources. To target a specific resource/context pair explicitly:

```csharp
builder.Services.AddEfCoreResource<Device, DeviceDbContext>();
```

> Tip: pass a stable database name to `UseInMemoryDatabase`. The configuration lambda can be invoked more than once, so a value generated inside it (e.g. `Guid.NewGuid().ToString()`) yields a different database per request.

### Relationships & transactions

Crestful deliberately has no repository layer — the `DbContext` is the unit of work, so EF Core's relationship and transaction behavior works unchanged. Navigations are modeled as ordinary CLR properties; Crestful's update path copies scalar properties only and never touches collections, so relationships survive create/update.

A related resource is just another `IResource` with a foreign key (and its own generated endpoints):

```csharp
public sealed class Reading : IResource
{
    public Guid Id { get; set; }
    public int DeviceId { get; set; }        // FK to Device
    public double Value { get; set; }
}

public sealed class Device : IResource
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public List<Reading> Readings { get; set; } = [];
}
```

Sending a nested graph in one request persists atomically — `SaveChanges` runs a single transaction, so the device and its readings either all persist or none do:

```csharp
POST /api/devices
{ "name": "Gateway", "readings": [ { "value": 1.5 }, { "value": 2.5 } ] }
```

Guid keys (and other generated keys) are produced by EF Core when the value is left at its default.

### Custom data sources

`IResourceDataSource<T>` is the persistence abstraction. Implement and register your own to take full control:

```csharp
services.AddScoped<IResourceDataSource<Device>, MyDataSource>();
```

If multiple data sources are registered for one resource, the last one wins.

## Validation

`AddResourceValidation()` enables validation for create and update requests (including PATCH). Invalid input returns `400 Bad Request` with a `application/problem+json` body whose `errors` dictionary keys the offending properties.

### Data Annotations

On by default. Decorate resource properties as usual:

```csharp
public sealed class Device : IResource
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
}
```

### FluentValidation

Validators are discovered from the calling and entry assemblies:

```csharp
using FluentValidation;

public sealed class Order : IResource
{
    public int Id { get; set; }
    public string? Reference { get; set; }
}

public sealed class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(o => o.Reference).NotEmpty().MaximumLength(3);
    }
}
```

Both engines run; their errors are aggregated into a single response.

## Hooks

Hooks run around each operation. There are two ways to register them:

### Options hooks

Set delegates on the resource's options while mapping:

```csharp
app.MapResource<Device>(o =>
{
    o.BeforeCreate = ctx => { ctx.Resource.Name = ctx.Resource.Name.Trim(); return Task.CompletedTask; };
    o.AfterCreate  = ctx => { /* e.g. send a notification */ return Task.CompletedTask; };
});
```

Available for create, update, delete, and save:

| Hook | Runs |
| --- | --- |
| `BeforeCreate` / `AfterCreate` | Before / after a new resource is persisted |
| `BeforeUpdate` / `AfterUpdate` | Before / after an existing resource is updated |
| `BeforeDelete` / `AfterDelete` | Before / after an existing resource is deleted |
| `BeforeSave` / `AfterSave` | Immediately before / after the persistence call |

### DI hooks

Register an `IResourceHook<T>` implementation in the container; it runs for every operation of that resource:

```csharp
public sealed class DeviceHook : IResourceHook<Device>
{
    public Task AfterCreateAsync(CreateContext<Device> context) { /* ... */ return Task.CompletedTask; }
}

// Program.cs
builder.Services.AddScoped<IResourceHook<Device>, DeviceHook>();
```

### Order

For a create, the documented order is:

```
options:before-create → di:before-create → di:before-save → [persist] → di:after-save → di:after-create → options:after-create
```

Options hooks wrap the DI hooks: they run first on the way in and last on the way out. The same pattern applies to update and delete. Exceptions thrown by a hook surface as `500 Internal Server Error` (formatted by the exception handler).

## Custom endpoints

`MapResource<T>()` returns a `ResourceRouteGroup` you can extend with handlers grouped under the same route prefix:

```csharp
app.MapResource<Device>()
   .MapGet("/count", () => Results.Ok(7))
   .MapGet("/echo/{value}", (string value) => Results.Text(value));
```

Generated CRUD endpoints are mapped first; your handlers are added afterwards. Handlers receive normal minimal API binding (route values, query strings, services, and so on).

## Error handling

The generated endpoints return RFC 7807 ProblemDetails responses:

| Status | When |
| --- | --- |
| `400` | The route key or request body is invalid, or validation failed |
| `404` | No resource exists with the given key |
| `409` | A create collides with an existing key |

## Query engine

The list endpoint supports Eve-style query parameters for filtering, sorting, pagination, search, and field selection.

### Filtering (`?where=`)

Filter by passing a JSON object in the `where` query parameter:

```
GET /api/devices?where={"Name":"Thermostat"}
GET /api/devices?where={"IsActive":true}
GET /api/devices?where={"Quantity":5}
```

### Sorting (`?sort=`)

Sort by one or more fields. Prefix with `-` for descending order:

```
GET /api/devices?sort=Name          # ascending by Name
GET /api/devices?sort=-Quantity     # descending by Quantity
GET /api/devices?sort=Model,-Name   # ascending by Model, then descending by Name
```

### Pagination (`?page=&max_results=`)

Paginate results. `max_results` also works standalone to limit the result count:

```
GET /api/devices?page=1&max_results=10    # first page, 10 items
GET /api/devices?max_results=5            # first 5 items
```

Default page size is 25. Maximum page size defaults to 100.

### Search (`?search=`)

Full-text search across all string properties:

```
GET /api/devices?search=temperature
```

### Field selection (`?field=`)

Return only specified fields:

```
GET /api/devices?field=Name,Model
```

### Per-resource query configuration

Configure query behavior per resource:

```csharp
app.MapResource<Device>(o =>
{
    o.Query.MaxPageSize = 50;           // cap max_results at 50
    o.Query.DefaultPageSize = 10;       // default page size when only ?page= is used
    o.Query.SearchEnabled = false;      // disable search for this resource
    o.Query.FieldSelectionEnabled = false; // disable field selection
    o.Query.AllowedFilterFields = ["Name", "Model"]; // restrict filterable fields
    o.Query.AllowedSortFields = ["Name", "CreatedAt"]; // restrict sortable fields
});
```

## Soft delete

Resources that implement `ISoftDeletable` can opt into soft delete. The endpoints stay the same, but the behavior changes:

| Operation | Without soft delete | With soft delete |
| --- | --- | --- |
| `DELETE` | Removes the record | Stamps `DeletedAt`, record stays |
| `GET /{id}` | Returns the record | Returns 404 if soft-deleted |
| `GET /` | Returns all records | Excludes soft-deleted items |
| `PUT` | Updates the record | Restores soft-deleted item (clears `DeletedAt`) or updates active item |
| `PATCH` | Updates the record | Restores soft-deleted item (clears `DeletedAt`) or updates active item |

### Define a soft-deletable resource

```csharp
using Crestful;

public sealed class Device : IResource, ISoftDeletable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset? DeletedAt { get; set; }
}
```

### Enable soft delete per resource

```csharp
app.MapResource<Device>(o =>
{
    o.SoftDelete.Enabled = true;
    o.SoftDelete.DeletedAtFieldName = "DeletedAt"; // default
});
```

### Behavior

| Operation | What happens |
| --- | --- |
| `DELETE` | Sets `DeletedAt` to the current timestamp, record stays in the database |
| `GET /{id}` | Returns 404 if the item is soft-deleted |
| `GET /` | Automatically excludes soft-deleted items from results |
| `PUT` | If the item is soft-deleted, restores it (clears `DeletedAt`). Otherwise updates normally |
| `PATCH` | If the item is soft-deleted, restores it (clears `DeletedAt`). Otherwise updates normally |

### Custom field name

If your entity uses a different property name for the soft-delete timestamp:

```csharp
app.MapResource<Device>(o =>
{
    o.SoftDelete.Enabled = true;
    o.SoftDelete.DeletedAtFieldName = "RemovedAt";
});
```

## Auditing

Resources that implement `IAuditable` can opt into automatic auditing. The framework populates `CreatedAt`, `UpdatedAt`, `CreatedBy`, and `UpdatedBy` on create and update, so you don't have to set them yourself.

### Define an auditable resource

```csharp
using Crestful;

public sealed class Device : IResource, IAuditable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
```

### Enable auditing per resource

```csharp
app.MapResource<Device>(o => o.Auditing.Enabled = true);
```

### Behavior

| Operation | What happens |
| --- | --- |
| `POST` | Sets `CreatedAt` and `UpdatedAt` to now; sets `CreatedBy` and `UpdatedBy` from `HttpContext.User.Identity?.Name` (null if anonymous) |
| `PUT` | Updates `UpdatedAt` and `UpdatedBy`; preserves the original `CreatedAt` and `CreatedBy` |
| `PATCH` | Updates `UpdatedAt` and `UpdatedBy`; preserves the original `CreatedAt` and `CreatedBy` |

`CreatedAt` and `CreatedBy` are always preserved across updates — a client cannot overwrite them.

### Custom field names

If your entity uses different property names:

```csharp
app.MapResource<Device>(o =>
{
    o.Auditing.Enabled = true;
    o.Auditing.CreatedAtFieldName = "InsertedAt";
    o.Auditing.UpdatedAtFieldName = "ModifiedAt";
    o.Auditing.CreatedByFieldName = "InsertedBy";
    o.Auditing.UpdatedByFieldName = "ModifiedBy";
});
```

## Configuration reference

### `CrestfulOptions` (passed to `AddResources`)

| Member | Default | Description |
| --- | --- | --- |
| `Assemblies` | calling + entry | Assemblies scanned for `IResource` types |
| `DefaultRoutePrefix` | `"api"` | Route prefix shared by every resource |
| `DefaultResourceOptions` | `null` | Applied to every discovered resource before mapping |
| `DiscoverFromAssemblyContaining<T>()` | — | Adds an assembly to discovery |

### `ResourceOptions<T>` (passed to `MapResource` / via `DefaultResourceOptions`)

| Member | Default | Description |
| --- | --- | --- |
| `Name` | pluralized type name | Route name override |
| `ListEnabled` | `true` | Generate `GET` on the collection |
| `GetEnabled` | `true` | Generate `GET` by id |
| `CreateEnabled` | `true` | Generate `POST` |
| `UpdateEnabled` | `true` | Generate `PUT` and `PATCH` |
| `DeleteEnabled` | `true` | Generate `DELETE` |
| `Query` | default | Query engine configuration (see [Query engine](#query-engine)) |
| `SoftDelete` | default | Soft delete configuration (see [Soft delete](#soft-delete)) |
| `Auditing` | default | Auditing configuration (see [Auditing](#auditing)) |

For example, disable delete and rename the route globally:

```csharp
builder.Services.AddResources(o =>
{
    o.DiscoverFromAssemblyContaining<Device>();
    o.DefaultResourceOptions = opts => opts.DeleteEnabled = false;
});
```
