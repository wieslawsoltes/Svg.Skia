using System;
using SkiaSharp;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorSelectionPreviewTests
{
    [Fact]
    public void RetainedSelectionKeepsNestedTransformsGradientClipAndOpacity()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <defs>
                <linearGradient id="paint"><stop stop-color="red"/><stop offset="1" stop-color="blue"/></linearGradient>
                <clipPath id="clip"><rect width="8" height="20"/></clipPath>
              </defs>
              <g transform="translate(20 10)" opacity=".5"><g transform="scale(2)" clip-path="url(#clip)">
                <rect id="selected" x="2" y="3" width="10" height="10" fill="url(#paint)"/>
                <rect id="sibling" x="3" y="3" width="2" height="10" fill="lime"/>
              </g></g>
            </svg>
            """);
        var source = editor.GetSvg();
        var originalPicture = editor.Renderer.Picture;
        var revision = editor.RenderRevision;
        using var preview = editor.Renderer.CreateRetainedSceneSelectionPicture(new SvgElement[]
            { editor.Document!.GetElementById("selected") });
        Assert.NotNull(preview);
        using var bitmap = Render(preview!);
        var color = bitmap.GetPixel(28, 20);
        Assert.InRange(color.Alpha, (byte)127, (byte)129);
        Assert.True(color.Red > 0 && color.Blue > 0);
        Assert.Equal((byte)0, color.Green);
        Assert.Equal((byte)0, bitmap.GetPixel(40, 20).Alpha);
        Assert.Equal((byte)0, bitmap.GetPixel(10, 20).Alpha);
        Assert.Same(originalPicture, editor.Renderer.Picture);
        Assert.Equal(source, editor.GetSvg());
        Assert.Equal(revision, editor.RenderRevision);
        Assert.False(editor.IsDirty);
        Assert.Equal(0, editor.Session.UndoCount);
    }

    [Fact]
    public void MultipleSelectedChildrenKeepPaintOrderAndOneGroupOpacityLayer()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
              <g id="group" opacity=".5">
                <rect id="red" x="10" y="10" width="30" height="30" fill="red"/>
                <rect id="blue" x="20" y="10" width="30" height="30" fill="blue"/>
                <rect id="excluded" x="60" y="10" width="10" height="10" fill="lime"/>
              </g>
            </svg>
            """);
        using var preview = editor.Renderer.CreateRetainedSceneSelectionPicture(new SvgElement[]
        {
            editor.Document!.GetElementById("blue"), editor.Document.GetElementById("red")
        });
        using var bitmap = Render(preview!);
        var overlap = bitmap.GetPixel(25, 20);
        Assert.InRange(overlap.Alpha, (byte)127, (byte)129);
        Assert.Equal((byte)0, overlap.Red);
        Assert.Equal((byte)255, overlap.Blue);
        Assert.Equal((byte)0, bitmap.GetPixel(65, 15).Alpha);

        // Selecting an ancestor and its descendant renders that descendant only once.
        using var wholeGroup = editor.Renderer.CreateRetainedSceneSelectionPicture(new SvgElement[]
        {
            editor.Document.GetElementById("group"), editor.Document.GetElementById("blue")
        });
        using var groupBitmap = Render(wholeGroup!);
        Assert.Equal(overlap, groupBitmap.GetPixel(25, 20));
        Assert.InRange(groupBitmap.GetPixel(65, 15).Alpha, (byte)127, (byte)129);
    }

    [Fact]
    public void RetainedSelectionKeepsAncestorMaskAndCanBeReplayedAfterRendererDisposal()
    {
        SKPicture? preview;
        using (var editor = new SvgEditorController())
        {
            editor.LoadSvg("""
                <svg xmlns="http://www.w3.org/2000/svg" width="100" height="80">
                  <defs><mask id="mask" maskUnits="userSpaceOnUse" x="0" y="0" width="100" height="80">
                    <rect x="10" y="10" width="10" height="20" fill="white"/>
                  </mask></defs>
                  <g mask="url(#mask)"><rect id="shape" x="10" y="10" width="30" height="20" fill="red"/></g>
                </svg>
                """);
            preview = editor.Renderer.CreateRetainedSceneSelectionPicture(new SvgElement[]
                { editor.Document!.GetElementById("shape") });
        }

        using (preview)
        using (var bitmap = Render(preview!, SKMatrix.CreateTranslation(5, 7)))
        {
            Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 22));
            Assert.Equal((byte)0, bitmap.GetPixel(30, 22).Alpha);
        }
    }

    [Fact]
    public void EmptySelectionDoesNotCreateAPicture()
    {
        using var editor = new SvgEditorController();
        editor.NewDocument();
        Assert.Null(editor.Renderer.CreateRetainedSceneSelectionPicture(Array.Empty<SvgElement>()));
    }

    private static SKBitmap Render(SKPicture picture, SKMatrix? transform = null)
    {
        var bitmap = new SKBitmap(100, 80);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        if (transform is { } matrix)
            canvas.Concat(matrix);
        canvas.DrawPicture(picture);
        return bitmap;
    }
}
