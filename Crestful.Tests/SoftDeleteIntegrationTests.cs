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
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Thermostat" }, TestContext.Current.CancellationToken);
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);
            Assert.NotNull(created);

            var deleteResponse = await client.DeleteAsync($"/api/softdeletedevices/{created.Id}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getResponse = await client.GetAsync($"/api/softdeletedevices/{created.Id}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

            var listResponse = await client.GetAsync("/api/softdeletedevices", TestContext.Current.CancellationToken);
            var items = await listResponse.Content.ReadFromJsonAsync<List<SoftDeleteDevice>>(TestContext.Current.CancellationToken);
            Assert.NotNull(items);
            Assert.Empty(items!);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task List_excludes_soft_deleted_items()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Device A" }, TestContext.Current.CancellationToken);
            var post2 = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Device B" }, TestContext.Current.CancellationToken);
            var deviceB = await post2.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Device C" }, TestContext.Current.CancellationToken);

            await client.DeleteAsync($"/api/softdeletedevices/{deviceB!.Id}", TestContext.Current.CancellationToken);

            var listResponse = await client.GetAsync("/api/softdeletedevices", TestContext.Current.CancellationToken);
            var items = await listResponse.Content.ReadFromJsonAsync<List<SoftDeleteDevice>>(TestContext.Current.CancellationToken);
            Assert.NotNull(items);
            Assert.Equal(2, items!.Count);
            Assert.All(items, d => Assert.Null(d.DeletedAt));
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_soft_deleted_item()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Doomed" }, TestContext.Current.CancellationToken);
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);

            await client.DeleteAsync($"/api/softdeletedevices/{created!.Id}", TestContext.Current.CancellationToken);

            var response = await client.GetAsync($"/api/softdeletedevices/{created.Id}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task PUT_restores_soft_deleted_item()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Phoenix" }, TestContext.Current.CancellationToken);
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);

            await client.DeleteAsync($"/api/softdeletedevices/{created!.Id}", TestContext.Current.CancellationToken);

            var putResponse = await client.PutAsJsonAsync($"/api/softdeletedevices/{created.Id}",
                new { name = "Phoenix Restored", model = "R1" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

            var restored = await putResponse.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);
            Assert.NotNull(restored);
            Assert.Null(restored!.DeletedAt);
            Assert.Equal("Phoenix Restored", restored.Name);

            var getResponse = await client.GetAsync($"/api/softdeletedevices/{created.Id}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task PATCH_restores_soft_deleted_item()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Rising" }, TestContext.Current.CancellationToken);
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);

            await client.DeleteAsync($"/api/softdeletedevices/{created!.Id}", TestContext.Current.CancellationToken);

            var patchResponse = await client.PatchAsJsonAsync($"/api/softdeletedevices/{created.Id}",
                new { name = "Rising Again" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

            var restored = await patchResponse.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);
            Assert.NotNull(restored);
            Assert.Null(restored!.DeletedAt);
            Assert.Equal("Rising Again", restored.Name);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task POST_works_normally_with_soft_delete_enabled()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            var response = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "New" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var created = await response.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);
            Assert.NotNull(created);
            Assert.Null(created!.DeletedAt);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Soft_deleted_item_excluded_from_where_filter()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Target" }, TestContext.Current.CancellationToken);
            var post2 = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Target" }, TestContext.Current.CancellationToken);
            var device2 = await post2.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);

            await client.DeleteAsync($"/api/softdeletedevices/{device2!.Id}", TestContext.Current.CancellationToken);

            var response = await client.GetAsync("/api/softdeletedevices?where={\"Name\":\"Target\"}", TestContext.Current.CancellationToken);
            var items = await response.Content.ReadFromJsonAsync<List<SoftDeleteDevice>>(TestContext.Current.CancellationToken);
            Assert.NotNull(items);
            Assert.Single(items!);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Soft_deleted_item_excluded_from_search()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "UniqueAlpha" }, TestContext.Current.CancellationToken);
            var post2 = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "UniqueBeta" }, TestContext.Current.CancellationToken);
            var device2 = await post2.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);

            await client.DeleteAsync($"/api/softdeletedevices/{device2!.Id}", TestContext.Current.CancellationToken);

            var response = await client.GetAsync("/api/softdeletedevices?search=Unique", TestContext.Current.CancellationToken);
            var items = await response.Content.ReadFromJsonAsync<List<SoftDeleteDevice>>(TestContext.Current.CancellationToken);
            Assert.NotNull(items);
            Assert.Single(items!);
            Assert.Equal("UniqueAlpha", items![0].Name);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ISoftDeletable_resource_without_soft_delete_enabled_does_hard_delete()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = false));
        try
        {
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "HardDelete" }, TestContext.Current.CancellationToken);
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);

            var deleteResponse = await client.DeleteAsync($"/api/softdeletedevices/{created!.Id}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getResponse = await client.GetAsync($"/api/softdeletedevices/{created.Id}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Multiple_soft_deletes_set_correct_timestamps()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: app => app.MapResource<SoftDeleteDevice>(o => o.SoftDelete.Enabled = true));
        try
        {
            var post = await client.PostAsJsonAsync("/api/softdeletedevices", new { name = "Multi" }, TestContext.Current.CancellationToken);
            var created = await post.Content.ReadFromJsonAsync<SoftDeleteDevice>(TestContext.Current.CancellationToken);

            var beforeDelete = DateTimeOffset.UtcNow;

            await client.DeleteAsync($"/api/softdeletedevices/{created!.Id}", TestContext.Current.CancellationToken);

            var listResponse = await client.GetAsync("/api/softdeletedevices", TestContext.Current.CancellationToken);
            var items = await listResponse.Content.ReadFromJsonAsync<List<SoftDeleteDevice>>(TestContext.Current.CancellationToken);
            Assert.NotNull(items);
            Assert.Empty(items!);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
