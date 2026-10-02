using System.Reflection;

namespace AzHST.Infrastructure.Tests;

public sealed class EmbeddedMarkdownDocumentLoaderTests
{
    [Fact]
    public void Load_ReadsEmbeddedMarkdownAndBuildsSecuredHtml()
    {
        var loader = new EmbeddedMarkdownDocumentLoader();

        var document = loader.Load(
            Assembly.GetExecutingAssembly(),
            "AzHST.Tests.EmbeddedDocument.md",
            "Embedded test");

        Assert.Contains("# Embedded document", document.Markdown);
        Assert.StartsWith("<!doctype html>", document.Html);
        Assert.Contains("<h1 id=\"embedded-document\">", document.Html);
        Assert.Contains("<strong>formatted text</strong>", document.Html);
        Assert.Contains("<table>", document.Html);
        Assert.Contains("Content-Security-Policy", document.Html);
        Assert.Contains("script-src 'none'", document.Html);
        Assert.DoesNotContain("<script>", document.Html);
    }

    [Fact]
    public void Load_RejectsMissingEmbeddedResource()
    {
        var loader = new EmbeddedMarkdownDocumentLoader();

        var exception = Assert.Throws<InvalidOperationException>(
            () => loader.Load(
                Assembly.GetExecutingAssembly(),
                "AzHST.Tests.Missing.md",
                "Missing"));

        Assert.Contains("was not found", exception.Message);
    }
}
