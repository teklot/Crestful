# Crest

Convention-first REST endpoints from strongly-typed resources for ASP.NET Core.

Crest is inspired by [Eve](https://docs.python-eve.org/), the REST framework for Python/Flask: describe a resource once and receive a production-ready REST API. Where Eve let Flask developers turn a class into a full CRUD service, Crest does the same for ASP.NET Core minimal APIs — while staying close to the platform and never locking you in.

```csharp
public sealed class Device : IResource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Model { get; set; }
}
```

That single class gives you `GET`, `GET/{id}`, `POST`, `PUT`, `PATCH`, and `DELETE` against `/api/devices`.

## Features

- **Resource discovery** — scan the calling/entry assemblies (or an explicit list) for `IResource` types; nothing to register per resource.
- **Full CRUD generation** — list, get, create, update, patch, and delete endpoints via minimal APIs, with per-resource operation toggles.
- **Persistence without ceremony** — a thread-safe in-memory data source is registered for every discovered resource; swap in EF Core with one call.
- **Automatic validation** — Data Annotations by default, plus any FluentValidation validators found in your assemblies.
- **Lifecycle hooks** — options-based and DI-based hooks that run before/after create, update, delete, and save.
- **ProblemDetails errors** — 400 (invalid body/key/validation), 404 (missing resource), and 409 (key conflict) return standard `application/problem+json`.
- **Custom endpoints** — add your own handlers under a resource's route group.
- **Plain ASP.NET Core** — built on minimal APIs, route groups, and Microsoft DI. Every convention is overridable.

## Packages

| Package | Description |
| --- | --- |
| `Crest` | Core framework: discovery, endpoint generation, in-memory data source, hooks. |
| `Crest.EFCore` | EF Core-backed data sources via `AddEfCore` / `AddEfCoreResource`. |
| `Crest.Validation` | Data Annotations + FluentValidation request validation via `AddResourceValidation`. |

Multi-targeted at **net8.0** and **net10.0**.

## Quick start

```csharp
// Program.cs
using Crest;
using Crest.EFCore;
using Crest.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResources(options => options.DiscoverFromAssemblyContaining<Device>());
builder.Services.AddResourceValidation();

var app = builder.Build();
app.UseExceptionHandler();
app.MapResources();

app.Run();
```

For EF Core persistence:

```csharp
builder.Services.AddDbContext<DeviceDbContext>(o => o.UseInMemoryDatabase("devices"));
builder.Services.AddEfCore();
```

A complete runnable example lives in [`Crest.Sample`](Crest.Sample).

## Generated endpoints

For a resource `Device` (key `int`), with the default `api` route prefix:

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/devices` | List all devices |
| `GET` | `/api/devices/{id}` | Get one device |
| `POST` | `/api/devices` | Create a device |
| `PUT` | `/api/devices/{id}` | Replace a device |
| `PATCH` | `/api/devices/{id}` | Partially update a device |
| `DELETE` | `/api/devices/{id}` | Delete a device |

## Documentation

- [Getting started](docs/GETTING_STARTED.md) — resources, keys, persistence, validation, hooks, and custom endpoints.
- [Project requirements](crest_prd.md) — the product requirements document.

## License

MIT
