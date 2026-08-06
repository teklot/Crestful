using Crestful;
using Crestful.EFCore;
using Crestful.Sample;
using Crestful.Validation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResources(options => options.DiscoverFromAssemblyContaining<Device>());
builder.Services.AddDbContext<DeviceDbContext>(o => o.UseInMemoryDatabase("devices"));
builder.Services.AddEfCore();
builder.Services.AddResourceValidation();

var app = builder.Build();

app.UseExceptionHandler();

app.MapResources();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DeviceDbContext>();
    db.Database.EnsureCreated();
    if (!db.Devices.Any())
    {
        var thermostat = new Device { Name = "Thermostat", Model = "T-100" };
        thermostat.Readings.Add(new Reading { Value = 21.5, Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5) });

        var smokeSensor = new Device { Name = "Smoke sensor", Model = "S-200" };
        smokeSensor.Readings.Add(new Reading { Value = 0.02, Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1) });

        db.Devices.AddRange(thermostat, smokeSensor);
        db.SaveChanges();
    }
}

app.Run();
