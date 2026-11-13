using System;
using Svg.Editor.Skia.Uno;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgEditorCanvasRenderScheduleTests
{
    [Fact]
    public void FullCameraPaintsUseOneSurfaceAndFoldLaterInteractionInvalidations()
    {
        var painter = new Painter();
        painter.Flush();
        painter.ResetCounters();

        for (var index = 0; index < 120; index++)
        {
            painter.InvalidateDocument();
            // Gesture updates request feedback after the viewport notification.
            Assert.Equal(SvgEditorCanvasInvalidation.None, painter.InvalidateInteraction());
            painter.Flush();
        }

        Assert.Equal(120, painter.DocumentPaints);
        Assert.Equal(120, painter.CombinedInteractionPaints);
        Assert.Equal(0, painter.InteractionSurfacePaints);
        Assert.False(painter.InteractionVisible);
    }

    [Fact]
    public void FirstInteractionSeparatesOnceThenRetainsTheDocument()
    {
        var painter = new Painter();
        painter.Flush();
        painter.InvalidateDocument();
        painter.Flush();
        painter.ResetCounters();

        Assert.Equal(SvgEditorCanvasInvalidation.Document | SvgEditorCanvasInvalidation.Interaction,
            painter.InvalidateInteraction());
        Assert.False(painter.InteractionVisible);
        // A suppressed child callback must not consume the queued transition.
        Assert.False(painter.Schedule.BeginInteractionPaint());
        painter.Flush();
        Assert.True(painter.InteractionVisible);

        for (var index = 0; index < 120; index++)
        {
            Assert.Equal(SvgEditorCanvasInvalidation.Interaction, painter.InvalidateInteraction());
            painter.Flush();
        }

        Assert.Equal(1, painter.DocumentPaints);
        Assert.Equal(0, painter.CombinedInteractionPaints);
        Assert.Equal(121, painter.InteractionSurfacePaints);
    }

    [Fact]
    public void FullPaintSupersedesAnUnpaintedSeparation()
    {
        var painter = new Painter();
        painter.Flush();
        painter.InvalidateDocument();
        painter.Flush();
        painter.ResetCounters();

        painter.InvalidateInteraction();
        painter.InvalidateDocument();
        painter.InvalidateInteraction();
        painter.Flush();

        Assert.Equal(1, painter.DocumentPaints);
        Assert.Equal(1, painter.CombinedInteractionPaints);
        Assert.Equal(0, painter.InteractionSurfacePaints);
        Assert.Equal(SvgEditorCanvasInvalidation.Document | SvgEditorCanvasInvalidation.Interaction,
            painter.InvalidateInteraction());
        painter.Flush();
    }

    [Fact]
    public void BaseTypedInvalidationRefreshesBothModes()
    {
        var painter = new Painter();
        painter.Flush();
        painter.InvalidateDocument();
        painter.Flush();
        painter.ResetCounters();

        painter.InvalidateBase();
        painter.Flush();
        Assert.False(painter.InteractionVisible);
        Assert.Equal(1, painter.DocumentPaints);
        Assert.Equal(1, painter.CombinedInteractionPaints);
        Assert.Equal(0, painter.InteractionSurfacePaints);

        painter.InvalidateInteraction();
        painter.Flush();
        painter.ResetCounters();
        painter.InvalidateBase();
        painter.Flush();
        Assert.True(painter.InteractionVisible);
        Assert.Equal(1, painter.DocumentPaints);
        Assert.Equal(0, painter.CombinedInteractionPaints);
        Assert.Equal(1, painter.InteractionSurfacePaints);
    }

    [Fact]
    public void InvalidationsRaisedDuringPaintRemainPending()
    {
        var painter = new Painter();
        painter.Flush();
        painter.InvalidateDocument();
        painter.AfterDocumentPaint = () => painter.InvalidateInteraction();
        painter.Flush();
        Assert.True(painter.InteractionVisible);

        painter.InvalidateInteraction();
        painter.AfterInteractionPaint = painter.InvalidateDocument;
        painter.Flush();
        Assert.False(painter.InteractionVisible);

        painter.InvalidateInteraction();
        painter.AfterInteractionPaint = () => painter.InvalidateInteraction();
        painter.Flush();
        Assert.True(painter.InteractionVisible);
    }

    [Fact]
    public void BootstrapRequiresSuccessfulChildPaintAndRestartsAfterUnload()
    {
        var schedule = new SvgEditorCanvasRenderSchedule();
        Assert.Equal(new SvgEditorCanvasPaintPlan(false, true), schedule.BeginDocumentPaint());
        Assert.True(schedule.BeginInteractionPaint());
        schedule.InvalidateDocument();
        Assert.False(schedule.BeginDocumentPaint().CombineInteraction);
        Assert.True(schedule.BeginInteractionPaint());
        schedule.CompleteInteractionPaint();
        schedule.InvalidateDocument();
        Assert.True(schedule.BeginDocumentPaint().CombineInteraction);
        schedule.Reset();
        Assert.Equal(new SvgEditorCanvasPaintPlan(false, true), schedule.BeginDocumentPaint());
    }

    [Fact]
    public void MixedInvalidationBatchesNeverPresentMissingDuplicatedOrStaleFeedback()
    {
        var random = new Random(291801);
        var painter = new Painter();
        painter.Flush();
        for (var batch = 0; batch < 2000; batch++)
        {
            var count = random.Next(1, 9);
            for (var index = 0; index < count; index++)
            {
                switch (random.Next(4))
                {
                    case 0:
                        painter.InvalidateDocument();
                        break;
                    case 1:
                    case 2:
                        painter.InvalidateInteraction();
                        break;
                    default:
                        painter.InvalidateBase();
                        break;
                }
            }
            painter.Flush();
        }
    }

    // This host models retained surface contents independently of the scheduler.
    // Every completed flush must present the latest document and exactly one
    // current interaction version, including canceled and reentrant transitions.
    private sealed class Painter
    {
        private bool _documentDirty = true;
        private bool _interactionDirty;
        private int _documentVersion;
        private int _interactionVersion;
        private int _paintedDocument = -1;
        private int? _combinedInteraction;
        private int _paintedInteraction = -1;

        public SvgEditorCanvasRenderSchedule Schedule { get; } = new();
        public bool InteractionVisible { get; private set; } = true;
        public int DocumentPaints { get; private set; }
        public int CombinedInteractionPaints { get; private set; }
        public int InteractionSurfacePaints { get; private set; }
        public Action? AfterDocumentPaint { get; set; }
        public Action? AfterInteractionPaint { get; set; }

        public void InvalidateDocument()
        {
            _documentVersion++;
            _interactionVersion++;
            Schedule.InvalidateDocument();
            _documentDirty = true;
        }

        public SvgEditorCanvasInvalidation InvalidateInteraction()
        {
            _interactionVersion++;
            var invalidation = Schedule.InvalidateInteraction();
            _documentDirty |= (invalidation & SvgEditorCanvasInvalidation.Document) != 0;
            _interactionDirty |= (invalidation & SvgEditorCanvasInvalidation.Interaction) != 0;
            return invalidation;
        }

        public void InvalidateBase()
        {
            _documentVersion++;
            _interactionVersion++;
            _documentDirty = true;
        }

        public void Flush()
        {
            var frames = 0;
            while (_documentDirty || (InteractionVisible && _interactionDirty))
            {
                Assert.True(++frames < 8, "An invalidation caused a repeating idle paint loop.");
                if (_documentDirty)
                {
                    _documentDirty = false;
                    var plan = Schedule.BeginDocumentPaint();
                    InteractionVisible = !plan.CombineInteraction;
                    _interactionDirty |= plan.InvalidateInteraction;
                    _paintedDocument = _documentVersion;
                    _combinedInteraction = plan.CombineInteraction ? _interactionVersion : null;
                    DocumentPaints++;
                    if (plan.CombineInteraction)
                        CombinedInteractionPaints++;
                    var callback = AfterDocumentPaint;
                    AfterDocumentPaint = null;
                    callback?.Invoke();
                }

                if (InteractionVisible && _interactionDirty)
                {
                    _interactionDirty = false;
                    Assert.True(Schedule.BeginInteractionPaint());
                    _paintedInteraction = _interactionVersion;
                    InteractionSurfacePaints++;
                    var callback = AfterInteractionPaint;
                    AfterInteractionPaint = null;
                    callback?.Invoke();
                    Schedule.CompleteInteractionPaint();
                }
            }

            Assert.Equal(_documentVersion, _paintedDocument);
            Assert.Equal(1, (_combinedInteraction.HasValue ? 1 : 0) + (InteractionVisible ? 1 : 0));
            Assert.Equal(_interactionVersion, _combinedInteraction ?? _paintedInteraction);
        }

        public void ResetCounters()
        {
            DocumentPaints = 0;
            CombinedInteractionPaints = 0;
            InteractionSurfacePaints = 0;
        }
    }
}
