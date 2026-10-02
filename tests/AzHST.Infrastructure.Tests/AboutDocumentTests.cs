namespace AzHST.Infrastructure.Tests;

public sealed class AboutDocumentTests
{
    private static readonly string[] SynchronizedSections =
    [
        "Highlights",
        "Quick user guide",
        "Azure icon usage",
        "Disclaimer",
        "Reporting bugs",
        "License",
    ];

    [Fact]
    public void AboutDocument_MatchesSelectedReadmeSections()
    {
        var repositoryRoot = FindRepositoryRoot();
        var readme = ReadMarkdown(repositoryRoot, "README.md");
        var about = ReadMarkdown(repositoryRoot, "ABOUT.md");

        Assert.Equal(
            ExtractProductDescription(readme),
            ExtractProductDescription(about));
        foreach (var section in SynchronizedSections)
        {
            Assert.Equal(
                ExtractSection(readme, section),
                ExtractSection(about, section));
        }

        Assert.DoesNotContain("## Quick installation", about);
        Assert.DoesNotContain("## Contributing", about);
        Assert.Contains(
            "https://github.com/btnguyen2k/AzHST/blob/main/LICENSE.md",
            ExtractSection(about, "License"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AzHST.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the AzHST repository root.");
    }

    private static string ReadMarkdown(
        string repositoryRoot,
        string fileName)
    {
        return File.ReadAllText(Path.Combine(repositoryRoot, fileName))
            .ReplaceLineEndings("\n");
    }

    private static string ExtractProductDescription(string markdown)
    {
        const string DescriptionStart = "**Azure - How stuff works (AzHST)**";
        var descriptionStart = markdown.IndexOf(
            DescriptionStart,
            StringComparison.Ordinal);
        Assert.True(descriptionStart >= 0);

        var sectionStart = markdown.IndexOf(
            "\n## ",
            descriptionStart,
            StringComparison.Ordinal);
        Assert.True(sectionStart > descriptionStart);
        return markdown[descriptionStart..sectionStart].Trim();
    }

    private static string ExtractSection(
        string markdown,
        string heading)
    {
        var marker = $"## {heading}";
        var sectionStart = markdown.IndexOf(
            marker,
            StringComparison.Ordinal);
        Assert.True(sectionStart >= 0, $"Missing section '{heading}'.");

        var nextSection = markdown.IndexOf(
            "\n## ",
            sectionStart + marker.Length,
            StringComparison.Ordinal);
        return (nextSection >= 0
                ? markdown[sectionStart..nextSection]
                : markdown[sectionStart..])
            .Trim();
    }
}
