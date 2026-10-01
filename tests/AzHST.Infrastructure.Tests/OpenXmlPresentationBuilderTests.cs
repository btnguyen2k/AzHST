using System.Text;
using AzHST.Application.Models;
using DocumentFormat.OpenXml.Packaging;

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

        var artifact = await builder.BuildAsync(CreatePlan(), visualization);

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

        var first = await builder.BuildAsync(CreatePlan(), visualization);
        var firstLength = new FileInfo(first.FilePath).Length;
        var changedPlan = CreatePlan();
        changedPlan.Title = "Updated Azure Application Gateway";

        var second = await builder.BuildAsync(changedPlan, visualization);

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
