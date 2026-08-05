using Crest;
using Crest.EFCore;
using Crest.Samples.Devices;
using Crest.Validation;
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
        db.Devices.AddRange(
            new Device { Name = "Thermostat", Model = "T-100" },
            new Device { Name = "Smoke sensor", Model = "S-200" });
        db.SaveChanges();
    }
}

app.Run();
