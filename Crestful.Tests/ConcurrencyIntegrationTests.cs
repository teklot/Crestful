using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crestful.EFCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crestful.Tests;

public class ConcurrencyIntegrationTests
{
    private const string Route = "/api/versioned-devices";
    private const string SoftDeleteRoute = "/api/versioned-soft-delete-devices";
    private const string AuditedRoute = "/api/audited-devices";

    private static Task<(WebApplication App, HttpClient Client)> CreateVersionedAsync()
        => TestHostHelper.CreateAsync(map: app => app.MapResource<VersionedDevice>(o =>
        {
            o.Name = "versioned-devices";
            o.Concurrency.Enabled = true;
            o.Auditing.Enabled = true;
        }));

    private static Task<(WebApplication App, HttpClient Client)> CreateSoftDeleteAsync()
        => TestHostHelper.CreateAsync(map: app => app.MapResource<VersionedSoftDeleteDevice>(o =>
        {
            o.Name = "versioned-soft-delete-devices";
            o.Concurrency.Enabled = true;
            o.SoftDelete.Enabled = true;
        }));

    private static Task<(WebApplication App, HttpClient Client)> CreateAuditedAsync()
        => TestHostHelper.CreateAsync(map: app => app.MapResource<AuditedDevice>(o =>
        {
            o.Name = "audited-devices";
            o.Auditing.Enabled = true;
        }));

    // --- Validators on responses -------------------------------------------------------------------

    [Fact]
    public async Task Get_returns_etag_and_last_modified()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var response = await client.GetAsync($"{Route}/{id}", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.ETag?.Tag is not null);
            Assert.True(response.Content.Headers.LastModified is not null);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Post_returns_etag()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var create = await client.PostAsJsonAsync(Route, new { name = "Smoke sensor" }, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            Assert.True(create.Headers.ETag?.Tag is not null);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    // --- If-Match is mandatory on writes -------------------------------------------------------------

    [Fact]
    public async Task Put_without_if_match_returns_428()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var response = await SendAsync(client, HttpMethod.Put, $"{Route}/{id}", """{"name":"Renamed"}""", ifMatch: null);

            Assert.Equal((HttpStatusCode)428, response.StatusCode);

            var unchanged = await client.GetAsync($"{Route}/{id}", TestContext.Current.CancellationToken);
            var current = await unchanged.Content.ReadFromJsonAsync<VersionedDevice>(TestContext.Current.CancellationToken);
            Assert.Equal("Thermostat", current!.Name);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Patch_without_if_match_returns_428()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var response = await SendAsync(client, HttpMethod.Patch, $"{Route}/{id}", """{"name":"Renamed"}""", ifMatch: null);

            Assert.Equal((HttpStatusCode)428, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Delete_without_if_match_returns_428()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var response = await SendAsync(client, HttpMethod.Delete, $"{Route}/{id}", null, ifMatch: null);

            Assert.Equal((HttpStatusCode)428, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Route}/{id}", TestContext.Current.CancellationToken)).StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Missing_resource_returns_404_rather_than_428()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var response = await SendAsync(client, HttpMethod.Delete, $"{Route}/9999", null, ifMatch: null);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    // --- If-Match mismatch ---------------------------------------------------------------------------

    [Fact]
    public async Task Put_with_stale_if_match_returns_412()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            await RotateAsync(client, id);

            var response = await SendAsync(client, HttpMethod.Put, $"{Route}/{id}", """{"name":"Renamed"}""", ifMatch: "\"stale\"");

            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Patch_with_stale_if_match_returns_412()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            await RotateAsync(client, id);

            var response = await SendAsync(client, HttpMethod.Patch, $"{Route}/{id}", """{"name":"Renamed"}""", ifMatch: "\"stale\"");

            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Delete_with_stale_if_match_returns_412()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            await RotateAsync(client, id);

            var response = await SendAsync(client, HttpMethod.Delete, $"{Route}/{id}", null, ifMatch: "\"stale\"");

            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Route}/{id}", TestContext.Current.CancellationToken)).StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    // --- Successful conditional writes ---------------------------------------------------------------

    [Fact]
    public async Task Put_with_current_if_match_succeeds_and_rotates_etag()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var before = await GetETagAsync(client, id);

            var response = await SendAsync(client, HttpMethod.Put, $"{Route}/{id}", """{"name":"Renamed","model":"T-200"}""", ifMatch: before);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var after = response.Headers.ETag?.Tag;
            Assert.NotNull(after);
            Assert.NotEqual(before, after);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Patch_with_current_if_match_succeeds_and_rotates_etag()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var before = await GetETagAsync(client, id);

            var response = await SendAsync(client, HttpMethod.Patch, $"{Route}/{id}", """{"model":"T-300"}""", ifMatch: before);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEqual(before, response.Headers.ETag?.Tag);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Rotated_etag_cannot_be_reused()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var first = await GetETagAsync(client, id);
            await SendAsync(client, HttpMethod.Patch, $"{Route}/{id}", """{"model":"T-300"}""", ifMatch: first);

            var replay = await SendAsync(client, HttpMethod.Patch, $"{Route}/{id}", """{"model":"T-400"}""", ifMatch: first);

            Assert.Equal(HttpStatusCode.PreconditionFailed, replay.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task If_match_wildcard_is_accepted()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");

            var response = await SendAsync(client, HttpMethod.Patch, $"{Route}/{id}", """{"model":"T-300"}""", ifMatch: "*");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task If_match_accepts_a_comma_separated_list()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var etag = await GetETagAsync(client, id);

            var response = await SendAsync(client, HttpMethod.Patch, $"{Route}/{id}", """{"model":"T-300"}""",
                ifMatch: $"\"other\", {etag}, \"another\"");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task If_match_accepts_weak_and_unquoted_tags()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");

            // Each write rotates the version, so the current tag has to be re-read every iteration.
            foreach (var transform in new Func<string, string>[] { e => $"W/{e}", e => e.Trim('"'), e => e })
            {
                var etag = await GetETagAsync(client, id);

                var response = await SendAsync(client, HttpMethod.Patch, $"{Route}/{id}", """{"model":"T-300"}""",
                    ifMatch: transform(etag));

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Patch_body_cannot_overwrite_the_row_version()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var etag = await GetETagAsync(client, id);

            var forged = Convert.ToBase64String(Enumerable.Range(1, 8).Select(i => (byte)i).ToArray());
            var body = JsonSerializer.Serialize(new { model = "T-300", rowVersion = Convert.FromBase64String(forged) });

            var response = await SendAsync(client, HttpMethod.Patch, $"{Route}/{id}", body, etag);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEqual($"\"{forged}\"", response.Headers.ETag?.Tag);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    // --- Conditional GET -----------------------------------------------------------------------------

    [Fact]
    public async Task If_none_match_hit_returns_304_with_validators_and_no_body()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var etag = await GetETagAsync(client, id);

            var response = await SendAsync(client, HttpMethod.Get, $"{Route}/{id}", null, ifNoneMatch: etag);

            Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
            Assert.Equal(etag, response.Headers.ETag?.Tag);
            Assert.True(response.Content.Headers.LastModified is not null);
            Assert.Empty(await response.Content!.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task If_none_match_miss_returns_200()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");

            var response = await SendAsync(client, HttpMethod.Get, $"{Route}/{id}", null, ifNoneMatch: "\"stale\"");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task If_none_match_uses_weak_comparison()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var etag = await GetETagAsync(client, id);

            var response = await SendAsync(client, HttpMethod.Get, $"{Route}/{id}", null, ifNoneMatch: $"W/{etag}");

            Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task If_none_match_wildcard_returns_304_for_existing_resource()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");

            var response = await SendAsync(client, HttpMethod.Get, $"{Route}/{id}", null, ifNoneMatch: "*");

            Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task If_modified_since_hit_returns_304()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var lastModified = await GetLastModifiedAsync(client, id);

            var response = await SendAsync(client, HttpMethod.Get, $"{Route}/{id}", null, ifModifiedSince: lastModified);

            Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task If_none_match_takes_precedence_over_if_modified_since()
    {
        var (app, client) = await CreateVersionedAsync();
        try
        {
            var id = await CreateAsync(client, "Thermostat");
            var lastModified = await GetLastModifiedAsync(client, id);

            // Both validators are sent and only the ETag matches. If-Modified-Since alone would have
            // produced a 304, so a 200 proves the ETag took precedence.
            var response = await SendAsync(client, HttpMethod.Get, $"{Route}/{id}", null,
                ifNoneMatch: "\"stale\"", ifModifiedSince: lastModified);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Soft_deleted_resource_returns_404_even_when_validator_matches()
    {
        var (app, client) = await CreateSoftDeleteAsync();
        try
        {
            var create = await client.PostAsJsonAsync(SoftDeleteRoute, new { name = "Thermostat" }, TestContext.Current.CancellationToken);
            var id = (await create.Content.ReadFromJsonAsync<VersionedSoftDeleteDevice>(TestContext.Current.CancellationToken))!.Id;
            var etag = create.Headers.ETag?.Tag;

            var delete = await SendAsync(client, HttpMethod.Delete, $"{SoftDeleteRoute}/{id}", null, etag);
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

            var response = await SendAsync(client, HttpMethod.Get, $"{SoftDeleteRoute}/{id}", null, ifNoneMatch: etag);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Auditing_only_resource_supports_conditional_get_without_etag()
    {
        var (app, client) = await CreateAuditedAsync();
        try
        {
            var create = await client.PostAsJsonAsync(AuditedRoute, new { name = "Thermostat" }, TestContext.Current.CancellationToken);
            var id = (await create.Content.ReadFromJsonAsync<AuditedDevice>(TestContext.Current.CancellationToken))!.Id;

            Assert.Null(create.Headers.ETag?.Tag);
            Assert.True(create.Content.Headers.LastModified is not null);

            var get = await client.GetAsync($"{AuditedRoute}/{id}", TestContext.Current.CancellationToken);
            var notModified = await SendAsync(client, HttpMethod.Get, $"{AuditedRoute}/{id}", null,
                ifModifiedSince: get.Content.Headers.LastModified!.Value.ToString("R"));
            Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);

            // No row version, so If-Match cannot be required.
            var put = await client.PutAsJsonAsync($"{AuditedRoute}/{id}", new { id, name = "Renamed" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            Assert.Null(put.Headers.ETag?.Tag);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Default_updated_at_is_not_offered_as_a_cache_validator()
    {
        // An entity written straight to a DbContext never has its audit stamps populated, because
        // auditing is applied by the endpoints. Emitting a year-one Last-Modified would be worse than
        // emitting none: it is not a real modification time, and honouring an If-Modified-Since against
        // it would answer 304 to almost any date the client sends.
        var dbName = Guid.NewGuid().ToString();
        await using (var seed = NewContext(dbName))
        {
            seed.VersionedDevices.Add(new VersionedDevice { Name = "Unstamped", RowVersion = [1, 2, 3] });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (app, client) = await TestHostHelper.CreateAsync(
            configureServices: services =>
            {
                services.AddDbContext<VersionedDbContext>(o => o.UseInMemoryDatabase(dbName));

                // Registered after AddResources' assembly scan, so this overrides the in-memory source.
                services.AddEfCoreResource<VersionedDevice, VersionedDbContext>();
            },
            map: app => app.MapResource<VersionedDevice>(o =>
            {
                o.Name = "versioned-devices";
                o.Concurrency.Enabled = true;
                o.Auditing.Enabled = true;
            }));

        try
        {
            await using var verify = NewContext(dbName);
            var seeded = await verify.VersionedDevices.SingleAsync(TestContext.Current.CancellationToken);

            var response = await client.GetAsync($"{Route}/{seeded.Id}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // The entity tag is still meaningful, so it is still sent.
            Assert.Equal($"\"{Convert.ToBase64String(seeded.RowVersion)}\"", response.Headers.ETag?.Tag);
            Assert.Null(response.Content.Headers.LastModified);

            // An If-Modified-Since before year one is compared exactly, not treated as "older".
            var notModified = await SendAsync(client, HttpMethod.Get, $"{Route}/{seeded.Id}", null,
                ifModifiedSince: DateTimeOffset.MinValue.ToString("R", CultureInfo.InvariantCulture));
            Assert.Equal(HttpStatusCode.OK, notModified.StatusCode);

            // The wildcard reports existence regardless of the missing timestamp.
            var wildcard = await SendAsync(client, HttpMethod.Get, $"{Route}/{seeded.Id}", null, ifNoneMatch: "*");
            Assert.Equal(HttpStatusCode.NotModified, wildcard.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Resource_without_row_version_needs_no_if_match()
    {
        var (app, client) = await TestHostHelper.CreateAsync(map: app => app.MapResource<Device>());
        try
        {
            var create = await client.PostAsJsonAsync("/api/devices", new { name = "Thermostat" }, TestContext.Current.CancellationToken);
            var id = (await create.Content.ReadFromJsonAsync<Device>(TestContext.Current.CancellationToken))!.Id;

            Assert.Null(create.Headers.ETag?.Tag);

            var put = await client.PutAsJsonAsync($"/api/devices/{id}", new { id, name = "Renamed" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    // --- EF Core ---------------------------------------------------------------------------------------

    private sealed class VersionedDbContext : DbContext
    {
        public VersionedDbContext(DbContextOptions<VersionedDbContext> options)
            : base(options)
        {
        }

        public DbSet<VersionedDevice> VersionedDevices => Set<VersionedDevice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // IsConcurrencyToken, not IsRowVersion: the in-memory provider neither generates row
            // versions nor tolerates them being null, but it does perform real concurrency checks.
            modelBuilder.Entity<VersionedDevice>()
                .Property(e => e.RowVersion)
                .IsConcurrencyToken();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            RotateRowVersions();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            RotateRowVersions();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>Stands in for the database maintaining the token, as SQL Server's rowversion does.</summary>
        private void RotateRowVersions()
        {
            foreach (var entry in ChangeTracker.Entries<VersionedDevice>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    var token = new byte[8];
                    RandomNumberGenerator.Fill(token);
                    entry.Property(e => e.RowVersion).CurrentValue = token;
                }
            }
        }
    }

    [Fact]
    public async Task Ef_update_with_a_stale_tracked_entity_surfaces_as_concurrency_exception()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var first = NewContext(dbName);
        await using var second = NewContext(dbName);

        first.VersionedDevices.Add(new VersionedDevice { Name = "Thermostat", Model = "T-100" });
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        var id = first.VersionedDevices.Local.Single().Id;

        // first holds the token as of v1; second moves the stored row on to v2.
        await using (var racer = NewContext(dbName))
        {
            var winner = await racer.VersionedDevices.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
            winner.Model = "T-200";
            await racer.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var stale = await first.VersionedDevices.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
        stale.Model = "T-300";
        var source = new EfCoreResourceDataSource<VersionedDevice, VersionedDbContext>(await CreateRegistryAsync(), first);

        await Assert.ThrowsAsync<ResourceConcurrencyException>(() =>
            source.UpdateAsync(stale, stale, TestContext.Current.CancellationToken));

        await using var verify = NewContext(dbName);
        Assert.Equal("T-200", (await verify.VersionedDevices.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken)).Model);
    }

    [Fact]
    public async Task Ef_delete_with_a_stale_tracked_entity_surfaces_as_concurrency_exception()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var first = NewContext(dbName);

        first.VersionedDevices.Add(new VersionedDevice { Name = "Thermostat" });
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        var id = first.VersionedDevices.Local.Single().Id;

        await using (var racer = NewContext(dbName))
        {
            var winner = await racer.VersionedDevices.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
            winner.Model = "T-200";
            await racer.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // FindAsync returns the instance first already has tracked, so the delete is issued against v1.
        var source = new EfCoreResourceDataSource<VersionedDevice, VersionedDbContext>(await CreateRegistryAsync(), first);
        await Assert.ThrowsAsync<ResourceConcurrencyException>(() =>
            source.DeleteAsync(id, TestContext.Current.CancellationToken));

        await using var verify = NewContext(dbName);
        Assert.Single(await verify.VersionedDevices.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ef_endpoints_round_trip_the_etag()
    {
        var dbName = Guid.NewGuid().ToString();
        var (app, client) = await TestHostHelper.CreateAsync(
            configureServices: services =>
            {
                services.AddDbContext<VersionedDbContext>(o => o.UseInMemoryDatabase(dbName));
                services.AddEfCore();
            },
            map: app => app.MapResource<VersionedDevice>(o =>
            {
                o.Name = "versioned-devices";
                o.Concurrency.Enabled = true;
                o.Auditing.Enabled = true;
            }));
        try
        {
            var create = await client.PostAsJsonAsync(Route, new { name = "Thermostat" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var id = (await create.Content.ReadFromJsonAsync<VersionedDevice>(TestContext.Current.CancellationToken))!.Id;
            var etag = create.Headers.ETag?.Tag;
            Assert.NotNull(etag);

            var patch = await SendAsync(client, HttpMethod.Patch, $"{Route}/{id}", """{"model":"T-200"}""", etag);
            Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
            Assert.NotEqual(etag, patch.Headers.ETag?.Tag);

            var notModified = await SendAsync(client, HttpMethod.Get, $"{Route}/{id}", null, ifNoneMatch: patch.Headers.ETag?.Tag);
            Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    // --- Helpers ---------------------------------------------------------------------------------------

    private static VersionedDbContext NewContext(string dbName)
        => new(new DbContextOptionsBuilder<VersionedDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    private static async Task<ResourceRegistry> CreateRegistryAsync()
    {
        var services = new ServiceCollection();
        services.AddResources(o =>
        {
            o.Assemblies.Add(typeof(ConcurrencyIntegrationTests).Assembly);
            o.DefaultResourceOptions = options => options.Concurrency.Enabled = true;
        });
        await using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ResourceRegistry>();
    }

    private static async Task<int> CreateAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(Route, new { name }, TestContext.Current.CancellationToken);
        var created = await response.Content.ReadFromJsonAsync<VersionedDevice>(TestContext.Current.CancellationToken);
        return created!.Id;
    }

    private static async Task<string> GetETagAsync(HttpClient client, int id)
    {
        var response = await client.GetAsync($"{Route}/{id}", TestContext.Current.CancellationToken);
        return response.Headers.ETag?.Tag!;
    }

    private static async Task<string> GetLastModifiedAsync(HttpClient client, int id)
    {
        var response = await client.GetAsync($"{Route}/{id}", TestContext.Current.CancellationToken);
        return response.Content.Headers.LastModified!.Value.ToString("R");
    }

    /// <summary>Performs a write that advances the stored version, invalidating any earlier ETag.</summary>
    private static async Task RotateAsync(HttpClient client, int id)
    {
        var etag = await GetETagAsync(client, id);
        var response = await SendAsync(client, HttpMethod.Patch, $"{Route}/{id}", """{"model":"T-999"}""", etag);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string? json,
        string? ifMatch = null,
        string? ifNoneMatch = null,
        string? ifModifiedSince = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (ifNoneMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        if (ifModifiedSince is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Modified-Since", ifModifiedSince);
        }

        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return response;
    }
}