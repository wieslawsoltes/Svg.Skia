using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.Skia;
using Svg.Transforms;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgTextStoryTests
{
    [Fact]
    public void ParagraphsWrapWithMeasuredAdvancesInsideFrameBounds()
    {
        var story = Story("The quick brown fox jumps over the lazy dog. A shaped paragraph uses real font advances.");
        story.Frames[0].Width = 160;
        var layout = SvgTextLayoutService.Layout(story);
        Assert.False(layout.HasOverflow);
        Assert.True(layout.Lines.Count > 3);
        Assert.All(layout.Lines, line =>
        {
            Assert.InRange(line.Width, 1, line.AvailableWidth + 0.01f);
            Assert.InRange(line.X, 10, 150);
            Assert.InRange(line.Baseline, 10, 590);
        });
        Assert.Equal(story.Text.Replace(" ", ""), string.Concat(layout.Lines.SelectMany(line => line.Runs).Select(run => run.Text)).Replace(" ", ""));
    }

    [Fact]
    public void LinkedFramesAndColumnsConsumeTheStoryInOrder()
    {
        var story = Story(string.Join(" ", Enumerable.Repeat("linked columns", 50)));
        story.Frames[0].Width = 230;
        story.Frames[0].Height = 95;
        story.Frames[0].Columns = 2;
        story.Frames[0].ColumnGap = 10;
        story.Frames.Add(new SvgTextFrameDefinition { X = 300, Width = 500, Height = 1000 });
        var layout = SvgTextLayoutService.Layout(story);
        Assert.False(layout.HasOverflow);
        Assert.Contains(layout.Lines, line => line.FrameIndex == 0 && line.ColumnIndex == 0);
        Assert.Contains(layout.Lines, line => line.FrameIndex == 0 && line.ColumnIndex == 1);
        Assert.Contains(layout.Lines, line => line.FrameIndex == 1);
        Assert.Equal(layout.Lines.Select(line => line.Start).OrderBy(value => value), layout.Lines.Select(line => line.Start));
    }

    [Fact]
    public void OverflowRetainsUnplacedTextInStoryMetadata()
    {
        var story = Story("This complete story has more text than one tiny frame can accommodate.");
        story.Frames[0].Width = 70;
        story.Frames[0].Height = 50;
        var layout = SvgTextLayoutService.Layout(story);
        Assert.True(layout.HasOverflow);
        Assert.Equal(story.Text.Substring(layout.OverflowStart), layout.OverflowText);
        var group = SvgTextStoryService.CreateGroup(story, layout);
        Assert.Equal(layout.OverflowLength.ToString(), group.CustomAttributes[SvgTextStoryService.OverflowCountAttribute]);
        Assert.Equal(story.Text, SvgTextStoryService.Read(group).Text);
    }

    [Fact]
    public void ATooShortFrameFlowsToTheNextWithoutLosingText()
    {
        var story = Story("A frame may be too short for a single baseline.");
        story.Frames[0].Height = 21;
        story.Frames.Add(new SvgTextFrameDefinition { Y = 50, Width = 400, Height = 400 });
        var layout = SvgTextLayoutService.Layout(story);
        Assert.False(layout.HasOverflow);
        Assert.All(layout.Lines, line => Assert.Equal(1, line.FrameIndex));
        Assert.Equal(0, layout.Lines[0].Start);
    }

    [Theory]
    [InlineData("a\u0301a\u0301a\u0301")]
    [InlineData("👩🏽‍🚀👩🏽‍🚀👩🏽‍🚀")]
    [InlineData("🇵🇱🇵🇱🇵🇱")]
    public void EmergencyWrapNeverDividesUnicodeTextElements(string text)
    {
        var story = Story(text);
        story.Frames[0].Width = 21;
        story.Frames[0].Height = 300;
        var layout = SvgTextLayoutService.Layout(story);
        Assert.Equal(3, layout.Lines.Count);
        Assert.Equal(text, string.Concat(layout.Lines.SelectMany(line => line.Runs).Select(run => run.Text)));
        Assert.All(layout.Lines, line => Assert.Equal(text.Length / 3, line.Length));
        Assert.True(SvgTextStoryService.CreateGroup(story, layout).CustomAttributes.ContainsKey(SvgTextStoryService.HorizontalOverflowAttribute));
    }

    [Fact]
    public void EmptyParagraphsAndParagraphSpacingCreateRealVerticalSpace()
    {
        var story = Story("One\n\nThree");
        var layout = SvgTextLayoutService.Layout(story);
        Assert.Equal(3, layout.Lines.Count);
        Assert.Empty(layout.Lines[1].Runs);
        Assert.True(layout.Lines[2].Baseline - layout.Lines[0].Baseline > story.DefaultStyle.FontSize * 2);
    }

    [Fact]
    public void AlignmentIndentAndJustificationUseTheAvailableColumnWidth()
    {
        var story = Story("A sentence with spaces to justify across a narrow column and a final line.");
        story.Frames[0].Width = 190;
        story.Paragraph.Alignment = SvgParagraphAlignment.Justify;
        story.Paragraph.FirstLineIndent = 20;
        var layout = SvgTextLayoutService.Layout(story);
        Assert.Equal(30, layout.Lines[0].X);
        Assert.Equal(150, layout.Lines[0].AvailableWidth);
        Assert.True(layout.Lines[0].JustificationSpacing > 0);
        Assert.Equal(layout.Lines[0].AvailableWidth, layout.Lines[0].Width, 3);
        Assert.Equal(0, layout.Lines[layout.Lines.Count - 1].JustificationSpacing);
        story.Paragraph.Alignment = SvgParagraphAlignment.Center;
        var centered = SvgTextLayoutService.Layout(story);
        Assert.True(centered.Lines[0].X > 30);
    }

    [Fact]
    public void ParagraphOverridesAndRichRangesSurviveMetadataRoundTrip()
    {
        var story = Story("Bold title\nA second paragraph with its own alignment.");
        story.Styles.Add(new SvgStoryStyleRange { Start = 0, Length = 10, Style = new SvgStoryTextStyle { FontSize = 30, FontWeight = 700, Fill = "#ff3300" } });
        story.ParagraphOverrides.Add(1, new SvgParagraphFormat { Alignment = SvgParagraphAlignment.End, SpaceBefore = 20 });
        var serialized = SvgTextStoryService.Serialize(story);
        var restored = SvgTextStoryService.Deserialize(serialized);
        Assert.Equal(serialized, SvgTextStoryService.Serialize(restored));
        var layout = SvgTextLayoutService.Layout(restored);
        Assert.Equal(30, layout.Lines[0].Runs[0].Style.FontSize);
        Assert.True(layout.Lines[1].X > layout.Lines[0].X);
        var group = SvgTextStoryService.CreateGroup(story, layout);
        Assert.Contains(group.Descendants().OfType<SvgTextSpan>(), span => span.FontWeight == SvgFontWeight.W700 && span.FontSize.Value == 30);
    }

    [Fact]
    public void StandardSvgRoundTripRetainsLayoutAndRendersText()
    {
        var story = Story("Portable paragraph text\nNative SVG tspan output.");
        var document = new SvgDocument { Width = 500, Height = 600, ViewBox = new SvgViewBox(0, 0, 500, 600) };
        var group = SvgTextStoryService.CreateGroup(story);
        group.ID = "story";
        document.Children.Add(group);
        var xml = document.GetXML();
        Assert.Contains("<text", xml);
        Assert.Contains("<tspan", xml);
        var roundTrip = SvgDocument.FromSvg<SvgDocument>(xml);
        var restored = roundTrip.GetElementById<SvgGroup>("story");
        Assert.Equal(story.Text, SvgTextStoryService.Read(restored).Text);
        using var renderer = new SKSvg();
        renderer.FromSvgDocument(roundTrip);
        using var surface = SKSurface.Create(new SKImageInfo(500, 600));
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawPicture(renderer.Picture!);
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 0);
    }

    [Fact]
    public void ReflowPreservesGroupIdentityTransformAndOneUndoBoundary()
    {
        using var editor = new SvgEditorController();
        editor.NewDocument();
        var story = Story("A linked story that can be edited and undone.");
        var group = SvgTextStoryService.Apply(editor, story);
        editor.Execute("Move story", () => group.Transforms = new SvgTransformCollection { new SvgTranslate(15, 25) });
        editor.MarkSaved();
        var id = group.ID;
        var edited = SvgTextStoryService.Read(group);
        SvgTextStoryService.ReplaceText(edited, "Updated text with a second frame.");
        edited.Frames.Add(new SvgTextFrameDefinition { X = 500 });
        SvgTextStoryService.Apply(editor, edited, group);
        Assert.Same(group, editor.Document!.GetElementById<SvgGroup>(id));
        Assert.Single(group.Transforms);
        Assert.Equal("Updated text with a second frame.", SvgTextStoryService.Read(group).Text);
        editor.Undo();
        Assert.Equal(story.Text, SvgTextStoryService.Read(editor.Document!.GetElementById<SvgGroup>(id)).Text);
        Assert.False(editor.IsDirty);
        editor.Redo();
        Assert.Equal(2, SvgTextStoryService.Read(editor.Document!.GetElementById<SvgGroup>(id)).Frames.Count);
    }

    [Fact]
    public void LockedOrManuallyEditedStoriesAreNeverOverwritten()
    {
        using var editor = new SvgEditorController();
        editor.NewDocument();
        var story = Story("Protected text");
        var group = SvgTextStoryService.Apply(editor, story);
        group.CustomAttributes["data-unodraw-locked"] = "true";
        Assert.Throws<InvalidOperationException>(() => SvgTextStoryService.Apply(editor, story, group));
        group.CustomAttributes.Remove("data-unodraw-locked");
        group.Descendants().OfType<SvgTextSpan>().First().Text = "Manual SVG edit";
        Assert.Throws<InvalidOperationException>(() => SvgTextStoryService.Read(group));
        Assert.Throws<InvalidOperationException>(() => SvgTextStoryService.Apply(editor, story, group));
        Assert.Equal("Manual SVG edit", group.Descendants().OfType<SvgTextSpan>().First().Text);
    }

    [Fact]
    public void ReplacingTextPreservesSurvivingRichStylesAndParagraphFormats()
    {
        var story = Story("Red title\nBlue footer");
        story.Styles.Add(new SvgStoryStyleRange { Start = 0, Length = 9, Style = new SvgStoryTextStyle { Fill = "red" } });
        story.Styles.Add(new SvgStoryStyleRange { Start = 10, Length = 11, Style = new SvgStoryTextStyle { Fill = "blue" } });
        story.ParagraphOverrides.Add(1, new SvgParagraphFormat { Alignment = SvgParagraphAlignment.End });
        SvgTextStoryService.ReplaceText(story, "Red longer title\nBlue footer");
        SvgTextLayoutService.Validate(story);
        Assert.Contains(story.Styles, range => range.Start == 17 && range.Style.Fill == "blue");
        Assert.Equal(SvgParagraphAlignment.End, story.ParagraphOverrides[1].Alignment);
        Assert.Equal("Red longer title\nBlue footer", story.Text);
    }

    [Fact]
    public void ApplyStyleSplitsOverlappingRangesWithoutDestroyingOtherStyles()
    {
        var story = Story("0123456789");
        story.Styles.Add(new SvgStoryStyleRange { Start = 0, Length = 10, Style = new SvgStoryTextStyle { Fill = "red" } });
        SvgTextStoryService.ApplyStyle(story, 3, 4, new SvgStoryTextStyle { Fill = "blue", FontWeight = 700 });
        Assert.Equal(new[] { 3, 4, 3 }, story.Styles.Select(range => range.Length));
        Assert.Equal(new[] { "red", "blue", "red" }, story.Styles.Select(range => range.Style.Fill));
    }

    [Fact]
    public void SplittingAChainPreservesContentStylesTransformsAndOneUndoStep()
    {
        using var editor = new SvgEditorController();
        editor.NewDocument();
        var story = Story(string.Join(" ", Enumerable.Repeat("The linked story flows into another frame.", 8)));
        story.Frames[0].Width = 200;
        story.Frames[0].Height = 90;
        story.Frames.Add(new SvgTextFrameDefinition { X = 250, Width = 300, Height = 600 });
        story.Styles.Add(new SvgStoryStyleRange { Start = 0, Length = story.Text.Length, Style = new SvgStoryTextStyle { FontSize = 18, Fill = "#336699" } });
        var group = SvgTextStoryService.Apply(editor, story);
        var parent = new SvgGroup { ID = "parent", Transforms = new SvgTransformCollection { new SvgScale(2) } };
        editor.Execute("Move into parent", () =>
        {
            editor.Document!.Children.Remove(group);
            editor.Document.Children.Add(parent);
            parent.Children.Add(group);
            group.Transforms = new SvgTransformCollection { new SvgTranslate(12, 18) };
            group.Opacity = 0.7f;
        });
        var split = SvgTextStoryService.SplitAtFrame(story, 1, editor.Renderer.AssetLoader);
        Assert.Equal(story.Text, split.Before.Text + split.After.Text);
        Assert.NotEmpty(split.After.Text);
        var first = SvgTextStoryService.ApplyIndependentStories(editor, new[] { split.Before, split.After }, group);
        var second = (SvgGroup)group.Children[1];
        Assert.Single(parent.Children);
        Assert.Equal(2, group.Children.Count);
        Assert.Same(group, second.Parent);
        Assert.Single(group.Transforms);
        Assert.True(second.Transforms is null || second.Transforms.Count == 0);
        Assert.Equal(0.7f, group.Opacity);
        Assert.Equal(1f, second.Opacity);
        Assert.NotEqual(first.ID, second.ID);
        Assert.Equal("#336699", SvgTextStoryService.Read(second).Styles.Single().Style.Fill);
        editor.Undo();
        var restoredParent = editor.Document!.GetElementById<SvgGroup>("parent");
        Assert.Single(restoredParent.Children);
        Assert.Equal(2, SvgTextStoryService.Read((SvgGroup)restoredParent.Children[0]).Frames.Count);
        Assert.Equal(story.Text, SvgTextStoryService.Read((SvgGroup)restoredParent.Children[0]).Text);
    }

    [Fact]
    public void NewFrameMapsSelectionThroughRootViewportAndNestedTransforms()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='800' height='400' viewBox='100 200 400 200'><g transform='translate(20 30) scale(2)'><rect id='reference' x='60' y='100' width='80' height='50'/></g></svg>");
        var reference = editor.Document!.GetElementById<SvgRectangle>("reference");
        editor.Select(reference);
        var frame = SvgTextStoryService.CreateFrameFromSelection(editor);
        Assert.Equal(140, frame.X, 2);
        Assert.Equal(230, frame.Y, 2);
        Assert.Equal(160, frame.Width, 2);
        Assert.Equal(100, frame.Height, 2);
        Assert.Single(editor.Document.Children);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void FrameOrderControlsTextFlowWithoutChangingStableFrameKeys()
    {
        var story = Story("Text begins in the first linked frame.");
        var original = story.Frames[0];
        var added = new SvgTextFrameDefinition { X = 600, Width = 400, Height = 200 };
        story.Frames.Insert(0, added);
        var layout = SvgTextLayoutService.Layout(story);
        Assert.True(layout.Lines[0].X >= 600);
        Assert.Equal(original.Key, story.Frames[1].Key);
        story.Frames.RemoveAt(0);
        Assert.True(SvgTextLayoutService.Layout(story).Lines[0].X < 600);
    }

    [Fact]
    public void PaintingWithDocumentResourcesSurvivesGenerationAndReload()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='500' height='600'><defs><linearGradient id='ink'><stop offset='0' stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient></defs></svg>");
        var story = Story("Linked paint stays in the document.");
        story.DefaultStyle.Fill = "url(#ink)";
        var group = SvgTextStoryService.Apply(editor, story);
        var span = group.Descendants().OfType<SvgTextSpan>().First();
        Assert.IsType<SvgLinearGradientServer>(SvgDeferredPaintServer.TryGet<SvgPaintServer>(span.Fill, span));
        var saved = editor.GetSvg();
        editor.LoadSvg(saved);
        var reloaded = editor.Document!.GetElementById<SvgGroup>(group.ID);
        Assert.Equal("url(#ink)", SvgTextStoryService.Read(reloaded).DefaultStyle.Fill);
        var restoredSpan = reloaded.Descendants().OfType<SvgTextSpan>().First();
        Assert.IsType<SvgLinearGradientServer>(SvgDeferredPaintServer.TryGet<SvgPaintServer>(restoredSpan.Fill, restoredSpan));
    }

    [Theory]
    [InlineData(float.NaN, 100)]
    [InlineData(100, float.PositiveInfinity)]
    [InlineData(0, 100)]
    [InlineData(10, 10)]
    public void InvalidFrameBoundsFailBeforeMutatingDocument(float width, float height)
    {
        using var editor = new SvgEditorController();
        editor.NewDocument();
        var before = editor.GetSvg();
        var story = Story("Text");
        story.Frames[0].Width = width;
        story.Frames[0].Height = height;
        Assert.Throws<ArgumentException>(() => SvgTextStoryService.Apply(editor, story));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void MalformedRangesDuplicateFrameKeysAndExternalEntitiesAreRejected()
    {
        var story = Story("😀 emoji");
        story.Styles.Add(new SvgStoryStyleRange { Start = 1, Length = 1 });
        Assert.Throws<ArgumentException>(() => SvgTextLayoutService.Layout(story));
        story.Styles.Clear();
        story.Frames.Add(story.Frames[0].Clone());
        Assert.Throws<ArgumentException>(() => SvgTextLayoutService.Layout(story));
        Assert.Throws<System.Xml.XmlException>(() => SvgTextStoryService.Deserialize("<!DOCTYPE story [<!ENTITY x SYSTEM 'file:///not-read'>]><story version='1'>&x;</story>"));
    }

    [Fact]
    public void RightToLeftParagraphUsesDirectedShapingAndRightAlignment()
    {
        var story = Story("مرحبا بالعالم");
        story.Paragraph.RightToLeft = true;
        var layout = SvgTextLayoutService.Layout(story);
        Assert.Single(layout.Lines);
        Assert.True(layout.Lines[0].RightToLeft);
        Assert.True(layout.Lines[0].X > 10);
        var group = SvgTextStoryService.CreateGroup(story, layout);
        Assert.All(group.Descendants().OfType<SvgTextSpan>(), span =>
        {
            Assert.True(span.TryGetAttribute("direction", out var direction));
            Assert.Equal("rtl", direction);
        });
    }

    private static SvgTextStory Story(string text)
    {
        var story = new SvgTextStory { Text = text, DefaultStyle = new SvgStoryTextStyle { FontSize = 18, FontFamily = "DejaVu Sans" } };
        story.Frames.Add(new SvgTextFrameDefinition { Width = 480, Height = 600, Padding = 10 });
        return story;
    }
}
