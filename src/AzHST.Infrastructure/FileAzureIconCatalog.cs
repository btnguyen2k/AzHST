using System.Text;
using System.Text.RegularExpressions;
using AzHST.Application.Abstractions;
using AzHST.Application.Models;

namespace AzHST.Infrastructure;

public sealed partial class FileAzureIconCatalog : IAzureIconCatalog
{
    public const int MaximumIconFileLength = 128 * 1024;

    private static readonly string[] PreferredServiceNames =
    [
        "Application Gateways",
        "Front Door and CDN Profiles",
        "App Services",
        "Function Apps",
        "Virtual Machine",
        "Kubernetes Services",
        "Container Instances",
        "Container Registries",
        "Virtual Networks",
        "Load Balancers",
        "Firewalls",
        "Web Application Firewall Policies WAF",
        "Private Endpoints",
        "Key Vaults",
        "Managed Identities",
        "Azure Cosmos DB",
        "SQL Database",
        "Storage Accounts",
        "API Management Services",
        "Azure Service Bus",
        "Event Grid Topics",
        "Event Hubs",
        "Application Insights",
        "Log Analytics Workspaces",
        "Azure Monitor",
    ];

    private static readonly HashSet<string> QueryStopWords = new(
        [
            "a",
            "an",
            "and",
            "architecture",
            "azure",
            "does",
            "for",
            "how",
            "in",
            "is",
            "microsoft",
            "of",
            "on",
            "service",
            "the",
            "to",
            "what",
            "with",
            "work",
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, int> PreferredScores =
        PreferredServiceNames
            .Select((name, index) => new
            {
                Name = NormalizeForSearch(name),
                Score = PreferredServiceNames.Length - index,
            })
            .ToDictionary(item => item.Name, item => item.Score, StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, IconEntry> _iconsByKey;
    private readonly IconEntry[] _searchableIcons;

    public FileAzureIconCatalog(string iconDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iconDirectory);

        var rootDirectory = Path.GetFullPath(iconDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The Azure icon directory was not found: {rootDirectory}");
        }

        var icons = Directory
            .EnumerateFiles(rootDirectory, "*.svg", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(path => CreateEntry(rootDirectory, path))
            .ToArray();

        if (icons.Length == 0)
        {
            throw new InvalidOperationException(
                $"The Azure icon directory contains no SVG files: {rootDirectory}");
        }

        var duplicateKey = icons
            .GroupBy(icon => icon.Descriptor.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateKey is not null)
        {
            throw new InvalidOperationException(
                $"The Azure icon catalog contains the duplicate key '{duplicateKey.Key}'.");
        }

        _searchableIcons = icons;
        _iconsByKey = icons.ToDictionary(
            icon => icon.Descriptor.Key,
            StringComparer.OrdinalIgnoreCase);
    }

    public static FileAzureIconCatalog CreateDefault()
    {
        return new FileAzureIconCatalog(
            Path.Combine(AppContext.BaseDirectory, "resources", "azure-icons"));
    }

    public IReadOnlyList<AzureIconDescriptor> FindRelevant(
        string query,
        int maximumResults)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumResults, 1);

        var queryTokens = Tokenize(query)
            .Where(token => !QueryStopWords.Contains(token))
            .ToHashSet(StringComparer.Ordinal);
        var normalizedQuery = string.Join(' ', queryTokens.Order(StringComparer.Ordinal));

        return _searchableIcons
            .Select(icon => new
            {
                Icon = icon,
                Score = CalculateScore(icon, queryTokens, normalizedQuery),
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Icon.Descriptor.Key, StringComparer.Ordinal)
            .GroupBy(item => item.Icon.SearchName, StringComparer.Ordinal)
            .Select(group => group.First().Icon.Descriptor)
            .Take(maximumResults)
            .ToArray();
    }

    public bool TryGetDataUri(string key, out string dataUri)
    {
        if (_iconsByKey.TryGetValue(key, out var icon))
        {
            dataUri = icon.DataUri;
            return true;
        }

        dataUri = string.Empty;
        return false;
    }

    private static IconEntry CreateEntry(string rootDirectory, string path)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > MaximumIconFileLength)
        {
            throw new InvalidOperationException(
                $"Azure icon '{path}' exceeds the {MaximumIconFileLength / 1024} KB limit.");
        }

        var bytes = File.ReadAllBytes(path);
        var svg = Encoding.UTF8.GetString(bytes);
        ValidateSvg(path, svg);

        var relativePath = Path.GetRelativePath(rootDirectory, path);
        var categoryPath = Path.GetDirectoryName(relativePath) ?? "general";
        var category = categoryPath
            .Replace(Path.DirectorySeparatorChar, ' ')
            .Replace(Path.AltDirectorySeparatorChar, ' ');
        var fileName = Path.GetFileNameWithoutExtension(path);
        var markerIndex = fileName.IndexOf(
            "-icon-service-",
            StringComparison.OrdinalIgnoreCase);
        var iconId = markerIndex > 0 ? fileName[..markerIndex] : "icon";
        var serviceName = markerIndex > 0
            ? fileName[(markerIndex + "-icon-service-".Length)..]
            : fileName;
        var displayName = serviceName.Replace('-', ' ').Trim();
        var categoryKey = Slugify(categoryPath);
        var key = $"{categoryKey}/{Slugify(iconId)}-{Slugify(serviceName)}";
        var descriptor = new AzureIconDescriptor(key, displayName, category);

        return new IconEntry(
            descriptor,
            $"data:image/svg+xml;base64,{Convert.ToBase64String(bytes)}",
            NormalizeForSearch(displayName),
            Tokenize(displayName).ToHashSet(StringComparer.Ordinal),
            Tokenize(category).ToHashSet(StringComparer.Ordinal));
    }

    private static int CalculateScore(
        IconEntry icon,
        IReadOnlySet<string> queryTokens,
        string normalizedQuery)
    {
        var score = PreferredScores.TryGetValue(icon.SearchName, out var preferredScore)
            ? preferredScore
            : 0;

        if (normalizedQuery.Length > 0
            && normalizedQuery.Contains(icon.SearchName, StringComparison.Ordinal))
        {
            score += 1_000;
        }

        score += queryTokens.Count(icon.ServiceTokens.Contains) * 100;
        score += queryTokens.Count(icon.CategoryTokens.Contains) * 20;

        if (icon.ServiceTokens.Contains("classic") && !queryTokens.Contains("classic"))
        {
            score -= 500;
        }

        if (icon.ServiceTokens.Contains("deprecated") && !queryTokens.Contains("deprecated"))
        {
            score -= 500;
        }

        return score;
    }

    private static void ValidateSvg(string path, string svg)
    {
        if (!SvgRootRegex().IsMatch(svg)
            || UnsafeSvgContentRegex().IsMatch(svg)
            || ExternalSvgCssUrlRegex().IsMatch(svg))
        {
            throw new InvalidOperationException(
                $"Azure icon '{path}' contains unsupported SVG content.");
        }

        foreach (Match match in SvgReferenceRegex().Matches(svg))
        {
            if (!match.Groups["value"].Value.StartsWith('#'))
            {
                throw new InvalidOperationException(
                    $"Azure icon '{path}' references external content.");
            }
        }
    }

    private static string NormalizeForSearch(string value)
    {
        return string.Join(' ', Tokenize(value).Order(StringComparer.Ordinal));
    }

    private static string[] Tokenize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : ' ');
        }

        return builder
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Stem)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string Stem(string token)
    {
        if (token.Length > 4 && token.EndsWith("ies", StringComparison.Ordinal))
        {
            return $"{token[..^3]}y";
        }

        if (token.Length > 3
            && token.EndsWith('s')
            && !token.EndsWith("ss", StringComparison.Ordinal)
            && !token.EndsWith("is", StringComparison.Ordinal)
            && !token.EndsWith("os", StringComparison.Ordinal)
            && !token.EndsWith("us", StringComparison.Ordinal))
        {
            return token[..^1];
        }

        return token;
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else if (builder.Length > 0)
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }

    private sealed record IconEntry(
        AzureIconDescriptor Descriptor,
        string DataUri,
        string SearchName,
        IReadOnlySet<string> ServiceTokens,
        IReadOnlySet<string> CategoryTokens);

    [GeneratedRegex(
        """^\s*(?:<\?xml\b[^>]*>\s*)?<svg\b""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SvgRootRegex();

    [GeneratedRegex(
        """(?:<script\b|<foreignObject\b|<!DOCTYPE\b|<!ENTITY\b|\son[a-z]+\s*=)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeSvgContentRegex();

    [GeneratedRegex(
        """(?:href|xlink:href)\s*=\s*["'](?<value>[^"']+)["']""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SvgReferenceRegex();

    [GeneratedRegex(
        """url\(\s*["']?\s*(?:https?:|file:|ftp:|//)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalSvgCssUrlRegex();
}
