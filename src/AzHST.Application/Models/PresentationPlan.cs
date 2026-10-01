namespace AzHST.Application.Models;

public sealed class PresentationPlan
{
    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public List<PresentationSourcePlan> Sources { get; set; } = [];

    public List<PresentationSlidePlan> Slides { get; set; } = [];
}

public sealed class PresentationSourcePlan
{
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}

public sealed class PresentationSlidePlan
{
    public string Kind { get; set; } = string.Empty;

    public string SectionTitle { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Callout { get; set; } = string.Empty;

    public List<string> Bullets { get; set; } = [];

    public List<PresentationNodePlan> Nodes { get; set; } = [];

    public List<PresentationConnectionPlan> Connections { get; set; } = [];

    public List<string> Sources { get; set; } = [];
}

public sealed class PresentationNodePlan
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public string IconKey { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;
}

public sealed class PresentationConnectionPlan
{
    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}
