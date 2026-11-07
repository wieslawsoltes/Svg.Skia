using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Xml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Editor.Skia;
using Svg.Editor.Svg;
using Svg.Editor.Svg.Models;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private sealed record EditorHistoryEntry(string Label, int Steps, bool Redo, SvgEditorController Owner, long Revision)
    {
        public override string ToString() => $"{Steps:00}  {Label}";
    }

    private readonly AppearanceService _editorAppearance = new();
    private ListView? _editorGraphicStyles, _editorUndoHistory, _editorRedoHistory;
    private Button? _editorCaptureStyle, _editorApplyStyle, _editorRemoveStyle, _editorUndoAction, _editorRedoAction;
    private TextBlock? _editorStyleSummary, _editorDocumentInfo, _editorSelectionInfo, _editorHistorySummary, _editorNavigatorSummary;
    private StudioNavigator? _editorNavigator;
    private NumberBox? _editorNavigatorZoom;
    private SvgEditorController? _editorUtilityController;
    private long _editorUtilityRevision = -1;
    private int _editorUndoHistoryCount = -1, _editorRedoHistoryCount = -1;
    private string? _editorStyleFingerprint, _editorInfoFile, _editorInfoName;
    private string? _editorPendingStyleName;
    private bool _syncingEditorUtilityPanels;

    private void BuildEditorUtilityPanels()
    {
        var styles = StudioPanel("graphic-styles", "Graphic Styles");
        _editorGraphicStyles = EditorPanelList("Document graphic styles");
        _editorGraphicStyles.IsItemClickEnabled = false;
        _editorGraphicStyles.ItemsSource = _editorAppearance.Styles;
        _editorGraphicStyles.SelectionChanged += (_, _) => RefreshEditorStyleActions();
        _editorGraphicStyles.DoubleTapped += async (_, _) => await RunCommandAsync("style-apply");
        _editorStyleSummary = StudioHint("Capture a selected object's fill, stroke, and opacity as a reusable document style.");
        _editorCaptureStyle = ActionButton("style-capture", "New style…");
        _editorApplyStyle = ActionButton("style-apply", "Apply style");
        _editorRemoveStyle = ActionButton("style-remove", "Remove style");
        styles.Children.Add(_editorGraphicStyles);
        styles.Children.Add(_editorStyleSummary);
        styles.Children.Add(StudioPair(_editorCaptureStyle, _editorApplyStyle));
        styles.Children.Add(_editorRemoveStyle);

        var navigator = StudioPanel("navigator", "Navigator");
        _editorNavigator = new StudioNavigator { Height = 168, HorizontalAlignment = HorizontalAlignment.Stretch };
        _editorNavigator.NavigationStarting += (_, _) => { CommitInspectorEdits(); Canvas.CancelGesture(); };
        _editorNavigator.NavigationFailed += (_, args) => _ = ShowErrorAsync(args.Exception.Message);
        _editorNavigator.ViewChanged += (_, _) => RefreshEditorNavigatorStatus();
        AutomationProperties.SetAutomationId(_editorNavigator, "EditorNavigator");
        _editorNavigatorZoom = new NumberBox
        {
            Header = "Zoom %",
            Minimum = SvgEditorViewport.MinimumZoom * 100,
            Maximum = SvgEditorViewport.MaximumZoom * 100,
            SmallChange = 10,
            LargeChange = 100,
            Value = 100,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            NumberFormatter = new EditorNumberFormatter(),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _editorNavigatorZoom.ValueChanged += (_, args) =>
        {
            if (_syncingEditorUtilityPanels || !double.IsFinite(args.NewValue) || args.NewValue <= 0)
                return;
            ApplySafely(() =>
            {
                CommitInspectorEdits();
                Canvas.CancelGesture();
                Canvas.Viewport.SetZoomAt(args.NewValue / 100, new SKPoint((float)Canvas.Viewport.Width / 2, (float)Canvas.Viewport.Height / 2));
            });
        };
        AutomationProperties.SetAutomationId(_editorNavigatorZoom, "EditorNavigatorZoom");
        _editorNavigatorSummary = StudioHint(string.Empty);
        navigator.Children.Add(_editorNavigator);
        navigator.Children.Add(_editorNavigatorZoom);
        navigator.Children.Add(StudioPair(ActionButton("fit", "Fit artboard"), ActionButton("fitselection", "Fit selection")));
        navigator.Children.Add(_editorNavigatorSummary);

        var info = StudioPanel("document-info", "Document Info");
        _editorDocumentInfo = StudioHint(string.Empty);
        _editorSelectionInfo = StudioHint(string.Empty);
        _editorDocumentInfo.IsTextSelectionEnabled = _editorSelectionInfo.IsTextSelectionEnabled = true;
        AutomationProperties.SetAutomationId(_editorDocumentInfo, "EditorDocumentInfo");
        AutomationProperties.SetAutomationId(_editorSelectionInfo, "EditorSelectionInfo");
        info.Children.Add(StudioSection("Document", _editorDocumentInfo));
        info.Children.Add(StudioSection("Selection", _editorSelectionInfo));
        info.Children.Add(StudioPair(ActionButton("artboard", "Artboard size…"), ActionButton("preflight", "Preflight…")));

        var history = StudioPanel("history", "History");
        _editorHistorySummary = StudioHint(string.Empty);
        _editorUndoAction = ActionButton("undo", "Undo");
        _editorRedoAction = ActionButton("redo", "Redo");
        _editorUndoHistory = EditorPanelList("Operations to undo, newest first");
        _editorRedoHistory = EditorPanelList("Operations to redo, next first");
        _editorUndoHistory.ItemClick += EditorHistoryItemClicked;
        _editorRedoHistory.ItemClick += EditorHistoryItemClicked;
        AutomationProperties.SetAutomationId(_editorUndoHistory, "EditorUndoHistory");
        AutomationProperties.SetAutomationId(_editorRedoHistory, "EditorRedoHistory");
        history.Children.Add(_editorHistorySummary);
        history.Children.Add(StudioPair(_editorUndoAction, _editorRedoAction));
        history.Children.Add(StudioSection("Undo through operation", _editorUndoHistory));
        history.Children.Add(StudioSection("Redo through operation", _editorRedoHistory));
        history.Children.Add(StudioHint("Choose a row to step through that operation. Each change remains available in the opposite list."));
        RefreshEditorUtilityPanels();
    }

    private void BuildEditorUtilityCommands()
    {
        _commands.Add(new EditorCommand("style-capture", "New graphic style…", "Object", string.Empty, CaptureEditorGraphicStyleAsync)
        { CanExecute = () => Controller?.SelectedElements.Count == 1 });
        _commands.Add(new EditorCommand("style-apply", "Apply graphic style", "Object", string.Empty, () =>
        {
            ApplyEditorGraphicStyle();
            return Task.CompletedTask;
        })
        { CanExecute = CanApplyEditorGraphicStyle });
        _commands.Add(new EditorCommand("style-remove", "Remove graphic style", "Object", string.Empty, () =>
        {
            RemoveEditorGraphicStyle();
            return Task.CompletedTask;
        })
        { CanExecute = () => Controller?.Document is not null && _editorGraphicStyles?.SelectedItem is StyleEntry });
    }

    private bool CanApplyEditorGraphicStyle() => Controller is { } controller && controller.SelectedElements.Count > 0
        && !controller.SelectedElements.Any(SvgEditorController.IsEditLocked) && _editorGraphicStyles?.SelectedItem is StyleEntry;

    private async Task CaptureEditorGraphicStyleAsync()
    {
        if (Controller is not { Document: { } document } controller || controller.SelectedElements.Count != 1)
            throw new InvalidOperationException("Select one object to capture its appearance.");
        var revision = controller.DocumentRevision;
        var captured = StyleEntry.FromElement(string.Empty, controller.SelectedElements[0]);
        var known = new AppearanceService();
        known.Load(document);
        var index = 1;
        while (known.Styles.Any(style => style.Name == "Style_" + index))
            index++;
        var name = new TextBox { Header = "Style name", Text = "Style_" + index, MaxLength = 80, HorizontalAlignment = HorizontalAlignment.Stretch };
        var error = StudioHint(string.Empty);
        error.Foreground = (Brush)Resources["StudioDanger"];
        var content = new StackPanel { Spacing = 10, Width = DialogContentWidth(320) };
        content.Children.Add(name);
        content.Children.Add(StudioHint($"Fill: {captured.Fill ?? "inherited"}\nStroke: {captured.Stroke ?? "inherited"}\nOpacity: {captured.Opacity * 100:0.#}%"));
        content.Children.Add(error);
        var dialog = MakeDialog("New graphic style", content, "Create style");
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                if (!ReferenceEquals(Controller, controller) || controller.DocumentRevision != revision)
                    throw new InvalidOperationException("The document changed. Close this dialog and capture the style again.");
                var label = name.Text.Trim();
                if (string.IsNullOrEmpty(label))
                    throw new InvalidOperationException("Enter a style name.");
                try
                { XmlConvert.VerifyNCName("data-style-" + label); }
                catch (XmlException) { throw new InvalidOperationException("Use letters, numbers, hyphens, or underscores in the style name."); }
                if (known.Styles.Any(style => string.Equals(style.Name, label, StringComparison.Ordinal)))
                    throw new InvalidOperationException("A style with this name already exists. Enter another name.");
                captured.Name = label;
                _editorPendingStyleName = label;
                try
                { controller.Execute("Create graphic style", () => known.AddOrUpdateStyle(document, captured)); }
                catch { _editorPendingStyleName = null; throw; }
                StatusText.Text = "Created graphic style " + label + ".";
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
        };
        await ShowDialogAsync(dialog);
        RefreshEditorUtilityPanels();
    }

    private StyleEntry ResolveEditorGraphicStyle(SvgEditorController controller)
    {
        if (_editorGraphicStyles?.SelectedItem is not StyleEntry selected)
            throw new InvalidOperationException("Choose a graphic style first.");
        var current = new AppearanceService();
        current.Load(controller.Document);
        return current.Styles.FirstOrDefault(style => style.Name == selected.Name)
            ?? throw new InvalidOperationException("The selected style is no longer in this document.");
    }

    private void ApplyEditorGraphicStyle()
    {
        if (Controller is not { } controller || controller.SelectedElements.Count == 0)
            throw new InvalidOperationException("Select artwork before applying a graphic style.");
        controller.EnsureSelectionEditable();
        var style = ResolveEditorGraphicStyle(controller);
        if (!float.IsFinite(style.Opacity) || style.Opacity is < 0 or > 1)
            throw new InvalidOperationException("The stored style has an invalid opacity.");
        var converter = TypeDescriptor.GetConverter(typeof(SvgPaintServer));
        foreach (var paint in new[] { style.Fill, style.Stroke })
            if (paint is not null && converter.ConvertFromInvariantString(paint) is not SvgPaintServer)
                throw new InvalidOperationException("The stored style has an invalid paint value.");
        var selected = controller.SelectedElements.ToArray();
        controller.Execute("Apply graphic style", () =>
        {
            foreach (var element in selected)
                style.Apply(element);
        });
        StatusText.Text = $"Applied {style.Name} to {selected.Length} object{(selected.Length == 1 ? string.Empty : "s")}.";
    }

    private void RemoveEditorGraphicStyle()
    {
        if (Controller is not { Document: { } document } controller)
            return;
        var style = ResolveEditorGraphicStyle(controller);
        var retained = _editorAppearance.Styles.FirstOrDefault(item => item.Name == style.Name) ?? style;
        controller.Execute("Remove graphic style", () => _editorAppearance.RemoveStyle(document, retained));
        StatusText.Text = "Removed graphic style " + style.Name + ".";
    }

    private void RefreshEditorUtilityPanels()
    {
        if (_syncingEditorUtilityPanels)
            return;
        _syncingEditorUtilityPanels = true;
        try
        {
            var controller = Controller;
            _editorNavigator?.Bind(controller, controller is null ? null : Canvas.Viewport);
            if (controller?.Document is not { } document)
            {
                _editorAppearance.Load(null);
                if (_editorDocumentInfo is not null)
                    _editorDocumentInfo.Text = "Open a document to inspect its properties.";
                if (_editorSelectionInfo is not null)
                    _editorSelectionInfo.Text = "No selection.";
                if (_editorUndoHistory is not null)
                    _editorUndoHistory.ItemsSource = null;
                if (_editorRedoHistory is not null)
                    _editorRedoHistory.ItemsSource = null;
                _editorUtilityController = null;
                _editorUtilityRevision = -1;
                _editorUndoHistoryCount = _editorRedoHistoryCount = -1;
                _editorStyleFingerprint = _editorInfoFile = _editorInfoName = null;
            }
            else
            {
                var documentChanged = !ReferenceEquals(controller, _editorUtilityController) || _editorUtilityRevision != controller.DocumentRevision;
                if (documentChanged)
                {
                    var selectedName = _editorPendingStyleName ?? (ReferenceEquals(controller, _editorUtilityController)
                        ? (_editorGraphicStyles?.SelectedItem as StyleEntry)?.Name : null);
                    _editorPendingStyleName = null;
                    var fingerprint = string.Join("\n", document.CustomAttributes.Where(pair => pair.Key.StartsWith("data-style-", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value));
                    if (!ReferenceEquals(controller, _editorUtilityController) || fingerprint != _editorStyleFingerprint)
                    {
                        _editorAppearance.Load(document);
                        _editorStyleFingerprint = fingerprint;
                        if (_editorGraphicStyles is not null)
                            _editorGraphicStyles.SelectedItem = _editorAppearance.Styles.FirstOrDefault(style => style.Name == selectedName);
                    }
                    _editorUtilityController = controller;
                    _editorUtilityRevision = controller.DocumentRevision;
                }
                if (documentChanged || _editorInfoFile != controller.Session.CurrentFile || _editorInfoName != _current?.Name)
                {
                    RefreshEditorDocumentInfo(controller, document);
                    _editorInfoFile = controller.Session.CurrentFile;
                    _editorInfoName = _current?.Name;
                }
                RefreshEditorSelectionInfo(controller);
                if (documentChanged || _editorUndoHistoryCount != controller.Session.UndoCount || _editorRedoHistoryCount != controller.Session.RedoCount)
                {
                    if (_editorUndoHistory is not null)
                        _editorUndoHistory.ItemsSource = controller.Session.UndoLabels.Select((label, index) =>
                            new EditorHistoryEntry(label, index + 1, false, controller, controller.DocumentRevision)).ToArray();
                    if (_editorRedoHistory is not null)
                        _editorRedoHistory.ItemsSource = controller.Session.RedoLabels.Select((label, index) =>
                            new EditorHistoryEntry(label, index + 1, true, controller, controller.DocumentRevision)).ToArray();
                    _editorUndoHistoryCount = controller.Session.UndoCount;
                    _editorRedoHistoryCount = controller.Session.RedoCount;
                }
            }
            if (_editorHistorySummary is not null)
                _editorHistorySummary.Text = controller is null ? "No document history." : $"{controller.Session.UndoCount} earlier operations · {controller.Session.RedoCount} operations to redo";
            if (_editorUndoAction is not null)
                _editorUndoAction.IsEnabled = controller?.CanUndo == true;
            if (_editorRedoAction is not null)
                _editorRedoAction.IsEnabled = controller?.CanRedo == true;
            RefreshEditorStyleActions();
        }
        finally { _syncingEditorUtilityPanels = false; }
        RefreshEditorNavigatorStatus();
    }

    private void RefreshEditorStyleActions()
    {
        if (_editorCaptureStyle is not null)
            _editorCaptureStyle.IsEnabled = Controller?.SelectedElements.Count == 1;
        if (_editorApplyStyle is not null)
            _editorApplyStyle.IsEnabled = CanApplyEditorGraphicStyle();
        if (_editorRemoveStyle is not null)
            _editorRemoveStyle.IsEnabled = Controller?.Document is not null && _editorGraphicStyles?.SelectedItem is StyleEntry;
        if (_editorStyleSummary is not null)
            _editorStyleSummary.Text = _editorGraphicStyles?.SelectedItem is StyleEntry style
                ? $"{style.Name}\nFill: {style.Fill ?? "inherited"}\nStroke: {style.Stroke ?? "inherited"}\nOpacity: {style.Opacity * 100:0.#}%"
                : "Capture one object's fill, stroke, and opacity. Select artwork and a style to apply it.";
    }

    private void RefreshEditorNavigatorStatus()
    {
        if (_syncingEditorUtilityPanels)
            return;
        _syncingEditorUtilityPanels = true;
        try
        {
            if (_editorNavigatorZoom is not null)
            {
                _editorNavigatorZoom.IsEnabled = Controller?.Document is not null;
                if (!double.IsFinite(_editorNavigatorZoom.Value) || Math.Abs(_editorNavigatorZoom.Value - Canvas.ZoomPercent) > 0.0001)
                    _editorNavigatorZoom.Value = Canvas.ZoomPercent;
            }
            if (_editorNavigatorSummary is not null)
            {
                var bounds = Canvas.Viewport.VisibleWorldBounds;
                _editorNavigatorSummary.Text = Controller is null ? "Open a document to navigate."
                    : $"View center  {bounds.MidX:0.#}, {bounds.MidY:0.#}\nVisible area  {bounds.Width:0.#} × {bounds.Height:0.#}";
            }
        }
        finally { _syncingEditorUtilityPanels = false; }
    }

    private void RefreshEditorDocumentInfo(SvgEditorController controller, SvgDocument document)
    {
        if (_editorDocumentInfo is null)
            return;
        var elements = document.Descendants().ToArray();
        var visuals = elements.OfType<SvgVisualElement>().ToArray();
        var images = elements.OfType<SvgImage>().ToArray();
        var layers = elements.OfType<SvgGroup>().Count(group => group.CustomAttributes.TryGetValue("data-layer", out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        var title = document.Children.OfType<SvgTitle>().FirstOrDefault()?.Content;
        var description = document.Children.OfType<SvgDescription>().FirstOrDefault()?.Content;
        var information = new StringBuilder()
            .AppendLine(_current?.Name ?? "Untitled.svg")
            .AppendLine($"Artboard  {document.Width} × {document.Height}")
            .AppendLine($"ViewBox  {document.ViewBox}")
            .AppendLine($"Root ID  {(string.IsNullOrEmpty(document.ID) ? "none" : document.ID)}")
            .AppendLine()
            .AppendLine($"Elements  {elements.Length}")
            .AppendLine($"Visual elements  {visuals.Length}")
            .AppendLine($"Layers  {layers}")
            .AppendLine($"Paths  {elements.OfType<SvgPath>().Count()}")
            .AppendLine($"Text objects  {elements.OfType<SvgText>().Count()}")
            .AppendLine($"Images  {images.Length}")
            .AppendLine($"Linked images  {images.Count(image => !string.IsNullOrEmpty(image.Href) && !image.Href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))}")
            .AppendLine($"Symbols  {elements.OfType<SvgSymbol>().Count()}")
            .AppendLine($"Gradients  {elements.OfType<SvgGradientServer>().Count()}")
            .AppendLine($"Graphic styles  {_editorAppearance.Styles.Count}");
        if (!string.IsNullOrWhiteSpace(title))
            information.AppendLine().AppendLine("Title  " + title);
        if (!string.IsNullOrWhiteSpace(description))
            information.AppendLine("Description  " + description);
        if (!string.IsNullOrWhiteSpace(controller.Session.CurrentFile))
            information.AppendLine().AppendLine("Source  " + controller.Session.CurrentFile);
        _editorDocumentInfo.Text = information.ToString().TrimEnd();
    }

    private void RefreshEditorSelectionInfo(SvgEditorController controller)
    {
        if (_editorSelectionInfo is null)
            return;
        var selected = controller.SelectedElements;
        if (selected.Count == 0)
        { _editorSelectionInfo.Text = "No objects selected."; return; }
        var information = new StringBuilder().AppendLine($"Selected  {selected.Count}");
        if (controller.SelectionBounds is { } bounds)
        {
            information.AppendLine($"Position  {bounds.Left:0.###}, {bounds.Top:0.###}");
            information.AppendLine($"Size  {bounds.Width:0.###} × {bounds.Height:0.###}");
        }
        information.AppendLine($"Locked  {selected.Count(SvgEditorController.IsEditLocked)}");
        foreach (var group in selected.GroupBy(element => SvgElementInfo.GetElementName(element.GetType())).OrderBy(group => group.Key, StringComparer.Ordinal))
            information.AppendLine(group.Key + "  " + group.Count().ToString(CultureInfo.CurrentCulture));
        information.AppendLine();
        foreach (var element in selected.Take(10))
            information.AppendLine(string.IsNullOrEmpty(element.ID) ? "<" + SvgElementInfo.GetElementName(element.GetType()) + ">" : element.ID);
        if (selected.Count > 10)
            information.AppendLine($"… {selected.Count - 10} more");
        _editorSelectionInfo.Text = information.ToString().TrimEnd();
    }

    private void EditorHistoryItemClicked(object sender, ItemClickEventArgs args)
    {
        if (_syncingEditorUtilityPanels || args.ClickedItem is not EditorHistoryEntry entry)
            return;
        ApplySafely(() =>
        {
            CommitInspectorEdits();
            Canvas.CancelGesture();
            if (!ReferenceEquals(Controller, entry.Owner) || entry.Owner.DocumentRevision != entry.Revision)
            {
                RefreshEditorUtilityPanels();
                StatusText.Text = "History changed. Choose the current operation again.";
                return;
            }
            for (var index = 0; index < entry.Steps; index++)
            {
                if (entry.Redo)
                { if (!entry.Owner.CanRedo) break; entry.Owner.Redo(); }
                else
                { if (!entry.Owner.CanUndo) break; entry.Owner.Undo(); }
            }
            RefreshEditorUtilityPanels();
            StatusText.Text = (entry.Redo ? "Redid " : "Undid ") + entry.Steps + " operation" + (entry.Steps == 1 ? "." : "s.");
        });
    }

    private void DisposeEditorUtilityPanels()
    {
        _editorNavigator?.Dispose();
        _editorAppearance.Load(null);
        if (_editorUndoHistory is not null)
            _editorUndoHistory.ItemsSource = null;
        if (_editorRedoHistory is not null)
            _editorRedoHistory.ItemsSource = null;
        _editorUtilityController = null;
    }
}
