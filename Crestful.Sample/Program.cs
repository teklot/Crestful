using Crestful;
using Crestful.EFCore;
using Crestful.Sample;
using Crestful.Validation;
using FluentHtml.Bootstrap.Components;
using FluentHtml.Elements;
using FluentHtml.Http;
using FluentHtml.Nodes;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddResources(options => options.DiscoverFromAssemblyContaining<Device>());
builder.Services.AddDbContext<DeviceDbContext>(o => o.UseInMemoryDatabase("devices"));
builder.Services.AddEfCore();
builder.Services.AddResourceValidation();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/", () =>
{
    var page = new HtmlElement(
        new HeadElement(
            new TitleElement("Crestful Sample API"),
            new MetaElement().Charset("utf-8"),
            new MetaElement().Name("viewport").Content("width=device-width, initial-scale=1.0"),
            new LinkElement().Rel("icon").Href("data:,"),
            new StyleElement(
                new TextNode("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f5f5f5; color: #333; line-height: 1.6; padding: 2rem; margin: 0; }"),
                new TextNode(".container { max-width: 800px; margin: 0 auto; }"),
                new TextNode(".subtitle { color: #666; margin-bottom: 2rem; }"),
                new TextNode(".section { background: #fff; border-radius: 8px; padding: 1.5rem; margin-bottom: 1.5rem; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }"),
                new TextNode(".section h2 { font-size: 1.1rem; margin-bottom: 1rem; color: #555; text-transform: uppercase; letter-spacing: 0.05em; }"),
                new TextNode(".endpoint { display: flex; align-items: center; padding: 0.75rem 0; border-bottom: 1px solid #eee; }"),
                new TextNode(".endpoint:last-child { border-bottom: none; }"),
                new TextNode(".method { display: inline-block; padding: 0.15rem 0.5rem; border-radius: 4px; font-size: 0.75rem; font-weight: 600; margin-right: 0.75rem; min-width: 4rem; text-align: center; }"),
                new TextNode(".get { background: #e8f5e9; color: #2e7d32; }"),
                new TextNode(".post { background: #e3f2fd; color: #1565c0; }"),
                new TextNode(".put { background: #fff3e0; color: #e65100; }"),
                new TextNode(".patch { background: #fce4ec; color: #c62828; }"),
                new TextNode(".delete { background: #ffebee; color: #b71c1c; }"),
                new TextNode(".path { font-family: 'SF Mono', 'Fira Code', monospace; font-size: 0.9rem; }"),
                new TextNode(".desc { color: #666; margin-left: auto; font-size: 0.85rem; }")
            )
        ),
        new BodyElement(
            new DivElement(
                new Heading1Element("Crestful Sample API"),
                new ParagraphElement("A demo of the Crestful convention-first REST framework. Two resources are seeded with data.").Class("subtitle"),

                EndpointSection("Resources", new[]
                {
                    ("GET",    "/api/devices",       "List all devices"),
                    ("GET",    "/api/devices/{id}",   "Get device by ID"),
                    ("POST",   "/api/devices",        "Create a device"),
                    ("PUT",    "/api/devices/{id}",   "Replace a device"),
                    ("PATCH",  "/api/devices/{id}",   "Update a device"),
                    ("DELETE", "/api/devices/{id}",   "Delete a device"),
                    ("GET",    "/api/readings",       "List all readings"),
                    ("POST",   "/api/readings",       "Create a reading"),
                }),

                EndpointSection("Query Examples", new[]
                {
                    ("GET", "/api/devices?where={\"Name\":\"Thermostat\"}", "Filter by name"),
                    ("GET", "/api/devices?sort=-Name",                     "Sort descending"),
                    ("GET", "/api/devices?page=1&max_results=1",           "Pagination"),
                    ("GET", "/api/devices?search=smoke",                   "Full-text search"),
                    ("GET", "/api/devices?field=Name,Model",               "Field selection"),
                })
            ).Class("container")
        )
    );

    return page.ToHtmlResult();
});

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

static DivElement EndpointSection(string title, (string Method, string Path, string Desc)[] endpoints)
{
    var children = new List<Node>();
    children.Add(new Heading2Element(title));
    foreach (var e in endpoints)
        children.Add(EndpointRow(e.Method, e.Path, e.Desc));

    return new DivElement(children.ToArray()).Class("section");
}

static DivElement EndpointRow(string method, string path, string desc)
{
    return new DivElement(
        new SpanElement(method).Class($"method {method.ToLower()}"),
        new SpanElement(path).Class("path"),
        new SpanElement(desc).Class("desc")
    ).Class("endpoint");
}
