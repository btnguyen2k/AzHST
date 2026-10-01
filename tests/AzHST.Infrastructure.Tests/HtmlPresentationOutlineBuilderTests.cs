namespace AzHST.Infrastructure.Tests;

public sealed class HtmlPresentationOutlineBuilderTests
{
    [Fact]
    public void Build_PreservesPageStructureAndVisibleVisualLabels()
    {
        const string html = """
            <!doctype html>
            <html>
            <head>
              <title>Azure request flow</title>
              <style>.hidden { color: red; }</style>
            </head>
            <body>
              <main>
                <h1>How the request is processed</h1>
                <section aria-label="Interactive request path">
                  <h2>Request flow</h2>
                  <p>A client request passes through the gateway.</p>
                  <svg aria-label="Architecture diagram">
                    <title>Regional request path</title>
                    <text>Client</text>
                    <text>Application Gateway</text>
                  </svg>
                  <div data-node="gateway">
                    <img data-azure-icon-resolved="networking/app-gateway" alt="Application Gateway">
                    <strong>Application Gateway</strong>
                    <small>Routes healthy traffic</small>
                  </div>
                  <button>Show failover path</button>
                  <button data-choice="health" data-value="unhealthy">Unhealthy</button>
                </section>
                <table>
                  <tr><th>Option</th><th>Use when</th></tr>
                  <tr><td>WAF_v2</td><td>Autoscaling is required</td></tr>
                </table>
              </main>
              <script>
                const steps = [
                  {
                    node: "listener",
                    title: "Listener accepts TLS",
                    text: "The listener terminates the client TLS connection."
                  }
                ];
                scenarioCopy.textContent = "The unhealthy backend is excluded from new requests.";
                window.secret = "do not include";
              </script>
            </body>
            </html>
            """;

        var outline = new HtmlPresentationOutlineBuilder().Build(html);

        Assert.Contains("PAGE TITLE: Azure request flow", outline);
        Assert.Contains("H1: How the request is processed", outline);
        Assert.Contains("SECTION: Interactive request path", outline);
        Assert.Contains("H2: Request flow", outline);
        Assert.Contains("TEXT: A client request passes through the gateway.", outline);
        Assert.Contains("DIAGRAM: Architecture diagram", outline);
        Assert.Contains(
            "DIAGRAM LABELS: Regional request path | Client | Application Gateway",
            outline);
        Assert.Contains(
            "VISUAL NODE: gateway | Application Gateway | Routes healthy traffic | networking/app-gateway",
            outline);
        Assert.Contains("CONTROL: Show failover path", outline);
        Assert.Contains(
            "SCENARIO OPTION: health=unhealthy | Unhealthy",
            outline);
        Assert.Contains("TABLE ROW: Option | Use when", outline);
        Assert.Contains("TABLE ROW: WAF_v2 | Autoscaling is required", outline);
        Assert.Contains(
            "INTERACTION STEP: listener | Listener accepts TLS | The listener terminates the client TLS connection.",
            outline);
        Assert.Contains(
            "INTERACTION STATE: The unhealthy backend is excluded from new requests.",
            outline);
        Assert.DoesNotContain("hidden", outline);
        Assert.DoesNotContain("window.secret", outline);
    }

    [Fact]
    public void Build_BoundsLargeVisibleContent()
    {
        var paragraphs = string.Concat(
            Enumerable.Range(0, 200)
                .Select(index =>
                    $"<p>{index:D3}-{new string('x', 2_000)}</p>"));
        var html = $"""
            <!doctype html>
            <html>
            <head><title>Large page</title></head>
            <body>
              {paragraphs}
            </body>
            </html>
            """;

        var outline = new HtmlPresentationOutlineBuilder().Build(html);

        Assert.InRange(
            outline.Length,
            HtmlPresentationOutlineBuilder.MaximumOutlineLength - 10,
            HtmlPresentationOutlineBuilder.MaximumOutlineLength);
        Assert.EndsWith("...", outline);
    }
}
