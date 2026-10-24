using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgPreparedDivisionTests
{
    private const string Source = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect id='a' x='10' y='10' width='40' height='40'/><rect id='b' x='30' y='10' width='40' height='40'/></svg>";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreparedEditMatchesSynchronousDivideAndDoesNotMutateDuringComputation(bool worker)
    {
        using var expected = Create();
        using var actual = Create();
        expected.DivideSelection();
        var before = actual.GetSvg();
        var picture = actual.Renderer.Picture;
        var revision = actual.RenderRevision;
        var selected = actual.SelectedElements.ToArray();
        var prepared = await actual.PrepareDivisionAsync(useWorkerThread: worker);
        Assert.Equal(3, prepared.PieceCount);
        Assert.Equal(before, actual.GetSvg());
        Assert.Same(picture, actual.Renderer.Picture);
        Assert.Equal(revision, actual.RenderRevision);
        Assert.Equal(selected, actual.SelectedElements.ToArray());
        Assert.False(actual.IsDirty);
        Assert.False(actual.CanUndo);
        actual.ApplyPreparedDivision(prepared);
        Assert.Equal(expected.GetSvg(), actual.GetSvg());
        Assert.Equal(1, actual.Session.UndoCount);
        actual.Undo();
        Assert.Equal(before, actual.GetSvg());
    }

    [Fact]
    public async Task ConcurrentEditInvalidatesResultWithoutOverwritingNewWork()
    {
        using var editor = Create();
        var prepared = await editor.PrepareDivisionAsync();
        editor.MoveSelection(3, 7);
        var newer = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyPreparedDivision(prepared));
        Assert.Equal(newer, editor.GetSvg());
        Assert.Equal(1, editor.Session.UndoCount);
    }

    [Fact]
    public async Task ReloadingIdenticalSvgAndChangingSelectionInvalidatePreparedResult()
    {
        using var editor = Create();
        var prepared = await editor.PrepareDivisionAsync();
        editor.ClearSelection();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyPreparedDivision(prepared));
        editor.SelectAll();
        prepared = await editor.PrepareDivisionAsync();
        editor.LoadSvg(Source);
        editor.SelectAll();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyPreparedDivision(prepared));
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public async Task PreparedEditsCannotBeAppliedTwiceOrToAnotherController()
    {
        using var editor = Create();
        using var other = Create();
        var prepared = await editor.PrepareDivisionAsync();
        Assert.Throws<InvalidOperationException>(() => other.ApplyPreparedDivision(prepared));
        editor.ApplyPreparedDivision(prepared);
        var after = editor.GetSvg();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyPreparedDivision(prepared));
        Assert.Equal(after, editor.GetSvg());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationDuringExecutionLeavesDocumentAndNativeInputsIntact(bool worker)
    {
        using var editor = Create();
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress(value => { if (value.OperandsCompleted >= 1) cancellation.Cancel(); });
        var before = editor.GetSvg();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => editor.PrepareDivisionAsync(
            cancellationToken: cancellation.Token, progress: progress, useWorkerThread: worker));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
        Assert.False(editor.IsDirty);
        var next = await editor.PrepareDivisionAsync(useWorkerThread: worker);
        Assert.Equal(3, next.PieceCount);
    }

    [Fact]
    public async Task PieceLimitFailureIsAtomic()
    {
        using var editor = Create();
        var before = editor.GetSvg();
        await Assert.ThrowsAsync<InvalidOperationException>(() => editor.PrepareDivisionAsync(2));
        Assert.Equal(before, editor.GetSvg());
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public async Task AsyncGeometryEngineMatchesSyncAndDoesNotOwnOperands()
    {
        using var a = new SKPath(); a.AddCircle(20, 20, 15);
        using var b = new SKPath(); b.AddRect(SKRect.Create(15, 0, 20, 40));
        using var sync = SvgPathDivisionService.Divide(new[] { a, b });
        using var asyncResult = await SvgPathDivisionService.DivideAsync(new[] { a, b });
        Assert.Equal(sync.Pieces.Count, asyncResult.Pieces.Count);
        for (var index = 0; index < sync.Pieces.Count; index++)
        {
            Assert.Equal(sync.Pieces[index].SourceIndex, asyncResult.Pieces[index].SourceIndex);
            Assert.Equal(sync.Pieces[index].Geometry.ToSvgPathData(), asyncResult.Pieces[index].Geometry.ToSvgPathData());
        }
        asyncResult.Dispose();
        Assert.True(a.Contains(20, 20));
        Assert.True(b.Contains(20, 20));
    }

    [Fact]
    public async Task ThrowingProgressConsumerUnwindsNativeTemporaries()
    {
        using var a = new SKPath(); a.AddRect(SKRect.Create(0, 0, 20, 20));
        using var b = new SKPath(); b.AddRect(SKRect.Create(10, 0, 20, 20));
        var progress = new InlineProgress(_ => throw new InvalidOperationException("observer failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => SvgPathDivisionService.DivideAsync(new[] { a, b }, progress: progress));
        using var retry = await SvgPathDivisionService.DivideAsync(new[] { a, b });
        Assert.Equal(3, retry.Pieces.Count);
    }

    private static SvgEditorController Create()
    {
        var editor = new SvgEditorController();
        editor.LoadSvg(Source); editor.SelectAll(); return editor;
    }
    private sealed class InlineProgress : IProgress<SvgPathDivisionProgress>
    {
        private readonly Action<SvgPathDivisionProgress> _report;
        public InlineProgress(Action<SvgPathDivisionProgress> report) => _report = report;
        public void Report(SvgPathDivisionProgress value) => _report(value);
    }
}
