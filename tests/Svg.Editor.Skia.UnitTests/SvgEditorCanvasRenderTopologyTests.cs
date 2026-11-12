using System;
using Svg.Editor.Skia.Uno;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorCanvasRenderTopologyTests
{
    [Fact]
    public void StartupSelectionIsPerInstanceAndDoesNotChangeTheLayeredDefault()
    {
        var original = new SvgEditorCanvasRenderTopology();
        var layered = new SvgEditorCanvasRenderTopology();

        original.UseSingleCanvas(null);

        Assert.True(original.IsSingleCanvas);
        Assert.False(layered.IsSingleCanvas);
        Assert.Throws<InvalidOperationException>(() => original.UseSingleCanvas(null));
    }

    [Fact]
    public void StartingOrUnloadingDoesNotAllowTheTopologyToChange()
    {
        var topology = new SvgEditorCanvasRenderTopology();
        topology.Start();
        Assert.Throws<InvalidOperationException>(() => topology.UseSingleCanvas(null));

        topology.CancelPaint();
        Assert.Throws<InvalidOperationException>(() => topology.UseSingleCanvas(null));
    }

    [Fact]
    public void CompletionRequiresAStartedCurrentPaintAndSurvivesCanceledAttempts()
    {
        var completions = 0;
        var topology = new SvgEditorCanvasRenderTopology();
        topology.UseSingleCanvas(() => completions++);
        topology.CompletePaint(topology.Generation);
        Assert.Equal(0, completions);

        topology.Start();
        var interrupted = topology.Generation;
        topology.CancelPaint();
        topology.CompletePaint(interrupted);
        Assert.Equal(0, completions);

        topology.CompletePaint(topology.Generation);
        topology.CompletePaint(topology.Generation);
        Assert.Equal(1, completions);
    }

    [Fact]
    public void ReentrantCompletionCannotReportTwice()
    {
        var completions = 0;
        var topology = new SvgEditorCanvasRenderTopology();
        topology.UseSingleCanvas(() =>
        {
            completions++;
            topology.CompletePaint(topology.Generation);
        });
        topology.Start();

        topology.CompletePaint(topology.Generation);

        Assert.Equal(1, completions);
    }

    [Fact]
    public void ObserverErrorsAreNotSuppressedOrReportedAgain()
    {
        var topology = new SvgEditorCanvasRenderTopology();
        topology.UseSingleCanvas(() => throw new InvalidOperationException("observer"));
        topology.Start();

        Assert.Throws<InvalidOperationException>(() => topology.CompletePaint(topology.Generation));
        topology.CompletePaint(topology.Generation);
    }

    [Fact]
    public void DisposalCancelsPendingCompletionAndRejectsConfiguration()
    {
        var completions = 0;
        var topology = new SvgEditorCanvasRenderTopology();
        topology.UseSingleCanvas(() => completions++);
        topology.Start();
        var generation = topology.Generation;

        topology.Dispose();
        topology.CompletePaint(generation);
        topology.CompletePaint(topology.Generation);

        Assert.False(topology.IsCurrent(generation));
        Assert.False(topology.IsCurrent(topology.Generation));
        Assert.Equal(0, completions);
        Assert.Throws<ObjectDisposedException>(() => topology.UseSingleCanvas(null));
    }
}
