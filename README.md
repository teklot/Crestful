# Crestful — Resource-First REST for ASP.NET Core

[![CI](https://github.com/teklot/Crestful/actions/workflows/ci.yml/badge.svg)](https://github.com/teklot/Crestful/actions/workflows/ci.yml)
[![NuGet Version](https://img.shields.io/nuget/v/Crestful)](https://www.nuget.org/packages/Crestful)
[![.NET](https://img.shields.io/badge/.NET-net8.0%20%7C%20net10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue)](LICENSE)

Every ASP.NET Core team I've worked with builds the same thing: a `Controller`, a `Service`, a repository, request DTOs, response DTOs, a mapper, validation logic, and a `ProblemDetails` handler — duplicated across every API, each with different conventions, none composable. The resource gets described three times (entity, request, response), and adding one endpoint means touching controller, service, DI, and mapping by hand.

Crestful is the convention layer that sits on top of ASP.NET Core — **define a resource once, receive a production-ready REST API.** Not an app framework, not an alternative to ASP.NET Core. A thin productivity layer between your resource model and your endpoints.

**Guiding principle:** Never replace the Microsoft ecosystem. Generate infrastructure, not business logic.

**Crest** stands for **C**onvention-first **R**EST **E**ndpoints from **S**trongly-**T**yped resources; *Crestful* is the play on RESTful.

## The Problem

```csharp
// Typical API — every resource needs the same boilerplate, by hand:
public sealed class Controller : ControllerBase
{
    [HttpGet]     public IActionResult List() => ...;
    [HttpGet("{id}")] public IActionResult Get(int id) => ...;
    [HttpPost]    public IActionResult Create(CreateDeviceRequest dto) => ...;
    [HttpPut("{id}")]  public IActionResult Update(int id, UpdateDeviceRequest dto) => ...;
    [HttpPatch("{id}")] public IActionResult Patch(int id, JsonPatchDocument<Device> patch) => ...;
    [HttpDelete("{id}")] public IActionResult Delete(int id) => ...;
    // + a service, a repository, a mapper, validators, ProblemDetails handlers...
}
```

The resource is declared once and then re-declared as a request DTO, a response DTO, and again in EF. Validation, persistence wiring, and error handling are reinvented in every project. Teams that want CRUD infrastructure without adopting an opinionated app framework have no lightweight option on ASP.NET Core — the gap Eve filled for Python.

**Crestful eliminates the seam.** One strongly-typed class becomes the single source of truth, and the entire CRUD API derives from it.

## How It Works

```
Resource (IResource)
      ↓ discovered at startup
Crestful builds the ResourceModel
      ↓
Route group → Minimal API endpoints → Data source → Response
      ↓              ↓                    ↓
   Validation      Hooks            In-memory / EF Core
```

```csharp
public sealed class Device : IResource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Model { get; set; }
}
```

That class — plus `AddResources()` and `MapResources()` — gives you `GET`, `GET/{id}`, `POST`, `PUT`, `PATCH`, and `DELETE` against `/api/devices`.

### Discovery, Zero Registration

`AddResources()` scans the calling assemblies for `IResource` types. No per-resource service registration, no DTO mapping, no manual endpoint mapping — add a class, restart, the API exists.

### Persistence Without a Repository

Every resource gets a thread-safe in-memory data source by default, so prototypes work with zero setup. When you're ready for a database, one call swaps EF Core in — Crestful scans your `DbContext`s and backs every matching `DbSet<T>` resource with EF directly. No repository layer, no new abstraction.

### Validation On by Default

Data Annotations validate every create, update, and patch automatically. FluentValidation validators in your assemblies are discovered and applied the same way. Invalid input returns a standard `application/problem+json` with the offending properties.

### Lifecycle Hooks

Before/after hooks run around create, update, delete, and save — both configuration-based and dependency-injected. Audit logging, defaulting, side effects: every step of the lifecycle is an escape hatch.

### Errors in the Standard Shape

400 (bad request, key, or validation), 404 (missing), and 409 (conflict) all return ProblemDetails. No bespoke error formats.

### Escape Hatches

Every convention is overridable. Disable any operation per resource, override the route prefix, or map your own handlers onto a resource's route group — plain ASP.NET Core underneath, never a lock-in.

## Use Cases

### Rapid Prototypes

`AddResources()` + in-memory storage = a complete CRUD API from a single class. Validate the data model first, add EF Core and FluentValidation when the shape stabilizes.

### Internal Enterprise CRUD

Standardized endpoints, validation, and error handling across every internal service. Teams agree on the resource model and get consistent API behavior for free.

### SaaS & Admin Backends

A resource definition is the contract. The same class drives the public API, an internal admin surface, and persistence — no parallel DTO hierarchies to keep in sync.

## Technical Differentiators

| vs. | Crestful |
|---|---|
| **Hand-rolled controllers** | No controllers, services, repositories, or DTO mapping. The resource is the only artifact. |
| **ABP framework** | Crestful is a thin convention layer, not an application framework — no opinions on project structure, persistence, or front end. |
| **EF Core alone** | EF gives you the store, not the API. Crestful composes EF Core and adds endpoints, validation, hooks, and errors on top. |
| **Eve (Python)** | The same resource-first model, on ASP.NET Core Minimal APIs — built on Microsoft DI, routing, and ProblemDetails. |

## Packages

| Package | Description |
|---|---|
| **Crestful** | Core framework: resource discovery, endpoint generation, in-memory data source, hooks, custom endpoints, ProblemDetails. |
| **Crestful.EFCore** | EF Core-backed data sources via `AddEfCore` / `AddEfCoreResource` — DbContext discovery, no repository layer. |
| **Crestful.Validation** | Data Annotations + FluentValidation request validation via `AddResourceValidation`. |

Multi-targeted at **net8.0** and **net10.0**.

## Installation

```shell
dotnet add package Crestful
dotnet add package Crestful.EFCore
dotnet add package Crestful.Validation
```

For a minimal in-memory API, `Crestful` alone is enough.

## Quick Start

```csharp
// Program.cs
using Crestful;
using Crestful.EFCore;
using Crestful.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResources(options => options.DiscoverFromAssemblyContaining<Device>());
builder.Services.AddResourceValidation();

var app = builder.Build();
app.UseExceptionHandler();
app.MapResources();

app.Run();
```

A complete runnable example lives in [`Crestful.Sample`](Crestful.Sample) — a device domain with a `Reading` sub-resource, EF Core persistence, seeded data, and validation.

## Generated Endpoints

For a resource `Device` (key `int`), with the default `api` route prefix:

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/devices` | List all devices |
| `GET` | `/api/devices/{id}` | Get one device |
| `POST` | `/api/devices` | Create a device |
| `PUT` | `/api/devices/{id}` | Replace a device |
| `PATCH` | `/api/devices/{id}` | Partially update a device |
| `DELETE` | `/api/devices/{id}` | Delete a device |

Keys are discovered by convention — `[Key]`, `Id`, `{TypeName}Id`, or the type name with a trailing `Resource` stripped — and may be numeric, `Guid`, or `string`. Related resources are plain `IResource` classes with a foreign key; send a nested graph in one request and it persists in a single transaction.

## Supported Frameworks

- **.NET 8+**: `net8.0` and `net10.0` packages.
- **ASP.NET Core Minimal APIs**: built on routing, DI, and ProblemDetails — no framework-specific hosting.

## Documentation

- [Getting started](docs/GETTING_STARTED.md) — resources, keys, persistence, validation, hooks, and custom endpoints.

## License

[Apache 2.0](LICENSE)
