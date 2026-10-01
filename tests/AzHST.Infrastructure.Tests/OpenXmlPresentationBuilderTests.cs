using System.Text;
using AzHST.Application.Models;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AzHST.Infrastructure.Tests;

public sealed class OpenXmlPresentationBuilderTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "AzHST.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task BuildAsync_CreatesPresentationWithNativeSlidesAndAzureIcon()
    {
        var iconDirectory = Path.Combine(_testDirectory, "icons");
        CreateIcon(iconDirectory);
        var catalog = new FileAzureIconCatalog(iconDirectory);
        var builder = new OpenXmlPresentationBuilder(catalog);
        var visualization = CreateVisualization();

        var artifact = await builder.BuildAsync(
            CreatePlan(),
            visualization,
            CreateTheme());

        Assert.Equal(4, artifact.SlideCount);
        Assert.Equal(
            Path.Combine(visualization.DirectoryPath, "presentation.pptx"),
            artifact.FilePath);
        Assert.True(File.Exists(artifact.FilePath));
        Assert.True(artifact.FileUri.IsFile);

        using var document = PresentationDocument.Open(artifact.FilePath, false);
        var presentationPart = Assert.IsType<PresentationPart>(
            document.PresentationPart);
        Assert.Equal(4, presentationPart.SlideParts.Count());
        Assert.Single(
            presentationPart.SlideParts.SelectMany(slide => slide.ImageParts));

        var text = string.Join(
            " ",
            presentationPart.SlideParts
                .SelectMany(GetSlideText)
                .Select(item => item.Text));
        Assert.Contains("Azure Application Gateway", text);
        Assert.Contains("Validate before production", text);
    }

    [Fact]
    public async Task BuildAsync_ReplacesExistingPresentationAtomically()
    {
        var iconDirectory = Path.Combine(_testDirectory, "icons");
        CreateIcon(iconDirectory);
        var builder = new OpenXmlPresentationBuilder(
            new FileAzureIconCatalog(iconDirectory));
        var visualization = CreateVisualization();

        var first = await builder.BuildAsync(
            CreatePlan(),
            visualization,
            CreateTheme());
        var firstLength = new FileInfo(first.FilePath).Length;
        var changedPlan = CreatePlan();
        changedPlan.Title = "Updated Azure Application Gateway";

        var second = await builder.BuildAsync(
            changedPlan,
            visualization,
            CreateTheme());

        Assert.Equal(first.FilePath, second.FilePath);
        Assert.NotEqual(0, new FileInfo(second.FilePath).Length);
        using var document = PresentationDocument.Open(second.FilePath, false);
        var text = string.Join(
            " ",
            document.PresentationPart!.SlideParts
                .SelectMany(GetSlideText)
                .Select(item => item.Text));
        Assert.Contains("Updated Azure Application Gateway", text);
        Assert.True(firstLength > 0);
    }

    [Fact]
    public async Task BuildAsync_UsesProfessionalLightTheme()
    {
        var iconDirectory = Path.Combine(_testDirectory, "icons");
        CreateIcon(iconDirectory);
        var builder = new OpenXmlPresentationBuilder(
            new FileAzureIconCatalog(iconDirectory));

        var artifact = await builder.BuildAsync(
            CreatePlan(),
            CreateVisualization(),
            CreateTheme());

        using var document = PresentationDocument.Open(artifact.FilePath, false);
        var presentationPart = Assert.IsType<PresentationPart>(
            document.PresentationPart);
        var titleSlide = Assert.Single(
            presentationPart.SlideParts,
            slide => GetSlideText(slide)
                .Any(text => text.Text == "Request routing and protection"));
        var contentSlide = Assert.Single(
            presentationPart.SlideParts,
            slide => GetSlideText(slide)
                .Any(text => text.Text == "Executive overview"));

        Assert.Contains("F4F7FB", titleSlide.Slide!.OuterXml);
        Assert.Contains("0F2744", titleSlide.Slide.OuterXml);
        Assert.DoesNotContain("0B1020", titleSlide.Slide.OuterXml);
        Assert.Contains("F4F7FB", contentSlide.Slide!.OuterXml);
        Assert.Contains("0078D4", contentSlide.Slide.OuterXml);
    }

    [Fact]
    public async Task BuildAsync_AppliesConfiguredPaletteAndFonts()
    {
        var iconDirectory = Path.Combine(_testDirectory, "icons");
        CreateIcon(iconDirectory);
        var builder = new OpenXmlPresentationBuilder(
            new FileAzureIconCatalog(iconDirectory));
        var defaultTheme = CreateTheme();
        var configuredTheme = defaultTheme with
        {
            Palette = defaultTheme.Palette with
            {
                Canvas = "#FAFBFC",
                Primary = "#123456",
            },
            Typography = defaultTheme.Typography with
            {
                FontFamily = "Arial",
                DisplayFontFamily = "Georgia",
            },
        };

        var artifact = await builder.BuildAsync(
            CreatePlan(),
            CreateVisualization(),
            configuredTheme);

        using var document = PresentationDocument.Open(artifact.FilePath, false);
        var presentationPart = Assert.IsType<PresentationPart>(
            document.PresentationPart);
        var slideXml = string.Join(
            Environment.NewLine,
            presentationPart.SlideParts.Select(slide => slide.Slide!.OuterXml));
        var themeXml = presentationPart.SlideMasterParts
            .Select(part => part.ThemePart?.Theme?.OuterXml)
            .First(xml => xml is not null);

        Assert.Contains("FAFBFC", slideXml);
        Assert.Contains("123456", slideXml);
        Assert.Contains("typeface=\"Arial\"", themeXml);
        Assert.Contains("typeface=\"Georgia\"", themeXml);
        Assert.Contains("typeface=\"Arial\"", slideXml);
        Assert.Contains("typeface=\"Georgia\"", slideXml);
    }

    [Fact]
    public async Task BuildAsync_ConnectsAdjacentDiagramNodesAtTheirEdges()
    {
        var iconDirectory = Path.Combine(_testDirectory, "icons");
        CreateIcon(iconDirectory);
        var builder = new OpenXmlPresentationBuilder(
            new FileAzureIconCatalog(iconDirectory));

        var artifact = await builder.BuildAsync(
            CreatePlan(),
            CreateVisualization(),
            CreateTheme());

        using var document = PresentationDocument.Open(artifact.FilePath, false);
        var presentationPart = Assert.IsType<PresentationPart>(
            document.PresentationPart);
        var diagramSlide = Assert.Single(
            presentationPart.SlideParts,
            slide => GetSlideText(slide)
                .Any(text => text.Text == "Request flow"));
        var connectors = diagramSlide.Slide!
            .Descendants<P.ConnectionShape>()
            .ToArray();

        Assert.Equal(2, connectors.Length);
        Assert.All(
            connectors,
            connector =>
            {
                var transform = Assert.IsType<A.Transform2D>(
                    connector.ShapeProperties!.Transform2D);
                var extents = Assert.IsType<A.Extents>(transform.Extents);
                var outline = Assert.IsType<A.Outline>(
                    connector.ShapeProperties.GetFirstChild<A.Outline>());
                var headEnd = Assert.IsType<A.HeadEnd>(
                    outline.GetFirstChild<A.HeadEnd>());
                var tailEnd = Assert.IsType<A.TailEnd>(
                    outline.GetFirstChild<A.TailEnd>());

                Assert.InRange(extents.Cx!.Value, 1, 300_000);
                Assert.Equal(A.LineEndValues.None, headEnd.Type!.Value);
                Assert.Equal(A.LineEndValues.Triangle, tailEnd.Type!.Value);
            });
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private VisualizationArtifact CreateVisualization()
    {
        var directory = Path.Combine(
            _testDirectory,
            "generated",
            "0199abcd1234-application-gateway");
        Directory.CreateDirectory(directory);
        var htmlPath = Path.Combine(directory, "index.html");
        File.WriteAllText(htmlPath, "<!doctype html><html></html>");

        return new VisualizationArtifact(
            "0199abcd1234-application-gateway",
            directory,
            htmlPath,
            new Uri(htmlPath, UriKind.Absolute));
    }

    private static PresentationPlan CreatePlan()
    {
        return new PresentationPlan
        {
            Title = "Azure Application Gateway",
            Subtitle = "Request routing and protection",
            Slides =
            [
                new PresentationSlidePlan
                {
                    Kind = "content",
                    Title = "Executive overview",
                    Summary = "Application Gateway is a regional layer 7 load balancer.",
                    Bullets =
                    [
                        "Terminates TLS and evaluates routing rules.",
                        "Can apply Web Application Firewall policies.",
                    ],
                    Sources = ["Microsoft Learn"],
                },
                new PresentationSlidePlan
                {
                    Kind = "diagram",
                    Title = "Request flow",
                    Summary = "A request passes through the gateway before reaching the backend.",
                    Nodes =
                    [
                        new PresentationNodePlan
                        {
                            Id = "client",
                            Label = "Client",
                            Detail = "Sends an HTTPS request.",
                        },
                        new PresentationNodePlan
                        {
                            Id = "gateway",
                            Label = "Azure Application Gateway",
                            Detail = "Evaluates WAF and routing rules.",
                            IconKey = "networking/10076-application-gateways",
                        },
                        new PresentationNodePlan
                        {
                            Id = "backend",
                            Label = "Backend pool",
                            Detail = "Receives healthy routed traffic.",
                        },
                    ],
                    Connections =
                    [
                        new PresentationConnectionPlan
                        {
                            From = "client",
                            To = "gateway",
                            Label = "HTTPS",
                        },
                        new PresentationConnectionPlan
                        {
                            From = "gateway",
                            To = "backend",
                            Label = "HTTP(S)",
                        },
                    ],
                    Sources = ["Azure Architecture Center"],
                },
                new PresentationSlidePlan
                {
                    Kind = "summary",
                    Title = "Validate before production",
                    Summary = "Confirm design assumptions against current Azure guidance.",
                    Bullets =
                    [
                        "Check regional and SKU availability.",
                        "Validate quotas, pricing, and WAF policy behavior.",
                    ],
                    Sources = ["Microsoft Learn"],
                },
            ],
        };
    }

    private static PresentationThemeDefinition CreateTheme()
    {
        return new PresentationThemeDefinition
        {
            SchemaVersion = 1,
            Id = "professional-light",
            DisplayName = "Professional Light",
            Palette = new PresentationThemePalette
            {
                Canvas = "#F4F7FB",
                Surface = "#FFFFFF",
                SummarySurface = "#EAF4FC",
                Title = "#0F2744",
                Text = "#172033",
                TextMuted = "#526176",
                Border = "#CBD6E2",
                Primary = "#0078D4",
                Accent = "#009FDA",
                IconTile = "#EAF4FC",
                Success = "#14B8A6",
                Warning = "#F59E0B",
                Comparison = "#8B5CF6",
                Danger = "#EF4444",
                Hyperlink = "#0563C1",
                FollowedHyperlink = "#954F72",
            },
            Typography = new PresentationThemeTypography
            {
                FontFamily = "Aptos",
                DisplayFontFamily = "Aptos Display",
                TitleSizePoints = 32,
                SubtitleSizePoints = 17,
                SlideTitleSizePoints = 20,
                SummarySizePoints = 15,
                BodySizePoints = 17,
                NodeLabelSizePoints = 11,
                NodeDetailSizePoints = 8,
                ConnectionLabelSizePoints = 7,
                FooterSizePoints = 7,
            },
            Appearance = new PresentationThemeAppearance
            {
                RoundedCards = true,
                ShowHeaderRule = true,
                IconTreatment = PresentationIconTreatment.LightTile,
            },
        };
    }

    private static void CreateIcon(string iconDirectory)
    {
        var directory = Path.Combine(iconDirectory, "networking");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(
                directory,
                "10076-icon-service-Application-Gateways.svg"),
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 18 18">
              <path d="M1 1h16v16H1z" fill="#0078d4" />
            </svg>
            """,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static IEnumerable<DocumentFormat.OpenXml.Drawing.Text> GetSlideText(
        SlidePart slidePart)
    {
        return (slidePart.Slide
                ?? throw new InvalidOperationException("Test slide is missing."))
            .Descendants<DocumentFormat.OpenXml.Drawing.Text>();
    }
}
