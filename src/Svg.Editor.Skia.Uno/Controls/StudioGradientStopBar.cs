using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Skia;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace Svg.Editor.Skia.Uno;

public sealed class StudioGradientOffsetEventArgs : EventArgs
{
    public StudioGradientOffsetEventArgs(Guid id, double offset) { Id = id; Offset = offset; }
    public Guid Id { get; }
    public double Offset { get; }
}

/// <summary>
/// A retained, keyboard-editable gradient ramp. Its stop buttons keep their identity
/// during drags and reordering, so focus and pointer capture are not discarded.
/// Gestures are bracketed for one local undo step; the control never edits an SVG.
/// </summary>
public sealed class StudioGradientStopBar : UserControl
{
    private const double HandleSize = 22;
    private readonly Border _ramp = new() { Height = 26, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1) };
    private readonly Microsoft.UI.Xaml.Controls.Canvas _handles = new() { Height = 30, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
    private readonly Dictionary<Guid, Button> _buttons = new();
    private SvgGradientRamp? _model;
    private Guid _selected;
    private uint? _pointer;
    private Guid _dragged;
    private bool _finishing;
    public event EventHandler<Guid>? StopSelected;
    public event EventHandler<double>? AddStopRequested;
    public event EventHandler<Guid>? RemoveStopRequested;
    public event EventHandler<StudioGradientOffsetEventArgs>? OffsetRequested;
    public event EventHandler? EditStarted;
    /// <summary>True means the current gesture was cancelled and should be reverted.</summary>
    public event EventHandler<bool>? EditCompleted;
    public Guid SelectedStopId => _selected;
    public bool IsDragging => _pointer is not null;

    public StudioGradientStopBar()
    {
        IsTabStop = false; MinHeight = 62; HorizontalAlignment = HorizontalAlignment.Stretch;
        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(HandleSize / 2, 0, HandleSize / 2, 0) };
        var checker = new Grid { Height = 26, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 190, 194, 202)) };
        for (var index = 0; index < 16; index++)
        {
            checker.ColumnDefinitions.Add(new ColumnDefinition());
            var square = new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 232, 236)), Height = 13,
                VerticalAlignment = index % 2 == 0 ? VerticalAlignment.Top : VerticalAlignment.Bottom };
            Grid.SetColumn(square, index); checker.Children.Add(square);
        }
        checker.Children.Add(_ramp); Grid.SetColumnSpan(_ramp, 16);
        stack.Children.Add(checker); stack.Children.Add(_handles); Content = stack;
        AutomationProperties.SetName(this, "Gradient stops");
        AutomationProperties.SetAutomationId(this, "StudioGradientRamp");
        ToolTipService.SetToolTip(_ramp, "Click to insert a stop. Drag a stop or use arrow keys. Shift moves ten percent. Escape cancels a drag.");
        _ramp.PointerPressed += (_, args) =>
        {
            if (_model is null || !args.GetCurrentPoint(_ramp).Properties.IsLeftButtonPressed) return;
            AddStopRequested?.Invoke(this, PositionToOffset(args.GetCurrentPoint(_ramp).Position.X)); args.Handled = true;
        };
        SizeChanged += (_, _) => PositionButtons();
        AddHandler(PointerMovedEvent, new PointerEventHandler(MovePointer), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(ReleasePointer), true);
        PointerCanceled += (_, _) => Finish(true);
        PointerCaptureLost += (_, _) => { if (!_finishing) Finish(true); };
        Unloaded += (_, _) => Finish(true);
    }

    public void SetRamp(SvgGradientRamp? ramp, Guid selected)
    {
        _model = ramp; _selected = selected;
        var alive = ramp?.Stops.Select(stop => stop.Id).ToHashSet() ?? new HashSet<Guid>();
        foreach (var id in _buttons.Keys.Where(id => !alive.Contains(id)).ToArray())
        { _handles.Children.Remove(_buttons[id]); _buttons.Remove(id); }
        if (ramp is null) { _ramp.Background = null; return; }
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        for (var index = 0; index < ramp.Stops.Count; index++)
        {
            var stop = ramp.Stops[index];
            var color = stop.PreviewColor;
            brush.GradientStops.Add(new GradientStop { Offset = stop.Offset, Color = Windows.UI.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue) });
            if (!_buttons.TryGetValue(stop.Id, out var button))
            {
                var id = stop.Id;
                button = new Button { Width = HandleSize, Height = 26, MinWidth = 0, MinHeight = 0,
                    Padding = new Thickness(3), CornerRadius = new CornerRadius(3), UseSystemFocusVisuals = true };
                button.AddHandler(PointerPressedEvent, new PointerEventHandler((_, args) => PressPointer(id, args)), true);
                button.Click += (_, _) => Select(id);
                button.KeyDown += (_, args) => Key(id, args);
                _buttons.Add(id, button); _handles.Children.Add(button);
            }
            button.Content = new Border { Width = 12, Height = 15, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue)), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray) };
            button.TabIndex = index;
            button.BorderThickness = new Thickness(stop.Id == selected ? 2 : 1);
            button.BorderBrush = new SolidColorBrush(stop.Id == selected ? Windows.UI.Color.FromArgb(255, 83, 137, 224) : Windows.UI.Color.FromArgb(255, 128, 136, 150));
            AutomationProperties.SetName(button, $"Gradient stop {index + 1}, {stop.Offset * 100:0.##} percent");
            AutomationProperties.SetAutomationId(button, "StudioGradientStop" + index);
            ToolTipService.SetToolTip(button, $"Stop {index + 1} · {stop.Offset * 100:0.##}% · arrows to move · Delete to remove");
        }
        _ramp.Background = brush; PositionButtons();
    }
    private void PositionButtons()
    {
        if (_model is null) return;
        foreach (var stop in _model.Stops)
        {
            var button = _buttons[stop.Id];
            Microsoft.UI.Xaml.Controls.Canvas.SetLeft(button, stop.Offset * Math.Max(0, _handles.ActualWidth) - HandleSize / 2);
            Microsoft.UI.Xaml.Controls.Canvas.SetTop(button, 1);
            Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(button, stop.Id == _selected ? 2 : 1);
        }
    }
    private double PositionToOffset(double x) => Math.Clamp(x / Math.Max(1, _ramp.ActualWidth), 0, 1);
    private void Select(Guid id)
    {
        if (_model is null || !_model.Stops.Any(stop => stop.Id == id)) return;
        _selected = id; StopSelected?.Invoke(this, id); PositionButtons();
    }
    private void PressPointer(Guid id, PointerRoutedEventArgs args)
    {
        if (_pointer is not null || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !IsEnabled) return;
        Select(id);
        _buttons[id].Focus(FocusState.Pointer);
        if (!CapturePointer(args.Pointer)) return;
        _pointer = args.Pointer.PointerId; _dragged = id; EditStarted?.Invoke(this, EventArgs.Empty); args.Handled = true;
    }
    private void MovePointer(object sender, PointerRoutedEventArgs args)
    {
        if (_pointer != args.Pointer.PointerId) return;
        OffsetRequested?.Invoke(this, new StudioGradientOffsetEventArgs(_dragged, PositionToOffset(args.GetCurrentPoint(_ramp).Position.X)));
        args.Handled = true;
    }
    private void ReleasePointer(object sender, PointerRoutedEventArgs args)
    {
        if (_pointer != args.Pointer.PointerId) return;
        Finish(false); args.Handled = true;
    }
    private void Finish(bool cancelled)
    {
        if (_pointer is null) return;
        _pointer = null; _finishing = true;
        try { ReleasePointerCaptures(); EditCompleted?.Invoke(this, cancelled); }
        finally { _finishing = false; }
    }
    private void Key(Guid id, KeyRoutedEventArgs args)
    {
        if (_model is null) return;
        if (args.Key == VirtualKey.Escape && _pointer is not null) { Finish(true); args.Handled = true; return; }
        if (args.Key is VirtualKey.Delete or VirtualKey.Back)
        { RemoveStopRequested?.Invoke(this, id); args.Handled = true; return; }
        if (args.Key is not (VirtualKey.Left or VirtualKey.Right or VirtualKey.Home or VirtualKey.End)) return;
        Select(id);
        var step = (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) != 0 ? 0.1 : 0.01;
        var next = args.Key == VirtualKey.Home ? 0 : args.Key == VirtualKey.End ? 1
            : Math.Clamp(_model.Find(id).Offset + (args.Key == VirtualKey.Left ? -step : step), 0, 1);
        EditStarted?.Invoke(this, EventArgs.Empty);
        OffsetRequested?.Invoke(this, new StudioGradientOffsetEventArgs(id, next));
        EditCompleted?.Invoke(this, false); args.Handled = true;
    }
}
