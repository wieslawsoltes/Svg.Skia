using System;
using Svg.Editor.Skia.Uno;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class StudioPanelRefreshStateTests
{
    private const string Source = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><defs><linearGradient id='g'><stop stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient></defs><rect id='a' width='30' height='30' fill='url(#g)'/><rect id='b' x='50' width='30' height='30' fill='green'/></svg>";

    [Fact]
    public void HiddenSelectionChangesDoNotConstructDraftsAndOpeningCatchesUpOnce()
    {
        using var controller = Create();
        var state = new StudioPanelRefreshState(observeSelection: true);
        var parses = 0;
        SvgGradientEdit? draft = null;
        void Refresh(bool visible)
        {
            if (!state.NeedsRefresh(visible, controller))
                return;
            draft = controller.BeginGradientEdit();
            parses++;
            state.Complete(controller);
        }
        var document = controller.Document;
        var picture = controller.Renderer.Picture;
        var revision = controller.DocumentRevision;
        for (var index = 0; index < 100; index++)
        {
            controller.Select(document!.GetElementById<SvgRectangle>(index % 2 == 0 ? "a" : "b"));
            Refresh(false);
        }
        Assert.Equal(0, parses);
        Assert.Null(draft);
        Refresh(true);
        Assert.Equal(1, parses);
        Assert.True(draft!.IsCurrent(controller));
        Assert.Null(draft.OriginalGradientId);
        Refresh(true);
        Assert.Equal(1, parses);
        Assert.Same(document, controller.Document);
        Assert.Same(picture, controller.Renderer.Picture);
        Assert.Equal(revision, controller.DocumentRevision);
        Assert.False(controller.CanUndo);
    }

    [Fact]
    public void HiddenEditsAreNotConsumedByAnotherVisiblePanel()
    {
        using var controller = Create();
        var history = new StudioPanelRefreshState();
        var resources = new StudioPanelRefreshState();
        history.Complete(controller);
        resources.Complete(controller);
        controller.MoveSelection(2, 0);
        Assert.True(resources.NeedsRefresh(true, controller));
        resources.Complete(controller);
        Assert.False(history.NeedsRefresh(false, controller));
        controller.MoveSelection(3, 0);
        resources.Complete(controller);
        Assert.True(history.NeedsRefresh(true, controller));
        Assert.Equal(2, controller.Session.UndoLabels.Count);
        history.Complete(controller);
        Assert.False(history.NeedsRefresh(true, controller));
        Assert.False(resources.NeedsRefresh(true, controller));
    }

    [Fact]
    public void SelectionOnlyChangesRefreshDraftsButRetainDocumentCollections()
    {
        using var controller = Create();
        var collection = new StudioPanelRefreshState();
        var draft = new StudioPanelRefreshState(observeSelection: true);
        collection.Complete(controller);
        draft.Complete(controller);
        var revision = controller.DocumentRevision;
        controller.Select(controller.Document!.GetElementById<SvgRectangle>("b"));
        Assert.Equal(revision, controller.DocumentRevision);
        Assert.False(collection.NeedsRefresh(true, controller));
        Assert.True(draft.NeedsRefresh(true, controller));
    }

    [Fact]
    public void PendingDraftSurvivesDeferralAndStillRejectsAChangedTarget()
    {
        using var controller = Create();
        var state = new StudioPanelRefreshState(observeSelection: true);
        var draft = controller.BeginGradientEdit();
        draft.Ramp = draft.Ramp.Reverse();
        var pendingRamp = draft.Ramp;
        state.Complete(controller);
        controller.Select(controller.Document!.GetElementById<SvgRectangle>("b"));
        Assert.False(state.NeedsRefresh(false, controller));
        Assert.True(state.NeedsRefresh(true, controller));
        Assert.False(state.CanPrepareDraft(controller, hasPendingDraft: true));
        // A pending editor revalidates its retained draft instead of rebuilding it.
        state.Complete(controller);
        Assert.Same(pendingRamp, draft.Ramp);
        Assert.False(draft.IsCurrent(controller));
        var svg = controller.GetSvg();
        Assert.Throws<InvalidOperationException>(() => controller.ApplyGradientEdit(draft));
        Assert.Equal(svg, controller.GetSvg());
        Assert.False(controller.CanUndo);
    }

    [Fact]
    public void AnotherControllerAtTheSameRevisionRequiresPopulation()
    {
        using var first = Create();
        using var second = Create();
        Assert.Equal(first.DocumentRevision, second.DocumentRevision);
        var state = new StudioPanelRefreshState();
        state.Complete(first);
        Assert.False(state.NeedsRefresh(false, second));
        Assert.True(state.NeedsRefresh(true, second));
        state.Complete(second);
        Assert.False(state.NeedsRefresh(true, second));
    }

    [Fact]
    public void UndoReplacementInvalidatesRowsEvenWhenSelectionIdsMatch()
    {
        using var controller = Create();
        controller.MoveSelection(2, 0);
        var state = new StudioPanelRefreshState(observeSelection: true);
        state.Complete(controller);
        var document = controller.Document;
        controller.Undo();
        Assert.NotSame(document, controller.Document);
        Assert.Equal("a", controller.SelectedElements[0].ID);
        Assert.True(state.NeedsRefresh(true, controller));
    }

    [Fact]
    public void GestureStateRevalidatesDraftWithoutInvalidatingDocumentCollections()
    {
        using var controller = Create();
        var state = new StudioPanelRefreshState(observeSelection: true);
        var collection = new StudioPanelRefreshState();
        state.Complete(controller);
        collection.Complete(controller);
        controller.BeginEdit("gesture");
        Assert.True(state.NeedsRefresh(true, controller));
        Assert.False(collection.NeedsRefresh(true, controller));
        state.Complete(controller);
        controller.CommitEdit();
        Assert.True(state.NeedsRefresh(true, controller));
        Assert.False(collection.NeedsRefresh(true, controller));
    }

    [Fact]
    public void IncompletePopulationAndExplicitInvalidationRemainPending()
    {
        using var controller = Create();
        var state = new StudioPanelRefreshState();
        Assert.True(state.NeedsRefresh(true, controller));
        Assert.True(state.NeedsRefresh(true, controller));
        state.Complete(controller);
        Assert.False(state.NeedsRefresh(true, controller));
        state.Invalidate();
        Assert.True(state.NeedsRefresh(true, controller));
        state.Complete(null);
        Assert.False(state.NeedsRefresh(true, null));
        Assert.True(state.NeedsRefresh(true, controller));
    }

    [Fact]
    public void HiddenCommandEligibilityDoesNotParseButExecutionCanCreateOneCurrentDraft()
    {
        using var controller = Create();
        var state = new StudioPanelRefreshState(observeSelection: true);
        var previous = controller.BeginGradientEdit();
        state.Complete(controller);
        controller.Select(controller.Document!.GetElementById<SvgRectangle>("b"));
        var source = controller.GetSvg();
        var picture = controller.Renderer.Picture;
        for (var index = 0; index < 100; index++)
            Assert.True(state.CanPrepareDraft(controller, hasPendingDraft: false));
        Assert.False(previous.IsCurrent(controller));
        Assert.Same(picture, controller.Renderer.Picture);
        Assert.Equal(source, controller.GetSvg());
        Assert.False(controller.CanUndo);
        var prepared = controller.BeginGradientEdit();
        state.Complete(controller);
        Assert.False(state.CanPrepareDraft(controller, hasPendingDraft: false));
        prepared.Ramp = prepared.Ramp.Reverse();
        controller.ApplyGradientEdit(prepared);
        Assert.Equal(1, controller.Session.UndoCount);
        Assert.NotEqual(source, controller.GetSvg());
    }

    [Fact]
    public void LazyCommandsRejectEmptyLockedAndInProgressTargets()
    {
        using var controller = Create();
        var state = new StudioPanelRefreshState(observeSelection: true);
        Assert.True(state.CanPrepareDraft(controller, hasPendingDraft: false));
        controller.BeginEdit("gesture");
        Assert.False(state.CanPrepareDraft(controller, hasPendingDraft: false));
        controller.CommitEdit();
        controller.SetLocked(true);
        Assert.False(state.CanPrepareDraft(controller, hasPendingDraft: false));
        controller.ClearSelection();
        Assert.False(state.CanPrepareDraft(controller, hasPendingDraft: false));
        Assert.False(state.CanPrepareDraft(null, hasPendingDraft: false));
    }

    [Fact]
    public void ForeignCleanDraftStateCanBeReleasedWithoutPopulatingHiddenEditor()
    {
        using var controller = Create();
        var state = new StudioPanelRefreshState(observeSelection: true);
        state.Complete(controller);
        Assert.True(state.BelongsTo(controller));
        controller.LoadSvg(Source);
        Assert.False(state.BelongsTo(controller));
        state.Invalidate();
        Assert.False(state.NeedsRefresh(false, controller));
        Assert.True(state.NeedsRefresh(true, controller));
    }

    private static SvgEditorController Create()
    {
        var controller = new SvgEditorController();
        controller.LoadSvg(Source);
        controller.Select(controller.Document!.GetElementById<SvgRectangle>("a"));
        return controller;
    }
}
