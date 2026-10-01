using AzHST.Application.Abstractions;
using AzHST.Application.Exceptions;
using AzHST.Application.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AzHST.Infrastructure;

public sealed class OpenXmlPresentationBuilder : IPresentationBuilder
{
    private const long SlideWidth = 12_192_000;
    private const long SlideHeight = 6_858_000;
    private const long EmusPerInch = 914_400;

    private readonly IAzureIconCatalog _azureIcons;

    public OpenXmlPresentationBuilder(IAzureIconCatalog azureIcons)
    {
        _azureIcons = azureIcons;
    }

    public Task<PresentationArtifact> BuildAsync(
        PresentationPlan plan,
        VisualizationArtifact visualization,
        PresentationThemeDefinition theme,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(visualization);
        ArgumentNullException.ThrowIfNull(theme);
        var renderTheme = PresentationRenderTheme.Create(theme);

        return Task.Run(
            () => Build(plan, visualization, renderTheme, cancellationToken),
            cancellationToken);
    }

    private PresentationArtifact Build(
        PresentationPlan plan,
        VisualizationArtifact visualization,
        PresentationRenderTheme theme,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outputDirectory = Path.GetFullPath(visualization.DirectoryPath);
        Directory.CreateDirectory(outputDirectory);

        var filePath = Path.Combine(outputDirectory, "presentation.pptx");
        var temporaryFilePath = Path.Combine(
            outputDirectory,
            $".presentation-{Guid.NewGuid():N}.tmp");

        try
        {
            CreatePresentation(
                temporaryFilePath,
                plan,
                visualization.Id,
                theme,
                cancellationToken);
            ValidatePresentation(temporaryFilePath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryFilePath, filePath, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PresentationGenerationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PresentationGenerationException(
                "AzHST could not build the PowerPoint presentation.",
                exception);
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }

        return new PresentationArtifact(
            visualization.Id,
            filePath,
            new Uri(filePath, UriKind.Absolute),
            plan.Slides.Count + 1);
    }

    private void CreatePresentation(
        string filePath,
        PresentationPlan plan,
        string visualizationId,
        PresentationRenderTheme theme,
        CancellationToken cancellationToken)
    {
        using var document = PresentationDocument.Create(
            filePath,
            PresentationDocumentType.Presentation);
        var presentationPart = document.AddPresentationPart();
        var slideLayoutPart = CreatePresentationParts(
            presentationPart,
            theme);
        var presentation = presentationPart.Presentation
            ?? throw new PresentationGenerationException(
                "The PowerPoint presentation did not initialize.");
        var slideIdList = presentation.SlideIdList
            ?? throw new PresentationGenerationException(
                "The PowerPoint presentation did not initialize its slide list.");
        uint slideId = 255;

        AddSlide(
            presentationPart,
            slideLayoutPart,
            slideIdList,
            ref slideId,
            slidePart => BuildTitleSlide(
                slidePart,
                plan.Title,
                plan.Subtitle,
                visualizationId,
                theme));

        for (var index = 0; index < plan.Slides.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var slidePlan = plan.Slides[index];
            var slideNumber = index + 2;

            AddSlide(
                presentationPart,
                slideLayoutPart,
                slideIdList,
                ref slideId,
                slidePart => BuildContentSlide(
                    slidePart,
                    slidePlan,
                    visualizationId,
                    slideNumber,
                    plan.Slides.Count + 1,
                    theme));
        }

        presentationPart.Presentation.Save();
    }

    private static SlideLayoutPart CreatePresentationParts(
        PresentationPart presentationPart,
        PresentationRenderTheme theme)
    {
        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
        var themePart = slideMasterPart.AddNewPart<ThemePart>();

        slideLayoutPart.SlideLayout = new P.SlideLayout(
            new P.CommonSlideData(CreateShapeTree())
            {
                Name = "AzHST Blank",
            },
            new P.ColorMapOverride(new A.MasterColorMapping()))
        {
            Type = P.SlideLayoutValues.Blank,
            Preserve = true,
        };
        slideLayoutPart.AddPart(slideMasterPart);

        var slideLayoutRelationshipId =
            slideMasterPart.GetIdOfPart(slideLayoutPart);
        slideMasterPart.SlideMaster = new P.SlideMaster(
            new P.CommonSlideData(CreateShapeTree())
            {
                Name = "AzHST Master",
            },
            CreateColorMap(),
            new P.SlideLayoutIdList(
                new P.SlideLayoutId
                {
                    Id = 2_147_483_649U,
                    RelationshipId = slideLayoutRelationshipId,
                }),
            new P.TextStyles(
                new P.TitleStyle(),
                new P.BodyStyle(),
                new P.OtherStyle()));

        themePart.Theme = CreateTheme(theme);

        var slideMasterRelationshipId =
            presentationPart.GetIdOfPart(slideMasterPart);
        presentationPart.Presentation = new P.Presentation(
            new P.SlideMasterIdList(
                new P.SlideMasterId
                {
                    Id = 2_147_483_648U,
                    RelationshipId = slideMasterRelationshipId,
                }),
            new P.SlideIdList(),
            new P.SlideSize
            {
                Cx = checked((int)SlideWidth),
                Cy = checked((int)SlideHeight),
                Type = P.SlideSizeValues.Screen16x9,
            },
            new P.NotesSize
            {
                Cx = 6_858_000,
                Cy = 9_144_000,
            });

        slideLayoutPart.SlideLayout.Save();
        slideMasterPart.SlideMaster.Save();
        themePart.Theme.Save();
        return slideLayoutPart;
    }

    private static void AddSlide(
        PresentationPart presentationPart,
        SlideLayoutPart slideLayoutPart,
        P.SlideIdList slideIdList,
        ref uint slideId,
        Action<SlidePart> buildSlide)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slidePart.AddPart(slideLayoutPart);
        buildSlide(slidePart);
        (slidePart.Slide
            ?? throw new PresentationGenerationException(
                "A PowerPoint slide was not initialized."))
            .Save();

        slideId++;
        slideIdList.Append(new P.SlideId
        {
            Id = slideId,
            RelationshipId = presentationPart.GetIdOfPart(slidePart),
        });
    }

    private static void BuildTitleSlide(
        SlidePart slidePart,
        string title,
        string subtitle,
        string visualizationId,
        PresentationRenderTheme theme)
    {
        var shapeTree = CreateShapeTree();
        uint shapeId = 1;

        AddRectangle(
            shapeTree,
            ref shapeId,
            0,
            0,
            SlideWidth,
            SlideHeight,
            theme.Canvas,
            theme.Canvas,
            cornerRadius: false);
        AddRectangle(
            shapeTree,
            ref shapeId,
            Emu(0.8),
            Emu(1.08),
            Emu(1.15),
            Emu(0.09),
            theme.Primary,
            theme.Primary,
            cornerRadius: false);
        AddTextBox(
            shapeTree,
            ref shapeId,
            theme,
            Emu(0.8),
            Emu(1.45),
            Emu(11.7),
            Emu(1.55),
            [
                new TextParagraph(
                    title,
                    theme.TitleSize,
                    theme.Title,
                    true,
                    UseDisplayFont: true),
            ],
            A.TextAnchoringTypeValues.Center);
        AddTextBox(
            shapeTree,
            ref shapeId,
            theme,
            Emu(0.82),
            Emu(3.15),
            Emu(10.7),
            Emu(1.0),
            [new TextParagraph(subtitle, theme.SubtitleSize, theme.Text, false)],
            A.TextAnchoringTypeValues.Top);
        AddTextBox(
            shapeTree,
            ref shapeId,
            theme,
            Emu(0.82),
            Emu(6.72),
            Emu(11.6),
            Emu(0.35),
            [
                new TextParagraph(
                    $"Generated by AzHST  |  {visualizationId}",
                    theme.FooterSize,
                    theme.TextMuted,
                    false),
            ],
            A.TextAnchoringTypeValues.Center);

        slidePart.Slide = CreateSlide(shapeTree);
    }

    private void BuildContentSlide(
        SlidePart slidePart,
        PresentationSlidePlan plan,
        string visualizationId,
        int slideNumber,
        int totalSlides,
        PresentationRenderTheme theme)
    {
        var shapeTree = CreateShapeTree();
        uint shapeId = 1;

        AddRectangle(
            shapeTree,
            ref shapeId,
            0,
            0,
            SlideWidth,
            SlideHeight,
            theme.Canvas,
            theme.Canvas,
            cornerRadius: false);
        AddRectangle(
            shapeTree,
            ref shapeId,
            0,
            0,
            SlideWidth,
            Emu(0.92),
            theme.Surface,
            theme.Surface,
            cornerRadius: false);
        if (theme.ShowHeaderRule)
        {
            AddRectangle(
                shapeTree,
                ref shapeId,
                0,
                Emu(0.88),
                SlideWidth,
                Emu(0.04),
                theme.Primary,
                theme.Primary,
                cornerRadius: false);
        }

        AddTextBox(
            shapeTree,
            ref shapeId,
            theme,
            Emu(0.62),
            Emu(0.16),
            Emu(11.6),
            Emu(0.54),
            [
                new TextParagraph(
                    plan.Title,
                    theme.SlideTitleSize,
                    theme.Title,
                    true,
                    UseDisplayFont: true),
            ],
            A.TextAnchoringTypeValues.Center);

        if (plan.Kind is "diagram" or "comparison")
        {
            BuildVisualSlide(
                slidePart,
                shapeTree,
                ref shapeId,
                plan,
                theme);
        }
        else
        {
            BuildTextSlide(shapeTree, ref shapeId, plan, theme);
        }

        AddFooter(
            shapeTree,
            ref shapeId,
            plan.Sources,
            visualizationId,
            slideNumber,
            totalSlides,
            theme);

        slidePart.Slide = CreateSlide(shapeTree);
    }

    private static void BuildTextSlide(
        P.ShapeTree shapeTree,
        ref uint shapeId,
        PresentationSlidePlan plan,
        PresentationRenderTheme theme)
    {
        if (plan.Summary.Length > 0)
        {
            AddTextBox(
                shapeTree,
                ref shapeId,
                theme,
                Emu(0.75),
                Emu(1.25),
                Emu(11.8),
                Emu(0.9),
                [
                    new TextParagraph(
                        plan.Summary,
                        theme.SummarySize,
                        theme.Text,
                        false),
                ],
                A.TextAnchoringTypeValues.Center,
                theme.SummarySurface,
                theme.Border,
                cornerRadius: theme.RoundedCards);
        }

        var bulletTop = plan.Summary.Length > 0 ? 2.38 : 1.35;
        var paragraphs = plan.Bullets
            .Select(bullet => new TextParagraph(
                $"•  {bullet}",
                theme.BodySize,
                theme.Text,
                false))
            .ToArray();

        AddTextBox(
            shapeTree,
            ref shapeId,
            theme,
            Emu(0.95),
            Emu(bulletTop),
            Emu(11.25),
            Emu(3.65),
            paragraphs,
            A.TextAnchoringTypeValues.Top);
    }

    private void BuildVisualSlide(
        SlidePart slidePart,
        P.ShapeTree shapeTree,
        ref uint shapeId,
        PresentationSlidePlan plan,
        PresentationRenderTheme theme)
    {
        if (plan.Summary.Length > 0)
        {
            AddTextBox(
                shapeTree,
                ref shapeId,
                theme,
                Emu(0.72),
                Emu(1.08),
                Emu(11.9),
                Emu(0.58),
                [
                    new TextParagraph(
                        plan.Summary,
                        Math.Min(theme.SummarySize, 1_200),
                        theme.TextMuted,
                        false),
                ],
                A.TextAnchoringTypeValues.Center);
        }

        var bounds = new SlideRect(
            Emu(0.62),
            Emu(plan.Summary.Length > 0 ? 1.82 : 1.35),
            Emu(12.1),
            Emu(plan.Summary.Length > 0 ? 4.35 : 4.82));
        var positions = CalculateNodePositions(plan.Nodes.Count, bounds);

        if (plan.Kind == "diagram")
        {
            var positionsById = plan.Nodes
                .Select((node, index) => new { node.Id, Position = positions[index] })
                .ToDictionary(item => item.Id, item => item.Position, StringComparer.Ordinal);

            foreach (var connection in plan.Connections)
            {
                AddConnection(
                    shapeTree,
                    ref shapeId,
                    positionsById[connection.From],
                    positionsById[connection.To],
                    connection.Label,
                    theme);
            }
        }

        var imageRelationships = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < plan.Nodes.Count; index++)
        {
            AddNode(
                slidePart,
                shapeTree,
                ref shapeId,
                plan.Nodes[index],
                positions[index],
                imageRelationships,
                plan.Kind == "comparison",
                theme);
        }
    }

    private void AddNode(
        SlidePart slidePart,
        P.ShapeTree shapeTree,
        ref uint shapeId,
        PresentationNodePlan node,
        SlideRect position,
        Dictionary<string, string> imageRelationships,
        bool comparison,
        PresentationRenderTheme theme)
    {
        AddRectangle(
            shapeTree,
            ref shapeId,
            position.X,
            position.Y,
            position.Width,
            position.Height,
            theme.Surface,
            comparison ? theme.Comparison : theme.Primary,
            cornerRadius: theme.RoundedCards);

        var hasIcon = node.IconKey.Length > 0;
        if (hasIcon)
        {
            var relationshipId = GetOrAddIconPart(
                slidePart,
                node.IconKey,
                imageRelationships);
            var iconSize = Math.Min(Emu(0.48), position.Height / 3);
            var iconX = position.X + ((position.Width - iconSize) / 2);
            var iconY = position.Y + Emu(0.16);
            if (theme.ShowIconTile)
            {
                var tilePadding = Emu(0.06);
                AddRectangle(
                    shapeTree,
                    ref shapeId,
                    iconX - tilePadding,
                    iconY - tilePadding,
                    iconSize + (tilePadding * 2),
                    iconSize + (tilePadding * 2),
                    theme.IconTile,
                    theme.IconTile,
                    cornerRadius: theme.RoundedCards);
            }

            AddPicture(
                shapeTree,
                ref shapeId,
                relationshipId,
                node.Label,
                iconX,
                iconY,
                iconSize,
                iconSize);
        }

        var labelTop = position.Y + (hasIcon
            ? Emu(theme.ShowIconTile ? 0.72 : 0.68)
            : Emu(0.2));
        AddTextBox(
            shapeTree,
            ref shapeId,
            theme,
            position.X + Emu(0.12),
            labelTop,
            position.Width - Emu(0.24),
            Emu(0.48),
            [
                new TextParagraph(
                    node.Label,
                    theme.NodeLabelSize,
                    theme.Text,
                    true),
            ],
            A.TextAnchoringTypeValues.Center,
            horizontalAlignment: A.TextAlignmentTypeValues.Center);

        if (node.Detail.Length > 0 && position.Height >= Emu(1.2))
        {
            AddTextBox(
                shapeTree,
                ref shapeId,
                theme,
                position.X + Emu(0.14),
                labelTop + Emu(0.48),
                position.Width - Emu(0.28),
                position.Height - (labelTop - position.Y) - Emu(0.55),
                [
                    new TextParagraph(
                        node.Detail,
                        theme.NodeDetailSize,
                        theme.TextMuted,
                        false),
                ],
                A.TextAnchoringTypeValues.Top,
                horizontalAlignment: A.TextAlignmentTypeValues.Center);
        }
    }

    private string GetOrAddIconPart(
        SlidePart slidePart,
        string iconKey,
        IDictionary<string, string> imageRelationships)
    {
        if (imageRelationships.TryGetValue(iconKey, out var existingRelationship))
        {
            return existingRelationship;
        }

        if (!_azureIcons.TryGetDataUri(iconKey, out var dataUri))
        {
            throw new PresentationGenerationException(
                $"The Azure icon '{iconKey}' is unavailable while building the PowerPoint presentation.");
        }

        const string prefix = "data:image/svg+xml;base64,";
        if (!dataUri.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new PresentationGenerationException(
                $"The Azure icon '{iconKey}' is not an SVG data image.");
        }

        var bytes = Convert.FromBase64String(dataUri[prefix.Length..]);
        var imagePart = slidePart.AddImagePart(ImagePartType.Svg);
        using (var stream = new MemoryStream(bytes, writable: false))
        {
            imagePart.FeedData(stream);
        }

        var relationshipId = slidePart.GetIdOfPart(imagePart);
        imageRelationships.Add(iconKey, relationshipId);
        return relationshipId;
    }

    private static IReadOnlyList<SlideRect> CalculateNodePositions(
        int nodeCount,
        SlideRect bounds)
    {
        var columns = Math.Min(4, nodeCount <= 3 ? nodeCount : (int)Math.Ceiling(Math.Sqrt(nodeCount)));
        var rows = (int)Math.Ceiling(nodeCount / (double)columns);
        var horizontalGap = Emu(0.22);
        var verticalGap = Emu(0.25);
        var width = (bounds.Width - (horizontalGap * (columns - 1))) / columns;
        var height = (bounds.Height - (verticalGap * (rows - 1))) / rows;
        var positions = new List<SlideRect>(nodeCount);

        for (var index = 0; index < nodeCount; index++)
        {
            var row = index / columns;
            var column = index % columns;
            positions.Add(new SlideRect(
                bounds.X + (column * (width + horizontalGap)),
                bounds.Y + (row * (height + verticalGap)),
                width,
                height));
        }

        return positions;
    }

    private static void AddConnection(
        P.ShapeTree shapeTree,
        ref uint shapeId,
        SlideRect from,
        SlideRect to,
        string label,
        PresentationRenderTheme theme)
    {
        var deltaX = to.CenterX - from.CenterX;
        var deltaY = to.CenterY - from.CenterY;
        var fromScale = CalculateBoundaryScale(from, deltaX, deltaY);
        var toScale = CalculateBoundaryScale(to, deltaX, deltaY);
        var startX = from.CenterX + (long)Math.Round(deltaX * fromScale);
        var startY = from.CenterY + (long)Math.Round(deltaY * fromScale);
        var endX = to.CenterX - (long)Math.Round(deltaX * toScale);
        var endY = to.CenterY - (long)Math.Round(deltaY * toScale);
        var x = Math.Min(startX, endX);
        var y = Math.Min(startY, endY);
        var width = Math.Max(Math.Abs(endX - startX), 1);
        var height = Math.Max(Math.Abs(endY - startY), 1);

        var transform = new A.Transform2D(
            new A.Offset { X = x, Y = y },
            new A.Extents { Cx = width, Cy = height })
        {
            HorizontalFlip = endX < startX,
            VerticalFlip = endY < startY,
        };
        var outline = new A.Outline(
            new A.SolidFill(new A.RgbColorModelHex { Val = theme.Primary }),
            new A.HeadEnd { Type = A.LineEndValues.None },
            new A.TailEnd { Type = A.LineEndValues.Triangle })
        {
            Width = 22_860,
        };

        shapeId++;
        shapeTree.Append(new P.ConnectionShape(
            new P.NonVisualConnectionShapeProperties(
                new P.NonVisualDrawingProperties
                {
                    Id = shapeId,
                    Name = $"Connection {shapeId}",
                },
                new P.NonVisualConnectorShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                transform,
                new A.PresetGeometry(new A.AdjustValueList())
                {
                    Preset = A.ShapeTypeValues.Line,
                },
                outline)));

        if (label.Length > 0)
        {
            AddTextBox(
                shapeTree,
                ref shapeId,
                theme,
                ((startX + endX) / 2) - Emu(0.7),
                ((startY + endY) / 2) - Emu(0.18),
                Emu(1.4),
                Emu(0.36),
                [
                    new TextParagraph(
                        label,
                        theme.ConnectionLabelSize,
                        theme.TextMuted,
                        false),
                ],
                A.TextAnchoringTypeValues.Center,
                theme.Surface,
                theme.Border,
                cornerRadius: theme.RoundedCards,
                horizontalAlignment: A.TextAlignmentTypeValues.Center);
        }
    }

    private static double CalculateBoundaryScale(
        SlideRect rectangle,
        long deltaX,
        long deltaY)
    {
        if (deltaX == 0 && deltaY == 0)
        {
            return 0;
        }

        var horizontalScale = deltaX == 0
            ? double.PositiveInfinity
            : (rectangle.Width / 2d) / Math.Abs(deltaX);
        var verticalScale = deltaY == 0
            ? double.PositiveInfinity
            : (rectangle.Height / 2d) / Math.Abs(deltaY);

        return Math.Min(horizontalScale, verticalScale);
    }

    private static void AddFooter(
        P.ShapeTree shapeTree,
        ref uint shapeId,
        IReadOnlyList<string> sources,
        string visualizationId,
        int slideNumber,
        int totalSlides,
        PresentationRenderTheme theme)
    {
        var sourceText = sources.Count > 0
            ? $"Sources: {string.Join(" · ", sources)}"
            : "Validate current details against Microsoft documentation.";
        AddTextBox(
            shapeTree,
            ref shapeId,
            theme,
            Emu(0.6),
            Emu(6.45),
            Emu(9.6),
            Emu(0.28),
            [
                new TextParagraph(
                    sourceText,
                    theme.FooterSize,
                    theme.TextMuted,
                    false),
            ],
            A.TextAnchoringTypeValues.Center);
        AddTextBox(
            shapeTree,
            ref shapeId,
            theme,
            Emu(10.3),
            Emu(6.45),
            Emu(2.3),
            Emu(0.28),
            [
                new TextParagraph(
                    $"{visualizationId}  |  {slideNumber}/{totalSlides}",
                    theme.FooterSize,
                    theme.TextMuted,
                    false),
            ],
            A.TextAnchoringTypeValues.Center,
            horizontalAlignment: A.TextAlignmentTypeValues.Right);
    }

    private static void AddRectangle(
        P.ShapeTree shapeTree,
        ref uint shapeId,
        long x,
        long y,
        long width,
        long height,
        string fillColor,
        string outlineColor,
        bool cornerRadius)
    {
        shapeId++;
        shapeTree.Append(new P.Shape(
            CreateNonVisualShapeProperties(shapeId, $"Shape {shapeId}"),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = x, Y = y },
                    new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList())
                {
                    Preset = cornerRadius
                        ? A.ShapeTypeValues.RoundRectangle
                        : A.ShapeTypeValues.Rectangle,
                },
                new A.SolidFill(new A.RgbColorModelHex { Val = fillColor }),
                new A.Outline(
                    new A.SolidFill(new A.RgbColorModelHex { Val = outlineColor }))
                {
                    Width = 12_700,
                })));
    }

    private static void AddTextBox(
        P.ShapeTree shapeTree,
        ref uint shapeId,
        PresentationRenderTheme theme,
        long x,
        long y,
        long width,
        long height,
        IReadOnlyList<TextParagraph> paragraphs,
        A.TextAnchoringTypeValues verticalAlignment,
        string? fillColor = null,
        string? outlineColor = null,
        bool cornerRadius = false,
        A.TextAlignmentTypeValues? horizontalAlignment = null)
    {
        shapeId++;
        var shapeProperties = new P.ShapeProperties(
            new A.Transform2D(
                new A.Offset { X = x, Y = y },
                new A.Extents { Cx = width, Cy = height }),
            new A.PresetGeometry(new A.AdjustValueList())
            {
                Preset = cornerRadius
                    ? A.ShapeTypeValues.RoundRectangle
                    : A.ShapeTypeValues.Rectangle,
            });

        shapeProperties.Append(fillColor is null
            ? new A.NoFill()
            : new A.SolidFill(new A.RgbColorModelHex { Val = fillColor }));
        shapeProperties.Append(outlineColor is null
            ? new A.Outline(new A.NoFill())
            : new A.Outline(
                new A.SolidFill(new A.RgbColorModelHex { Val = outlineColor }))
            {
                Width = 12_700,
            });

        var textBody = new P.TextBody(
            new A.BodyProperties
            {
                Wrap = A.TextWrappingValues.Square,
                Anchor = verticalAlignment,
                LeftInset = 91_440,
                RightInset = 91_440,
                TopInset = 45_720,
                BottomInset = 45_720,
            },
            new A.ListStyle());

        foreach (var paragraph in paragraphs)
        {
            textBody.Append(CreateParagraph(
                paragraph,
                horizontalAlignment ?? A.TextAlignmentTypeValues.Left,
                theme));
        }

        if (paragraphs.Count == 0)
        {
            textBody.Append(new A.Paragraph(new A.EndParagraphRunProperties()));
        }

        shapeTree.Append(new P.Shape(
            CreateNonVisualShapeProperties(shapeId, $"Text {shapeId}"),
            shapeProperties,
            textBody));
    }

    private static A.Paragraph CreateParagraph(
        TextParagraph paragraph,
        A.TextAlignmentTypeValues alignment,
        PresentationRenderTheme theme)
    {
        var runProperties = new A.RunProperties
        {
            Language = "en-US",
            FontSize = paragraph.FontSize,
            Bold = paragraph.Bold,
            Dirty = false,
        };
        runProperties.Append(
            new A.SolidFill(new A.RgbColorModelHex { Val = paragraph.Color }),
            new A.LatinFont
            {
                Typeface = paragraph.UseDisplayFont
                    ? theme.DisplayFontFamily
                    : theme.FontFamily,
            });

        return new A.Paragraph(
            new A.ParagraphProperties
            {
                Alignment = alignment,
            },
            new A.Run(
                runProperties,
                new A.Text(paragraph.Text)),
            new A.EndParagraphRunProperties
            {
                Language = "en-US",
                FontSize = paragraph.FontSize,
            });
    }

    private static void AddPicture(
        P.ShapeTree shapeTree,
        ref uint shapeId,
        string relationshipId,
        string description,
        long x,
        long y,
        long width,
        long height)
    {
        shapeId++;
        shapeTree.Append(new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties
                {
                    Id = shapeId,
                    Name = $"Azure icon {shapeId}",
                    Description = description,
                },
                new P.NonVisualPictureDrawingProperties(
                    new A.PictureLocks
                    {
                        NoChangeAspect = true,
                    }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.BlipFill(
                new A.Blip
                {
                    Embed = relationshipId,
                },
                new A.Stretch(new A.FillRectangle())),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = x, Y = y },
                    new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList())
                {
                    Preset = A.ShapeTypeValues.Rectangle,
                },
                new A.NoFill(),
                new A.Outline(new A.NoFill()))));
    }

    private static P.NonVisualShapeProperties CreateNonVisualShapeProperties(
        uint shapeId,
        string name)
    {
        return new P.NonVisualShapeProperties(
            new P.NonVisualDrawingProperties
            {
                Id = shapeId,
                Name = name,
            },
            new P.NonVisualShapeDrawingProperties(
                new A.ShapeLocks
                {
                    NoGrouping = true,
                }),
            new P.ApplicationNonVisualDrawingProperties());
    }

    private static P.Slide CreateSlide(P.ShapeTree shapeTree)
    {
        return new P.Slide(
            new P.CommonSlideData(shapeTree),
            new P.ColorMapOverride(new A.MasterColorMapping()));
    }

    private static P.ShapeTree CreateShapeTree()
    {
        return new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties
                {
                    Id = 1U,
                    Name = string.Empty,
                },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(
                new A.TransformGroup(
                    new A.Offset { X = 0, Y = 0 },
                    new A.Extents { Cx = 0, Cy = 0 },
                    new A.ChildOffset { X = 0, Y = 0 },
                    new A.ChildExtents { Cx = 0, Cy = 0 })));
    }

    private static P.ColorMap CreateColorMap()
    {
        return new P.ColorMap
        {
            Background1 = A.ColorSchemeIndexValues.Light1,
            Text1 = A.ColorSchemeIndexValues.Dark1,
            Background2 = A.ColorSchemeIndexValues.Light2,
            Text2 = A.ColorSchemeIndexValues.Dark2,
            Accent1 = A.ColorSchemeIndexValues.Accent1,
            Accent2 = A.ColorSchemeIndexValues.Accent2,
            Accent3 = A.ColorSchemeIndexValues.Accent3,
            Accent4 = A.ColorSchemeIndexValues.Accent4,
            Accent5 = A.ColorSchemeIndexValues.Accent5,
            Accent6 = A.ColorSchemeIndexValues.Accent6,
            Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
            FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink,
        };
    }

    private static A.Theme CreateTheme(PresentationRenderTheme theme)
    {
        var colorScheme = new A.ColorScheme(
            new A.Dark1Color(
                new A.SystemColor
                {
                    Val = A.SystemColorValues.WindowText,
                    LastColor = "000000",
                }),
            new A.Light1Color(
                new A.SystemColor
                {
                    Val = A.SystemColorValues.Window,
                    LastColor = theme.Surface,
                }),
            new A.Dark2Color(new A.RgbColorModelHex { Val = theme.Title }),
            new A.Light2Color(new A.RgbColorModelHex { Val = theme.Canvas }),
            new A.Accent1Color(new A.RgbColorModelHex { Val = theme.Primary }),
            new A.Accent2Color(new A.RgbColorModelHex { Val = theme.Accent }),
            new A.Accent3Color(new A.RgbColorModelHex { Val = theme.Success }),
            new A.Accent4Color(new A.RgbColorModelHex { Val = theme.Warning }),
            new A.Accent5Color(new A.RgbColorModelHex { Val = theme.Comparison }),
            new A.Accent6Color(new A.RgbColorModelHex { Val = theme.Danger }),
            new A.Hyperlink(new A.RgbColorModelHex { Val = theme.Hyperlink }),
            new A.FollowedHyperlinkColor(
                new A.RgbColorModelHex { Val = theme.FollowedHyperlink }))
        {
            Name = "AzHST",
        };

        var fontScheme = new A.FontScheme(
            new A.MajorFont(
                new A.LatinFont { Typeface = theme.DisplayFontFamily },
                new A.EastAsianFont { Typeface = string.Empty },
                new A.ComplexScriptFont { Typeface = string.Empty }),
            new A.MinorFont(
                new A.LatinFont { Typeface = theme.FontFamily },
                new A.EastAsianFont { Typeface = string.Empty },
                new A.ComplexScriptFont { Typeface = string.Empty }))
        {
            Name = "AzHST",
        };

        var formatScheme = new A.FormatScheme(
            new A.FillStyleList(
                CreateSchemeSolidFill(),
                CreateSchemeSolidFill(),
                CreateSchemeSolidFill()),
            new A.LineStyleList(
                CreateSchemeOutline(6_350),
                CreateSchemeOutline(12_700),
                CreateSchemeOutline(19_050)),
            new A.EffectStyleList(
                new A.EffectStyle(new A.EffectList()),
                new A.EffectStyle(new A.EffectList()),
                new A.EffectStyle(new A.EffectList())),
            new A.BackgroundFillStyleList(
                CreateSchemeSolidFill(),
                CreateSchemeSolidFill(),
                CreateSchemeSolidFill()))
        {
            Name = "AzHST",
        };

        return new A.Theme(
            new A.ThemeElements(colorScheme, fontScheme, formatScheme),
            new A.ObjectDefaults(),
            new A.ExtraColorSchemeList())
        {
            Name = "AzHST",
        };
    }

    private static A.SolidFill CreateSchemeSolidFill()
    {
        return new A.SolidFill(
            new A.SchemeColor
            {
                Val = A.SchemeColorValues.PhColor,
            });
    }

    private static A.Outline CreateSchemeOutline(int width)
    {
        return new A.Outline(
            CreateSchemeSolidFill(),
            new A.PresetDash
            {
                Val = A.PresetLineDashValues.Solid,
            })
        {
            Width = width,
        };
    }

    private static void ValidatePresentation(string filePath)
    {
        using var document = PresentationDocument.Open(filePath, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2019)
            .Validate(document)
            .Take(10)
            .ToArray();

        if (errors.Length > 0)
        {
            var details = string.Join(
                "; ",
                errors.Select(error => error.Description));
            throw new PresentationGenerationException(
                $"The generated PowerPoint package is invalid: {details}");
        }
    }

    private static long Emu(double inches)
    {
        return checked((long)Math.Round(inches * EmusPerInch));
    }

    private sealed record TextParagraph(
        string Text,
        int FontSize,
        string Color,
        bool Bold,
        bool UseDisplayFont = false);

    private sealed record PresentationRenderTheme(
        string Canvas,
        string Surface,
        string SummarySurface,
        string Title,
        string Text,
        string TextMuted,
        string Border,
        string Primary,
        string Accent,
        string IconTile,
        string Success,
        string Warning,
        string Comparison,
        string Danger,
        string Hyperlink,
        string FollowedHyperlink,
        string FontFamily,
        string DisplayFontFamily,
        int TitleSize,
        int SubtitleSize,
        int SlideTitleSize,
        int SummarySize,
        int BodySize,
        int NodeLabelSize,
        int NodeDetailSize,
        int ConnectionLabelSize,
        int FooterSize,
        bool RoundedCards,
        bool ShowHeaderRule,
        bool ShowIconTile)
    {
        public static PresentationRenderTheme Create(
            PresentationThemeDefinition definition)
        {
            return new PresentationRenderTheme(
                Color(definition.Palette.Canvas),
                Color(definition.Palette.Surface),
                Color(definition.Palette.SummarySurface),
                Color(definition.Palette.Title),
                Color(definition.Palette.Text),
                Color(definition.Palette.TextMuted),
                Color(definition.Palette.Border),
                Color(definition.Palette.Primary),
                Color(definition.Palette.Accent),
                Color(definition.Palette.IconTile),
                Color(definition.Palette.Success),
                Color(definition.Palette.Warning),
                Color(definition.Palette.Comparison),
                Color(definition.Palette.Danger),
                Color(definition.Palette.Hyperlink),
                Color(definition.Palette.FollowedHyperlink),
                definition.Typography.FontFamily,
                definition.Typography.DisplayFontFamily,
                Points(definition.Typography.TitleSizePoints),
                Points(definition.Typography.SubtitleSizePoints),
                Points(definition.Typography.SlideTitleSizePoints),
                Points(definition.Typography.SummarySizePoints),
                Points(definition.Typography.BodySizePoints),
                Points(definition.Typography.NodeLabelSizePoints),
                Points(definition.Typography.NodeDetailSizePoints),
                Points(definition.Typography.ConnectionLabelSizePoints),
                Points(definition.Typography.FooterSizePoints),
                definition.Appearance.RoundedCards,
                definition.Appearance.ShowHeaderRule,
                definition.Appearance.IconTreatment
                    == PresentationIconTreatment.LightTile);
        }

        private static string Color(string value)
        {
            return value[1..].ToUpperInvariant();
        }

        private static int Points(int value)
        {
            return checked(value * 100);
        }
    }

    private sealed record SlideRect(
        long X,
        long Y,
        long Width,
        long Height)
    {
        public long CenterX => X + (Width / 2);

        public long CenterY => Y + (Height / 2);
    }
}
