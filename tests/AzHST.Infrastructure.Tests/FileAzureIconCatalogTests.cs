using System.Text;

namespace AzHST.Infrastructure.Tests;

public sealed class FileAzureIconCatalogTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "AzHST.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void FindRelevant_ReturnsMatchingServiceAndPreservesSvgBytes()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 18 18">
              <path d="M1 1h16v16H1z" fill="#0078d4" />
            </svg>
            """;
        CreateIcon(
            "networking",
            "10076-icon-service-Application-Gateways.svg",
            svg);
        CreateIcon(
            "storage",
            "10086-icon-service-Storage-Accounts.svg",
            """<svg xmlns="http://www.w3.org/2000/svg"><path d="M0 0h1v1z" /></svg>""");
        var catalog = new FileAzureIconCatalog(_testDirectory);

        var matches = catalog.FindRelevant(
            "How does Azure Application Gateway route requests?",
            maximumResults: 5);

        var icon = Assert.Single(
            matches,
            item => item.Key == "networking/10076-application-gateways");
        Assert.Equal("Application Gateways", icon.DisplayName);
        Assert.Equal("networking", icon.Category);
        Assert.True(catalog.TryGetDataUri(icon.Key, out var dataUri));
        Assert.StartsWith("data:image/svg+xml;base64,", dataUri);
        Assert.Equal(
            Encoding.UTF8.GetBytes(svg),
            Convert.FromBase64String(dataUri["data:image/svg+xml;base64,".Length..]));
    }

    [Fact]
    public void Constructor_RejectsSvgWithExecutableContent()
    {
        CreateIcon(
            "networking",
            "10076-icon-service-Application-Gateways.svg",
            """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""");

        var exception = Assert.Throws<InvalidOperationException>(
            () => new FileAzureIconCatalog(_testDirectory));

        Assert.Contains("unsupported SVG content", exception.Message);
    }

    [Fact]
    public void Constructor_RejectsSvgWithExternalReference()
    {
        CreateIcon(
            "networking",
            "10076-icon-service-Application-Gateways.svg",
            """<svg xmlns="http://www.w3.org/2000/svg"><use href="https://example.com/icon.svg" /></svg>""");

        var exception = Assert.Throws<InvalidOperationException>(
            () => new FileAzureIconCatalog(_testDirectory));

        Assert.Contains("external content", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private void CreateIcon(string category, string fileName, string svg)
    {
        var directory = Path.Combine(_testDirectory, category);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, fileName),
            svg,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
