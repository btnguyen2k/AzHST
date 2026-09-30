using AzHST.Application.Exceptions;
using AzHST.Application.Services;

namespace AzHST.Application.Tests;

public sealed class GeneratedHtmlDocumentProcessorTests
{
    private readonly GeneratedHtmlDocumentProcessor _processor = new();

    [Fact]
    public void Process_AcceptsFencedHtmlAndAddsSecurityMetadata()
    {
        var response = """
            Here is the page:
            ```html
            <html>
            <head><title>Result</title></head>
            <body><script>document.body.dataset.ready = "true";</script></body>
            </html>
            ```
            """;

        var result = _processor.Process(response);

        Assert.StartsWith("<!doctype html>", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content-Security-Policy", result);
        Assert.Contains("connect-src 'none'", result);
        Assert.Contains("<script>", result);
    }

    [Fact]
    public void Process_ReplacesModelProvidedContentSecurityPolicy()
    {
        var response = """
            <!doctype html>
            <html>
            <head>
              <meta http-equiv="Content-Security-Policy" content="default-src *">
              <title>Result</title>
            </head>
            <body>Safe</body>
            </html>
            """;

        var result = _processor.Process(response);

        Assert.DoesNotContain("default-src *", result);
        Assert.Contains("default-src 'none'", result);
    }

    [Fact]
    public void Process_RemovesBaseElement()
    {
        var response = """
            <!doctype html>
            <html>
            <head><base href="/"><title>Result</title></head>
            <body>Safe</body>
            </html>
            """;

        var result = _processor.Process(response);

        Assert.DoesNotContain("<base", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""<script src="https://example.com/app.js"></script>""")]
    [InlineData("""<a href="javascript:alert(1)">Open</a>""")]
    [InlineData("""<style>.hero { background: url(//example.com/image.png); }</style>""")]
    public void Process_RejectsExternalOrActiveReferences(string unsafeMarkup)
    {
        var response = $"""
            <!doctype html>
            <html>
            <head><title>Result</title></head>
            <body>{unsafeMarkup}</body>
            </html>
            """;

        var exception = Assert.Throws<VisualizationGenerationException>(
            () => _processor.Process(response));

        Assert.Contains("external content", exception.Message);
    }

    [Fact]
    public void Process_RejectsIncompleteDocument()
    {
        var response = "<html><head><title>Result</title></head></html>";

        var exception = Assert.Throws<VisualizationGenerationException>(
            () => _processor.Process(response));

        Assert.Contains("complete HTML", exception.Message);
    }

    [Fact]
    public void Process_RejectsOversizedDocument()
    {
        var payload = new string('x', GeneratedHtmlDocumentProcessor.MaximumDocumentLength);
        var response = $"<html><head><title>Result</title></head><body>{payload}</body></html>";

        var exception = Assert.Throws<VisualizationGenerationException>(
            () => _processor.Process(response));

        Assert.Contains("safety limit", exception.Message);
    }
}
