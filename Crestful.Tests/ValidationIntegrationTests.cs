using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Crestful.Tests;

public class ValidationIntegrationTests
{
    [Fact]
    public async Task Data_annotations_violation_returns_400_with_errors()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { model = "T100" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
            Assert.Equal("application/problem+json", create.Content.Headers.ContentType?.MediaType);

            var problem = await create.Content.ReadFromJsonAsync<ValidationProblemJson>(TestContext.Current.CancellationToken);
            Assert.NotNull(problem);
            Assert.Contains(problem!.Errors, e => e.Key == "Name");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task FluentValidation_violation_returns_400_with_errors()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            var create = await client.PostAsJsonAsync("/api/orders", new { reference = "TOO-LONG" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

            var problem = await create.Content.ReadFromJsonAsync<ValidationProblemJson>(TestContext.Current.CancellationToken);
            Assert.NotNull(problem);
            Assert.Contains(problem!.Errors, e => e.Key == "Reference");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Valid_resources_pass_validation()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            var create = await client.PostAsJsonAsync("/api/orders", new { reference = "AB" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Patch_is_validated()
    {
        var (app, client) = await TestHostHelper.CreateAsync();
        try
        {
            var create = await client.PostAsJsonAsync("/api/orders", new { reference = "AB" }, TestContext.Current.CancellationToken);
            var created = await create.Content.ReadFromJsonAsync<Order>(TestContext.Current.CancellationToken);

            var patch = await client.PatchAsJsonAsync($"/api/orders/{created!.Id}", new { reference = "WAY-TOO-LONG" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, patch.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private sealed class ValidationProblemJson
    {
        public Dictionary<string, string[]> Errors { get; set; } = new();
    }
}
