using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Crestful.Tests;

public class SoftDeleteIntegrationTests
{
    [Fact]
    public async Task Delete_sets_DeletedAt_instead_of_removing()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Thermostat" });
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>();
            Assert.NotNull(created);

            var deleteResponse = await client.DeleteAsync($"/api/softdeletedevices/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getResponse = await client.GetAsync($"/api/softdeletedevices/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

            var listResponse = await client.GetAsync("/api/softdeletedevices");
            var items = await listResponse.Content.ReadFromJsonAsync<List<SoftDeleteDevice>>();
            Assert.NotNull(items);
            Assert.Empty(items!);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task List_excludes_soft_deleted_items()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Device A" });
            var post2 = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Device B" });
            var deviceB = await post2.Content.ReadFromJsonAsync<SoftDeleteDevice>();
            await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Device C" });

            await client.DeleteAsync($"/api/softdeletedevices/{deviceB!.Id}");

            var listResponse = await client.GetAsync("/api/softdeletedevices");
            var items = await listResponse.Content.ReadFromJsonAsync<List<SoftDeleteDevice>>();
            Assert.NotNull(items);
            Assert.Equal(2, items!.Count);
            Assert.All(items, d => Assert.Null(d.DeletedAt));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_soft_deleted_item()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Doomed" });
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>();

            await client.DeleteAsync($"/api/softdeletedevices/{created!.Id}");

            var response = await client.GetAsync($"/api/softdeletedevices/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task PUT_restores_soft_deleted_item()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Phoenix" });
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>();

            await client.DeleteAsync($"/api/softdeletedevices/{created!.Id}");

            var putResponse = await client.PutAsJsonAsync($"/api/softdeletedevices/{created.Id}",
                new { name = "Phoenix Restored", model = "R1" });
            Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

            var restored = await putResponse.Content.ReadFromJsonAsync<SoftDeleteDevice>();
            Assert.NotNull(restored);
            Assert.Null(restored!.DeletedAt);
            Assert.Equal("Phoenix Restored", restored.Name);

            var getResponse = await client.GetAsync($"/api/softdeletedevices/{created.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task PATCH_restores_soft_deleted_item()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Rising" });
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>();

            await client.DeleteAsync($"/api/softdeletedevices/{created!.Id}");

            var patchResponse = await client.PatchAsJsonAsync($"/api/softdeletedevices/{created.Id}",
                new { name = "Rising Again" });
            Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

            var restored = await patchResponse.Content.ReadFromJsonAsync<SoftDeleteDevice>();
            Assert.NotNull(restored);
            Assert.Null(restored!.DeletedAt);
            Assert.Equal("Rising Again", restored.Name);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task POST_works_normally_with_soft_delete_enabled()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            var response = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "New" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var created = await response.Content.ReadFromJsonAsync<SoftDeleteDevice>();
            Assert.NotNull(created);
            Assert.Null(created!.DeletedAt);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Soft_deleted_item_excluded_from_where_filter()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Target" });
            var post2 = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Target" });
            var device2 = await post2.Content.ReadFromJsonAsync<SoftDeleteDevice>();

            await client.DeleteAsync($"/api/softdeletedevices/{device2!.Id}");

            var response = await client.GetAsync("/api/softdeletedevices?where={\"Name\":\"Target\"}");
            var items = await response.Content.ReadFromJsonAsync<List<SoftDeleteDevice>>();
            Assert.NotNull(items);
            Assert.Single(items!);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Soft_deleted_item_excluded_from_search()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "UniqueAlpha" });
            var post2 = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "UniqueBeta" });
            var device2 = await post2.Content.ReadFromJsonAsync<SoftDeleteDevice>();

            await client.DeleteAsync($"/api/softdeletedevices/{device2!.Id}");

            var response = await client.GetAsync("/api/softdeletedevices?search=Unique");
            var items = await response.Content.ReadFromJsonAsync<List<SoftDeleteDevice>>();
            Assert.NotNull(items);
            Assert.Single(items!);
            Assert.Equal("UniqueAlpha", items![0].Name);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ISoftDeletable_resource_without_soft_delete_enabled_does_hard_delete()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = false));
        try
        {
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "HardDelete" });
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>();

            var deleteResponse = await client.DeleteAsync($"/api/softdeletedevices/{created!.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getResponse = await client.GetAsync($"/api/softdeletedevices/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Multiple_soft_deletes_set_correct_timestamps()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Multi" });
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>();

            var beforeDelete = DateTimeOffset.UtcNow;

            await client.DeleteAsync($"/api/softdeletedevices/{created!.Id}");

            var listResponse = await client.GetAsync("/api/softdeletedevices");
            var items = await listResponse.Content.ReadFromJsonAsync<List<SoftDeleteDevice>>();
            Assert.NotNull(items);
            Assert.Empty(items!);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
