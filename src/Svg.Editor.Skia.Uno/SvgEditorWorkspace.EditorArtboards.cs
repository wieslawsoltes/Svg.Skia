using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private sealed record EditorArtboardChoice(string Id, string Name, SKRect Bounds, int Index)
    {
        public override string ToString() => $"{Index + 1:00}  {Name}\n      {Bounds.Width:0.###} × {Bounds.Height:0.###}";
    }

    private ListView? _editorArtboards;
    private TextBlock? _editorArtboardsSummary;
    private StudioNumericField? _editorArtboardX, _editorArtboardY, _editorArtboardWidth, _editorArtboardHeight;
    private readonly Dictionary<string, Button> _editorArtboardActions = new(StringComparer.Ordinal);
    private SvgEditorController? _editorArtboardController;
    private long _editorArtboardRevision = -1;
    private bool _refreshingEditorArtboards;

    private bool EditorArtboardsEditable => Controller?.Document is { } document && !SvgEditorController.IsEditLocked(document);

    private void BuildEditorArtboardCommands()
    {
        void Action(string id, string label, Action action, Func<bool>? enabled = null)
            => _commands.Add(new EditorCommand(id, label, "Object", string.Empty, () => { action(); return Task.CompletedTask; })
            { CanExecute = enabled ?? (() => EditorArtboardsEditable) });
        void Async(string id, string label, Func<Task> action)
            => _commands.Add(new EditorCommand(id, label, "Object", string.Empty, action) { CanExecute = () => EditorArtboardsEditable });
        Async("artboard-new", "New artboard…", () => EditEditorArtboardAsync(true));
        Async("artboard-options", "Artboard options…", () => EditEditorArtboardAsync(false));
        Async("artboard-rename", "Rename artboard…", () => EditEditorArtboardAsync(false, true));
        Action("artboard-duplicate", "Duplicate artboard", () =>
        {
            if (Controller?.ActiveArtboard is not { } board)
                return;
            Controller.DuplicateArtboard(board.Id, new SKPoint(board.Bounds.Width + 24, 0));
            FitEditorActiveArtboard();
        });
        Action("artboard-delete", "Delete artboard", () =>
        {
            if (Controller?.ActiveArtboardId is not { } id)
                return;
            Controller.RemoveArtboard(id);
            StatusText.Text = "Artboard removed. Its artwork remains in the document.";
        }, () => EditorArtboardsEditable && Controller!.GetArtboards().Count > 1);
        Action("artboard-up", "Move artboard earlier", () => MoveEditorArtboard(-1), () => CanMoveEditorArtboard(-1));
        Action("artboard-down", "Move artboard later", () => MoveEditorArtboard(1), () => CanMoveEditorArtboard(1));
        Action("artboard-fit", "Fit active artboard", FitEditorActiveArtboard, () => Controller?.ActiveArtboard is not null);
    }

    private void BuildEditorArtboardPanel()
    {
        var panel = StudioPanel("artboards", "Artboards");
        _editorArtboards = EditorPanelList("Artboards in document order");
        _editorArtboards.MaxHeight = 220;
        _editorArtboards.IsItemClickEnabled = false;
        _editorArtboards.SelectionChanged += (_, _) =>
        {
            if (_refreshingEditorArtboards || _editorArtboards.SelectedItem is not EditorArtboardChoice choice)
                return;
            ApplySafely(() => { CommitInspectorEdits(); Canvas.CancelGesture(); Controller?.ActivateArtboard(choice.Id); });
        };
        _editorArtboards.DoubleTapped += async (_, _) => await RunCommandAsync("artboard-fit");
        AutomationProperties.SetAutomationId(_editorArtboards, "EditorArtboards");
        _editorArtboardsSummary = StudioHint(string.Empty);
        _editorArtboardX = ArtboardField("X", "EditorArtboardX", false, (bounds, value) => SKRect.Create(value, bounds.Top, bounds.Width, bounds.Height));
        _editorArtboardY = ArtboardField("Y", "EditorArtboardY", false, (bounds, value) => SKRect.Create(bounds.Left, value, bounds.Width, bounds.Height));
        _editorArtboardWidth = ArtboardField("W", "EditorArtboardWidth", true, (bounds, value) => SKRect.Create(bounds.Left, bounds.Top, value, bounds.Height));
        _editorArtboardHeight = ArtboardField("H", "EditorArtboardHeight", true, (bounds, value) => SKRect.Create(bounds.Left, bounds.Top, bounds.Width, value));
        panel.Children.Add(_editorArtboards);
        panel.Children.Add(_editorArtboardsSummary);
        panel.Children.Add(StudioPair(_editorArtboardX, _editorArtboardY));
        panel.Children.Add(StudioPair(_editorArtboardWidth, _editorArtboardHeight));
        Button Button(string id, string label)
        {
            var button = ActionButton(id, label);
            _editorArtboardActions[id] = button;
            return button;
        }
        panel.Children.Add(StudioPair(Button("artboard-new", "New…"), Button("artboard-duplicate", "Duplicate")));
        panel.Children.Add(StudioPair(Button("artboard-options", "Options…"), Button("artboard-delete", "Delete")));
        panel.Children.Add(StudioPair(Button("artboard-up", "Move up"), Button("artboard-down", "Move down")));
        panel.Children.Add(StudioPair(Button("artboard-fit", "Fit artboard"), Button("fitall", "Fit all")));
        panel.Children.Add(Button("tool-artboard", "Edit artboards on canvas"));
        panel.Children.Add(StudioHint("X, Y, width, and height use SVG user units. Double-click an artboard to fit it in the window. Deleting a region keeps its artwork."));
        RefreshEditorArtboardPanel();
    }

    private StudioNumericField ArtboardField(string header, string automationId, bool positive, Func<SKRect, float, SKRect> update)
    {
        var field = new StudioNumericField { Header = header, Minimum = positive ? 0.001 : -10000000, Maximum = 10000000 };
        AutomationProperties.SetAutomationId(field, automationId);
        field.CaptureTargetValidation = () =>
        {
            var controller = Controller;
            var document = controller?.Document;
            var id = controller?.ActiveArtboardId;
            return () => ReferenceEquals(controller, Controller) && ReferenceEquals(document, controller?.Document) && controller?.ActiveArtboardId == id;
        };
        field.EditRejected += (_, message) => StatusText.Text = message;
        field.ValueEdited += (_, args) =>
        {
            if (_refreshingEditorArtboards || Controller is not { } controller || controller.ActiveArtboardId is not { } id)
                return;
            ApplySafely(() =>
            {
                Canvas.CancelGesture();
                var current = controller.GetArtboardsInRootCoordinates().First(board => board.Id == id);
                controller.SetArtboardBoundsInRootCoordinates(id, update(current.Bounds, (float)args.Value));
            });
            RefreshEditorArtboardPanel();
        };
        return field;
    }

    private void RefreshEditorArtboardPanel()
    {
        if (_refreshingEditorArtboards || _editorArtboards is null)
            return;
        _refreshingEditorArtboards = true;
        try
        {
            var controller = Controller;
            var boards = controller?.GetArtboardsInRootCoordinates() ?? Array.Empty<SvgEditorArtboard>();
            if (!ReferenceEquals(controller, _editorArtboardController) || controller?.DocumentRevision != _editorArtboardRevision)
            {
                _editorArtboards.ItemsSource = boards.Select((board, index) => new EditorArtboardChoice(board.Id, board.Name, board.Bounds, index)).ToArray();
                _editorArtboardController = controller;
                _editorArtboardRevision = controller?.DocumentRevision ?? -1;
            }
            var activeId = controller?.ActiveArtboardId;
            var choice = (_editorArtboards.ItemsSource as EditorArtboardChoice[])?.FirstOrDefault(board => board.Id == activeId);
            _editorArtboards.SelectedItem = choice;
            if (_editorArtboardsSummary is not null)
                _editorArtboardsSummary.Text = choice is null ? "Open a document to edit its artboards."
                    : $"{boards.Count} artboard{(boards.Count == 1 ? string.Empty : "s")} · {choice.Name}";
            foreach (var field in new[] { _editorArtboardX, _editorArtboardY, _editorArtboardWidth, _editorArtboardHeight })
                if (field is not null)
                    field.IsEnabled = EditorArtboardsEditable && choice is not null;
            if (choice is not null)
            {
                _editorArtboardX!.Value = choice.Bounds.Left;
                _editorArtboardY!.Value = choice.Bounds.Top;
                _editorArtboardWidth!.Value = choice.Bounds.Width;
                _editorArtboardHeight!.Value = choice.Bounds.Height;
            }
            foreach (var (id, button) in _editorArtboardActions)
            {
                var command = _commands.FirstOrDefault(command => command.Id == id);
                button.IsEnabled = controller is not null && command is not null && command.CanExecute?.Invoke() != false;
            }
        }
        finally { _refreshingEditorArtboards = false; }
    }

    private bool CanMoveEditorArtboard(int direction)
    {
        if (!EditorArtboardsEditable || Controller is not { } controller)
            return false;
        var boards = controller.GetArtboards();
        var index = boards.Select(board => board.Id).ToList().IndexOf(controller.ActiveArtboardId!);
        return index >= 0 && index + direction >= 0 && index + direction < boards.Count;
    }

    private void MoveEditorArtboard(int direction)
    {
        if (!CanMoveEditorArtboard(direction) || Controller is not { } controller)
            return;
        var id = controller.ActiveArtboardId!;
        var index = controller.GetArtboards().Select(board => board.Id).ToList().IndexOf(id);
        controller.ReorderArtboard(id, index + direction);
    }

    private void FitEditorActiveArtboard()
    {
        if (Controller?.ActiveArtboard is { } board)
            Canvas.Viewport.Fit(board.Bounds);
    }

    private async Task EditEditorArtboardAsync(bool create, bool nameOnly = false)
    {
        if (Controller is not { } controller || controller.ActiveArtboardId is not { } id)
            return;
        var boards = controller.GetArtboardsInRootCoordinates();
        var active = boards.First(board => board.Id == id);
        var revision = controller.DocumentRevision;
        var bounds = active.Bounds;
        if (create)
            bounds.Offset(bounds.Width + Math.Max(16, bounds.Width * 0.05f), 0);
        var proposedName = active.Name;
        if (create)
        {
            var index = 1;
            while (boards.Any(board => board.Name == "Artboard " + index))
                index++;
            proposedName = "Artboard " + index;
        }
        var name = new TextBox { Header = "Name", Text = proposedName, MaxLength = 256, HorizontalAlignment = HorizontalAlignment.Stretch };
        var x = NumberInput("X", bounds.Left, -10000000, 10000000);
        var y = NumberInput("Y", bounds.Top, -10000000, 10000000);
        var width = NumberInput("Width", bounds.Width, 0.001, 10000000);
        var height = NumberInput("Height", bounds.Height, 0.001, 10000000);
        var error = StudioHint(string.Empty);
        error.Foreground = (Brush)Resources["StudioDanger"];
        var content = new StackPanel { Spacing = 10, Width = DialogContentWidth(340) };
        content.Children.Add(name);
        if (!nameOnly)
        {
            content.Children.Add(StudioPair(x, y));
            content.Children.Add(StudioPair(width, height));
            content.Children.Add(StudioHint("Dimensions use SVG user units. Artwork remains in place when artboards move or resize."));
        }
        content.Children.Add(error);
        var dialog = MakeDialog(create ? "New artboard" : nameOnly ? "Rename artboard" : "Artboard options", content, create ? "Create artboard" : "Apply");
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                if (!ReferenceEquals(Controller, controller) || controller.DocumentRevision != revision || controller.ActiveArtboardId != id)
                    throw new InvalidOperationException("The document changed. Close this dialog and edit the current artboard again.");
                var label = name.Text.Trim();
                SvgEditorArtboardService.ValidateName(label);
                var changed = SKRect.Create((float)x.Value, (float)y.Value, (float)width.Value, (float)height.Value);
                SvgEditorArtboardService.ValidateBounds(changed);
                if (create)
                    controller.AddArtboardInRootCoordinates(changed, label);
                else if (label != active.Name || changed != active.Bounds)
                {
                    controller.Execute(nameOnly ? "Rename artboard" : "Edit artboard", () =>
                    {
                        if (label != active.Name)
                            controller.RenameArtboard(id, label);
                        if (changed != active.Bounds)
                            controller.SetArtboardBoundsInRootCoordinates(controller.ActiveArtboardId!, changed);
                    });
                }
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
        };
        dialog.Opened += (_, _) => { name.Focus(FocusState.Programmatic); name.SelectAll(); };
        if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary)
        {
            RefreshEditorArtboardPanel();
            if (!nameOnly)
                FitEditorActiveArtboard();
        }
    }
}
