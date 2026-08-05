using Crest;
using Crest.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crest.Tests;

internal static class TestHostHelper
{
    public static async Task<(WebApplication App, HttpClient Client)> CreateAsync(
        Action<IServiceCollection>? configureServices = null,
        Action<WebApplication>? map = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddResources(o => o.Assemblies.Add(typeof(TestHostHelper).Assembly));
        builder.Services.AddResourceValidation();

        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseExceptionHandler();
        if (map is not null)
        {
            map(app);
        }
        else
        {
            app.MapResources();
        }

        await app.StartAsync();
        var server = (TestServer)app.Services.GetRequiredService<IServer>();
        var client = server.CreateClient();
        return (app, client);
    }

    public static async Task<(ResourceInfo Device, InMemoryResourceDataSource<Device> Source)> CreateInMemorySourceAsync()
    {
        var services = new ServiceCollection();
        services.AddResources(o => o.Assemblies.Add(typeof(TestHostHelper).Assembly));
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ResourceRegistry>();
        var info = registry.Get<Device>();
        var source = new InMemoryResourceDataSource<Device>(registry);
        return (info, source);
    }
}
