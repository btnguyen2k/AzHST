using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AzHST.Application.Models;

namespace AzHST.Application.Services;

public sealed partial class PresentationPlanHtmlManifest
{
    public const string ElementId = "azh-presentation-plan";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public PresentationPlan ExtractRequired(string html)
    {
        return TryExtract(html)
            ?? throw new InvalidDataException(
                $"The generated HTML does not contain the required '{ElementId}' presentation narrative.");
    }

    public PresentationPlan? TryExtract(string html)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        Match? manifest = null;
        foreach (Match script in ScriptElementRegex().Matches(html))
        {
            if (!IsManifestAttributes(script.Groups["attributes"].Value))
            {
                continue;
            }

            if (manifest is not null)
            {
                throw new InvalidDataException(
                    $"The generated HTML contains more than one '{ElementId}' presentation narrative.");
            }

            manifest = script;
        }

        if (manifest is null)
        {
            return null;
        }

        var attributes = manifest.Groups["attributes"].Value;
        if (!JsonTypeAttributeRegex().IsMatch(attributes))
        {
            throw new InvalidDataException(
                $"The '{ElementId}' presentation narrative must use type='application/json'.");
        }

        var json = manifest.Groups["content"].Value.Trim();
        if (json.Length == 0)
        {
            throw new InvalidDataException(
                $"The '{ElementId}' presentation narrative is empty.");
        }

        try
        {
            return JsonSerializer.Deserialize<PresentationPlan>(
                    json,
                    SerializerOptions)
                ?? throw new InvalidDataException(
                    $"The '{ElementId}' presentation narrative is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The '{ElementId}' presentation narrative is not valid JSON.",
                exception);
        }
    }

    public bool IsManifestAttributes(string attributes)
    {
        return ManifestIdAttributeRegex().IsMatch(attributes);
    }

    [GeneratedRegex(
        """<script\b(?<attributes>[^>]*)>(?<content>[\s\S]*?)</script\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptElementRegex();

    [GeneratedRegex(
        """\bid\s*=\s*(?<quote>["'])azh-presentation-plan\k<quote>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ManifestIdAttributeRegex();

    [GeneratedRegex(
        """\btype\s*=\s*(?<quote>["'])application/json\k<quote>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonTypeAttributeRegex();
}
