using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crestful.Tests;

public class InMemoryDataSourceTests
{
    private static InMemoryResourceDataSource<T> Create<T>() where T : class, IResource
    {
        var services = new ServiceCollection();
        services.AddResources(o => o.Assemblies.Add(typeof(Device).Assembly));
        using var provider = services.BuildServiceProvider();
        return new InMemoryResourceDataSource<T>(provider.GetRequiredService<ResourceRegistry>());
    }

    [Fact]
    public async Task Create_assigns_auto_increment_key()
    {
        var source = Create<Device>();
        var first = await source.CreateAsync(new Device { Name = "One" }, CancellationToken.None);
        var second = await source.CreateAsync(new Device { Name = "Two" }, CancellationToken.None);

        Assert.True(first.Id > 0);
        Assert.Equal(first.Id + 1, second.Id);
        Assert.Equal(2, source.Count);
    }

    [Fact]
    public async Task Create_with_explicit_key_conflicts()
    {
        var source = Create<Device>();
        await source.CreateAsync(new Device { Id = 42, Name = "One" }, CancellationToken.None);

        await Assert.ThrowsAsync<ResourceConflictException>(
            () => source.CreateAsync(new Device { Id = 42, Name = "Two" }, CancellationToken.None));
    }

    [Fact]
    public async Task Guid_keys_are_generated_when_empty()
    {
        var source = Create<GuidResource>();
        var created = await source.CreateAsync(new GuidResource { Label = "X" }, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, created.Id);
    }

    [Fact]
    public async Task String_keys_are_generated_when_empty()
    {
        var source = Create<StringKeyResource>();
        var created = await source.CreateAsync(new StringKeyResource { Label = "X" }, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(created.Code));
    }

    [Fact]
    public async Task Update_applies_scalar_values_and_preserves_key()
    {
        var source = Create<Device>();
        var created = await source.CreateAsync(new Device { Name = "Before", Model = "M1", Quantity = 1 }, CancellationToken.None);
        var original = await source.GetAsync(created.Id, CancellationToken.None);

        var updated = await source.UpdateAsync(new Device { Id = 999, Name = "After", Model = "M2", Quantity = 2 }, original!, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated!.Id);
        Assert.Equal("After", updated.Name);
        Assert.Equal("M2", updated.Model);
        Assert.Equal(2, updated.Quantity);
    }

    [Fact]
    public async Task Delete_removes_the_resource()
    {
        var source = Create<Device>();
        var created = await source.CreateAsync(new Device { Name = "X" }, CancellationToken.None);

        Assert.True(await source.DeleteAsync(created.Id, CancellationToken.None));
        Assert.False(await source.DeleteAsync(created.Id, CancellationToken.None));
        Assert.Null(await source.GetAsync(created.Id, CancellationToken.None));
    }
}
