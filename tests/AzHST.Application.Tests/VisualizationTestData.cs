using AzHST.Application.Models;

namespace AzHST.Application.Tests;

internal static class VisualizationTestData
{
    public const string ValidHtml = """
        <!doctype html>
        <html>
        <head>
          <title>Azure Functions</title>
          <style>
            .active { transition: transform 200ms ease; }
            @media (prefers-reduced-motion: reduce) {
              .active { transition: none; }
            }
          </style>
        </head>
        <body>
          <main>
            <button id="next" type="button">Next step</button>
            <div id="stage">Visualization</div>
            <section id="sources">
              <h2>Sources</h2>
              <a data-azh-source href="https://learn.microsoft.com/azure/azure-functions/" target="_blank" rel="noopener noreferrer">Azure Functions documentation</a>
            </section>
          </main>
          <script id="azh-presentation-plan" type="application/json">
          {
            "title": "Azure Functions",
            "subtitle": "Safe visualization",
            "sources": [
              {
                "title": "Azure Functions documentation",
                "url": "https://learn.microsoft.com/azure/azure-functions/"
              }
            ],
            "slides": [
              {
                "kind": "diagram",
                "title": "Request flow",
                "summary": "Follow the request.",
                "bullets": [],
                "nodes": [
                  { "id": "source", "label": "Source", "detail": "Starts work.", "iconKey": "", "tone": "neutral" },
                  { "id": "function", "label": "Function", "detail": "Processes work.", "iconKey": "", "tone": "primary" }
                ],
                "connections": [
                  { "from": "source", "to": "function", "label": "invoke" }
                ],
                "sources": []
              },
              {
                "kind": "cards",
                "title": "Behavior",
                "summary": "Visible behavior.",
                "bullets": [],
                "nodes": [
                  { "id": "trigger", "label": "Trigger", "detail": "Starts execution.", "iconKey": "", "tone": "primary" },
                  { "id": "binding", "label": "Binding", "detail": "Connects data.", "iconKey": "", "tone": "accent" }
                ],
                "connections": [],
                "sources": []
              },
              {
                "kind": "cards",
                "title": "Operations",
                "summary": "Operational guidance.",
                "bullets": [],
                "nodes": [
                  { "id": "monitor", "label": "Monitor", "detail": "Observe executions.", "iconKey": "", "tone": "success" },
                  { "id": "retry", "label": "Retry", "detail": "Handle failures.", "iconKey": "", "tone": "warning" }
                ],
                "connections": [],
                "sources": []
              },
              {
                "kind": "cards",
                "title": "Validate before production",
                "summary": "Confirm current details.",
                "bullets": [],
                "nodes": [
                  { "id": "availability", "label": "Availability", "detail": "Confirm regional support.", "iconKey": "", "tone": "warning" },
                  { "id": "pricing", "label": "Pricing", "detail": "Review current pricing.", "iconKey": "", "tone": "warning" }
                ],
                "connections": [],
                "sources": ["Microsoft Learn"]
              }
            ]
          }
          </script>
          <script>
            document.querySelector("#next").addEventListener("click", () => {
              document.querySelector("#stage").classList.toggle("active");
            });
          </script>
        </body>
        </html>
        """;

    public static HtmlThemeDefinition CreateHtmlTheme(
        string id = OutputThemeSettings.DefaultHtmlThemeId)
    {
        return new HtmlThemeDefinition
        {
            SchemaVersion = 1,
            Id = id,
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
}
