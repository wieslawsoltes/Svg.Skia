using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Svg.FilterEffects;
using Svg.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgSymbolToolTests
{
    private const string Symbols = "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='200' height='120'><defs><symbol id='symbol' viewBox='0 0 20 10'><rect id='symbol-art' width='20' height='10' fill='red'/></symbol></defs><use id='one' xlink:href='#symbol' x='50' y='50' width='40' height='20'/><use id='two' xlink:href='#symbol' x='120' y='50' width='40' height='20'/></svg>";

    [Fact]
    public void SprayPreviewAndCommitShareDeterministicPictureCoordinatesInImportedViewport()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='400' height='200' viewBox='10 20 200 100'><defs><symbol id='symbol' viewBox='0 0 20 10'><rect width='20' height='10' fill='red'/></symbol></defs></svg>");
        var original = editor.GetSvg();
        var options = new SvgSymbolBrushOptions { Radius = 5, Size = 20, Density = 4, SizeVariation = 0, RotationVariation = 0, Seed = 37 };
        var centers = new[] { new SKPoint(100, 80), new SKPoint(200, 80) };
        var preview = SvgSymbolSprayService.CreatePlacements(centers, options);
        var instances = editor.SpraySymbols("symbol", centers, options);
        Assert.Equal(8, instances.Count);
        Assert.Equal(8, instances.Select(instance => instance.ID).Distinct().Count());
        for (var index = 0; index < instances.Count; index++)
        {
            var scene = editor.GetSceneNode(instances[index]);
            Assert.NotNull(scene);
            var bounds = scene!.TransformedBounds;
            Assert.Equal(preview[index].Position.X, (bounds.Left + bounds.Right) / 2, 3);
            Assert.Equal(preview[index].Position.Y, (bounds.Top + bounds.Bottom) / 2, 3);
            Assert.Equal(20, bounds.Right - bounds.Left, 3);
            Assert.Equal(10, bounds.Bottom - bounds.Top, 3);
            Assert.Equal("#symbol", instances[index].ReferencedElement.OriginalString);
        }
        Assert.Single(editor.Document!.Descendants().OfType<SvgSymbol>());
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void NativeSymbolPreviewKeepsExplicitPaintAndDoesNotMutateSource()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Symbols);
        var original = editor.GetSvg();
        var document = editor.CreateSymbolPreviewDocument("symbol");
        using var renderer = new SKSvg();
        renderer.FromSvgDocument(document);
        using var bitmap = new SKBitmap(64, 64);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        renderer.Draw(canvas);
        Assert.True(bitmap.GetPixel(32, 32).Red > 240);
        Assert.True(bitmap.GetPixel(32, 32).Blue < 10);
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Theory]
    [InlineData(SvgSymbolBrushKind.Shift)]
    [InlineData(SvgSymbolBrushKind.Scrunch)]
    [InlineData(SvgSymbolBrushKind.Size)]
    [InlineData(SvgSymbolBrushKind.Spin)]
    [InlineData(SvgSymbolBrushKind.Screen)]
    public void SymbolBrushesPrepareWithoutWritesAndCommitOnlySelectedInstances(SvgSymbolBrushKind kind)
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Symbols);
        var first = editor.Document!.GetElementById<SvgUse>("one");
        var second = editor.Document.GetElementById<SvgUse>("two");
        editor.Select(first);
        var original = editor.GetSvg();
        using var preview = editor.PrepareSymbolBrush(kind, new[] { new SKPoint(70, 60), new SKPoint(85, 65) },
            new SvgSymbolBrushOptions { Radius = 200, Strength = 1 });
        Assert.True(preview.HasChanges);
        Assert.Single(preview.Bounds);
        Assert.Equal(original, editor.GetSvg());
        Assert.False(editor.CanUndo);
        Assert.True(editor.ApplySymbolBrush(preview));
        Assert.NotEqual(original, editor.GetSvg());
        Assert.Same(second, editor.Document.GetElementById("two"));
        Assert.Equal("#symbol", Assert.IsType<SvgUse>(Assert.Single(editor.SelectedElements)).ReferencedElement.OriginalString);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void SymbolStainerChangesExplicitSymbolPaintWithoutEditingTheSharedDefinition()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Symbols);
        editor.CurrentFill = "#0000FF";
        editor.Select(editor.Document!.GetElementById<SvgUse>("one"));
        var options = new SvgSymbolBrushOptions { Radius = 80, Strength = 1 };
        using (var preview = editor.PrepareSymbolBrush(SvgSymbolBrushKind.Stain, new[] { new SKPoint(70, 60) }, options))
            Assert.True(editor.ApplySymbolBrush(preview));
        Assert.Single(editor.Document.Descendants().OfType<SvgFilter>());
        Assert.Equal("#symbol", editor.Document.GetElementById<SvgUse>("one").ReferencedElement.OriginalString);
        using var bitmap = Render(editor);
        Assert.True(bitmap.GetPixel(70, 60).Blue > 240);
        Assert.True(bitmap.GetPixel(70, 60).Red < 10);
        Assert.True(bitmap.GetPixel(140, 60).Red > 240);
        using (var reverse = editor.PrepareSymbolBrush(SvgSymbolBrushKind.Stain, new[] { new SKPoint(70, 60) }, options, true))
            Assert.True(editor.ApplySymbolBrush(reverse));
        using var restored = Render(editor);
        Assert.True(restored.GetPixel(70, 60).Red > 240);
        Assert.Equal(2, editor.Session.UndoCount);
    }

    [Fact]
    public void SymbolStylerCreatesReusableVariantAndReverseRestoresOriginalReference()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Symbols.Replace("width='200'", "data-style-electric='fill:#0000FF;stroke:none;opacity:0.8' width='200'"));
        editor.Select(editor.Document!.GetElementById<SvgUse>("one"));
        var options = new SvgSymbolBrushOptions { Radius = 80, Strength = 1, GraphicStyleName = "electric" };
        var original = editor.GetSvg();
        using (var prepared = editor.PrepareSymbolBrush(SvgSymbolBrushKind.Style, new[] { new SKPoint(70, 60) }, options))
            Assert.True(editor.ApplySymbolBrush(prepared));
        var instance = editor.Document.GetElementById<SvgUse>("one");
        Assert.NotEqual("#symbol", instance.ReferencedElement.OriginalString);
        Assert.Equal(2, editor.Document.Descendants().OfType<SvgSymbol>().Count());
        using var bitmap = Render(editor);
        Assert.True(bitmap.GetPixel(70, 60).Blue > 240);
        Assert.True(bitmap.GetPixel(140, 60).Red > 240);
        using (var reversed = editor.PrepareSymbolBrush(SvgSymbolBrushKind.Style, new[] { new SKPoint(70, 60) }, options, true))
            Assert.True(editor.ApplySymbolBrush(reversed));
        Assert.Equal("#symbol", editor.Document.GetElementById<SvgUse>("one").ReferencedElement.OriginalString);
        editor.Undo();
        editor.Undo();
        Assert.Equal(original, editor.GetSvg());
    }

    [Fact]
    public void StainFilterCopiesReserveAllNestedIdsAndPreserveExistingFilters()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Symbols.Replace("</defs>", "<filter id='base-filter'><feGaussianBlur id='blur-node' stdDeviation='0.2'/></filter></defs>")
            .Replace("id='one'", "id='one' filter='url(#base-filter)'").Replace("id='two'", "id='two' filter='url(#base-filter)'"));
        editor.CurrentFill = "#0000FF";
        editor.SelectAll();
        using var prepared = editor.PrepareSymbolBrush(SvgSymbolBrushKind.Stain, new[] { new SKPoint(100, 60) },
            new SvgSymbolBrushOptions { Radius = 200, Strength = 1 });
        Assert.True(editor.ApplySymbolBrush(prepared));
        var ids = editor.Document!.Descendants().Select(element => element.ID).Where(id => !string.IsNullOrEmpty(id)).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(3, editor.Document.Descendants().OfType<SvgFilter>().Count());
        Assert.All(editor.Document.Descendants().OfType<SvgFilter>(), filter => Assert.Contains(filter.Children, child => child is SvgGaussianBlur));
    }

    [Fact]
    public void SymbolDraftsRejectStalenessAndInvalidSpraysWithoutPartialWrites()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg(Symbols);
        editor.Select(editor.Document!.GetElementById<SvgUse>("one"));
        using var prepared = editor.PrepareSymbolBrush(SvgSymbolBrushKind.Size, new[] { new SKPoint(70, 60) });
        editor.MoveSelection(10, 0);
        var changed = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.ApplySymbolBrush(prepared));
        Assert.Equal(changed, editor.GetSvg());
        Assert.Throws<InvalidOperationException>(() => editor.SpraySymbols("missing", new[] { new SKPoint(70, 60) }));
        Assert.Throws<InvalidOperationException>(() => editor.SpraySymbols("symbol", Enumerable.Repeat(new SKPoint(70, 60), 50).ToArray(), new SvgSymbolBrushOptions { Density = 100 }));
        Assert.Equal(changed, editor.GetSvg());
        Assert.Equal(1, editor.Session.UndoCount);
        using var distant = editor.PrepareSymbolBrush(SvgSymbolBrushKind.Size, new[] { new SKPoint(1000, 1000) });
        Assert.False(distant.HasChanges);
        Assert.False(editor.ApplySymbolBrush(distant));
        Assert.Equal(1, editor.Session.UndoCount);
    }

    private static SKBitmap Render(SvgEditorController editor)
    {
        var bitmap = new SKBitmap(200, 120);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        editor.Renderer.Draw(canvas);
        return bitmap;
    }
}
