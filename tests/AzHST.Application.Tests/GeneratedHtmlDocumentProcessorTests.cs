using AzHST.Application.Exceptions;
using AzHST.Application.Services;

namespace AzHST.Application.Tests;

public sealed class GeneratedHtmlDocumentProcessorTests
{
    private readonly GeneratedHtmlDocumentProcessor _processor = new();

    [Fact]
    public void Process_AcceptsFencedHtmlAndAddsSecurityMetadata()
    {
        var response = $"Here is the page:{Environment.NewLine}```html{Environment.NewLine}"
            + CreateDocument()
            + $"{Environment.NewLine}```";

        var result = _processor.Process(response);

        Assert.StartsWith("<!doctype html>", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content-Security-Policy", result);
        Assert.Contains("connect-src 'none'", result);
        Assert.Contains("<script>", result);
    }

    [Fact]
    public void Process_ReplacesModelProvidedContentSecurityPolicy()
    {
        var response = CreateDocument(
            headMarkup:
                """<meta http-equiv="Content-Security-Policy" content="default-src *">""");

        var result = _processor.Process(response);

        Assert.DoesNotContain("default-src *", result);
        Assert.Contains("default-src 'none'", result);
    }

    [Fact]
    public void Process_RemovesBaseElement()
    {
        var response = CreateDocument(headMarkup: """<base href="/">""");

        var result = _processor.Process(response);

        Assert.DoesNotContain("<base", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""<script src="https://example.com/app.js"></script>""")]
    [InlineData("""<a href="javascript:alert(1)">Open</a>""")]
    [InlineData("""<style>.hero { background: url(//example.com/image.png); }</style>""")]
    public void Process_RejectsExternalOrActiveReferences(string unsafeMarkup)
    {
        var response = CreateDocument(bodyMarkup: unsafeMarkup);

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
    public void Process_RejectsPageWithoutJavaScriptInteraction()
    {
        var response = CreateDocument(includeScript: false);

        var exception = Assert.Throws<VisualizationGenerationException>(
            () => _processor.Process(response));

        Assert.Contains("interactive controls", exception.Message);
    }

    [Fact]
    public void Process_RejectsPageWithoutSemanticControl()
    {
        var response = CreateDocument(includeControl: false);

        var exception = Assert.Throws<VisualizationGenerationException>(
            () => _processor.Process(response));

        Assert.Contains("interactive controls", exception.Message);
    }

    [Fact]
    public void Process_RejectsPageWithoutPurposefulMotion()
    {
        var response = CreateDocument(includeMotion: false);

        var exception = Assert.Throws<VisualizationGenerationException>(
            () => _processor.Process(response));

        Assert.Contains("animation or motion", exception.Message);
    }

    [Fact]
    public void Process_RejectsPageWithoutReducedMotionSupport()
    {
        var response = CreateDocument(includeReducedMotion: false);

        var exception = Assert.Throws<VisualizationGenerationException>(
            () => _processor.Process(response));

        Assert.Contains("reduced-motion", exception.Message);
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

    private static string CreateDocument(
        string headMarkup = "",
        string bodyMarkup = "",
        bool includeScript = true,
        bool includeControl = true,
        bool includeMotion = true,
        bool includeReducedMotion = true)
    {
        var motion = includeMotion ? "transition: transform 200ms ease;" : string.Empty;
        var reducedMotion = includeReducedMotion
            ? """
              @media (prefers-reduced-motion: reduce) {
                .node { transition: none; }
              }
              """
            : string.Empty;
        var control = includeControl
            ? """<button id="next" type="button">Next step</button>"""
            : string.Empty;
        var script = includeScript
            ? """
              <script>
                document.querySelector("#next")?.addEventListener("click", () => {
                  document.querySelector(".node")?.classList.toggle("active");
                });
              </script>
              """
            : string.Empty;

        return $$"""
            <!doctype html>
            <html>
            <head>
              {{headMarkup}}
              <title>Result</title>
              <style>
                .node { {{motion}} }
                {{reducedMotion}}
              </style>
            </head>
            <body>
              <main>
                {{control}}
                <div class="node">Safe visualization</div>
                {{bodyMarkup}}
              </main>
              {{script}}
            </body>
            </html>
            """;
    }
}
