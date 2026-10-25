using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Svg.Editor.Skia;
using Windows.System;
using Windows.UI.Core;

namespace Svg.Editor.Skia.Uno;

/// <summary>Retained gradient editing with immutable draft history and one-step document application.</summary>
public sealed class StudioGradientEditor : UserControl, IDisposable
{
    private sealed record StopChoice(Guid Id, string Label) { public override string ToString() => Label; }
    private sealed record Snapshot(SvgGradientRamp Ramp, SvgGradientKind Kind, bool Bounds, double Angle, SvgGradientSpread Spread);
    private readonly StudioChoiceStrip _target = new() { Header = "Paint", ItemsSource = new[] { "Fill", "Stroke" }, SelectedIndex = 0 };
    private readonly ComboBox _geometry = Choice("Geometry", "Keep imported geometry", "Linear · object bounds", "Radial · object bounds");
    private readonly ComboBox _spread = Choice("Outside gradient", "Pad", "Reflect", "Repeat");
    private readonly ComboBox _stops = new() { Header = "Selected stop", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly NumberBox _position = Number("Position %", 0, 100);
    private readonly NumberBox _opacity = Number("Stop opacity %", 0, 100);
    private readonly NumberBox _angle = Number("Angle °", -36000, 36000);
    private readonly StudioGradientStopBar _bar = new();
    private readonly StudioPaintChip _stopPaint = new() { Label = "Color", VerticalAlignment = VerticalAlignment.Bottom };
    private readonly ColorPicker _color = new()
    {
        IsAlphaEnabled = true, IsAlphaSliderVisible = true, IsAlphaTextInputVisible = true,
        IsColorSpectrumVisible = true, IsColorSliderVisible = true, IsHexInputVisible = true,
        IsColorChannelTextInputVisible = true, IsMoreButtonVisible = false, MinWidth = 0,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly CheckBox _shared = new() { Content = "Edit the shared definition" };
    private readonly CheckBox _showPreview = new() { Content = "Preview on artwork" };
    private readonly StudioSvgPreview _preview = new() { Height = 160, Visibility = Visibility.Collapsed };
    private readonly TextBlock _status = Hint(string.Empty);
    private readonly Button _apply = Action("Apply gradient");
    private readonly Button _reset = Action("Reset draft");
    private readonly Button _fresh = Action("New gradient");
    private readonly Button _undo = Action("Undo draft");
    private readonly Button _redo = Action("Redo draft");
    private readonly Button _add = Action("Add stop");
    private readonly Button _duplicate = Action("Duplicate");
    private readonly Button _remove = Action("Remove");
    private readonly Button _reverse = Action("Reverse");
    private readonly Button _distribute = Action("Even spacing");
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly List<Snapshot> _past = new();
    private readonly List<Snapshot> _future = new();
    private SvgEditorController? _controller;
    private SvgGradientEdit? _edit;
    private Snapshot? _baseline, _gestureStart;
    private Guid _selected;
    private bool _syncing, _loaded, _disposed, _queued, _applying;
    public Action? BeforeApply { get; set; }
    public bool HasPendingDraft => _edit is not null && _baseline != Capture();

    public StudioGradientEditor()
    {
        IsTabStop = false;
        Resources.MergedDictionaries.Add(new StudioThemeResources());
        BuildPresentation();
        AutomationProperties.SetAutomationId(_apply, "StudioGradientApply");
        AutomationProperties.SetAutomationId(_reverse, "StudioGradientReverse");
        AutomationProperties.SetAutomationId(_position, "StudioGradientPosition");
        AutomationProperties.SetAutomationId(_target, "StudioGradientTarget");
        AutomationProperties.SetAutomationId(_color, "StudioGradientStopColor");
        AutomationProperties.SetName(_color, "Gradient stop color, independent of stop opacity");
        ToolTipService.SetToolTip(_apply, "Apply the gradient as one document edit (Ctrl+Enter in this panel)");
        _target.SelectionChanging += (_, args) =>
        {
            if (!_syncing && HasPendingDraft)
            {
                args.Cancel = true;
                _status.Text = "Apply or Reset this draft before changing between fill and stroke.";
            }
        };
        _target.SelectionChanged += (_, _) =>
        {
            if (_syncing) return;
            if (HasPendingDraft && _edit is not null)
            {
                _syncing = true; _target.SelectedIndex = _edit.IsStroke ? 1 : 0; _syncing = false;
                _status.Text = "Apply or Reset this draft before changing between fill and stroke."; return;
            }
            ResetDraft();
        };
        _geometry.SelectionChanged += (_, _) => Change(() =>
        {
            if (_geometry.SelectedIndex == 0) _edit!.PreserveOriginalGeometry();
            else _edit!.UseObjectBounds(_geometry.SelectedIndex == 2 ? SvgGradientKind.Radial : SvgGradientKind.Linear, Finite(_angle.Value) ? _angle.Value : 0);
        });
        _spread.SelectionChanged += (_, _) => Change(() => _edit!.Spread = (SvgGradientSpread)Math.Max(0, _spread.SelectedIndex));
        _angle.ValueChanged += (_, args) => { if (Finite(args.NewValue)) Change(() => _edit!.UseObjectBounds(_edit.Kind, args.NewValue)); };
        _stops.SelectionChanged += (_, _) =>
        { if (!_syncing && _stops.SelectedItem is StopChoice choice) { _selected = choice.Id; SynchronizeFields(); } };
        _position.ValueChanged += (_, args) => { if (Finite(args.NewValue)) Change(() => _edit!.Ramp = _edit.Ramp.SetOffset(_selected, args.NewValue / 100)); };
        _opacity.ValueChanged += (_, args) => { if (Finite(args.NewValue)) Change(() => _edit!.Ramp = _edit.Ramp.SetOpacity(_selected, args.NewValue / 100)); };
        _color.ColorChanged += (_, args) => Change(() =>
        {
            var color = args.NewColor;
            _edit!.Ramp = _edit.Ramp.SetColor(_selected, new SKColor(color.R, color.G, color.B, color.A));
        });
        _bar.StopSelected += (_, id) => { _selected = id; SynchronizeFields(); };
        _bar.AddStopRequested += (_, offset) => AddStop(offset);
        _bar.RemoveStopRequested += (_, id) => { _selected = id; RemoveStop(); };
        _bar.OffsetRequested += (_, args) => Change(() => _edit!.Ramp = _edit.Ramp.SetOffset(args.Id, args.Offset), _gestureStart is null);
        _bar.EditStarted += (_, _) => { if (_edit?.IsCurrent(_controller) == true) _gestureStart = Capture(); };
        _bar.EditCompleted += (_, cancelled) =>
        {
            if (_gestureStart is not { } before || _edit is null) return;
            _gestureStart = null;
            if (cancelled) Restore(before); else if (before != Capture()) Remember(before);
            SynchronizeFields(); SchedulePreview();
        };
        _add.Click += (_, _) => AddStop(0.5);
        _duplicate.Click += (_, _) => Change(() => _edit!.Ramp = _edit.Ramp.Duplicate(_selected, out _selected));
        _remove.Click += (_, _) => RemoveStop();
        _reverse.Click += (_, _) => Change(() => _edit!.Ramp = _edit.Ramp.Reverse());
        _distribute.Click += (_, _) => Change(() => _edit!.Ramp = _edit.Ramp.DistributeEvenly());
        _undo.Click += (_, _) => Navigate(false); _redo.Click += (_, _) => Navigate(true);
        _reset.Click += (_, _) => ResetDraft(); _fresh.Click += (_, _) => ResetDraft(true);
        _apply.Click += (_, _) => Apply();
        _shared.Checked += (_, _) => SchedulePreview(); _shared.Unchecked += (_, _) => SchedulePreview();
        _showPreview.Checked += (_, _) => { _preview.Visibility = Visibility.Visible; SchedulePreview(); };
        _showPreview.Unchecked += (_, _) => { _previewTimer.Stop(); _preview.SetRenderer(null); _preview.Visibility = Visibility.Collapsed; };
        _previewTimer.Tick += (_, _) => RenderPreview();
        KeyDown += (_, args) =>
        {
            if (!args.Handled && args.Key == VirtualKey.Enter && (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0)
            { Apply(); args.Handled = true; }
        };
        Loaded += (_, _) =>
        {
            if (_disposed || _loaded) return;
            _loaded = true; if (_controller is not null) _controller.Changed += ControllerChanged; Synchronize();
        };
        Unloaded += (_, _) =>
        {
            if (_controller is not null) _controller.Changed -= ControllerChanged;
            _loaded = false; _previewTimer.Stop(); _preview.SetRenderer(null);
        };
        ResetDraft();
    }

    private void BuildPresentation()
    {
        var panel = new StackPanel { Spacing = 12 };
        var actions = Fields(112, 2, _apply, _reset);
        StudioTheme.ApplyRole(_apply, StudioActionRole.Primary, this);
        StudioTheme.ApplyRole(_remove, StudioActionRole.Destructive, this);
        panel.Children.Add(_target);
        panel.Children.Add(_bar);
        panel.Children.Add(actions);
        _status.Style = (Style)Resources["StudioHintStyle"];
        panel.Children.Add(_status);
        var stopRow = new Grid { ColumnSpacing = 8 };
        stopRow.ColumnDefinitions.Add(new ColumnDefinition());
        stopRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        stopRow.Children.Add(_stops); stopRow.Children.Add(_stopPaint); Grid.SetColumn(_stopPaint, 1);
        panel.Children.Add(stopRow);
        panel.Children.Add(Fields(120, 2, _position, _opacity));
        var colorSection = new StudioSection { Header = "Stop color and alpha", IsExpanded = false, Content = _color };
        _stopPaint.Click += (_, _) => { colorSection.IsExpanded = !colorSection.IsExpanded; if (colorSection.IsExpanded) colorSection.StartBringIntoView(); };
        panel.Children.Add(colorSection);
        panel.Children.Add(Fields(76, 3, _add, _duplicate, _remove));
        panel.Children.Add(Fields(112, 2, _reverse, _distribute));
        var geometry = new StackPanel { Spacing = 12 };
        geometry.Children.Add(_geometry); geometry.Children.Add(Fields(120, 2, _angle, _spread));
        geometry.Children.Add(_shared); geometry.Children.Add(_showPreview); geometry.Children.Add(_preview);
        panel.Children.Add(new StudioSection { Header = "Geometry and preview", Content = geometry });
        panel.Children.Add(Fields(112, 2, _undo, _redo));
        panel.Children.Add(_fresh);
        var help = Hint("Click the ramp to add a stop. Drag handles or use arrows for 1%, Shift+arrows for 10%. Home/End moves to an endpoint. Escape cancels a drag. Ctrl+Enter applies the draft.");
        help.Style = (Style)Resources["StudioHintStyle"];
        panel.Children.Add(new StudioSection { Header = "Keyboard and editing help", IsExpanded = false, Content = help });
        Content = panel;
    }
    private static StudioFieldGrid Fields(double minimum, int columns, params UIElement[] controls)
    {
        var grid = new StudioFieldGrid { MinimumColumnWidth = minimum, MaximumColumns = columns };
        foreach (var control in controls) grid.Children.Add(control);
        return grid;
    }
    public void BindController(SvgEditorController? controller)
    {
        if (_disposed || ReferenceEquals(controller, _controller)) return;
        if (_loaded && _controller is not null) _controller.Changed -= ControllerChanged;
        _controller = controller;
        if (_loaded && _controller is not null) _controller.Changed += ControllerChanged;
        Synchronize();
    }
    private void ControllerChanged(object? sender, EventArgs args)
    {
        if (_disposed || _queued || _applying) return;
        _queued = true;
        if (!DispatcherQueue.TryEnqueue(() => { _queued = false; if (_loaded && !_disposed) Synchronize(); })) _queued = false;
    }
    private void Synchronize()
    {
        if (HasPendingDraft) { UpdateAvailability(); SchedulePreview(); } else ResetDraft();
    }
    public void ResetDraft(bool createNew = false)
    {
        if (_disposed) return;
        _syncing = true;
        try
        {
            _edit = null; _baseline = null; _gestureStart = null; _past.Clear(); _future.Clear(); _shared.IsChecked = false;
            if (_controller is null || _controller.SelectedElements.Count == 0)
            { _bar.SetRamp(null, Guid.Empty); _status.Text = "Select artwork to create or edit a gradient."; return; }
            _edit = _controller.BeginGradientEdit(_target.SelectedIndex == 1, createNew);
            _selected = _edit.Ramp.Stops[0].Id; _baseline = Capture();
            _status.Text = _edit.IsMixed ? "Mixed paints. Apply replaces them with one gradient."
                : _edit.OriginalGradientId is not null ? "Imported geometry is preserved. Apply creates an isolated copy."
                : "New gradient draft. Apply creates one undo step.";
        }
        catch (Exception exception) { _status.Text = exception.Message; _bar.SetRamp(null, Guid.Empty); }
        finally { _syncing = false; SynchronizeFields(); SchedulePreview(); }
    }
    public void ReverseDraft() { RequireCurrentDraft(); Change(() => _edit!.Ramp = _edit.Ramp.Reverse()); }
    public void UndoDraft() { RequireCurrentDraft(); Navigate(false); }
    public void RedoDraft() { RequireCurrentDraft(); Navigate(true); }
    public void ApplyDraft()
    {
        RequireCurrentDraft(); BeforeApply?.Invoke(); RequireCurrentDraft();
        try { _applying = true; _controller!.ApplyGradientEdit(_edit!, _shared.IsChecked == true); ResetDraft(); }
        finally { _applying = false; }
    }
    private void RequireCurrentDraft()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StudioGradientEditor));
        if (_edit?.IsCurrent(_controller) != true) throw new InvalidOperationException("Select artwork and open or reset the gradient editor before applying a draft action.");
    }
    private Snapshot? Capture() => _edit is null ? null : new Snapshot(_edit.Ramp, _edit.Kind, _edit.UsesObjectBounds, _edit.Angle, _edit.Spread);
    private void Restore(Snapshot state)
    {
        if (_edit is null) return;
        _edit.Ramp = state.Ramp; _edit.Spread = state.Spread;
        if (state.Bounds) _edit.UseObjectBounds(state.Kind, state.Angle); else _edit.PreserveOriginalGeometry();
        if (!_edit.Ramp.Stops.Any(stop => stop.Id == _selected)) _selected = _edit.Ramp.Stops[0].Id;
    }
    private void Remember(Snapshot before)
    {
        _past.Add(before); if (_past.Count > 64) _past.RemoveAt(0); _future.Clear();
    }
    private void Change(Action action, bool record = true)
    {
        if (_syncing || _disposed || _edit is null) return;
        if (!_edit.IsCurrent(_controller)) { UpdateAvailability(); return; }
        var before = Capture()!;
        try { action(); if (record && before != Capture()) Remember(before); SynchronizeFields(); SchedulePreview(); }
        catch (Exception exception) { Restore(before); SynchronizeFields(); _status.Text = exception.Message; }
    }
    private void AddStop(double offset) => Change(() =>
    {
        var sample = _edit!.Ramp.Sample(offset); _edit.Ramp = _edit.Ramp.Add(offset, sample.Color, sample.Opacity, out _selected);
    });
    private void RemoveStop() => Change(() =>
    {
        var index = _edit!.Ramp.Stops.Select(stop => stop.Id).ToList().IndexOf(_selected);
        _edit.Ramp = _edit.Ramp.Remove(_selected); _selected = _edit.Ramp.Stops[Math.Min(index, _edit.Ramp.Stops.Count - 1)].Id;
    });
    private void Navigate(bool forward)
    {
        if (_edit?.IsCurrent(_controller) != true) return;
        var source = forward ? _future : _past; var destination = forward ? _past : _future;
        if (source.Count == 0) return;
        destination.Add(Capture()!); var state = source[source.Count - 1]; source.RemoveAt(source.Count - 1);
        Restore(state); SynchronizeFields(); SchedulePreview();
    }
    private void SynchronizeFields()
    {
        if (_syncing || _disposed) return;
        _syncing = true;
        try
        {
            if (_edit is null) return;
            if (!_edit.Ramp.Stops.Any(stop => stop.Id == _selected)) _selected = _edit.Ramp.Stops[0].Id;
            var stop = _edit.Ramp.Find(_selected); _bar.SetRamp(_edit.Ramp, _selected);
            var choices = _edit.Ramp.Stops.Select((item, index) => new StopChoice(item.Id, $"{index + 1}.  {item.Offset * 100:0.##}%")).ToArray();
            _stops.ItemsSource = choices; _stops.SelectedItem = choices.First(choice => choice.Id == _selected);
            _position.Value = stop.Offset * 100; _opacity.Value = stop.Opacity * 100;
            _color.Color = Windows.UI.Color.FromArgb(stop.Color.Alpha, stop.Color.Red, stop.Color.Green, stop.Color.Blue);
            _stopPaint.Paint = SvgSolidPaintDraft.FormatColor(stop.Color);
            _angle.Value = _edit.Angle; _spread.SelectedIndex = (int)_edit.Spread;
            _geometry.SelectedIndex = !_edit.UsesObjectBounds ? 0 : _edit.Kind == SvgGradientKind.Radial ? 2 : 1;
        }
        finally { _syncing = false; UpdateAvailability(); }
    }
    private void UpdateAvailability()
    {
        var current = _edit?.IsCurrent(_controller) == true;
        foreach (var control in new Control[] { _bar, _position, _opacity, _color, _stopPaint, _stops, _geometry, _spread, _reverse, _distribute }) control.IsEnabled = current;
        _angle.IsEnabled = current && _edit!.UsesObjectBounds && _edit.Kind == SvgGradientKind.Linear;
        _add.IsEnabled = _duplicate.IsEnabled = current && _edit!.Ramp.Stops.Count < SvgGradientRamp.MaximumStops;
        _remove.IsEnabled = current && _edit!.Ramp.Stops.Count > 2;
        _shared.IsEnabled = current && _edit!.OriginalGradientId is not null;
        _shared.Visibility = _edit?.OriginalGradientId is null ? Visibility.Collapsed : Visibility.Visible;
        _undo.IsEnabled = current && _past.Count > 0; _redo.IsEnabled = current && _future.Count > 0;
        _apply.IsEnabled = current && (HasPendingDraft || _edit!.OriginalGradientId is null);
        _reset.IsEnabled = _fresh.IsEnabled = _controller is not null;
        if (_edit is not null && !current) _status.Text = "The document or selection changed. Reset before applying.";
        else if (HasPendingDraft) _status.Text = "Unapplied draft. Apply saves one undo step; Reset discards it.";
    }
    private void SchedulePreview()
    {
        _previewTimer.Stop();
        if (_syncing || !_loaded || _disposed || _showPreview.IsChecked != true) return;
        if (_edit?.IsCurrent(_controller) == true) _previewTimer.Start(); else _preview.SetRenderer(null);
    }
    private void RenderPreview()
    {
        _previewTimer.Stop();
        if (!_loaded || _disposed || _edit?.IsCurrent(_controller) != true || _showPreview.IsChecked != true) return;
        try { _preview.SetRenderer(_controller!.CreateGradientPreviewRenderer(_edit, _shared.IsChecked == true)); }
        catch (Exception exception) { _preview.SetRenderer(null); _status.Text = "Preview: " + exception.Message; }
    }
    private void Apply()
    {
        try { ApplyDraft(); }
        catch (Exception exception) { _status.Text = exception.Message; _apply.IsEnabled = false; }
    }
    public new void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _previewTimer.Stop();
        if (_controller is not null) _controller.Changed -= ControllerChanged;
        _controller = null; _edit = null; _past.Clear(); _future.Clear(); BeforeApply = null; _preview.Dispose(); base.Dispose();
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static Button Action(string label) => new() { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch };
    private static TextBlock Hint(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private static ComboBox Choice(string header, params string[] items) => new() { Header = header, ItemsSource = items, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 0 };
    private static NumberBox Number(string header, double minimum, double maximum) => new()
    {
        Header = header, Minimum = minimum, Maximum = maximum, Value = 0, SmallChange = 1, LargeChange = 10,
        NumberFormatter = new EditorNumberFormatter(), SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 0
    };
}
