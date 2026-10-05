using System.Diagnostics.CodeAnalysis;
using AzHST.Application.Abstractions;
using AzHST.Application.Models;

namespace AzHST.Infrastructure.Tests;

public sealed class CopilotVisualizationClientTests
{
    [Fact]
    public async Task CreatePresentationPlanAsync_UsesEmbeddedNarrative()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "AzHST.Tests",
            Guid.NewGuid().ToString("N"));
        var paths = new ApplicationPaths(
            root,
            Path.Combine(root, "settings.json"),
            Path.Combine(root, "generated"),
            Path.Combine(root, "copilot"),
            Path.Combine(root, "azhst.db"));
        var client = new CopilotVisualizationClient(
            paths,
            new EmptyAzureIconCatalog());
        var visualization = new VisualizationArtifact(
            "0199abcd1234-app-gateway",
            Path.Combine(root, "generated", "0199abcd1234-app-gateway"),
            Path.Combine(
                root,
                "generated",
                "0199abcd1234-app-gateway",
                "index.html"),
            new Uri(
                "file:///C:/generated/0199abcd1234-app-gateway/index.html"))
        {
            Html = """
                <!doctype html>
                <html>
                <body>
                  <h1>Application Gateway</h1>
                  <script id="azh-presentation-plan" type="application/json">
                  {
                    "title": "Application Gateway request flow",
                    "subtitle": "The same narrative as the HTML",
                    "sources": [
                      {
                        "title": "Application Gateway documentation",
                        "url": "https://learn.microsoft.com/azure/application-gateway/"
                      }
                    ],
                    "slides": [
                      {
                        "kind": "diagram",
                        "title": "Follow one request",
                        "summary": "The listener accepts TLS first.",
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
                        "sources": []
                      }
                    ]
                  }
                  </script>
                </body>
                </html>
                """,
        };

        var plan = await client.CreatePresentationPlanAsync(
            "Explain Application Gateway",
            CopilotModelSelection.Automatic,
            visualization);

        Assert.Equal("Application Gateway request flow", plan.Title);
        Assert.Equal(
            "https://learn.microsoft.com/azure/application-gateway/",
            Assert.Single(plan.Sources).Url);
        Assert.Equal("Follow one request", Assert.Single(plan.Slides).Title);
    }

    private sealed class EmptyAzureIconCatalog : IAzureIconCatalog
    {
        public IReadOnlyList<AzureIconDescriptor> FindRelevant(
            string query,
            int maximumResults)
        {
            return [];
        }

        public bool TryGet(
            string key,
            [NotNullWhen(true)] out AzureIconDescriptor? descriptor,
            out string dataUri)
        {
            descriptor = null;
            dataUri = string.Empty;
            return false;
        }
    }
}
