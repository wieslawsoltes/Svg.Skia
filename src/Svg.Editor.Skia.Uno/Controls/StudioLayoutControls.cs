using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace Svg.Editor.Skia.Uno;

public sealed class StudioWrapPanel : Panel
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(StudioWrapPanel), new PropertyMetadata(4d, (sender, _) => ((StudioWrapPanel)sender).InvalidateMeasure()));
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width) : double.MaxValue;
        var spacing = double.IsFinite(Spacing) ? Math.Max(0, Spacing) : 0;
        var x = 0d; var y = 0d; var row = 0d; var measuredWidth = 0d;
        foreach (var child in Children.Where(child => child.Visibility == Visibility.Visible))
        {
            child.Measure(new Size(width, double.PositiveInfinity));
            var size = child.DesiredSize;
            var childWidth = Math.Min(size.Width, width);
            if (x > 0 && x + childWidth > width)
            { measuredWidth = Math.Max(measuredWidth, x - spacing); x = 0; y += row + spacing; row = 0; }
            x += childWidth + spacing; row = Math.Max(row, size.Height);
        }
        return new Size(Math.Max(measuredWidth, Math.Max(0, x - spacing)), y + row);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var spacing = double.IsFinite(Spacing) ? Math.Max(0, Spacing) : 0;
        var x = 0d; var y = 0d; var row = 0d;
        foreach (var child in Children.Where(child => child.Visibility == Visibility.Visible))
        {
            var size = child.DesiredSize;
            var width = Math.Min(size.Width, Math.Max(0, finalSize.Width));
            if (x > 0 && x + width > finalSize.Width) { x = 0; y += row + spacing; row = 0; }
            child.Arrange(new Rect(x, y, width, size.Height));
            x += width + spacing; row = Math.Max(row, size.Height);
        }
        return finalSize;
    }
}

public sealed class StudioResizeEventArgs : EventArgs
{
    public StudioResizeEventArgs(double horizontal, double vertical)
    { HorizontalChange = horizontal; VerticalChange = vertical; }
    public double HorizontalChange { get; }
    public double VerticalChange { get; }
}

/// <summary>Retemplatable, pointer-capturing and keyboard-accessible resize grip.</summary>
public sealed class StudioSplitter : Control
{
    private uint? _pointer;
    private Point _last;
    public event EventHandler<double>? KeyboardResize;
    public event EventHandler<StudioResizeEventArgs>? DragDelta;
    public event EventHandler? DragCompleted;

    public StudioSplitter()
    {
        DefaultStyleKey = typeof(StudioSplitter); IsTabStop = true; UseSystemFocusVisuals = true;
        AutomationProperties.SetName(this, "Resize panel. Use arrow keys to adjust.");
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs args)
    {
        base.OnPointerPressed(args);
        var point = args.GetCurrentPoint(XamlRoot?.Content ?? this);
        if (_pointer is not null || !point.Properties.IsLeftButtonPressed || !CapturePointer(args.Pointer)) return;
        _pointer = args.Pointer.PointerId; _last = point.Position;
        Focus(FocusState.Pointer); VisualStateManager.GoToState(this, "Pressed", false); args.Handled = true;
    }

    protected override void OnPointerMoved(PointerRoutedEventArgs args)
    {
        base.OnPointerMoved(args);
        if (_pointer != args.Pointer.PointerId) return;
        var point = args.GetCurrentPoint(XamlRoot?.Content ?? this).Position;
        var delta = new StudioResizeEventArgs(point.X - _last.X, point.Y - _last.Y);
        _last = point; DragDelta?.Invoke(this, delta); args.Handled = true;
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs args)
    {
        base.OnPointerReleased(args);
        if (_pointer != args.Pointer.PointerId) return;
        CompleteDrag(); ReleasePointerCapture(args.Pointer); args.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerRoutedEventArgs args) { base.OnPointerCaptureLost(args); CompleteDrag(); }
    protected override void OnPointerCanceled(PointerRoutedEventArgs args) { base.OnPointerCanceled(args); CompleteDrag(); }
    protected override void OnPointerEntered(PointerRoutedEventArgs args)
    { base.OnPointerEntered(args); if (_pointer is null) VisualStateManager.GoToState(this, "PointerOver", false); }
    protected override void OnPointerExited(PointerRoutedEventArgs args)
    { base.OnPointerExited(args); if (_pointer is null) VisualStateManager.GoToState(this, "Normal", false); }

    private void CompleteDrag()
    {
        if (_pointer is null) return;
        _pointer = null;
        VisualStateManager.GoToState(this, "Normal", false);
        DragCompleted?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyRoutedEventArgs args)
    {
        if (args.Key is VirtualKey.Left or VirtualKey.Up or VirtualKey.Right or VirtualKey.Down)
        {
            KeyboardResize?.Invoke(this, args.Key is VirtualKey.Left or VirtualKey.Up ? -16d : 16d);
            args.Handled = true;
        }
        base.OnKeyDown(args);
    }
}
