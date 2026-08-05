using System.Net.Http.Json;
using Crest.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crest.Tests;

public class EfCoreIntegrationTests
{
    private sealed class DeviceDbContext : DbContext
    {
        public DeviceDbContext(DbContextOptions<DeviceDbContext> options)
            : base(options)
        {
        }

        public DbSet<Device> Devices => Set<Device>();

        public DbSet<Reading> Readings => Set<Reading>();
    }

    [Fact]
    public async Task Ef_data_source_supports_full_crud()
    {
        var dbName = Guid.NewGuid().ToString();
        var (app, client) = await TestHostHelper.CreateAsync(configureServices: services =>
        {
            services.AddDbContext<DeviceDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddEfCore();
        });
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { name = "EF Device", model = "M1", quantity = 7 });
            Assert.Equal(System.Net.HttpStatusCode.Created, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<Device>();
            Assert.True(created!.Id > 0);
            Assert.Equal("EF Device", created.Name);

            var list = await client.GetAsync("/api/devices");
            var devices = await list.Content.ReadFromJsonAsync<List<Device>>();
            Assert.Single(devices!);

            var put = await client.PutAsJsonAsync($"/api/devices/{created.Id}", new { id = created.Id, name = "Renamed", model = "M2", quantity = 8 });
            Assert.Equal(System.Net.HttpStatusCode.OK, put.StatusCode);
            var updated = await put.Content.ReadFromJsonAsync<Device>();
            Assert.Equal("Renamed", updated!.Name);

            var patch = await client.PatchAsJsonAsync($"/api/devices/{created.Id}", new { quantity = 9 });
            var patched = await patch.Content.ReadFromJsonAsync<Device>();
            Assert.Equal(9, patched!.Quantity);

            var delete = await client.DeleteAsync($"/api/devices/{created.Id}");
            Assert.Equal(System.Net.HttpStatusCode.NoContent, delete.StatusCode);

            var after = await client.GetAsync($"/api/devices/{created.Id}");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, after.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task AddEfCore_scans_registered_contexts_and_overrides_in_memory()
    {
        var dbName = Guid.NewGuid().ToString();
        var (app, client) = await TestHostHelper.CreateAsync(configureServices: services =>
        {
            services.AddDbContext<DeviceDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddEfCore();
        });
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { name = "Persistence check" });
            Assert.Equal(System.Net.HttpStatusCode.Created, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<Device>();

            await app.StopAsync();
            await Task.Delay(50);

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();
                var stored = await db.Devices.SingleOrDefaultAsync(d => d.Id == created!.Id);
                Assert.NotNull(stored);
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Related_resources_round_trip_through_ef()
    {
        var dbName = Guid.NewGuid().ToString();
        var (app, client) = await TestHostHelper.CreateAsync(configureServices: services =>
        {
            services.AddDbContext<DeviceDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddEfCore();
        });
        try
        {
            var device = await client.PostAsJsonAsync("/api/devices", new { name = "Sensor hub" });
            Assert.Equal(System.Net.HttpStatusCode.Created, device.StatusCode);
            var createdDevice = await device.Content.ReadFromJsonAsync<Device>();

            var readingId = Guid.NewGuid();
            var reading = await client.PostAsJsonAsync("/api/readings",
                new { id = readingId, deviceId = createdDevice!.Id, value = 42.5 });
            Assert.Equal(System.Net.HttpStatusCode.Created, reading.StatusCode);
            var createdReading = await reading.Content.ReadFromJsonAsync<Reading>();
            Assert.Equal(createdDevice.Id, createdReading!.DeviceId);
            Assert.Equal(42.5, createdReading.Value);

            var get = await client.GetAsync($"/api/readings/{readingId}");
            var fetched = await get.Content.ReadFromJsonAsync<Reading>();
            Assert.Equal(createdDevice.Id, fetched!.DeviceId);

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();
                var stored = await db.Readings.SingleOrDefaultAsync(r => r.Id == readingId);
                Assert.NotNull(stored);
                Assert.Equal(createdDevice.Id, stored!.DeviceId);
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task Create_persists_related_graph_in_one_transaction()
    {
        var dbName = Guid.NewGuid().ToString();
        var (app, client) = await TestHostHelper.CreateAsync(configureServices: services =>
        {
            services.AddDbContext<DeviceDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddEfCore();
        });
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new
            {
                name = "Gateway",
                readings = new[]
                {
                    new { value = 1.5 },
                    new { value = 2.5 }
                }
            });
            Assert.Equal(System.Net.HttpStatusCode.Created, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<Device>();

            Assert.Equal(2, created!.Readings.Count);
            Assert.All(created.Readings, r => Assert.Equal(created.Id, r.DeviceId));
            Assert.All(created.Readings, r => Assert.NotEqual(Guid.Empty, r.Id));

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();
                var readings = await db.Readings.Where(r => r.DeviceId == created.Id).ToListAsync();
                Assert.Equal(2, readings.Count);
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
