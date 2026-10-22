using System;
using System.Linq;
using Svg;
using Svg.Editor.Skia;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorControllerRegressionTests
{
    [Fact]
    public void MoveSelection_UsesWorldCoordinatesInsideTransformedParent()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="400" height="300">
              <g transform="translate(60 30) rotate(30) scale(2 3)">
                <rect id="shape" x="10" y="20" width="30" height="25" fill="red" />
              </g>
            </svg>
            """);
        var shape = editor.Document!.GetElementById<SvgRectangle>("shape");
        var before = editor.GetSceneNode(shape)!.TransformedBounds;

        editor.Select(shape);
        editor.MoveSelection(17, -11);

        var after = editor.GetSceneNode(shape)!.TransformedBounds;
        Assert.InRange(after.Left - before.Left, 16.99f, 17.01f);
        Assert.InRange(after.Top - before.Top, -11.01f, -10.99f);
        Assert.InRange(after.Width - before.Width, -0.01f, 0.01f);
        Assert.InRange(after.Height - before.Height, -0.01f, 0.01f);
    }

    [Fact]
    public void SelectingObjects_DoesNotModifyDocumentHistoryOrRetainedPicture()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <rect id="shape" width="30" height="25" fill="red" />
              <circle id="circle" cx="70" cy="70" r="10" fill="blue" />
            </svg>
            """);
        var xml = editor.GetSvg();
        var picture = editor.Renderer.Picture;
        var revision = editor.RenderRevision;

        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        editor.Select(editor.Document.GetElementById<SvgCircle>("circle"), additive: true);
        editor.ClearSelection();
        editor.SelectAll();

        Assert.Equal(xml, editor.GetSvg());
        Assert.Same(picture, editor.Renderer.Picture);
        Assert.Equal(revision, editor.RenderRevision);
        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);
        Assert.False(editor.CanRedo);
    }

    [Fact]
    public void FailedMutation_RestoresDocumentSelectionAndHistory()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <rect id="shape" width="30" height="25" fill="red" />
            </svg>
            """);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        var before = editor.GetSvg();

        Assert.Throws<InvalidOperationException>(() => editor.Execute("Failing edit", () =>
        {
            editor.Document.Children.Clear();
            throw new InvalidOperationException("Simulated command failure.");
        }));

        Assert.Equal(before, editor.GetSvg());
        Assert.Equal("shape", Assert.Single(editor.SelectedElements).ID);
        Assert.False(editor.IsEditing);
        Assert.False(editor.IsDirty);
        Assert.False(editor.CanUndo);
        Assert.False(editor.CanRedo);
        Assert.NotNull(editor.Renderer.Picture);
    }

    [Fact]
    public void MovingAncestorAndDescendantSelection_MovesDescendantOnce()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200">
              <g id="group" transform="translate(20 30)">
                <rect id="shape" width="30" height="25" fill="red" />
              </g>
            </svg>
            """);
        var shape = editor.Document!.GetElementById<SvgRectangle>("shape");
        var before = editor.GetSceneNode(shape)!.TransformedBounds;
        editor.Select(editor.Document.GetElementById<SvgGroup>("group"));
        editor.Select(shape, additive: true);

        editor.MoveSelection(10, 15);

        var after = editor.GetSceneNode(shape)!.TransformedBounds;
        Assert.InRange(after.Left - before.Left, 9.99f, 10.01f);
        Assert.InRange(after.Top - before.Top, 14.99f, 15.01f);
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void UndoToSavedCheckpoint_ClearsDirtyAndRetainsRedo()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <rect id="shape" width="30" height="25" fill="red" />
            </svg>
            """);
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("shape"));
        editor.MoveSelection(10, 0);
        editor.MarkSaved();
        var saved = editor.GetSvg();

        editor.MoveSelection(0, 15);
        Assert.True(editor.IsDirty);
        editor.Undo();

        Assert.Equal(saved, editor.GetSvg());
        Assert.False(editor.IsDirty);
        Assert.True(editor.CanRedo);
        editor.Redo();
        Assert.True(editor.IsDirty);
    }

    [Fact]
    public void ConvertToPath_PreservesCompositingReferencesAndMetadata()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <clipPath id="clip"><rect width="20" height="20" /></clipPath>
                <filter id="blur"><feGaussianBlur stdDeviation="1" /></filter>
              </defs>
              <rect id="shape" width="30" height="25" fill="red" opacity="0.4"
                    clip-path="url(#clip)" filter="url(#blur)" data-note="keep me">
                <title>Editable shape metadata</title>
              </rect>
            </svg>
            """);
        var original = editor.Document!.GetElementById<SvgRectangle>("shape");
        var clip = original.ClipPath;
        var filter = original.Filter;
        editor.Select(original);

        editor.ConvertSelectionToPaths();

        var path = Assert.IsType<SvgPath>(Assert.Single(editor.SelectedElements));
        Assert.Equal("shape", path.ID);
        Assert.Equal(0.4f, path.Opacity);
        Assert.Equal(clip, path.ClipPath);
        Assert.Equal(filter, path.Filter);
        Assert.True(path.CustomAttributes.TryGetValue("data-note", out var note));
        Assert.Equal("keep me", note);
        Assert.Contains(path.Children, child => child is SvgTitle title && title.Content == "Editable shape metadata");
    }

    [Fact]
    public void Paste_DoesNotRemapReferencesTwiceWhenImportedIdsShareCopySuffix()
    {
        using var editor = new SvgEditorController();
        editor.NewDocument(100, 100);
        editor.Paste("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <linearGradient id="paint"><stop offset="0" stop-color="red" /></linearGradient>
                <linearGradient id="paint-copy"><stop offset="0" stop-color="blue" /></linearGradient>
              </defs>
              <rect id="first" width="30" height="25" fill="url(#paint)" />
              <rect id="second" x="40" width="30" height="25" fill="url(#paint-copy)" />
            </svg>
            """, 0, 0);

        var shapes = editor.SelectedElements.OfType<SvgRectangle>().OrderBy(shape => shape.X.Value).ToArray();
        Assert.Equal(2, shapes.Length);
        var firstPaint = SvgDeferredPaintServer.TryGet<SvgLinearGradientServer>(shapes[0].Fill, shapes[0]);
        var secondPaint = SvgDeferredPaintServer.TryGet<SvgLinearGradientServer>(shapes[1].Fill, shapes[1]);
        Assert.True(firstPaint is not null, $"The first paint reference must resolve after paste.\n{editor.GetSvg()}");
        Assert.True(secondPaint is not null, $"The second paint reference must resolve after paste.\n{editor.GetSvg()}");
        Assert.NotSame(firstPaint, secondPaint);
        var firstColor = Assert.IsType<SvgColourServer>(firstPaint.Children.OfType<SvgGradientStop>().First().StopColor).Colour;
        var secondColor = Assert.IsType<SvgColourServer>(secondPaint.Children.OfType<SvgGradientStop>().First().StopColor).Colour;
        Assert.Equal(System.Drawing.Color.Red.ToArgb(), firstColor.ToArgb());
        Assert.Equal(System.Drawing.Color.Blue.ToArgb(), secondColor.ToArgb());
    }
}
