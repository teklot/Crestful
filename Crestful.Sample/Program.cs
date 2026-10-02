using Crestful;
using Crestful.EFCore;
using Crestful.Sample;
using Crestful.Sample.Pages;
using Crestful.Validation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResources(options => options.DiscoverFromAssemblyContaining<Device>());
builder.Services.AddDbContext<DeviceDbContext>(o => o.UseInMemoryDatabase("devices"));
builder.Services.AddEfCore();
builder.Services.AddResourceValidation();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/", () => HomePage.Render());
app.MapGet("/concurrency", () => ConcurrencyPage.Render());

app.MapResource<Device>(o =>
{
    o.SoftDelete.Enabled = true;
    o.Auditing.Enabled = true;
    o.Concurrency.Enabled = true;
});
app.MapResource<Reading>();

Seed(app);

app.Run();

static void Seed(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();
    db.Database.EnsureCreated();

    if (db.Devices.Any())
    {
        return;
    }

    // Auditing is applied by the endpoints, not by SaveChanges, so an entity seeded straight into the
    // DbContext would keep default timestamps and make Last-Modified meaningless. Stamp them here.
    var now = DateTimeOffset.UtcNow;

    var thermostat = new Device { Name = "Thermostat", Model = "T-100", CreatedAt = now, UpdatedAt = now };
    thermostat.Readings.Add(new Reading { Value = 21.5, Timestamp = now.AddMinutes(-5) });

    var smokeSensor = new Device { Name = "Smoke sensor", Model = "S-200", CreatedAt = now, UpdatedAt = now };
    smokeSensor.Readings.Add(new Reading { Value = 0.02, Timestamp = now.AddMinutes(-1) });

    db.Devices.AddRange(thermostat, smokeSensor);
    db.SaveChanges();
}