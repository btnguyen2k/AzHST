using System.Globalization;
using System.Text;

namespace AzHST.Application.Services;

public sealed class VisualizationArtifactIdGenerator
{
    private const int MaximumSlugLength = 60;
    private readonly TimeProvider _timeProvider;

    public VisualizationArtifactIdGenerator(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string Create(string suggestedSlug, string query)
    {
        var timestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var timestampHex = timestamp.ToString("x12", CultureInfo.InvariantCulture);
        var slug = Slugify(
            string.IsNullOrWhiteSpace(suggestedSlug) ? query : suggestedSlug);

        return $"{timestampHex}-{slug}";
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(Math.Min(normalized.Length, MaximumSlugLength));
        var pendingSeparator = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is >= 'A' and <= 'Z')
            {
                AppendCharacter(builder, (char)(character + ('a' - 'A')), ref pendingSeparator);
            }
            else if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                AppendCharacter(builder, character, ref pendingSeparator);
            }
            else if (builder.Length > 0)
            {
                pendingSeparator = true;
            }

            if (builder.Length >= MaximumSlugLength)
            {
                break;
            }
        }

        var slug = builder.ToString().TrimEnd('-');
        return string.IsNullOrEmpty(slug) ? "azure-visualization" : slug;
    }

    private static void AppendCharacter(
        StringBuilder builder,
        char character,
        ref bool pendingSeparator)
    {
        if (pendingSeparator && builder.Length < MaximumSlugLength)
        {
            builder.Append('-');
        }

        if (builder.Length < MaximumSlugLength)
        {
            builder.Append(character);
        }

        pendingSeparator = false;
    }
}
