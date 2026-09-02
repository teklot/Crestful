using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Crestful.Tests;

public sealed class TestAuthHandlerOptions : AuthenticationSchemeOptions
{
    public string? UserName { get; set; }
}

public sealed class TestAuthHandler : AuthenticationHandler<TestAuthHandlerOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<TestAuthHandlerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] { new Claim(ClaimTypes.Name, Options.UserName ?? "") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class AuditingIntegrationTests
{
    private static async Task<(HttpClient Client, Func<Task> Cleanup)> CreateAuditedHostAsync(string? userName = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddResources(o =>
        {
            o.Assemblies.Add(typeof(AuditingIntegrationTests).Assembly);
            o.DefaultResourceOptions = opts =>
            {
                opts.Auditing.Enabled = true;
            };
        });

        if (userName is not null)
        {
            builder.Services.AddAuthentication("Test")
                .AddScheme<TestAuthHandlerOptions, TestAuthHandler>("Test", o => o.UserName = userName);
        }

        var app = builder.Build();
        app.UseExceptionHandler();
        if (userName is not null)
        {
            app.UseAuthentication();
        }
        app.MapResources();

        await app.StartAsync();
        var server = (TestServer)app.Services.GetRequiredService<IServer>();
        var client = server.CreateClient();

        if (userName is not null)
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        }

        return (client, () => app.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Create_SetsCreatedAtAndUpdatedAt()
    {
        var (client, cleanup) = await CreateAuditedHostAsync();
        try
        {
            var response = await client.PostAsJsonAsync("/api/auditeddevices", new { Name = "Sensor" }, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            var createdAt = json.GetProperty("createdAt").GetDateTimeOffset();
            var updatedAt = json.GetProperty("updatedAt").GetDateTimeOffset();

            Assert.True(createdAt > DateTimeOffset.MinValue);
            Assert.Equal(createdAt, updatedAt);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Create_SetsCreatedByAndUpdatedBy()
    {
        var (client, cleanup) = await CreateAuditedHostAsync("test-user");
        try
        {
            var response = await client.PostAsJsonAsync("/api/auditeddevices", new { Name = "Sensor" }, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            var createdBy = json.GetProperty("createdBy").GetString();
            var updatedBy = json.GetProperty("updatedBy").GetString();

            Assert.Equal("test-user", createdBy);
            Assert.Equal("test-user", updatedBy);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Create_AnonymousUser_SetsNullCreatedBy()
    {
        var (client, cleanup) = await CreateAuditedHostAsync(null);
        try
        {
            var response = await client.PostAsJsonAsync("/api/auditeddevices", new { Name = "Sensor" }, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            var createdBy = json.GetProperty("createdBy");

            Assert.Equal(JsonValueKind.Null, createdBy.ValueKind);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Update_SetsUpdatedAtAndUpdatedBy()
    {
        var (client, cleanup) = await CreateAuditedHostAsync("test-user");
        try
        {
            var createResponse = await client.PostAsJsonAsync("/api/auditeddevices", new { Name = "Sensor" }, TestContext.Current.CancellationToken);
            createResponse.EnsureSuccessStatusCode();
            var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            var id = created.GetProperty("id").GetInt32();
            var originalCreatedAt = created.GetProperty("createdAt").GetDateTimeOffset();

            await Task.Delay(10, TestContext.Current.CancellationToken);

            var updateResponse = await client.PutAsJsonAsync($"/api/auditeddevices/{id}", new { Id = id, Name = "Updated Sensor" }, TestContext.Current.CancellationToken);
            updateResponse.EnsureSuccessStatusCode();
            var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            var updatedAt = updated.GetProperty("updatedAt").GetDateTimeOffset();
            var updatedBy = updated.GetProperty("updatedBy").GetString();

            Assert.Equal(originalCreatedAt, updated.GetProperty("createdAt").GetDateTimeOffset());
            Assert.True(updatedAt > originalCreatedAt);
            Assert.Equal("test-user", updatedBy);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Patch_SetsUpdatedAtAndUpdatedBy()
    {
        var (client, cleanup) = await CreateAuditedHostAsync("test-user");
        try
        {
            var createResponse = await client.PostAsJsonAsync("/api/auditeddevices", new { Name = "Sensor" }, TestContext.Current.CancellationToken);
            createResponse.EnsureSuccessStatusCode();
            var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            var id = created.GetProperty("id").GetInt32();
            var originalCreatedAt = created.GetProperty("createdAt").GetDateTimeOffset();

            await Task.Delay(10, TestContext.Current.CancellationToken);

            var patchResponse = await client.PatchAsJsonAsync($"/api/auditeddevices/{id}", new { Name = "Patched Sensor" }, TestContext.Current.CancellationToken);
            patchResponse.EnsureSuccessStatusCode();
            var patched = await patchResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            var updatedAt = patched.GetProperty("updatedAt").GetDateTimeOffset();
            var updatedBy = patched.GetProperty("updatedBy").GetString();

            Assert.Equal(originalCreatedAt, patched.GetProperty("createdAt").GetDateTimeOffset());
            Assert.True(updatedAt > originalCreatedAt);
            Assert.Equal("test-user", updatedBy);
        }
        finally
        {
            await cleanup();
        }
    }

    [Fact]
    public async Task Create_DoesNotOverrideExistingValues()
    {
        var (client, cleanup) = await CreateAuditedHostAsync();
        try
        {
            var futureTime = DateTimeOffset.UtcNow.AddHours(1);
            var response = await client.PostAsJsonAsync("/api/auditeddevices", new
            {
                Name = "Sensor",
                CreatedAt = futureTime,
                UpdatedAt = futureTime
            }, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            var createdAt = json.GetProperty("createdAt").GetDateTimeOffset();

            Assert.NotEqual(futureTime, createdAt);
        }
        finally
        {
            await cleanup();
        }
    }
}