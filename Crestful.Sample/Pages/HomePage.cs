using FluentHtml.Bootstrap.Components;
using FluentHtml.Elements;
using FluentHtml.Http;

namespace Crestful.Sample.Pages;

/// <summary>The landing page: what the generated API exposes and which features are switched on.</summary>
internal static class HomePage
{
    // Fixed proportions so both tables line up with each other. Bootstrap's width scale has no w-35,
    // so the description column takes whatever is left over. text-break stops a long query string from
    // overflowing its now non-negotiable column.
    private const string MethodColumn = "w-25";
    private const string PathColumn = "w-40 text-break";

    public static HtmlResult Render()
    {
        return PageShell.Render(
            title: "Crestful Sample API",
            subtitle: "A demo of the convention-first REST framework. Device is seeded with data and has " +
                      "soft delete, auditing and concurrency enabled.",
            concurrencyActive: false,
            content:
            [
                EndpointTable("Resources",
                [
                    ("GET", "/api/devices", "List all devices"),
                    ("GET", "/api/devices/{id}", "Get device by ID"),
                    ("POST", "/api/devices", "Create a device"),
                    ("PUT", "/api/devices/{id}", "Replace a device"),
                    ("PATCH", "/api/devices/{id}", "Update a device"),
                    ("DELETE", "/api/devices/{id}", "Delete a device"),
                    ("GET", "/api/readings", "List all readings"),
                    ("POST", "/api/readings", "Create a reading"),
                ]),

                EndpointTable("Query examples",
                [
                    ("GET", "/api/devices?where={\"Name\":\"Thermostat\"}", "Filter by name"),
                    ("GET", "/api/devices?sort=-Name", "Sort descending"),
                    ("GET", "/api/devices?page=1&max_results=1", "Pagination"),
                    ("GET", "/api/devices?search=smoke", "Full-text search"),
                    ("GET", "/api/devices?field=Name,Model", "Field selection"),
                ]),

                new AlertComponent(
                    new StrongElement("Soft delete is enabled on Device. "),
                    new SpanElement("DELETE stamps DeletedAt instead of removing the row. GET and list hide " +
                                    "soft-deleted items, and PUT or PATCH on one restores it."))
                .Info().Mb(3),

                new AlertComponent(
                    new StrongElement("Auditing is enabled. "),
                    new SpanElement("CreatedAt, UpdatedAt, CreatedBy and UpdatedBy are populated automatically, " +
                                    "and UpdatedAt becomes the Last-Modified header."))
                .Info().Mb(3),

                new AlertComponent(
                    new StrongElement("Concurrency is enabled. "),
                    new SpanElement("GET returns an ETag. Send it back as If-Match on PUT, PATCH or DELETE or you " +
                                    "get 428. If it no longer matches, someone else wrote first and you get 412. " +
                                    "A GET that sends a matching If-None-Match gets 304 with no body."))
                .Warning().Mb(3),

                new AlertComponent(
                    new StrongElement("Validators are response headers. "),
                    new SpanElement("A browser will not show them to you, so use the playground to drive the " +
                                    "whole conditional request contract from a page."))
                .Light().Mb(4),
            ]);
    }

    private static CardComponent EndpointTable(string caption, (string Method, string Path, string Desc)[] rows)
    {
        var body = new TbodyElement();
        foreach (var (method, path, description) in rows)
        {
            body.AddChild(new TrElement(
                new TdElement(method).Class(MethodColumn),
                new TdElement(new CodeElement(path)).Class(PathColumn),
                new TdElement(description)));
        }

        // Bootstrap's .table is table-layout: auto, where column widths are negotiated from content and
        // percentages are only suggestions. Fixed layout makes the percentages binding, which is what
        // stops the long query strings from pushing the last column out of line with the other table.
        // Bootstrap ships no utility for this, so it is the one inline style on the page.
        var table = new TableElement(
            new TheadElement(
                new TrElement(
                    new ThElement("Method").Class(MethodColumn),
                    new ThElement("Path").Class(PathColumn),
                    new ThElement("What it does"))),
            body)
            .Class("table table-sm mb-0");
        table.Attributes.Set("style", "table-layout: fixed");

        return new CardComponent(
            new CardHeaderComponent(new CardTitleComponent(caption)),
            new CardBodyComponent(table))
            .Mb(4);
    }
}