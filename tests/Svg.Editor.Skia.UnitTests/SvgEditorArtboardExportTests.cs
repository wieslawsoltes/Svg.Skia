using System;
using System.Linq;
using SkiaSharp;
using Svg;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorArtboardExportTests
{
    [Theory]
    [InlineData("width='200' height='100' viewBox='10 20 100 50'")]
    [InlineData("width='200%' height='100%' viewBox='10 20 100 50'")]
    [InlineData("width='200' height='100' viewBox='10 20 100 50' transform='translate(7 9) scale(1.5 2)'")]
    public void NamedExportMatchesPictureCropAndDoesNotChangeEditor(string dimensions)
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg($"<svg xmlns='http://www.w3.org/2000/svg' {dimensions}><rect id='art' x='20' y='25' width='20' height='20' fill='#c53a51'/><circle cx='70' cy='40' r='8' fill='#2966ba'/></svg>");
        var id = editor.AddArtboard(new SKRect(20, 10, 100, 70), "Export region");
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("art"));
        var bounds = editor.ActiveArtboard!.Value.Bounds;
        var source = editor.GetSvg();
        var renderer = editor.Renderer;
        var revision = editor.DocumentRevision;
        var undo = editor.Session.UndoCount;
        var selected = editor.SelectedElements.ToArray();
        using var expected = new SKBitmap((int)bounds.Width, (int)bounds.Height);
        using (var canvas = new SKCanvas(expected))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(-bounds.Left, -bounds.Top);
            editor.Renderer.Draw(canvas);
        }
        using var exported = new SvgEditorController();
        exported.LoadSvg(editor.GetArtboardSvg(id));
        Assert.Equal(bounds.Width, exported.Renderer.Picture!.CullRect.Width, 3);
        Assert.Equal(bounds.Height, exported.Renderer.Picture.CullRect.Height, 3);
        using var actual = new SKBitmap((int)bounds.Width, (int)bounds.Height);
        using (var canvas = new SKCanvas(actual))
        {
            canvas.Clear(SKColors.Transparent);
            exported.Renderer.Draw(canvas);
        }
        Assert.Equal(expected.Pixels, actual.Pixels);
        Assert.Equal(source, editor.GetSvg());
        Assert.Same(renderer, editor.Renderer);
        Assert.Equal(revision, editor.DocumentRevision);
        Assert.Equal(undo, editor.Session.UndoCount);
        Assert.Equal(selected, editor.SelectedElements);
        Assert.Equal(id, editor.ActiveArtboardId);
    }

    [Fact]
    public void ReadOnlyExportRetainsRootLocksAndRejectsMissingRegion()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect width='100' height='100' fill='red'/></svg>");
        Assert.Equal(editor.GetSvg(), editor.GetArtboardSvg(string.Empty));
        var id = editor.AddArtboard(new SKRect(20, 30, 70, 90), "Locked export");
        editor.Document!.CustomAttributes["data-unodraw-locked"] = "true";
        var source = editor.GetSvg();
        var output = editor.GetArtboardSvg(id);
        Assert.Contains("data-unodraw-locked=\"true\"", output);
        Assert.Equal(source, editor.GetSvg());
        Assert.Throws<InvalidOperationException>(() => editor.GetArtboardSvg("missing"));
        Assert.Equal(source, editor.GetSvg());
    }
}
