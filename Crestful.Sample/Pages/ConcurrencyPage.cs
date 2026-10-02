using FluentHtml.Bootstrap.Components;
using FluentHtml.Elements;
using FluentHtml.Http;
using FluentHtml.Nodes;

namespace Crestful.Sample.Pages;

/// <summary>
/// An interactive page for the conditional request contract. ETags travel in response headers, which a
/// browser will not render, so the buttons here issue the requests and show what came back.
/// </summary>
internal static class ConcurrencyPage
{
    public static HtmlResult Render()
    {
        return PageShell.Render(
            title: "Concurrency Playground",
            subtitle: "Read the device, then use the ETag it returns. Press the actions in order: each one " +
                      "names the status it produces.",
            concurrencyActive: true,
            content:
            [
                new AlertComponent(
                    new StrongElement("Every successful write rotates the ETag. "),
                    new SpanElement("That is what makes the previous tag stale, and staleness is what produces " +
                                    "412 rather than a silent overwrite."))
                .Info().Mb(4),

                Card("Validators", ValidatorTable()),
                Card("Actions", ActionButtons()),
                Card("Response log", LogTable()),
            ],
            scripts: [new ScriptElement(Script)]);
    }

    private static CardComponent Card(string title, Node body)
    {
        return new CardComponent(
            new CardHeaderComponent(new CardTitleComponent(title)),
            new CardBodyComponent(body))
            .Mb(4);
    }

    private static TableElement ValidatorTable()
    {
        return new TableElement(
            new TbodyElement(
                new TrElement(
                    new ThElement("Validator").Class("w-25"),
                    new ThElement("Value")),
                Row("current ETag", "cur", "none yet"),
                Row("previous ETag", "prev", "none yet"),
                Row("Last-Modified", "lmod", "none yet")))
            .Class("table table-sm mb-0");
    }

    private static TrElement Row(string label, string id, string initial)
    {
        return new TrElement(
            new TdElement(label),
            new TdElement(new CodeElement(initial).Id(id)));
    }

    private static Node ActionButtons()
    {
        return new DivElement(
                Button("b-get", "GET the device", "200", "captures the ETag", b => b.Primary()),
                Button("b-patch-ok", "PATCH with current ETag", "200", "and rotates it", b => b.OutlineSecondary()),
                Button("b-patch-stale", "PATCH with previous ETag", "412", "a generation behind", b => b.OutlineDanger()),
                Button("b-patch-none", "PATCH with no If-Match", "428", "no validator at all", b => b.OutlineWarning()),
                Button("b-cond-get", "GET with If-None-Match", "304", "unchanged, no body", b => b.OutlineInfo()),
                Button("b-delete", "DELETE with current ETag", "204", "soft delete", b => b.OutlineDark()),
                Button("b-get-gone", "GET after the delete", "404", "absent over HTTP", b => b.OutlineDark()),
                Button("b-restore", "PUT to restore it", "200", "soft-deleted rows skip If-Match", b => b.OutlineSuccess()),
                new ButtonComponent("Clear the log")
                    .OutlineSecondary().Small().Type("button").Id("b-clear"))
            .DFlex().FlexWrap().Class("gap-2");
    }

    private static ButtonComponent Button(string id, string label, string expected, string note, Func<ButtonComponent, ButtonComponent> colour)
    {
        var button = new ButtonComponent(
            new DivElement(
                new StrongElement(label).Class("d-block"),
                new SpanElement($"{expected} - {note}").Class("d-block small opacity-75")));

        button.Id(id);
        button.Small().Type("button");
        return colour(button);
    }

private static Node LogTable()
    {
        // The header needs its own <thead>. While it sat in the <tbody>, the script that inserts each new
        // entry before the body's first child pushed the header row down one place per entry, so it ended
        // up underneath the log instead of above it.
        return new Fragment(
            new ParagraphElement("Nothing yet. Press an action above.").Id("empty").Class("text-secondary fst-italic mb-2"),
            new TableElement(
                new TheadElement(
                    new TrElement(
                        new ThElement("Method"),
                        new ThElement("Status"),
                        new ThElement("ETag"),
                        new ThElement("Why"))),
                new TbodyElement().Id("log"))
                .Class("table table-sm table-striped mb-0"));
    }

    /// <summary>
    /// Keeps two tags: the one the server most recently issued, and the one before it. A successful write
    /// moves <c>current</c> forward and leaves <c>previous</c> behind, so the same pair can demonstrate both
    /// a successful conditional write and a rejected stale one.
    /// </summary>
    private const string Script = """
        var target = '/api/devices/1';
        var current = null;
        var previous = null;

        function $(id) { return document.getElementById(id); }

        function rows() { return $('log'); }

        function reason(status) {
            if (status === 200) return 'OK';
            if (status === 204) return 'No Content';
            if (status === 304) return 'Not Modified';
            if (status === 404) return 'Not Found';
            if (status === 412) return 'Precondition Failed';
            if (status === 428) return 'Precondition Required';
            return '';
        }

        function tone(status) {
            if (status === 304) return 'text-bg-info';
            if (status >= 400) return 'text-bg-danger';
            if (status === 204) return 'text-bg-secondary';
            return 'text-bg-success';
        }

        function badge(text, colour) {
            var span = document.createElement('span');
            span.className = 'badge ' + colour;
            span.textContent = text;
            return span;
        }

        function cell(child, extra) {
            var td = document.createElement('td');
            if (extra) td.className = extra;
            td.appendChild(child);
            return td;
        }

        function record(method, status, etag, why) {
            var tag = document.createElement('code');
            tag.textContent = etag ? etag : 'no ETag';

            var row = document.createElement('tr');
            row.appendChild(cell(badge(method, 'text-bg-dark')));
            row.appendChild(cell(badge(status + ' ' + reason(status), tone(status))));
            row.appendChild(cell(tag));
            row.appendChild(cell(document.createTextNode(why), 'text-secondary small'));

            $('empty').style.display = 'none';
            rows().insertBefore(row, rows().firstChild);
        }

        function hint(message) {
            var row = document.createElement('tr');
            var td = document.createElement('td');
            // The log has four columns, so a single cell would be squeezed into the Method column
            // and wrap into a tall narrow block. Span the whole row instead.
            td.colSpan = 4;
            td.className = 'text-secondary fst-italic small';
            td.textContent = message;
            row.appendChild(td);
            rows().insertBefore(row, rows().firstChild);
        }

        function paint() {
            $('cur').textContent = current ? current : 'none yet';
            $('prev').textContent = previous ? previous : 'none yet';
        }

        async function send(method, headers, body, why) {
            var init = { method: method, headers: headers || {} };
            if (body) {
                init.headers['Content-Type'] = 'application/json';
                init.body = body;
            }

            var response = await fetch(target, init);
            var etag = response.headers.get('ETag');
            var modified = response.headers.get('Last-Modified');

            if (etag) current = etag;
            if (modified) $('lmod').textContent = modified;

            paint();
            record(method, response.status, etag, why);
        }

        function needsTag(what, tag) {
            if (tag) return true;
            hint(what + ' needs an ETag first, so press GET the device');
            return false;
        }

        function on(id, handler) { $(id).addEventListener('click', handler); }

        on('b-get', function () {
            send('GET', null, null, 'captures the ETag').then(function () {
                previous = current;
                paint();
            });
        });

        on('b-patch-ok', function () {
            if (!needsTag('This action', current)) return;
            previous = current;
            paint();
            send('PATCH', { 'If-Match': current }, '{"model":"T-777"}', 'matched, so the write applies');
        });

        on('b-patch-stale', function () {
            if (!needsTag('This action', previous)) return;
            send('PATCH', { 'If-Match': previous }, '{"model":"T-999"}', 'a generation behind, so it is refused');
        });

        on('b-patch-none', function () {
            send('PATCH', {}, '{"model":"T-999"}', 'no validator at all');
        });

        on('b-cond-get', function () {
            if (!needsTag('This action', current)) return;
            send('GET', { 'If-None-Match': current }, null, 'unchanged, so no body comes back');
        });

        on('b-delete', function () {
            if (!needsTag('This action', current)) return;
            send('DELETE', { 'If-Match': current }, null, 'soft delete, so the row survives');
        });

        on('b-get-gone', function () {
            send('GET', null, null, 'a soft-deleted resource is absent over HTTP');
        });

        on('b-restore', function () {
            send('PUT', {}, '{"name":"Thermostat","model":"T-100"}', 'a soft-deleted resource is exempt from If-Match');
        });

        on('b-clear', function () {
            rows().innerHTML = '';
            $('empty').style.display = '';
        });
        """;
}