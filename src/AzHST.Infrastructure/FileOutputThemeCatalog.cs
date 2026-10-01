using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;

namespace AzHST.Infrastructure;

public sealed partial class FileOutputThemeCatalog : IOutputThemeCatalog
{
    private const int SupportedSchemaVersion = 1;
    private const double MinimumTextContrast = 4.5;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };

    private readonly IReadOnlyDictionary<string, HtmlThemeDefinition> _htmlThemes;
    private readonly IReadOnlyDictionary<string, PresentationThemeDefinition> _presentationThemes;

    public FileOutputThemeCatalog(string themeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeDirectory);

        var rootDirectory = Path.GetFullPath(themeDirectory);
        _htmlThemes = LoadDefinitions<HtmlThemeDefinition>(
            Path.Combine(rootDirectory, "html"),
            ValidateHtmlTheme);
        _presentationThemes = LoadDefinitions<PresentationThemeDefinition>(
            Path.Combine(rootDirectory, "presentation"),
            ValidatePresentationTheme);

        HtmlThemes = _htmlThemes.Values
            .OrderBy(theme => theme.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(theme => new OutputThemeOption(theme.Id, theme.DisplayName))
            .ToArray();
        PresentationThemes = _presentationThemes.Values
            .OrderBy(theme => theme.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(theme => new OutputThemeOption(theme.Id, theme.DisplayName))
            .ToArray();
    }

    public IReadOnlyList<OutputThemeOption> HtmlThemes { get; }

    public IReadOnlyList<OutputThemeOption> PresentationThemes { get; }

    public static FileOutputThemeCatalog CreateDefault()
    {
        return new FileOutputThemeCatalog(
            Path.Combine(AppContext.BaseDirectory, "resources", "themes"));
    }

    public HtmlThemeDefinition GetHtmlTheme(string id)
    {
        return GetTheme(_htmlThemes, id, "HTML");
    }

    public PresentationThemeDefinition GetPresentationTheme(string id)
    {
        return GetTheme(_presentationThemes, id, "PowerPoint");
    }

    private static IReadOnlyDictionary<string, T> LoadDefinitions<T>(
        string directory,
        Action<T, string> validate)
        where T : class
    {
        if (!Directory.Exists(directory))
        {
            throw new OutputThemeConfigurationException(
                $"The theme directory was not found: {directory}");
        }

        var files = Directory
            .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0)
        {
            throw new OutputThemeConfigurationException(
                $"The theme directory contains no JSON definitions: {directory}");
        }

        var definitions = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            T definition;
            try
            {
                var json = File.ReadAllText(file);
                definition = JsonSerializer.Deserialize<T>(json, SerializerOptions)
                    ?? throw new OutputThemeConfigurationException(
                        $"The theme file '{file}' does not contain an object.");
            }
            catch (JsonException exception)
            {
                throw new OutputThemeConfigurationException(
                    $"The theme file '{file}' contains invalid JSON: {exception.Message}",
                    exception);
            }

            validate(definition, file);
            var id = definition switch
            {
                HtmlThemeDefinition html => html.Id,
                PresentationThemeDefinition presentation => presentation.Id,
                _ => throw new InvalidOperationException(
                    $"Unsupported theme definition type '{typeof(T).Name}'."),
            };

            if (!definitions.TryAdd(id, definition))
            {
                throw new OutputThemeConfigurationException(
                    $"The theme ID '{id}' is defined more than once in '{directory}'.");
            }
        }

        return definitions;
    }

    private static T GetTheme<T>(
        IReadOnlyDictionary<string, T> themes,
        string id,
        string outputKind)
    {
        var normalizedId = id?.Trim();
        if (string.IsNullOrEmpty(normalizedId))
        {
            throw new OutputThemeConfigurationException(
                $"Select an {outputKind} theme before generating output.");
        }

        if (themes.TryGetValue(normalizedId, out var theme))
        {
            return theme;
        }

        throw new OutputThemeConfigurationException(
            $"The configured {outputKind} theme '{normalizedId}' is unavailable.");
    }

    private static void ValidateHtmlTheme(
        HtmlThemeDefinition theme,
        string file)
    {
        ValidateCommon(theme.SchemaVersion, theme.Id, theme.DisplayName, file);
        var palette = theme.Palette
            ?? throw MissingSection(file, "palette");
        var typography = theme.Typography
            ?? throw MissingSection(file, "typography");
        var appearance = theme.Appearance
            ?? throw MissingSection(file, "appearance");
        var motion = theme.Motion
            ?? throw MissingSection(file, "motion");

        ValidateColors(
            file,
            ("palette.page", palette.Page),
            ("palette.surface", palette.Surface),
            ("palette.surfaceRaised", palette.SurfaceRaised),
            ("palette.surfaceSelected", palette.SurfaceSelected),
            ("palette.border", palette.Border),
            ("palette.text", palette.Text),
            ("palette.textMuted", palette.TextMuted),
            ("palette.primary", palette.Primary),
            ("palette.activeFlow", palette.ActiveFlow),
            ("palette.accent", palette.Accent),
            ("palette.success", palette.Success),
            ("palette.warning", palette.Warning),
            ("palette.danger", palette.Danger),
            ("palette.focus", palette.Focus),
            ("palette.iconTile", palette.IconTile));
        ValidateContrast(file, palette.Text, palette.Page, "text on page");
        ValidateContrast(file, palette.Text, palette.Surface, "text on surface");
        ValidateContrast(file, palette.TextMuted, palette.Surface, "muted text on surface");

        ValidateFontFamily(file, typography.FontFamily, "typography.fontFamily", 200);
        RequireRange(file, typography.BaseSizePixels, 12, 24, "typography.baseSizePixels");
        RequireRange(file, typography.LineHeight, 1.2, 2.0, "typography.lineHeight");

        if (appearance.ColorScheme == HtmlColorScheme.Unspecified)
        {
            throw InvalidValue(file, "appearance.colorScheme");
        }

        if (appearance.SurfaceStyle == HtmlSurfaceStyle.Unspecified)
        {
            throw InvalidValue(file, "appearance.surfaceStyle");
        }

        if (appearance.IconTreatment == HtmlIconTreatment.Unspecified)
        {
            throw InvalidValue(file, "appearance.iconTreatment");
        }

        RequireRange(file, appearance.MaximumGradients, 0, 3, "appearance.maximumGradients");
        RequireRange(file, appearance.CornerRadiusPixels, 0, 24, "appearance.cornerRadiusPixels");
        RequireRange(file, appearance.IconTileSizePixels, 24, 80, "appearance.iconTileSizePixels");
        RequireRange(
            file,
            motion.TransitionDurationMilliseconds,
            0,
            1_000,
            "motion.transitionDurationMilliseconds");
        RequireRange(
            file,
            motion.SequenceStepDurationMilliseconds,
            100,
            5_000,
            "motion.sequenceStepDurationMilliseconds");

        if (!appearance.AllowPureBlack
            && EnumerateHtmlColors(palette).Any(
                color => string.Equals(color, "#000000", StringComparison.OrdinalIgnoreCase)))
        {
            throw new OutputThemeConfigurationException(
                $"The theme file '{file}' prohibits pure black but contains #000000.");
        }
    }

    private static void ValidatePresentationTheme(
        PresentationThemeDefinition theme,
        string file)
    {
        ValidateCommon(theme.SchemaVersion, theme.Id, theme.DisplayName, file);
        var palette = theme.Palette
            ?? throw MissingSection(file, "palette");
        var typography = theme.Typography
            ?? throw MissingSection(file, "typography");
        var appearance = theme.Appearance
            ?? throw MissingSection(file, "appearance");

        ValidateColors(
            file,
            ("palette.canvas", palette.Canvas),
            ("palette.surface", palette.Surface),
            ("palette.summarySurface", palette.SummarySurface),
            ("palette.title", palette.Title),
            ("palette.text", palette.Text),
            ("palette.textMuted", palette.TextMuted),
            ("palette.border", palette.Border),
            ("palette.primary", palette.Primary),
            ("palette.accent", palette.Accent),
            ("palette.iconTile", palette.IconTile),
            ("palette.success", palette.Success),
            ("palette.warning", palette.Warning),
            ("palette.comparison", palette.Comparison),
            ("palette.danger", palette.Danger),
            ("palette.hyperlink", palette.Hyperlink),
            ("palette.followedHyperlink", palette.FollowedHyperlink));
        ValidateContrast(file, palette.Title, palette.Canvas, "title on canvas");
        ValidateContrast(file, palette.Text, palette.Canvas, "text on canvas");
        ValidateContrast(file, palette.TextMuted, palette.Canvas, "muted text on canvas");
        ValidateContrast(file, palette.Text, palette.Surface, "text on surface");

        ValidateFontFamily(file, typography.FontFamily, "typography.fontFamily", 80);
        ValidateFontFamily(
            file,
            typography.DisplayFontFamily,
            "typography.displayFontFamily",
            80);
        RequireRange(file, typography.TitleSizePoints, 24, 44, "typography.titleSizePoints");
        RequireRange(file, typography.SubtitleSizePoints, 12, 24, "typography.subtitleSizePoints");
        RequireRange(file, typography.SlideTitleSizePoints, 16, 28, "typography.slideTitleSizePoints");
        RequireRange(file, typography.SummarySizePoints, 10, 20, "typography.summarySizePoints");
        RequireRange(file, typography.BodySizePoints, 12, 24, "typography.bodySizePoints");
        RequireRange(file, typography.NodeLabelSizePoints, 8, 18, "typography.nodeLabelSizePoints");
        RequireRange(file, typography.NodeDetailSizePoints, 7, 14, "typography.nodeDetailSizePoints");
        RequireRange(
            file,
            typography.ConnectionLabelSizePoints,
            6,
            12,
            "typography.connectionLabelSizePoints");
        RequireRange(file, typography.FooterSizePoints, 6, 12, "typography.footerSizePoints");

        if (appearance.IconTreatment == PresentationIconTreatment.Unspecified)
        {
            throw InvalidValue(file, "appearance.iconTreatment");
        }
    }

    private static void ValidateCommon(
        int schemaVersion,
        string id,
        string displayName,
        string file)
    {
        if (schemaVersion != SupportedSchemaVersion)
        {
            throw new OutputThemeConfigurationException(
                $"The theme file '{file}' uses unsupported schemaVersion {schemaVersion}. Expected {SupportedSchemaVersion}.");
        }

        if (!ThemeIdRegex().IsMatch(id ?? string.Empty))
        {
            throw new OutputThemeConfigurationException(
                $"The theme file '{file}' has an invalid ID '{id}'.");
        }

        if (string.IsNullOrWhiteSpace(displayName)
            || displayName.Length > 80
            || displayName.Any(char.IsControl))
        {
            throw InvalidValue(file, "displayName");
        }
    }

    private static void ValidateColors(
        string file,
        params (string Name, string Value)[] colors)
    {
        foreach (var (name, value) in colors)
        {
            if (!HexColorRegex().IsMatch(value ?? string.Empty))
            {
                throw new OutputThemeConfigurationException(
                    $"The theme file '{file}' has invalid {name} value '{value}'. Use #RRGGBB.");
            }
        }
    }

    private static void ValidateContrast(
        string file,
        string foreground,
        string background,
        string usage)
    {
        var ratio = CalculateContrastRatio(foreground, background);
        if (ratio < MinimumTextContrast)
        {
            throw new OutputThemeConfigurationException(
                $"The theme file '{file}' has insufficient contrast for {usage}: {ratio:F2}:1.");
        }
    }

    private static double CalculateContrastRatio(
        string foreground,
        string background)
    {
        var foregroundLuminance = CalculateRelativeLuminance(foreground);
        var backgroundLuminance = CalculateRelativeLuminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double CalculateRelativeLuminance(string color)
    {
        var red = Convert.ToByte(color.Substring(1, 2), 16) / 255d;
        var green = Convert.ToByte(color.Substring(3, 2), 16) / 255d;
        var blue = Convert.ToByte(color.Substring(5, 2), 16) / 255d;

        return (0.2126 * Linearize(red))
            + (0.7152 * Linearize(green))
            + (0.0722 * Linearize(blue));
    }

    private static double Linearize(double channel)
    {
        return channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    private static void ValidateFontFamily(
        string file,
        string value,
        string property,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximumLength
            || !FontFamilyRegex().IsMatch(value))
        {
            throw InvalidValue(file, property);
        }
    }

    private static IEnumerable<string> EnumerateHtmlColors(
        HtmlThemePalette palette)
    {
        yield return palette.Page;
        yield return palette.Surface;
        yield return palette.SurfaceRaised;
        yield return palette.SurfaceSelected;
        yield return palette.Border;
        yield return palette.Text;
        yield return palette.TextMuted;
        yield return palette.Primary;
        yield return palette.ActiveFlow;
        yield return palette.Accent;
        yield return palette.Success;
        yield return palette.Warning;
        yield return palette.Danger;
        yield return palette.Focus;
        yield return palette.IconTile;
    }

    private static void RequireRange(
        string file,
        int value,
        int minimum,
        int maximum,
        string property)
    {
        if (value < minimum || value > maximum)
        {
            throw InvalidValue(file, property);
        }
    }

    private static void RequireRange(
        string file,
        double value,
        double minimum,
        double maximum,
        string property)
    {
        if (double.IsNaN(value)
            || double.IsInfinity(value)
            || value < minimum
            || value > maximum)
        {
            throw InvalidValue(file, property);
        }
    }

    private static OutputThemeConfigurationException MissingSection(
        string file,
        string section)
    {
        return new OutputThemeConfigurationException(
            $"The theme file '{file}' is missing the '{section}' section.");
    }

    private static OutputThemeConfigurationException InvalidValue(
        string file,
        string property)
    {
        return new OutputThemeConfigurationException(
            $"The theme file '{file}' has an invalid '{property}' value.");
    }

    [GeneratedRegex("""^[a-z0-9][a-z0-9-]{0,63}$""", RegexOptions.CultureInvariant)]
    private static partial Regex ThemeIdRegex();

    [GeneratedRegex("""^#[0-9A-Fa-f]{6}$""", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();

    [GeneratedRegex("""^[A-Za-z0-9 ,.'"_-]+$""", RegexOptions.CultureInvariant)]
    private static partial Regex FontFamilyRegex();
}
