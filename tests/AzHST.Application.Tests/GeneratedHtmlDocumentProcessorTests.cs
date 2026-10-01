using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;
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
    public void Process_AppliesConfiguredThemeAndReplacesModelThemeMarkup()
    {
        var response = CreateDocument(
                headMarkup:
                    """<style id="azh-output-theme">body { color: hotpink; }</style>""")
            .Replace(
                "<html>",
                """<html data-azh-theme="model-theme">""",
                StringComparison.Ordinal);

        var result = _processor.Process(response, CreateHtmlTheme());

        Assert.Contains("data-azh-theme=\"azure-night\"", result);
        Assert.Contains("""<style id="azh-output-theme">""", result);
        Assert.Contains("--azh-page: #071426;", result);
        Assert.Contains("--azh-transition-duration: 220ms;", result);
        Assert.Contains("color-scheme: dark;", result);
        Assert.DoesNotContain("model-theme", result);
        Assert.DoesNotContain("hotpink", result);
        Assert.Equal(
            1,
            result.Split("id=\"azh-output-theme\"", StringSplitOptions.None)
                .Length - 1);
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
    public void Process_EmbedsApprovedAzureIconAsDataImage()
    {
        const string key = "networking/10076-application-gateways";
        const string dataUri = "data:image/svg+xml;base64,PHN2Zy8+";
        var processor = new GeneratedHtmlDocumentProcessor(
            new StubAzureIconCatalog(key, dataUri));
        var response = CreateDocument(
            bodyMarkup:
                $"""<img class="service-icon" data-azure-icon="{key}" alt="Azure Application Gateway">""");

        var result = processor.Process(response);

        Assert.Contains($"data-azure-icon-resolved=\"{key}\"", result);
        Assert.Contains($"src=\"{dataUri}\"", result);
        Assert.DoesNotContain("data-azure-icon=", result);
    }

    [Fact]
    public void Process_RejectsUnavailableAzureIcon()
    {
        var response = CreateDocument(
            bodyMarkup:
                """<img data-azure-icon="networking/unknown" alt="Unknown service">""");

        var exception = Assert.Throws<VisualizationGenerationException>(
            () => _processor.Process(response));

        Assert.Contains("unavailable Azure icon", exception.Message);
    }

    [Fact]
    public void Process_RejectsAzureIconWithoutAltText()
    {
        const string key = "networking/10076-application-gateways";
        var processor = new GeneratedHtmlDocumentProcessor(
            new StubAzureIconCatalog(key, "data:image/svg+xml;base64,PHN2Zy8+"));
        var response = CreateDocument(
            bodyMarkup: $"""<img data-azure-icon="{key}">""");

        var exception = Assert.Throws<VisualizationGenerationException>(
            () => processor.Process(response));

        Assert.Contains("descriptive alt", exception.Message);
    }

    [Fact]
    public void Process_RejectsAzureIconPlaceholderWithSource()
    {
        const string key = "networking/10076-application-gateways";
        var processor = new GeneratedHtmlDocumentProcessor(
            new StubAzureIconCatalog(key, "data:image/svg+xml;base64,PHN2Zy8+"));
        var response = CreateDocument(
            bodyMarkup:
                $"""<img data-azure-icon="{key}" src="icon.svg" alt="Azure Application Gateway">""");

        var exception = Assert.Throws<VisualizationGenerationException>(
            () => processor.Process(response));

        Assert.Contains("must not provide", exception.Message);
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

    [Fact]
    public void Process_RejectsDocumentThatExceedsLimitAfterThemeInjection()
    {
        const string paddingToken = "THEME_PADDING";
        var template = CreateDocument(
            bodyMarkup: $"<!--{paddingToken}-->").Trim();
        var targetLength =
            GeneratedHtmlDocumentProcessor.MaximumDocumentLength - 1;
        var paddingLength =
            targetLength - template.Length + paddingToken.Length;
        var response = template.Replace(
            paddingToken,
            new string('x', paddingLength),
            StringComparison.Ordinal);
        Assert.Equal(targetLength, response.Length);

        var exception = Assert.Throws<VisualizationGenerationException>(
            () => _processor.Process(response, CreateHtmlTheme()));

        Assert.Contains("after final processing", exception.Message);
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

    private static HtmlThemeDefinition CreateHtmlTheme()
    {
        return new HtmlThemeDefinition
        {
            SchemaVersion = 1,
            Id = "azure-night",
            DisplayName = "Azure Night",
            Palette = new HtmlThemePalette
            {
                Page = "#071426",
                Surface = "#0D1B2A",
                SurfaceRaised = "#12263D",
                SurfaceSelected = "#173554",
                Border = "#29415F",
                Text = "#F3F7FC",
                TextMuted = "#A9B8CC",
                Primary = "#4CA6FF",
                ActiveFlow = "#22D3EE",
                Accent = "#7DD3FC",
                Success = "#34D399",
                Warning = "#FBBF24",
                Danger = "#FB7185",
                Focus = "#93C5FD",
                IconTile = "#EAF4FC",
            },
            Typography = new HtmlThemeTypography
            {
                FontFamily = "Inter, Segoe UI, sans-serif",
                BaseSizePixels = 16,
                LineHeight = 1.5,
            },
            Appearance = new HtmlThemeAppearance
            {
                ColorScheme = HtmlColorScheme.Dark,
                SurfaceStyle = HtmlSurfaceStyle.Layered,
                CornerRadiusPixels = 14,
                IconTileSizePixels = 44,
                AllowPureBlack = false,
                AllowGlassmorphism = false,
            },
            Motion = new HtmlThemeMotion
            {
                TransitionDurationMilliseconds = 220,
                SequenceStepDurationMilliseconds = 650,
                AllowContinuousDecorativeMotion = false,
            },
        };
    }

    private sealed class StubAzureIconCatalog(
        string? availableKey = null,
        string? dataUri = null) : IAzureIconCatalog
    {
        public IReadOnlyList<AzureIconDescriptor> FindRelevant(
            string query,
            int maximumResults)
        {
            return [];
        }

        public bool TryGetDataUri(string key, out string resolvedDataUri)
        {
            if (string.Equals(key, availableKey, StringComparison.OrdinalIgnoreCase))
            {
                resolvedDataUri = dataUri ?? string.Empty;
                return true;
            }

            resolvedDataUri = string.Empty;
            return false;
        }
    }
}
