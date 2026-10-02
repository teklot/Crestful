using FluentHtml.Bootstrap.Components;
using FluentHtml.Elements;
using FluentHtml.Http;
using FluentHtml.Nodes;

namespace Crestful.Sample.Pages;

/// <summary>
/// Shared document chrome for the sample's browsable pages: a sticky menu bar, then one full-width
/// container that supplies the side gutters and vertical padding, then a footer. Individual pages only
/// describe their own content.
/// </summary>
internal static class PageShell
{
    // Bootstrap's compiled stylesheet, so the sample's markup can stay free of bespoke CSS.
    private const string BootstrapCss = "https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css";

    public static HtmlResult Render(
        string title,
        string subtitle,
        bool concurrencyActive,
        IEnumerable<Node> content,
        params Node[] scripts)
    {
        var head = new HeadElement(
            new TitleElement($"{title} - Crestful Sample API"),
            new MetaElement().Charset("utf-8"),
            new MetaElement().Name("viewport").Content("width=device-width, initial-scale=1"),
            new LinkElement().Rel("icon").Href("data:,"),
            new LinkElement().Rel("stylesheet").Href(BootstrapCss));

        // container-fluid keeps the full viewport width, which is the most real estate Bootstrap allows,
        // while its own gutters supply the side margins. The px/py classes widen them a little.
        var column = new DivElement().Class("d-flex flex-column flex-grow-1 px-4 py-4");
        column.AddChild(new Heading1Element(title).Mb(2));
        column.AddChild(new ParagraphElement(subtitle).Class("text-secondary lh-lg mb-4"));

        foreach (var node in content)
        {
            column.AddChild(node);
        }

        foreach (var script in scripts)
        {
            column.AddChild(script);
        }

        column.AddChild(Footer());

        // ResponsiveLayoutComponent supplies <div class="d-flex"><main class="flex-grow-1">, and VH100
        // gives it min-height: 100vh, so a short page still pushes the footer to the bottom.
        var body = new BodyElement(
            Navbar(concurrencyActive),
            new ResponsiveLayoutComponent(
                new LayoutContentComponent(column)).VH100());

        return new PageElement(head, body).Lang("en").ToHtmlResult();
    }

    private static Node Navbar(bool concurrencyActive)
    {
        var nav = new NavbarComponent(
                new NavbarBrandComponent("Crestful.Sample").Href("/"),
                new NavbarNavComponent(
                    NavLink("Endpoints", "/", !concurrencyActive),
                    NavLink("Concurrency Playground", "/concurrency", concurrencyActive)))
            .Class("bg-dark")
            .StickyTop().ExpandLg().ContainerFluid().P(3);

        // data-bs-theme="dark" is how Bootstrap 5 asks for light text on a dark bar. The Bootstrap 4
        // shortcut for this, .navbar-dark, was removed in Bootstrap 5.
        nav.Attributes.Set("data-bs-theme", "dark");
        return nav;
    }

    private static NavbarNavItemComponent NavLink(string text, string href, bool current)
    {
        // FluentHtml's NavbarNavItemComponent does not put nav-link on the anchor, and Bootstrap 5
        // does not style a bare <a> inside .navbar-nav.
        var link = new AnchorElement(text).Href(href).Class(current ? "nav-link active" : "nav-link");
        if (current)
        {
            link.Attributes.Set("aria-current", "page");
        }

        return new NavbarNavItemComponent(link);
    }

    private static Node Footer()
    {
        return new DivElement(
                new ParagraphElement(
                    "Every request the playground makes is ordinary HTTP. Concurrency lives in headers, so " +
                    "curl or any HTTP client can do exactly the same thing.")
                    .Class("small text-secondary mb-0"))
            .Class("border-top mt-auto pt-3");
    }
}