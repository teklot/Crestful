using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Crest.Tests;

public class ErrorHandlingIntegrationTests
{
    [Fact]
    public async Task Missing_resource_returns_404_problem_details()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            var response = await client.GetAsync("/api/devices/424242");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal(404, problem!.Status);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Invalid_key_returns_400_problem_details()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            var response = await client.GetAsync("/api/devices/not-an-int");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal(400, problem!.Status);
            Assert.Contains("key", problem.Detail, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Malformed_body_returns_400_problem_details()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            using var content = new StringContent("{ this is not json", System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/devices", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(400, problem!.Status);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Duplicate_key_on_create_returns_409()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            var first = await client.PostAsJsonAsync("/api/devices", new { id = 1, name = "One" });
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            var second = await client.PostAsJsonAsync("/api/devices", new { id = 1, name = "Two" });
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

            var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.Equal(409, problem!.Status);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Update_and_delete_missing_resource_return_404()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            var put = await client.PutAsJsonAsync("/api/devices/999", new { id = 999, name = "X" });
            Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

            var patch = await client.PatchAsJsonAsync("/api/devices/999", new { name = "X" });
            Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);

            var delete = await client.DeleteAsync("/api/devices/999");
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
