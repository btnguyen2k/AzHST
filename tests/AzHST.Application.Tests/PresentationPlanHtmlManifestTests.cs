using AzHST.Application.Services;

namespace AzHST.Application.Tests;

public sealed class PresentationPlanHtmlManifestTests
{
    private readonly PresentationPlanHtmlManifest _manifest = new();

    [Fact]
    public void TryExtract_ReadsPresentationPlanFromHtml()
    {
        var plan = _manifest.TryExtract(CreateDocument(CreateManifest()));

        Assert.NotNull(plan);
        Assert.Equal("Application Gateway request flow", plan.Title);
        var source = Assert.Single(plan.Sources);
        Assert.Equal("Application Gateway documentation", source.Title);
        Assert.Equal(
            "https://learn.microsoft.com/azure/application-gateway/",
            source.Url);
        var slide = Assert.Single(plan.Slides);
        Assert.Equal("How requests are processed", slide.SectionTitle);
        Assert.Equal("Request path", slide.Title);
        Assert.Equal("Listener accepts HTTPS", slide.Subtitle);
        Assert.Equal(
            "Important: frontend and backend TLS are separate.",
            slide.Callout);
        Assert.Equal("listener", Assert.Single(plan.Slides[0].Nodes).Id);
    }

    [Fact]
    public void TryExtract_ReturnsNullWhenManifestIsAbsent()
    {
        var plan = _manifest.TryExtract(
            "<!doctype html><html><body><h1>Page</h1></body></html>");

        Assert.Null(plan);
    }

    [Theory]
    [InlineData(
        """<script id="azh-presentation-plan" type="text/javascript">{}</script>""",
        "application/json")]
    [InlineData(
        """<script id="azh-presentation-plan" type="application/json">{not-json}</script>""",
        "valid JSON")]
    public void ExtractRequired_RejectsInvalidManifest(
        string manifest,
        string expectedMessage)
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => _manifest.ExtractRequired(CreateDocument(manifest)));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public void ExtractRequired_RejectsDuplicateManifest()
    {
        var manifest = CreateManifest();

        var exception = Assert.Throws<InvalidDataException>(
            () => _manifest.ExtractRequired(
                CreateDocument($"{manifest}{manifest}")));

        Assert.Contains("more than one", exception.Message);
    }

    private static string CreateDocument(string body)
    {
        return $"<!doctype html><html><body>{body}</body></html>";
    }

    private static string CreateManifest()
    {
        return """
            <script id="azh-presentation-plan" type="application/json">
            {
              "title": "Application Gateway request flow",
              "subtitle": "Follow an HTTPS request",
              "sources": [
                {
                  "title": "Application Gateway documentation",
                  "url": "https://learn.microsoft.com/azure/application-gateway/"
                }
              ],
              "slides": [
                {
                  "kind": "diagram",
                  "sectionTitle": "How requests are processed",
                  "title": "Request path",
                  "subtitle": "Listener accepts HTTPS",
                  "summary": "The listener accepts the request.",
                  "callout": "Important: frontend and backend TLS are separate.",
                  "bullets": [],
                  "nodes": [
                    {
                      "id": "listener",
                      "label": "HTTPS listener",
                      "detail": "Terminates client TLS.",
                      "iconKey": "",
                      "tone": "primary"
                    }
                  ],
                  "connections": [],
                  "sources": ["Microsoft Learn"]
                }
              ]
            }
            </script>
            """;
    }
}
