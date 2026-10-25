using System;
using System.Globalization;
using System.Linq;
using SkiaSharp;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgSolidPaintDraftTests
{
    private const string Source = "<svg xmlns='http://www.w3.org/2000/svg' width='80' height='60'><rect id='a' x='5' y='5' width='20' height='20' fill='red' stroke='black' fill-opacity='0.5' opacity='0.5'/><rect id='b' x='40' y='5' width='20' height='20' fill='blue'/></svg>";

    [Fact]
    public void OpeningDraftNeverChangesPictureDocumentOrHistory()
    {
        using var editor = Create();
        var before = editor.GetSvg(); var picture = editor.Renderer.Picture; var revision = editor.DocumentRevision;
        var draft = new SvgSolidPaintDraft(editor);
        Assert.True(draft.IsMixed);
        Assert.False(draft.HasNonSolidPaint);
        Assert.Equal(before, editor.GetSvg()); Assert.Same(picture, editor.Renderer.Picture);
        Assert.Equal(revision, editor.DocumentRevision); Assert.False(editor.CanUndo);
    }

    [Fact]
    public void ApplyingOneColorToMultipleObjectsIsOneUndoableEdit()
    {
        using var editor = Create(); var before = editor.GetSvg();
        var draft = new SvgSolidPaintDraft(editor);
        draft.Apply(editor, new SKColor(17, 83, 211));
        Assert.Equal(1, editor.Session.UndoCount);
        foreach (var element in editor.SelectedElements)
        {
            var paint = Assert.IsType<SvgColourServer>(element.Fill);
            Assert.Equal(17, paint.Colour.R); Assert.Equal(83, paint.Colour.G); Assert.Equal(211, paint.Colour.B);
        }
        Assert.False(draft.IsCurrent(editor));
        editor.Undo(); Assert.Equal(before, editor.GetSvg());
    }

    [Fact]
    public void ColorAlphaAndIndependentOpacitySurviveRepeatedReopening()
    {
        using var editor = Create();
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("a"));
        new SvgSolidPaintDraft(editor).Apply(editor, new SKColor(17, 83, 211, 128));
        for (var iteration = 0; iteration < 3; iteration++)
        {
            using var bitmap = new SKBitmap(80, 60);
            using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.Transparent); editor.Renderer.Draw(canvas);
            // 128/255 color alpha × 0.5 fill opacity × 0.5 object opacity.
            Assert.InRange((int)bitmap.GetPixel(15, 15).Alpha, 31, 33);
            var element = editor.Document!.GetElementById<SvgRectangle>("a");
            Assert.Equal(0.5f, element.FillOpacity); Assert.Equal(0.5f, element.Opacity);
            editor.LoadSvg(editor.GetSvg());
        }
    }

    [Fact]
    public void StrokeAndNoPaintEditsDoNotReplaceTheOtherPaint()
    {
        using var editor = Create();
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("a"));
        var fill = editor.SelectedElements[0].Fill;
        new SvgSolidPaintDraft(editor, true).Apply(editor, SKColors.Lime);
        Assert.Same(fill, editor.SelectedElements[0].Fill);
        new SvgSolidPaintDraft(editor, true).ApplyNone(editor);
        Assert.Equal(SvgPaintServer.None, editor.SelectedElements[0].Stroke);
        Assert.Same(fill, editor.SelectedElements[0].Fill);
    }

    [Fact]
    public void AnyInterveningDocumentEditMakesTheColorDraftStale()
    {
        using var editor = Create(); var draft = new SvgSolidPaintDraft(editor);
        editor.MoveSelection(1, 0); var newer = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => draft.Apply(editor, SKColors.Lime));
        Assert.Equal(newer, editor.GetSvg()); Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public void ChangedSelectionOrControllerCannotReceiveTheDraft()
    {
        using var editor = Create(); using var other = Create();
        var draft = new SvgSolidPaintDraft(editor);
        Assert.Throws<InvalidOperationException>(() => draft.Apply(other, SKColors.Lime));
        editor.ClearSelection();
        Assert.Throws<InvalidOperationException>(() => draft.Apply(editor, SKColors.Lime));
        Assert.False(editor.CanUndo); Assert.False(other.CanUndo);
    }

    [Fact]
    public void EmptyOrLockedSelectionCannotStartADraft()
    {
        using var editor = Create();
        editor.ClearSelection(); Assert.Throws<InvalidOperationException>(() => new SvgSolidPaintDraft(editor));
        editor.SelectAll(); editor.SetLocked(true);
        Assert.Throws<InvalidOperationException>(() => new SvgSolidPaintDraft(editor));
    }

    [Fact]
    public void LinkedPaintIsNotReplacedUntilApply()
    {
        using var editor = new SvgEditorController();
        editor.LoadSvg("<svg xmlns='http://www.w3.org/2000/svg' width='80' height='60'><defs><linearGradient id='g'><stop stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient></defs><rect id='a' width='20' height='20' fill='url(#g)'/></svg>");
        editor.Select(editor.Document!.GetElementById<SvgRectangle>("a"));
        var before = editor.GetSvg(); var draft = new SvgSolidPaintDraft(editor);
        Assert.True(draft.HasNonSolidPaint); Assert.Null(draft.InitialColor); Assert.Equal(before, editor.GetSvg());
        draft.Apply(editor, SKColors.Lime);
        Assert.NotNull(editor.Document.GetElementById("g"));
        Assert.IsType<SvgColourServer>(editor.SelectedElements[0].Fill);
    }

    [Fact]
    public void ColorFormattingIsCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            Assert.Equal("#1153D3", SvgSolidPaintDraft.FormatColor(new SKColor(17, 83, 211)));
            Assert.Equal("rgba(17,83,211,0.501960784)", SvgSolidPaintDraft.FormatColor(new SKColor(17, 83, 211, 128)));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static SvgEditorController Create()
    {
        var editor = new SvgEditorController(); editor.LoadSvg(Source); editor.SelectAll(); return editor;
    }
}
