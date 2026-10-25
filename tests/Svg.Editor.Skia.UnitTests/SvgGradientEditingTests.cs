using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgGradientEditingTests
{
    private const string Simple = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='art' x='10' y='10' width='80' height='60' fill='red'/></svg>";
    private static readonly XNamespace SvgNs = "http://www.w3.org/2000/svg";

    [Fact]
    public void DraftAndPreviewDoNotChangeLiveDocumentOrHistory()
    {
        using var editor = Load(Simple);
        var before = editor.GetSvg(); var picture = editor.Renderer.Picture; var revision = editor.RenderRevision;
        var draft = editor.BeginGradientEdit();
        draft.Ramp = new SvgGradientRamp(new[] { (0d, SKColors.Red, 1d), (1d, SKColors.Blue, 1d) });
        using var preview = editor.CreateGradientPreviewRenderer(draft);
        using var pixels = new SKBitmap(100, 80);
        using var canvas = new SKCanvas(pixels); canvas.Clear(SKColors.Transparent); preview.Draw(canvas);
        Assert.True(pixels.GetPixel(20, 30).Red > pixels.GetPixel(20, 30).Blue);
        Assert.True(pixels.GetPixel(80, 30).Blue > pixels.GetPixel(80, 30).Red);
        Assert.Equal(before, editor.GetSvg()); Assert.Same(picture, editor.Renderer.Picture);
        Assert.Equal(revision, editor.RenderRevision); Assert.False(editor.CanUndo); Assert.False(editor.IsDirty);
    }

    [Fact]
    public void ApplyCreatesOneUndoEntryAndRendersImmediatelyAndAfterReopen()
    {
        using var editor = Load(Simple);
        var before = editor.GetSvg(); var draft = editor.BeginGradientEdit();
        draft.Ramp = new SvgGradientRamp(new[] { (0d, SKColors.Red, 1d), (1d, SKColors.Blue, 1d) });
        editor.ApplyGradientEdit(draft);
        Assert.Equal(1, editor.Session.UndoCount);
        Assert.Equal("art", Assert.Single(editor.SelectedElements).ID);
        using var immediate = Render(editor);
        Assert.True(immediate.GetPixel(20, 30).Red > immediate.GetPixel(20, 30).Blue);
        var after = editor.GetSvg();
        using var reopened = Load(after); using var again = Render(reopened);
        Assert.Equal(immediate.Pixels, again.Pixels);
        editor.Undo(); Assert.Equal(before, editor.GetSvg()); Assert.False(editor.IsDirty);
        editor.Redo(); Assert.Equal(after, editor.GetSvg());
    }

    [Fact]
    public void ExistingGradientCopiesPreserveCoordinatesTransformsMetadataAndOtherConsumers()
    {
        using var editor = Load("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <defs><linearGradient id="g" gradientUnits="userSpaceOnUse" x1="10" y1="0" x2="90" y2="0" gradientTransform="translate(0 5)">
                <title>Retain gradient metadata</title><stop id="red" offset="0" stop-color="red"/><stop id="blue" offset="1" stop-color="blue"/>
              </linearGradient></defs>
              <rect id="art" x="10" y="10" width="80" height="40" fill="url(#g)"/>
              <rect id="other" x="10" y="60" width="80" height="10" fill="url(#g)"/>
            </svg>
            """);
        // Parsing already canonicalizes transform separators. The editor must retain
        // that canonical value, not reproduce the fixture's original whitespace.
        var original = XDocument.Parse(editor.GetSvg()).Descendants(SvgNs + "linearGradient").Single();
        var originalTransform = (string?)original.Attribute("gradientTransform");
        Assert.False(string.IsNullOrWhiteSpace(originalTransform));
        var draft = editor.BeginGradientEdit();
        Assert.Equal("g", draft.OriginalGradientId); Assert.False(draft.UsesObjectBounds);
        draft.Ramp = draft.Ramp.SetColor(draft.Ramp.Stops[0].Id, SKColors.Lime);
        editor.ApplyGradientEdit(draft);
        var tree = XDocument.Parse(editor.GetSvg()); var gradients = tree.Descendants(SvgNs + "linearGradient").ToArray();
        Assert.Equal(2, gradients.Length);
        var copy = gradients.Single(element => (string?)element.Attribute("id") != "g");
        foreach (var name in new[] { "gradientUnits", "gradientTransform", "x1", "y1", "x2", "y2" })
            Assert.Equal((string?)original.Attribute(name), (string?)copy.Attribute(name));
        Assert.Equal(originalTransform, (string?)copy.Attribute("gradientTransform"));
        Assert.Contains("Retain gradient metadata", copy.Value);
        var ids = tree.Descendants().Attributes("id").Select(attribute => attribute.Value).ToArray(); Assert.Equal(ids.Length, ids.Distinct().Count());
        using var pixels = Render(editor);
        Assert.True(pixels.GetPixel(15, 20).Green > pixels.GetPixel(15, 20).Red);
        Assert.True(pixels.GetPixel(15, 65).Red > pixels.GetPixel(15, 65).Green);
    }

    [Fact]
    public void InheritedStopsRemainEditableWithoutChangingTemplate()
    {
        using var editor = Load("""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="100" height="80">
              <defs><linearGradient id="template"><stop id="s" offset="0" style="stop-color:red;stop-opacity:0.5"/><stop offset="100%" stop-color="blue"/></linearGradient>
              <linearGradient id="g" xlink:href="#template" gradientTransform="rotate(10 .5 .5)"/></defs>
              <rect id="art" width="100" height="80" fill="url(#g)"/>
            </svg>
            """);
        var draft = editor.BeginGradientEdit();
        Assert.Equal(SKColors.Red, draft.Ramp.Stops[0].Color); Assert.Equal(0.5, draft.Ramp.Stops[0].Opacity);
        draft.Ramp = draft.Ramp.Reverse(); editor.ApplyGradientEdit(draft);
        var tree = XDocument.Parse(editor.GetSvg());
        var original = tree.Descendants(SvgNs + "linearGradient").Single(element => (string?)element.Attribute("id") == "template");
        Assert.Contains("red", original.ToString());
        var generated = tree.Descendants(SvgNs + "linearGradient").Single(element => (string?)element.Attribute("id") is not ("template" or "g"));
        Assert.Equal("#template", (string?)generated.Attribute(XNamespace.Get("http://www.w3.org/1999/xlink") + "href"));
        Assert.Equal(2, generated.Elements(SvgNs + "stop").Count());
    }

    [Fact]
    public void ImportedDecreasingOffsetsUseSvgMonotoneRuleInsteadOfReorderingColors()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><defs><linearGradient id='g'><stop offset='0.8' stop-color='red'/><stop offset='0.2' stop-color='blue'/></linearGradient></defs><rect id='art' width='100' height='80' fill='url(#g)'/></svg>");
        var draft = editor.BeginGradientEdit();
        Assert.Equal(0.8, draft.Ramp.Stops[0].Offset, 6); Assert.Equal(0.8, draft.Ramp.Stops[1].Offset, 6);
        Assert.Equal(SKColors.Red, draft.Ramp.Stops[0].Color); Assert.Equal(SKColors.Blue, draft.Ramp.Stops[1].Color);
    }

    [Fact]
    public void SharedEditsAreExplicitAndKeepOriginalResourceIdentity()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><defs><linearGradient id='g'><stop id='s' offset='0' stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient></defs><rect id='art' width='100' height='80' fill='url(#g)'/></svg>");
        var draft = editor.BeginGradientEdit(); draft.Ramp = draft.Ramp.Reverse(); editor.ApplyGradientEdit(draft, true);
        var gradient = Assert.Single(XDocument.Parse(editor.GetSvg()).Descendants(SvgNs + "linearGradient"));
        Assert.Equal("g", (string?)gradient.Attribute("id")); Assert.NotNull(editor.Document!.GetElementById("s"));
    }

    [Fact]
    public void LockedConsumerThroughGradientInheritanceBlocksSharedButNotIsolatedEditing()
    {
        using var editor = Load("""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="100" height="80">
              <defs><linearGradient id="g"><stop offset="0" stop-color="red"/><stop offset="1" stop-color="blue"/></linearGradient><linearGradient id="alias" xlink:href="#g"/></defs>
              <rect id="art" width="40" height="40" fill="url(#g)"/><rect id="locked" x="50" width="40" height="40" fill="url(#alias)" data-lock="true"/>
            </svg>
            """);
        var before = editor.GetSvg(); var draft = editor.BeginGradientEdit();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyGradientEdit(draft, true));
        Assert.Equal(before, editor.GetSvg()); Assert.False(editor.CanUndo);
        editor.ApplyGradientEdit(draft); Assert.True(editor.IsDirty);
    }

    [Fact]
    public void ExplicitGeometryReplacementSupportsRadialAndRemovesInheritedTransform()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><defs><linearGradient id='g' gradientUnits='userSpaceOnUse' gradientTransform='scale(2)' x2='100'><stop offset='0' stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient></defs><rect id='art' width='100' height='80' fill='url(#g)'/></svg>");
        var draft = editor.BeginGradientEdit(); draft.UseObjectBounds(SvgGradientKind.Radial); draft.Spread = SvgGradientSpread.Reflect;
        editor.ApplyGradientEdit(draft);
        var radial = Assert.Single(XDocument.Parse(editor.GetSvg()).Descendants(SvgNs + "radialGradient"));
        Assert.Equal("objectBoundingBox", (string?)radial.Attribute("gradientUnits"));
        Assert.Equal("reflect", (string?)radial.Attribute("spreadMethod")); Assert.Null(radial.Attribute("gradientTransform"));
        Assert.Null(radial.Attribute("x2"));
    }

    [Fact]
    public void ColorAlphaStopOpacityPaintOpacityAndObjectOpacityRemainIndependent()
    {
        using var editor = Load(Simple.Replace("fill='red'", "fill='red' fill-opacity='0.8' opacity='0.5'"));
        var draft = editor.BeginGradientEdit();
        draft.Ramp = new SvgGradientRamp(new[] { (0d, new SKColor(255, 0, 0, 128), 0.5), (1d, new SKColor(255, 0, 0, 128), 0.5) });
        editor.ApplyGradientEdit(draft);
        for (var pass = 0; pass < 3; pass++)
        {
            using var pixels = Render(editor); Assert.InRange(pixels.GetPixel(50, 40).Alpha, (byte)24, (byte)28);
            var art = editor.Document!.GetElementById<SvgRectangle>("art"); Assert.Equal(0.8f, art.FillOpacity); Assert.Equal(0.5f, art.Opacity);
            editor.LoadSvg(editor.GetSvg());
        }
    }

    [Fact]
    public void StrokeTargetDoesNotReplaceFillAndSurvivesCultureSpecificEnvironment()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            using var editor = Load(Simple.Replace("fill='red'", "fill='red' stroke='black' stroke-width='4'"));
            var draft = editor.BeginGradientEdit(true); draft.UseObjectBounds(SvgGradientKind.Linear, 37.5); editor.ApplyGradientEdit(draft);
            var art = editor.Document!.GetElementById<SvgRectangle>("art"); Assert.IsType<SvgColourServer>(art.Fill);
            Assert.Equal(4, art.StrokeWidth.Value); Assert.Contains("gradient", art.Stroke.ToString());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void StaleChangedSelectionReloadedDocumentAndRepeatedApplyAreRejected()
    {
        using var editor = Load(Simple); var edit = editor.BeginGradientEdit(); editor.MoveSelection(1, 0);
        Assert.Throws<InvalidOperationException>(() => editor.ApplyGradientEdit(edit));
        edit = editor.BeginGradientEdit(); editor.ClearSelection(); Assert.Throws<InvalidOperationException>(() => editor.ApplyGradientEdit(edit));
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("art")); edit = editor.BeginGradientEdit();
        editor.LoadSvg(editor.GetSvg()); editor.Select(editor.Document!.GetElementById<SvgRectangle>("art"));
        Assert.Throws<InvalidOperationException>(() => editor.ApplyGradientEdit(edit));
        edit = editor.BeginGradientEdit(); editor.ApplyGradientEdit(edit);
        Assert.Throws<InvalidOperationException>(() => editor.ApplyGradientEdit(edit));
    }

    [Fact]
    public void AnonymousSelectionAndMultipleTargetsSurviveApplication()
    {
        using var editor = new SvgEditorController(); editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect width='30' height='30'/><rect x='50' width='30' height='30' fill='red'/></svg>");
        editor.SelectAll(); var edit = editor.BeginGradientEdit(); Assert.True(edit.IsMixed);
        editor.ApplyGradientEdit(edit); Assert.Equal(2, editor.SelectedElements.Count);
        Assert.All(editor.SelectedElements, element => Assert.IsType<SvgRectangle>(element)); Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void AnimatedGradientRequiresExplicitReplacementInsteadOfSilentlyDiscardingAnimation()
    {
        using var editor = Load("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><defs><linearGradient id='g'><stop offset='0' stop-color='red'><animate attributeName='stop-color' values='red;blue' dur='1s'/></stop><stop offset='1' stop-color='blue'/></linearGradient></defs><rect id='art' width='100' height='80' fill='url(#g)'/></svg>");
        Assert.Throws<InvalidOperationException>(() => editor.BeginGradientEdit());
        Assert.False(editor.CanUndo); var draft = editor.BeginGradientEdit(createNew: true); editor.ApplyGradientEdit(draft);
        Assert.Single(XDocument.Parse(editor.GetSvg()).Descendants(SvgNs + "animate"));
    }

    private static SvgEditorController Load(string xml)
    {
        var editor = new SvgEditorController(); editor.LoadSvg(xml); editor.Select(editor.Document!.GetElementById<SvgVisualElement>("art")); return editor;
    }
    private static SKBitmap Render(SvgEditorController editor)
    {
        var bitmap = new SKBitmap(100, 80); using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.Transparent); editor.Renderer.Draw(canvas); return bitmap;
    }
}
