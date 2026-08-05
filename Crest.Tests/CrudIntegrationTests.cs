using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Crest.Tests;

public class CrudIntegrationTests
{
    [Fact]
    public async Task Full_crud_cycle_over_http()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { name = "Thermostat", model = "T100", quantity = 3 });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            Assert.Equal("application/json", create.Content.Headers.ContentType?.MediaType);

            var created = await create.Content.ReadFromJsonAsync<Device>();
            Assert.NotNull(created);
            Assert.Equal("Thermostat", created!.Name);
            Assert.True(created.Id > 0);
            Assert.Equal(3, created.Quantity);

            var list = await client.GetAsync("/api/devices");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var devices = await list.Content.ReadFromJsonAsync<List<Device>>();
            Assert.Contains(devices!, d => d.Id == created.Id);

            var detail = await client.GetAsync($"/api/devices/{created.Id}");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            var fetched = await detail.Content.ReadFromJsonAsync<Device>();
            Assert.Equal("Thermostat", fetched!.Name);

            var put = await client.PutAsJsonAsync($"/api/devices/{created.Id}", new { id = created.Id, name = "Updated", model = "T200", quantity = 5 });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var updated = await put.Content.ReadFromJsonAsync<Device>();
            Assert.Equal("Updated", updated!.Name);
            Assert.Equal("T200", updated.Model);
            Assert.Equal(5, updated.Quantity);

            var patch = await client.PatchAsJsonAsync($"/api/devices/{created.Id}", new { model = "T300", isActive = false });
            Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
            var patched = await patch.Content.ReadFromJsonAsync<Device>();
            Assert.Equal("T300", patched!.Model);
            Assert.False(patched.IsActive);
            Assert.Equal("Updated", patched.Name);

            var delete = await client.DeleteAsync($"/api/devices/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

            var after = await client.GetAsync($"/api/devices/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Guid_keyed_resource_works()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            var create = await client.PostAsJsonAsync("/api/guidresources", new { label = "Alpha" });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<GuidResource>();
            Assert.NotNull(created);
            Assert.NotEqual(Guid.Empty, created!.Id);

            var detail = await client.GetAsync($"/api/guidresources/{created.Id}");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            var fetched = await detail.Content.ReadFromJsonAsync<GuidResource>();
            Assert.Equal("Alpha", fetched!.Label);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Location_header_points_to_the_new_resource()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            var create = await client.PostAsJsonAsync("/api/guidresources", new { label = "Alpha" });
            var created = await create.Content.ReadFromJsonAsync<GuidResource>();

            Assert.NotNull(create.Headers.Location);
            Assert.Equal($"/api/guidresources/{created!.Id}", create.Headers.Location!.OriginalString);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Route_name_override_is_honored()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: a => a.MapResource<Device>(o => o.Name = "machines"));
        try
        {
            var create = await client.PostAsJsonAsync("/api/machines", new { name = "Robot" });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);

            var list = await client.GetAsync("/api/machines");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);

            var oldRoute = await client.GetAsync("/api/devices");
            Assert.Equal(HttpStatusCode.NotFound, oldRoute.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Mapping_a_resource_twice_throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestHostHelper.CreateAsync(map: a =>
        {
            a.MapResource<Device>();
            a.MapResources();
        }));
        Assert.Contains("already been mapped", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Disabled_operations_are_not_mapped()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: a => a.MapResource<Device>(o => o.DeleteEnabled = false));
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { name = "X" });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<Device>();

            var del = await client.DeleteAsync($"/api/devices/{created!.Id}");
            Assert.Equal(HttpStatusCode.MethodNotAllowed, del.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
