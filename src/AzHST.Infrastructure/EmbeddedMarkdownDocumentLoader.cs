using System.Net;
using System.Reflection;
using System.Text;
using Markdig;

namespace AzHST.Infrastructure;

public sealed record EmbeddedMarkdownDocument(
    string Markdown,
    string Html);

public sealed class EmbeddedMarkdownDocumentLoader
{
    private static readonly MarkdownPipeline MarkdownPipeline =
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

    public EmbeddedMarkdownDocument Load(
        Assembly assembly,
        string resourceName,
        string documentTitle)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentTitle);

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"The embedded document '{resourceName}' was not found in assembly '{assembly.GetName().Name}'.");
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        var markdown = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new InvalidOperationException(
                $"The embedded document '{resourceName}' is empty.");
        }

        var body = Markdown.ToHtml(markdown, MarkdownPipeline);
        return new EmbeddedMarkdownDocument(
            markdown,
            BuildHtmlDocument(documentTitle, body));
    }

    private static string BuildHtmlDocument(
        string documentTitle,
        string body)
    {
        var encodedTitle = WebUtility.HtmlEncode(documentTitle.Trim());

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta http-equiv="Content-Security-Policy" content="default-src 'none'; base-uri 'none'; connect-src 'none'; font-src 'none'; form-action 'none'; frame-src 'none'; img-src data:; object-src 'none'; script-src 'none'; style-src 'unsafe-inline'">
              <meta name="referrer" content="no-referrer">
              <title>{{encodedTitle}}</title>
              <style>
                :root {
                  color-scheme: dark;
                  font-family: Inter, "Segoe UI", sans-serif;
                  background: #0b1020;
                  color: #dce7ff;
                }
                * {
                  box-sizing: border-box;
                }
                body {
                  margin: 0;
                  background: #0b1020;
                  color: #dce7ff;
                  font-size: 15px;
                  line-height: 1.65;
                }
                main {
                  max-width: 980px;
                  margin: 0 auto;
                  padding: 28px 34px 44px;
                }
                h1, h2, h3, h4, h5, h6 {
                  margin: 1.4em 0 0.55em;
                  color: #f5f8ff;
                  line-height: 1.25;
                }
                h1 {
                  margin-top: 0;
                  font-size: 2rem;
                }
                h2 {
                  padding-bottom: 0.32em;
                  border-bottom: 1px solid #2b3a61;
                  font-size: 1.45rem;
                }
                h3 {
                  font-size: 1.15rem;
                }
                p, ul, ol, table, pre, blockquote {
                  margin: 0.75em 0 1em;
                }
                a {
                  color: #60a5fa;
                  text-decoration-thickness: 1px;
                  text-underline-offset: 3px;
                }
                a:focus-visible {
                  outline: 2px solid #93c5fd;
                  outline-offset: 3px;
                  border-radius: 3px;
                }
                strong {
                  color: #f5f8ff;
                }
                code {
                  padding: 0.16em 0.38em;
                  border: 1px solid #253454;
                  border-radius: 5px;
                  background: #121b31;
                  color: #bfdbfe;
                  font-family: "Cascadia Code", Consolas, monospace;
                }
                pre {
                  overflow-x: auto;
                  padding: 16px;
                  border: 1px solid #253454;
                  border-radius: 10px;
                  background: #080d18;
                }
                pre code {
                  padding: 0;
                  border: 0;
                  background: transparent;
                }
                table {
                  width: 100%;
                  border-collapse: collapse;
                }
                th, td {
                  padding: 9px 12px;
                  border: 1px solid #2b3a61;
                  text-align: left;
                  vertical-align: top;
                }
                th {
                  background: #17213b;
                  color: #f5f8ff;
                }
                tr:nth-child(even) td {
                  background: #10182c;
                }
                blockquote {
                  padding: 10px 16px;
                  border-left: 4px solid #2563eb;
                  background: #10182c;
                  color: #afc0df;
                }
                hr {
                  height: 1px;
                  border: 0;
                  background: #2b3a61;
                }
                li + li {
                  margin-top: 0.28em;
                }
              </style>
            </head>
            <body>
              <main>
            {{body}}
              </main>
            </body>
            </html>
            """;
    }
}
