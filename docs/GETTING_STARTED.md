# Getting started

This guide walks through the core concepts of Crest: defining resources, enabling persistence, validating requests, wiring hooks, and extending routes.

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
- [Configuration reference](#configuration-reference)

## Prerequisites

- .NET SDK 8.0 or 10.0 (the packages multi-target `net8.0` and `net10.0`)
- An ASP.NET Core application (empty web, web API, or minimal API template)

## Install

Add the NuGet packages you need:

```
dotnet add package Crest
dotnet add package Crest.EFCore       # EF Core persistence
dotnet add package Crest.Validation   # DataAnnotations + FluentValidation
```

## Define a resource

A resource is any class that implements the marker interface `IResource`.

```csharp
using System.ComponentModel.DataAnnotations;
using Crest;

public sealed class Device : IResource
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Model { get; set; }
}
```

Crest derives everything from this type:

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

- `AddResources` discovers `IResource` types and registers services for them. By default it scans the calling and entry assemblies; add more with `DiscoverFromAssemblyContaining<T>()` or by appending to `CrestOptions.Assemblies`.
- `AddResourceValidation` enables automatic request validation.
- `MapResources` generates the CRUD endpoints for every discovered resource.
- `UseExceptionHandler()` is optional but recommended; Crest surfaces domain errors as `ProblemDetails` and lets the exception handler format unexpected failures.

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

Crest finds the key property using the first rule that matches, in this order:

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

Crest registers a thread-safe in-memory data source (`InMemoryResourceDataSource<T>`) for every discovered resource, so `AddResources` alone is enough for prototypes and tests.

### EF Core

Register your `DbContext` and call `AddEfCore` to back every matching resource with EF Core:

```csharp
using Crest.EFCore;
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

Crest deliberately has no repository layer — the `DbContext` is the unit of work, so EF Core's relationship and transaction behavior works unchanged. Navigations are modeled as ordinary CLR properties; Crest's update path copies scalar properties only and never touches collections, so relationships survive create/update.

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

## Configuration reference

### `CrestOptions` (passed to `AddResources`)

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

For example, disable delete and rename the route globally:

```csharp
builder.Services.AddResources(o =>
{
    o.DiscoverFromAssemblyContaining<Device>();
    o.DefaultResourceOptions = opts => opts.DeleteEnabled = false;
});
```
