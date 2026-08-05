using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crest.Tests;

public class HooksIntegrationTests
{
    [Fact]
    public async Task Create_hooks_run_in_documented_order()
    {
        var log = new List<string>();
        var (app, client) = await TestHostHelper.CreateAsync(
            map: a => a.MapResource<Device>(o =>
            {
                o.BeforeCreate = ctx => Add(log, "options:before-create");
                o.AfterCreate = ctx => Add(log, "options:after-create");
                o.BeforeSave = ctx => Add(log, "options:before-save");
                o.AfterSave = ctx => Add(log, "options:after-save");
            }));
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { name = "X" });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);

            Assert.Equal(
                new[] { "options:before-create", "options:before-save", "options:after-save", "options:after-create" },
                log);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Di_hooks_run_after_options_hooks()
    {
        var log = new List<string>();
        var (app, client) = await TestHostHelper.CreateAsync(
            configureServices: s =>
            {
                s.AddSingleton(log);
                s.AddScoped<IResourceHook<Device>, DeviceHook>();
            },
            map: a => a.MapResource<Device>(o =>
            {
                o.BeforeCreate = ctx => Add(log, "options:before-create");
                o.AfterCreate = ctx => Add(log, "options:after-create");
            }));
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { name = "X" });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);

            Assert.Equal(
                new[] { "options:before-create", "di:before-create", "di:before-save", "di:after-save", "di:after-create", "options:after-create" },
                log);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Update_and_delete_hooks_fire()
    {
        var log = new List<string>();
        var (app, client) = await TestHostHelper.CreateAsync(
            map: a => a.MapResource<Device>(o =>
            {
                o.BeforeUpdate = ctx => Add(log, "before-update");
                o.AfterUpdate = ctx => Add(log, "after-update");
                o.BeforeDelete = ctx => Add(log, "before-delete");
                o.AfterDelete = ctx => Add(log, "after-delete");
            }));
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { name = "X" });
            var created = await create.Content.ReadFromJsonAsync<Device>();

            await client.PutAsJsonAsync($"/api/devices/{created!.Id}", new { id = created.Id, name = "Y" });
            Assert.Equal(new[] { "before-update", "after-update" }, log);
            log.Clear();

            await client.DeleteAsync($"/api/devices/{created.Id}");
            Assert.Equal(new[] { "before-delete", "after-delete" }, log);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Hook_can_modify_the_resource_before_create()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: a => a.MapResource<Device>(o =>
            {
                o.BeforeCreate = ctx =>
                {
                    ctx.Resource.Name = "Overridden";
                    return Task.CompletedTask;
                };
            }));
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { name = "Original" });
            var created = await create.Content.ReadFromJsonAsync<Device>();
            Assert.Equal("Overridden", created!.Name);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Hook_exceptions_become_500_problem_details()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            configureServices: s => s.AddScoped<IResourceHook<Device>, ThrowingHook>());
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { name = "X" });
            Assert.Equal(HttpStatusCode.InternalServerError, create.StatusCode);
            Assert.Equal("application/problem+json", create.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static Task Add(List<string> log, string value)
    {
        log.Add(value);
        return Task.CompletedTask;
    }
}
