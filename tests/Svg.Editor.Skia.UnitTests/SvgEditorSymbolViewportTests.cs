using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorSymbolViewportTests
{
    [Fact]
    public void SymbolViewportUsesRootUserUnitsAndKeepsNestedWorldGeometry()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='800' height='400' viewBox='100 200 400 200' opacity='0.6'><g transform='translate(20 30) scale(2)'><rect id='reference' x='60' y='100' width='80' height='50' fill='red'/></g></svg>");
        var original = editor.Document!.GetElementById<SvgRectangle>("reference");
        editor.Select(original);
        var bounds = editor.SelectionBounds!.Value;
        var instance = editor.CreateSymbol("nested-component");
        var symbol = editor.Document.GetElementById<SvgSymbol>("nested-component");
        Assert.Equal(140, symbol.ViewBox.MinX, 2);
        Assert.Equal(230, symbol.ViewBox.MinY, 2);
        Assert.Equal(160, symbol.ViewBox.Width, 2);
        Assert.Equal(100, symbol.ViewBox.Height, 2);
        Assert.Equal(140, instance.X.Value, 2);
        Assert.Equal(230, instance.Y.Value, 2);
        Assert.Equal(bounds, editor.SelectionBounds!.Value);
        Assert.Equal(1, editor.Session.UndoCount);
        editor.Execute("Hide original", () => original.Display = "none");
        using var bitmap = new SKBitmap(800, 400);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        editor.Renderer.Draw(canvas);
        var pixel = bitmap.GetPixel(200, 150);
        Assert.Equal(255, pixel.Red);
        Assert.Equal(0, pixel.Blue);
        Assert.InRange(pixel.Alpha, (byte)152, (byte)154);
        Assert.Equal(0, bitmap.GetPixel(40, 40).Alpha);
        editor.Undo();
        editor.Undo();
        Assert.Null(editor.Document!.GetElementById("nested-component"));
        Assert.False(editor.IsDirty);
    }
}
