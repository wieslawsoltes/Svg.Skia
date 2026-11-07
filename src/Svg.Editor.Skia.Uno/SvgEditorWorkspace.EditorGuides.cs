using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private uint? _rulerGuidePointer;
    private StudioRuler? _rulerGuideOwner;
    private global::Svg.Editor.Skia.SvgEditorController? _rulerGuideController;

    private void BuildEditorGuideCommands()
    {
        void Toggle(string id, string title, Action action, Func<bool> state)
            => _commands.Add(new EditorCommand(id, title, "View", string.Empty, () =>
            {
                action();
                SaveEditorPreferences();
                return Task.CompletedTask;
            })
            { IsChecked = state, CanExecute = () => true });
        Toggle("guides", "Show guides", () => Canvas.GuidesVisible = !Canvas.GuidesVisible, () => Canvas.GuidesVisible);
        Toggle("lockguides", "Lock guides", () => Canvas.GuidesLocked = !Canvas.GuidesLocked, () => Canvas.GuidesLocked);
        Toggle("snapguides", "Snap to guides", () => Canvas.SnapToGuides = !Canvas.SnapToGuides, () => Canvas.SnapToGuides);
        Toggle("smartguides", "Smart guides", () => Canvas.SmartGuides = !Canvas.SmartGuides, () => Canvas.SmartGuides);
        _commands.Add(new EditorCommand("newguide", "New guide…", "View", string.Empty, ShowEditorGuideAsync)
        { CanExecute = () => Controller?.Document is not null });
        _commands.Add(new EditorCommand("guide-bounds", "Guides from selection bounds", "View", string.Empty, () =>
        {
            Controller?.AddSelectionBoundsGuides();
            Canvas.GuidesVisible = true;
            return Task.CompletedTask;
        })
        { CanExecute = () => Controller?.SelectionBounds is not null });
        _commands.Add(new EditorCommand("clearguides", "Clear guides", "View", string.Empty, () =>
        {
            Controller?.ClearGuides();
            return Task.CompletedTask;
        })
        { CanExecute = () => Controller?.GetGuides().Count > 0 });
        AttachGuideRuler(HorizontalRuler, false);
        AttachGuideRuler(VerticalRuler, true);
        AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, args) =>
        {
            if (args.Key != VirtualKey.Escape || _rulerGuidePointer is null)
                return;
            CancelRulerGuide();
            args.Handled = true;
        }), true);
        Unloaded += (_, _) => CancelRulerGuide();
    }

    private async Task ShowEditorGuideAsync()
    {
        if (Controller is not { } controller)
            return;
        var orientation = new ComboBox
        {
            Header = "Orientation",
            ItemsSource = new[] { "Horizontal", "Vertical" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var position = NumberInput("Position · canvas units", 0, -10000000, 10000000);
        var content = new StackPanel { Spacing = 12, Width = DialogContentWidth(360) };
        content.Children.Add(orientation);
        content.Children.Add(position);
        content.Children.Add(StudioHint("Drag from a ruler to place a guide. Unlock guides to move them with Selection; drag a guide outside the canvas to remove it. Hold Alt while dragging from a ruler to change its orientation."));
        if (await ShowDialogAsync(MakeDialog("New Guide", content, "Create")) != ContentDialogResult.Primary
            || !ReferenceEquals(Controller, controller) || _disposed)
            return;
        if (!double.IsFinite(position.Value))
            throw new InvalidOperationException("Enter a finite guide position.");
        var vertical = orientation.SelectedIndex == 1;
        controller.AddGuide(vertical ? new SKPoint((float)position.Value, 0) : new SKPoint(0, (float)position.Value),
            vertical ? new SKPoint(0, 1) : new SKPoint(1, 0));
        Canvas.GuidesVisible = true;
    }

    private void AttachGuideRuler(StudioRuler ruler, bool vertical)
    {
        ToolTipService.SetToolTip(ruler, "Drag onto the canvas to create a guide. Hold Alt to change orientation.");
        ruler.PointerPressed += (_, args) =>
        {
            if (_disposed || _closing || _dialogVisible || Controller?.Document is null || _rulerGuidePointer is not null
                || !args.GetCurrentPoint(ruler).Properties.IsLeftButtonPressed)
                return;
            ApplySafely(() =>
            {
                CommitInspectorEdits();
                Canvas.CancelGesture();
                _rulerGuidePointer = args.Pointer.PointerId;
                _rulerGuideOwner = ruler;
                _rulerGuideController = Controller;
                ruler.CapturePointer(args.Pointer);
                Canvas.GuidesVisible = true;
                Preview(args);
                Focus(FocusState.Pointer);
                args.Handled = true;
            });
        };
        ruler.PointerMoved += (_, args) =>
        {
            if (_rulerGuidePointer != args.Pointer.PointerId || !ReferenceEquals(ruler, _rulerGuideOwner))
                return;
            Preview(args);
            args.Handled = true;
        };
        ruler.PointerReleased += (_, args) =>
        {
            if (_rulerGuidePointer != args.Pointer.PointerId || !ReferenceEquals(ruler, _rulerGuideOwner))
                return;
            var location = args.GetCurrentPoint(Canvas).Position;
            var axis = vertical ^ args.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu);
            var position = Canvas.Viewport.ViewToWorld(new SKPoint((float)location.X, (float)location.Y));
            var controller = _rulerGuideController;
            CancelRulerGuide();
            args.Handled = true;
            if (!ReferenceEquals(Controller, controller) || location.X < 0 || location.Y < 0 || location.X > Canvas.ActualWidth || location.Y > Canvas.ActualHeight)
                return;
            ApplySafely(() => controller?.AddGuide(position, axis ? new SKPoint(0, 1) : new SKPoint(1, 0)));
        };
        ruler.PointerCanceled += (_, args) => { if (_rulerGuidePointer == args.Pointer.PointerId) CancelRulerGuide(); };
        ruler.PointerCaptureLost += (_, args) => { if (_rulerGuidePointer == args.Pointer.PointerId) CancelRulerGuide(); };

        void Preview(PointerRoutedEventArgs args)
        {
            var point = args.GetCurrentPoint(Canvas).Position;
            Canvas.PreviewRulerGuide(Canvas.Viewport.ViewToWorld(new SKPoint((float)point.X, (float)point.Y)),
                vertical ^ args.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu));
        }
    }

    private void CancelRulerGuide()
    {
        _rulerGuidePointer = null;
        _rulerGuideController = null;
        var ruler = _rulerGuideOwner;
        _rulerGuideOwner = null;
        Canvas.PreviewRulerGuide(null, false);
        ruler?.ReleasePointerCaptures();
    }
}
