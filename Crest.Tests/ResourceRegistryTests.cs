using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crest.Tests;

public class ResourceRegistryTests
{
    [Fact]
    public void Pluralize_follows_english_conventions()
    {
        Assert.Equal("Devices", Pluralizer.Pluralize("Device"));
        Assert.Equal("Categories", Pluralizer.Pluralize("Category"));
        Assert.Equal("Boxes", Pluralizer.Pluralize("Box"));
        Assert.Equal("Statuses", Pluralizer.Pluralize("Status"));
        Assert.Equal("Guids", Pluralizer.Pluralize("Guid"));
    }

    [Fact]
    public void Discovered_resources_are_registered_with_routes()
    {
        var services = new ServiceCollection();
        services.AddResources(o => o.Assemblies.Add(typeof(Device).Assembly));

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ResourceRegistry>();

        var device = registry.Get<Device>();
        Assert.Equal("devices", device.Name);
        Assert.Equal("/api/devices", device.RoutePattern);
        Assert.Equal(typeof(int), device.KeyType);

        var guid = registry.Get<GuidResource>();
        Assert.Equal("/api/guidresources", guid.RoutePattern);
        Assert.Equal(typeof(Guid), guid.KeyType);

        Assert.Contains(registry.Resources, r => r.ResourceType == typeof(Order));
    }

    [Fact]
    public void TypeNameId_convention_is_supported()
    {
        var services = new ServiceCollection();
        services.AddResources(o => o.Assemblies.Add(typeof(Device).Assembly));

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ResourceRegistry>();
        var resource = registry.Get<DeviceIdResource>();
        Assert.Equal("DeviceId", resource.KeyProperty.Name);
    }

    [Fact]
    public void Missing_key_property_throws()
    {
        var services = new ServiceCollection();
        services.AddResources(o => o.Assemblies.Add(typeof(Device).Assembly));

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ResourceRegistry>();

        var info = registry.Get(typeof(NoKeyResource));
        var ex = Assert.Throws<InvalidOperationException>(() => _ = info.KeyProperty);
        Assert.Contains("key property", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Default_resource_options_apply_to_discovered_resources()
    {
        var services = new ServiceCollection();
        services.AddResources(o =>
        {
            o.Assemblies.Add(typeof(Device).Assembly);
            o.DefaultRoutePrefix = "";
            o.DefaultResourceOptions = opts => opts.DeleteEnabled = false;
        });

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ResourceRegistry>();

        var device = registry.Get<Device>();
        Assert.Equal("/devices", device.RoutePattern);
        Assert.False(device.Options.DeleteEnabled);
    }

    [Fact]
    public void MapResource_of_unknown_type_creates_info_and_custom_source_works()
    {
        var services = new ServiceCollection();
        services.AddResources(o => o.Assemblies.Add(typeof(Device).Assembly));
        services.AddSingleton<IResourceDataSource<GuidResource>, InMemoryResourceDataSource<GuidResource>>();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ResourceRegistry>();

        var info = registry.GetOrAdd<GuidResource>();
        Assert.Equal("/api/guidresources", info.RoutePattern);
        Assert.NotNull(provider.GetRequiredService<IResourceDataSource<GuidResource>>());
    }
}
