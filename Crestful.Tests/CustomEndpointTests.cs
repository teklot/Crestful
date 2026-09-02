using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Crestful.Tests;

public class CustomEndpointTests
{
    [Fact]
    public async Task Custom_handler_is_grouped_under_the_resource_route()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: a => a.MapResource<Device>().MapGet("/count", () => Results.Ok(7)));
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { name = "X" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);

            var response = await client.GetAsync("/api/devices/count", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("7", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Custom_handler_can_bind_from_the_resource_group()
    {
        var (app, client) = await TestHostHelper.CreateAsync(
            map: a => a.MapResource<Device>().MapGet("/echo/{value}", (string value) => Results.Text(value)));
        try
        {
            var response = await client.GetAsync("/api/devices/echo/hello", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("hello", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
