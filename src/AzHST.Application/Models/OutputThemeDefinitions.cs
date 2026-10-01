namespace AzHST.Application.Models;

public sealed record HtmlThemeDefinition
{
    public int SchemaVersion { get; init; }

    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public HtmlThemePalette Palette { get; init; } = new();

    public HtmlThemeTypography Typography { get; init; } = new();

    public HtmlThemeAppearance Appearance { get; init; } = new();

    public HtmlThemeMotion Motion { get; init; } = new();
}

public sealed record HtmlThemePalette
{
    public string Page { get; init; } = string.Empty;

    public string Surface { get; init; } = string.Empty;

    public string SurfaceRaised { get; init; } = string.Empty;

    public string SurfaceSelected { get; init; } = string.Empty;

    public string Border { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string TextMuted { get; init; } = string.Empty;

    public string Primary { get; init; } = string.Empty;

    public string ActiveFlow { get; init; } = string.Empty;

    public string Accent { get; init; } = string.Empty;

    public string Success { get; init; } = string.Empty;

    public string Warning { get; init; } = string.Empty;

    public string Danger { get; init; } = string.Empty;

    public string Focus { get; init; } = string.Empty;

    public string IconTile { get; init; } = string.Empty;
}

public sealed record HtmlThemeTypography
{
    public string FontFamily { get; init; } = string.Empty;

    public int BaseSizePixels { get; init; }

    public double LineHeight { get; init; }
}

public sealed record HtmlThemeAppearance
{
    public HtmlColorScheme ColorScheme { get; init; }

    public HtmlSurfaceStyle SurfaceStyle { get; init; }

    public HtmlIconTreatment IconTreatment { get; init; }

    public bool AllowPureBlack { get; init; }

    public bool AllowGlassmorphism { get; init; }

    public int MaximumGradients { get; init; }

    public int CornerRadiusPixels { get; init; }

    public int IconTileSizePixels { get; init; }
}

public sealed record HtmlThemeMotion
{
    public int TransitionDurationMilliseconds { get; init; }

    public int SequenceStepDurationMilliseconds { get; init; }

    public bool AllowContinuousDecorativeMotion { get; init; }
}

public enum HtmlColorScheme
{
    Unspecified,
    Dark,
    Light,
}

public enum HtmlSurfaceStyle
{
    Unspecified,
    Layered,
    Flat,
}

public enum HtmlIconTreatment
{
    Unspecified,
    LightTile,
    SurfaceTile,
}

public sealed record PresentationThemeDefinition
{
    public int SchemaVersion { get; init; }

    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public PresentationThemePalette Palette { get; init; } = new();

    public PresentationThemeTypography Typography { get; init; } = new();

    public PresentationThemeAppearance Appearance { get; init; } = new();
}

public sealed record PresentationThemePalette
{
    public string Canvas { get; init; } = string.Empty;

    public string Surface { get; init; } = string.Empty;

    public string SummarySurface { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string TextMuted { get; init; } = string.Empty;

    public string Border { get; init; } = string.Empty;

    public string Primary { get; init; } = string.Empty;

    public string Accent { get; init; } = string.Empty;

    public string IconTile { get; init; } = string.Empty;

    public string Success { get; init; } = string.Empty;

    public string Warning { get; init; } = string.Empty;

    public string Comparison { get; init; } = string.Empty;

    public string Danger { get; init; } = string.Empty;

    public string Hyperlink { get; init; } = string.Empty;

    public string FollowedHyperlink { get; init; } = string.Empty;
}

public sealed record PresentationThemeTypography
{
    public string FontFamily { get; init; } = string.Empty;

    public string DisplayFontFamily { get; init; } = string.Empty;

    public int TitleSizePoints { get; init; }

    public int SubtitleSizePoints { get; init; }

    public int SlideTitleSizePoints { get; init; }

    public int SummarySizePoints { get; init; }

    public int BodySizePoints { get; init; }

    public int NodeLabelSizePoints { get; init; }

    public int NodeDetailSizePoints { get; init; }

    public int ConnectionLabelSizePoints { get; init; }

    public int FooterSizePoints { get; init; }
}

public sealed record PresentationThemeAppearance
{
    public bool RoundedCards { get; init; }

    public bool ShowHeaderRule { get; init; }

    public PresentationIconTreatment IconTreatment { get; init; }
}

public enum PresentationIconTreatment
{
    Unspecified,
    LightTile,
    None,
}
