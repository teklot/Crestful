using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crestful.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Crestful;

/// <summary>
/// Implements the generated CRUD endpoints for a single resource type.
/// </summary>
internal sealed class ResourceEndpoint<TResource> where TResource : class, IResource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ResourceInfo<TResource> _info;

    public ResourceEndpoint(ResourceInfo<TResource> info)
    {
        _info = info;
    }

    public async Task<IResult> ListAsync(HttpContext http)
    {
        var dataSource = ResolveDataSource(http);
        var query = QueryParameterParser.Parse(http, _info.Options.Query);

        if (_info.SoftDeleteEnabled)
        {
            query = InjectSoftDeleteFilter(query, _info);
        }

        var items = await dataSource.ListAsync(query, http.RequestAborted);

        if (query.Fields is { Count: > 0 })
        {
            var json = JsonSerializer.Serialize(items, JsonOptions);
            var doc = JsonDocument.Parse(json);
            var filtered = FilterJsonProperties(doc.RootElement, query.Fields);
            return Results.Json(filtered, JsonOptions);
        }

        return Results.Json(items, JsonOptions);
    }

    public async Task<IResult> GetAsync(HttpContext http)
    {
        if (!TryGetKey(http, out var key))
        {
            return ResourceErrors.InvalidKey(_info);
        }

        var dataSource = ResolveDataSource(http);
        var item = await dataSource.GetAsync(key!, http.RequestAborted);
        if (item is null)
        {
            return ResourceErrors.NotFound(_info);
        }

        if (_info.SoftDeleteEnabled && _info.GetDeletedAt(item).HasValue)
        {
            return ResourceErrors.NotFound(_info);
        }

        return Results.Json(item, JsonOptions);
    }

    public async Task<IResult> CreateAsync(HttpContext http)
    {
        var resource = await ReadBodyAsync(http);
        if (resource is null)
        {
            return ResourceErrors.InvalidBody();
        }

        var validation = await ValidateAsync(http, resource);
        if (validation is not null)
        {
            return validation;
        }

        var dataSource = ResolveDataSource(http);
        var context = new CreateContext<TResource> { HttpContext = http, ResourceInfo = _info, Resource = resource };

        await InvokeCreateHooksAsync(http, context, before: true);
        await InvokeSaveHooksAsync(http, before: true);

        TResource created;
        try
        {
            created = await dataSource.CreateAsync(resource, http.RequestAborted);
        }
        catch (ResourceConflictException)
        {
            return ResourceErrors.Conflict(_info);
        }

        await InvokeSaveHooksAsync(http, before: false);
        await InvokeCreateHooksAsync(http, context, before: false);

        var key = _info.GetKey(created)?.ToString() ?? string.Empty;
        var location = $"{_info.RoutePattern}/{Uri.EscapeDataString(key)}";
        return Results.Created(location, created);
    }

    public async Task<IResult> UpdateAsync(HttpContext http)
    {
        if (!TryGetKey(http, out var key))
        {
            return ResourceErrors.InvalidKey(_info);
        }

        var dataSource = ResolveDataSource(http);
        var original = await dataSource.GetAsync(key!, http.RequestAborted);
        if (original is null)
        {
            return ResourceErrors.NotFound(_info);
        }

        var resource = await ReadBodyAsync(http);
        if (resource is null)
        {
            return ResourceErrors.InvalidBody();
        }

        _info.SetKey(resource, key);

        var validation = await ValidateAsync(http, resource);
        if (validation is not null)
        {
            return validation;
        }

        var context = new UpdateContext<TResource>
        {
            HttpContext = http,
            ResourceInfo = _info,
            Resource = resource,
            Original = original,
        };

        await InvokeUpdateHooksAsync(http, context, before: true);
        await InvokeSaveHooksAsync(http, before: true);

        if (_info.SoftDeleteEnabled)
        {
            _info.SetDeletedAt(original, null);
        }

        var updated = await dataSource.UpdateAsync(resource, original, http.RequestAborted);
        if (updated is null)
        {
            return ResourceErrors.NotFound(_info);
        }

        await InvokeSaveHooksAsync(http, before: false);
        await InvokeUpdateHooksAsync(http, context, before: false);

        return Results.Json(updated, JsonOptions);
    }

    public async Task<IResult> PatchAsync(HttpContext http)
    {
        if (!TryGetKey(http, out var key))
        {
            return ResourceErrors.InvalidKey(_info);
        }

        var dataSource = ResolveDataSource(http);
        var original = await dataSource.GetAsync(key!, http.RequestAborted);
        if (original is null)
        {
            return ResourceErrors.NotFound(_info);
        }

        var patch = await ReadPatchAsync(http);
        if (patch is null)
        {
            return ResourceErrors.InvalidBody();
        }

        ApplyPatch(original, patch);

        var validation = await ValidateAsync(http, original);
        if (validation is not null)
        {
            return validation;
        }

        var context = new UpdateContext<TResource>
        {
            HttpContext = http,
            ResourceInfo = _info,
            Resource = original,
            Original = original,
        };

        await InvokeUpdateHooksAsync(http, context, before: true);
        await InvokeSaveHooksAsync(http, before: true);

        if (_info.SoftDeleteEnabled)
        {
            _info.SetDeletedAt(original, null);
        }

        var updated = await dataSource.UpdateAsync(original, original, http.RequestAborted);
        if (updated is null)
        {
            return ResourceErrors.NotFound(_info);
        }

        await InvokeSaveHooksAsync(http, before: false);
        await InvokeUpdateHooksAsync(http, context, before: false);

        return Results.Json(updated, JsonOptions);
    }

    public async Task<IResult> DeleteAsync(HttpContext http)
    {
        if (!TryGetKey(http, out var key))
        {
            return ResourceErrors.InvalidKey(_info);
        }

        var dataSource = ResolveDataSource(http);
        var existing = await dataSource.GetAsync(key!, http.RequestAborted);
        if (existing is null)
        {
            return ResourceErrors.NotFound(_info);
        }

        var context = new DeleteContext<TResource> { HttpContext = http, ResourceInfo = _info, Resource = existing };

        await InvokeDeleteHooksAsync(http, context, before: true);
        await InvokeSaveHooksAsync(http, before: true);

        if (_info.SoftDeleteEnabled)
        {
            _info.SetDeletedAt(existing, DateTimeOffset.UtcNow);
            var updated = await dataSource.UpdateAsync(existing, existing, http.RequestAborted);
            if (updated is null)
            {
                return ResourceErrors.NotFound(_info);
            }
        }
        else
        {
            var removed = await dataSource.DeleteAsync(key!, http.RequestAborted);
            if (!removed)
            {
                return ResourceErrors.NotFound(_info);
            }
        }

        await InvokeSaveHooksAsync(http, before: false);
        await InvokeDeleteHooksAsync(http, context, before: false);

        return Results.NoContent();
    }

    private bool TryGetKey(HttpContext http, out object? key)
        => _info.TryConvertKey(http.Request.RouteValues["id"]?.ToString(), out key);

    private static ResourceQueryContext InjectSoftDeleteFilter(ResourceQueryContext query, ResourceInfo<TResource> info)
    {
        var deletedAtField = info.Options.SoftDelete.DeletedAtFieldName;
        var alreadyFiltered = query.Filters.Any(f =>
            string.Equals(f.PropertyName, deletedAtField, StringComparison.OrdinalIgnoreCase));

        if (alreadyFiltered)
        {
            return query;
        }

        var filters = new List<QueryFilter>(query.Filters)
        {
            new QueryFilter
            {
                PropertyName = deletedAtField,
                Operator = QueryFilterOperator.Equals,
                Value = null!
            }
        };

        return new ResourceQueryContext
        {
            Filters = filters,
            Sort = query.Sort,
            Search = query.Search,
            Page = query.Page,
            MaxResults = query.MaxResults,
            Fields = query.Fields,
            Embedded = query.Embedded
        };
    }

    private IResourceDataSource<TResource> ResolveDataSource(HttpContext http)
    {
        return http.RequestServices.GetService<IResourceDataSource<TResource>>()
            ?? throw new InvalidOperationException(
                $"No data source is registered for resource '{_info.Name}'. Register one with " +
                $"AddEfCore()/AddEfCoreResource<,>(), or ensure the resource's assembly is discovered by " +
                $"AddResources() so an in-memory data source is registered.");
    }

    private async Task<TResource?> ReadBodyAsync(HttpContext http)
    {
        try
        {
            return await http.Request.ReadFromJsonAsync<TResource>(JsonOptions, http.RequestAborted);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private async Task<JsonObject?> ReadPatchAsync(HttpContext http)
    {
        try
        {
            return await http.Request.ReadFromJsonAsync<JsonObject>(JsonOptions, http.RequestAborted);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private void ApplyPatch(TResource target, JsonObject patch)
    {
        var properties = typeof(TResource).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var namingPolicy = JsonOptions.PropertyNamingPolicy;
        foreach (var property in properties)
        {
            if (property == _info.KeyProperty || !property.CanWrite)
            {
                continue;
            }

            var wireName = namingPolicy?.ConvertName(property.Name) ?? property.Name;
            if (!patch.TryGetPropertyValue(wireName, out var node))
            {
                continue;
            }

            if (node is null)
            {
                if (Nullable.GetUnderlyingType(property.PropertyType) is null && property.PropertyType.IsValueType)
                {
                    continue;
                }

                property.SetValue(target, null);
                continue;
            }

            var value = node.Deserialize(property.PropertyType, JsonOptions);
            property.SetValue(target, value);
        }
    }

    private async Task<IResult?> ValidateAsync(HttpContext http, TResource resource)
    {
        var service = http.RequestServices.GetService<ResourceValidationService>();
        if (service is null)
        {
            return null;
        }

        var result = await service.ValidateAsync(http, resource, _info);
        return result.IsValid ? null : ResourceErrors.ValidationFailed(result);
    }

    private async Task InvokeCreateHooksAsync(HttpContext http, CreateContext<TResource> context, bool before)
    {
        var hooks = http.RequestServices.GetServices<IResourceHook<TResource>>();
        if (before)
        {
            if (_info.Options.BeforeCreate is { } handler) await handler(context);
            foreach (var hook in hooks) await hook.BeforeCreateAsync(context);
        }
        else
        {
            foreach (var hook in hooks) await hook.AfterCreateAsync(context);
            if (_info.Options.AfterCreate is { } handler) await handler(context);
        }
    }

    private async Task InvokeUpdateHooksAsync(HttpContext http, UpdateContext<TResource> context, bool before)
    {
        var hooks = http.RequestServices.GetServices<IResourceHook<TResource>>();
        if (before)
        {
            if (_info.Options.BeforeUpdate is { } handler) await handler(context);
            foreach (var hook in hooks) await hook.BeforeUpdateAsync(context);
        }
        else
        {
            foreach (var hook in hooks) await hook.AfterUpdateAsync(context);
            if (_info.Options.AfterUpdate is { } handler) await handler(context);
        }
    }

    private async Task InvokeDeleteHooksAsync(HttpContext http, DeleteContext<TResource> context, bool before)
    {
        var hooks = http.RequestServices.GetServices<IResourceHook<TResource>>();
        if (before)
        {
            if (_info.Options.BeforeDelete is { } handler) await handler(context);
            foreach (var hook in hooks) await hook.BeforeDeleteAsync(context);
        }
        else
        {
            foreach (var hook in hooks) await hook.AfterDeleteAsync(context);
            if (_info.Options.AfterDelete is { } handler) await handler(context);
        }
    }

    private async Task InvokeSaveHooksAsync(HttpContext http, bool before)
    {
        var context = new ResourceHookContext { HttpContext = http, ResourceInfo = _info };
        var hooks = http.RequestServices.GetServices<IResourceHook<TResource>>();
        if (before)
        {
            if (_info.Options.BeforeSave is { } handler) await handler(context);
            foreach (var hook in hooks) await hook.BeforeSaveAsync(context);
        }
        else
        {
            foreach (var hook in hooks) await hook.AfterSaveAsync(context);
            if (_info.Options.AfterSave is { } handler) await handler(context);
        }
    }

    private static JsonArray FilterJsonProperties(JsonElement element, IReadOnlyList<string> fields)
    {
        var result = new JsonArray();
        if (element.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var namingPolicy = JsonOptions.PropertyNamingPolicy;
        var fieldSet = new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);

        foreach (var item in element.EnumerateArray())
        {
            var obj = new JsonObject();
            foreach (var prop in item.EnumerateObject())
            {
                if (fieldSet.Contains(prop.Name))
                {
                    obj[prop.Name] = JsonNode.Parse(prop.Value.GetRawText());
                }
            }
            result.Add(obj);
        }

        return result;
    }
}
