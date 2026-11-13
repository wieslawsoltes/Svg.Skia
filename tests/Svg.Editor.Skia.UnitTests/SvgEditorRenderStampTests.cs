using System;
using SkiaSharp;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorRenderStampTests
{
    [Fact]
    public void SelectionAndSavepointDoNotChangeRenderedContent()
    {
        using var editor = CreateEditor();
        var before = SvgEditorRenderStamp.Capture(editor);
        editor.SelectAll();
        Assert.Equal(before, SvgEditorRenderStamp.Capture(editor));
        editor.ClearSelection();
        editor.MarkSaved();
        Assert.Equal(before, SvgEditorRenderStamp.Capture(editor));
        Assert.Equal(before.GetHashCode(), SvgEditorRenderStamp.Capture(editor).GetHashCode());
    }

    [Fact]
    public void PreviewCommitUndoAndReplacementInvalidateContent()
    {
        using var editor = CreateEditor();
        var before = SvgEditorRenderStamp.Capture(editor);
        var revision = editor.DocumentRevision;
        editor.Document!.GetElementById<SvgRectangle>("r").X = 40;
        editor.Refresh();
        Assert.Equal(revision, editor.DocumentRevision);
        var preview = SvgEditorRenderStamp.Capture(editor);
        Assert.NotEqual(before, preview);
        editor.SelectAll();
        editor.MoveSelection(3, 4);
        var moved = SvgEditorRenderStamp.Capture(editor);
        Assert.NotEqual(preview, moved);
        editor.Undo();
        var undone = SvgEditorRenderStamp.Capture(editor);
        Assert.NotEqual(moved, undone);
        editor.NewDocument();
        Assert.NotEqual(undone, SvgEditorRenderStamp.Capture(editor));
        Assert.NotEqual(default, SvgEditorRenderStamp.Capture(editor));
        Assert.Equal(default, SvgEditorRenderStamp.Capture(null));
    }

    [Fact]
    public void WireframeAndRendererOnlyChangesInvalidateContent()
    {
        using var editor = CreateEditor();
        var before = SvgEditorRenderStamp.Capture(editor);
        editor.Renderer.Wireframe = true;
        Assert.NotEqual(before, SvgEditorRenderStamp.Capture(editor));
        editor.Renderer.Wireframe = false;
        Assert.Equal(before, SvgEditorRenderStamp.Capture(editor));
        var revision = editor.RenderRevision;
        editor.Renderer.RebuildFromModel();
        Assert.Equal(revision, editor.RenderRevision);
        Assert.NotEqual(before, SvgEditorRenderStamp.Capture(editor));
    }

    [Fact]
    public void ViewerTransformsInvalidateContentWithoutReplacingThePictureOrModel()
    {
        using var editor = CreateEditor();
        var renderer = editor.Renderer;
        var picture = renderer.Picture;
        var model = renderer.Model;
        var documentRevision = editor.DocumentRevision;
        var renderRevision = editor.RenderRevision;
        var before = SvgEditorRenderStamp.Capture(editor);

        Assert.True(renderer.SetViewerTransform(1.75, new ShimSkiaSharp.SKPoint(12.5f, -8.25f)));
        var transformed = SvgEditorRenderStamp.Capture(editor);
        Assert.NotEqual(before, transformed);
        Assert.True(renderer.ZoomTo(2.5));
        var zoomed = SvgEditorRenderStamp.Capture(editor);
        Assert.NotEqual(transformed, zoomed);
        Assert.True(renderer.PanTo(new ShimSkiaSharp.SKPoint(-4.5f, 19.25f)));
        Assert.NotEqual(zoomed, SvgEditorRenderStamp.Capture(editor));

        Assert.Same(picture, renderer.Picture);
        Assert.Same(model, renderer.Model);
        Assert.Equal(documentRevision, editor.DocumentRevision);
        Assert.Equal(renderRevision, editor.RenderRevision);
        Assert.True(renderer.ResetViewerTransform());
        Assert.Equal(before, SvgEditorRenderStamp.Capture(editor));
        Assert.Equal(before.GetHashCode(), SvgEditorRenderStamp.Capture(editor).GetHashCode());
    }

    [Fact]
    public void AnimationAndDifferentControllersNeverReuseStaleContent()
    {
        using var editor = CreateEditor();
        using var other = CreateEditor();
        Assert.NotEqual(SvgEditorRenderStamp.Capture(editor), SvgEditorRenderStamp.Capture(other));
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect x='20' y='25' width='10' height='10'><animate attributeName='x' from='20' to='80' dur='1s' fill='freeze'/></rect></svg>");
        var before = SvgEditorRenderStamp.Capture(editor);
        editor.Renderer.SetAnimationTime(TimeSpan.FromMilliseconds(750));
        Assert.NotEqual(before, SvgEditorRenderStamp.Capture(editor));
    }

    private static SvgEditorController CreateEditor()
    {
        var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><rect id='r' x='20' y='25' width='10' height='10'/></svg>");
        return editor;
    }
}
