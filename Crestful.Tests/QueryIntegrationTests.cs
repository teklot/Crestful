using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Crestful.Tests;

public class QueryIntegrationTests
{
    [Fact]
    public async Task Where_filter_returns_matching_items()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            await client.PostAsJsonAsync("/api/devices", new { name = "Thermostat", model = "T100", quantity = 3 }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "Sensor", model = "S200", quantity = 5 }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "Thermostat Pro", model = "T300", quantity = 7 }, TestContext.Current.CancellationToken);

            var response = await client.GetAsync("/api/devices?where={\"Name\":\"Sensor\"}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var devices = await response.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices);
            Assert.Single(devices!);
            Assert.Equal("Sensor", devices[0].Name);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Sort_ascending_returns_items_in_order()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            await client.PostAsJsonAsync("/api/devices", new { name = "Charlie", model = "C", quantity = 30 }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "Alpha", model = "A", quantity = 10 }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "Bravo", model = "B", quantity = 20 }, TestContext.Current.CancellationToken);

            var response = await client.GetAsync("/api/devices?sort=Name", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var devices = await response.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices);
            Assert.Equal(3, devices!.Count);
            Assert.Equal("Alpha", devices[0].Name);
            Assert.Equal("Bravo", devices[1].Name);
            Assert.Equal("Charlie", devices[2].Name);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Sort_descending_returns_items_in_reverse_order()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            await client.PostAsJsonAsync("/api/devices", new { name = "Alpha", model = "A", quantity = 10 }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "Bravo", model = "B", quantity = 20 }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "Charlie", model = "C", quantity = 30 }, TestContext.Current.CancellationToken);

            var response = await client.GetAsync("/api/devices?sort=-Quantity", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var devices = await response.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices);
            Assert.Equal(3, devices!.Count);
            Assert.Equal(30, devices[0].Quantity);
            Assert.Equal(20, devices[1].Quantity);
            Assert.Equal(10, devices[2].Quantity);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Pagination_returns_correct_page()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            for (var i = 0; i < 5; i++)
            {
                await client.PostAsJsonAsync("/api/devices", new { name = $"Device{i}", model = $"M{i}", quantity = i * 10 }, TestContext.Current.CancellationToken);
            }

            var page1 = await client.GetAsync("/api/devices?page=1&max_results=2", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page1.StatusCode);
            var devices1 = await page1.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices1);
            Assert.Equal(2, devices1!.Count);

            var page2 = await client.GetAsync("/api/devices?page=2&max_results=2", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page2.StatusCode);
            var devices2 = await page2.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices2);
            Assert.Equal(2, devices2!.Count);

            var page3 = await client.GetAsync("/api/devices?page=3&max_results=2", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, page3.StatusCode);
            var devices3 = await page3.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices3);
            Assert.Single(devices3!);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Field_selection_returns_only_specified_fields()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            await client.PostAsJsonAsync("/api/devices", new { name = "Thermostat", model = "T100", quantity = 3 }, TestContext.Current.CancellationToken);

            var response = await client.GetAsync("/api/devices?field=Name,Model", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var doc = JsonDocument.Parse(json);
            var items = doc.RootElement;

            Assert.Equal(JsonValueKind.Array, items.ValueKind);
            var first = items[0];

            Assert.True(first.TryGetProperty("name", out _));
            Assert.True(first.TryGetProperty("model", out _));
            Assert.False(first.TryGetProperty("quantity", out _));
            Assert.False(first.TryGetProperty("id", out _));
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Search_finds_matches_in_string_properties()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            await client.PostAsJsonAsync("/api/devices", new { name = "Temperature Sensor", model = "T100" }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "Humidity Sensor", model = "H200" }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "Power Meter", model = "P300" }, TestContext.Current.CancellationToken);

            var response = await client.GetAsync("/api/devices?search=Temperature", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var devices = await response.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices);
            Assert.Single(devices!);
            Assert.Equal("Temperature Sensor", devices[0].Name);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Combined_where_and_sort_works_together()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            await client.PostAsJsonAsync("/api/devices", new { name = "Alpha", model = "A", quantity = 30 }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "Beta", model = "B", quantity = 10 }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "Gamma", model = "A", quantity = 20 }, TestContext.Current.CancellationToken);

            var response = await client.GetAsync("/api/devices?where={\"Model\":\"A\"}&sort=-Quantity", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var devices = await response.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices);
            Assert.Equal(2, devices!.Count);
            Assert.Equal("Alpha", devices[0].Name);
            Assert.Equal(30, devices[0].Quantity);
            Assert.Equal("Gamma", devices[1].Name);
            Assert.Equal(20, devices[1].Quantity);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Empty_where_returns_all_items()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            await client.PostAsJsonAsync("/api/devices", new { name = "A", model = "A" }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "B", model = "B" }, TestContext.Current.CancellationToken);

            var response = await client.GetAsync("/api/devices", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var devices = await response.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices);
            Assert.Equal(2, devices!.Count);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Invalid_where_json_returns_empty_results()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            await client.PostAsJsonAsync("/api/devices", new { name = "A", model = "A" }, TestContext.Current.CancellationToken);

            var response = await client.GetAsync("/api/devices?where=not-json", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var devices = await response.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices);
            Assert.Single(devices!);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Max_results_is_capped()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: a => a.MapResource<Device>(o =>
            {
                o.Query.MaxPageSize = 3;
            }));
        try
        {
            for (var i = 0; i < 10; i++)
            {
                await client.PostAsJsonAsync("/api/devices", new { name = $"Device{i}", model = $"M{i}" }, TestContext.Current.CancellationToken);
            }

            var response = await client.GetAsync("/api/devices?max_results=100", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var devices = await response.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices);
            Assert.Equal(3, devices!.Count);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Filtering_by_inactive_status()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            await client.PostAsJsonAsync("/api/devices", new { name = "Active", model = "A", isActive = true }, TestContext.Current.CancellationToken);
            await client.PostAsJsonAsync("/api/devices", new { name = "Inactive", model = "B", isActive = false }, TestContext.Current.CancellationToken);

            var response = await client.GetAsync("/api/devices?where={\"IsActive\":false}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var devices = await response.Content.ReadFromJsonAsync<List<Device>>(TestContext.Current.CancellationToken);
            Assert.NotNull(devices);
            Assert.Single(devices!);
            Assert.Equal("Inactive", devices[0].Name);
            Assert.False(devices[0].IsActive);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
