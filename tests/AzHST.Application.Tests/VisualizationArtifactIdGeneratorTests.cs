using AzHST.Application.Services;

namespace AzHST.Application.Tests;

public sealed class VisualizationArtifactIdGeneratorTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 10, 1, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public void Create_CombinesPaddedHexTimestampAndSanitizedSuggestedSlug()
    {
        var generator = CreateGenerator();

        var result = generator.Create("Azure App Gateway!!!", "ignored query");

        Assert.Equal(
            $"{FixedTime.ToUnixTimeMilliseconds():x12}-azure-app-gateway",
            result);
    }

    [Fact]
    public void Create_UsesQueryWhenSuggestedSlugIsBlank()
    {
        var generator = CreateGenerator();

        var result = generator.Create(
            " ",
            "Compare Azure Front Door and Application Gateway");

        Assert.EndsWith(
            "-compare-azure-front-door-and-application-gateway",
            result);
    }

    [Fact]
    public void Create_UsesFallbackWhenNoAsciiSlugCharactersExist()
    {
        var generator = CreateGenerator();

        var result = generator.Create("東京", "東京");

        Assert.EndsWith("-azure-visualization", result);
    }

    [Fact]
    public void Create_LimitsSlugLengthAndDoesNotEndWithSeparator()
    {
        var generator = CreateGenerator();

        var result = generator.Create(new string('a', 80) + " next", "ignored");
        var slug = result[(result.IndexOf('-') + 1)..];

        Assert.True(slug.Length <= 60);
        Assert.False(slug.EndsWith('-'));
    }

    private static VisualizationArtifactIdGenerator CreateGenerator()
    {
        return new VisualizationArtifactIdGenerator(new FixedTimeProvider(FixedTime));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
