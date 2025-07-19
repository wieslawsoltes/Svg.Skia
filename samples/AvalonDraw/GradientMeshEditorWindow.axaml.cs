using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ShimSkiaSharp;
using Svg.Model;

namespace AvalonDraw;

public partial class GradientMeshEditorWindow : Window
{
    private readonly Canvas _canvas;
    private GradientMeshPoint? _dragging;

    public ObservableCollection<GradientMeshPoint> Points { get; } = new();

    public GradientMeshEditorWindow(GradientMesh mesh)
    {
        InitializeComponent();
        Resources["SkColorConverter"] = new SkColorConverter();
        _canvas = this.FindControl<Canvas>("MeshCanvas");
        foreach (var p in mesh.Points)
            Points.Add(p);
        _canvas.PointerPressed += OnPointerPressed;
        _canvas.PointerReleased += OnPointerReleased;
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.DataContext = this;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(_canvas);
        foreach (var p in Points)
        {
            var rect = new Rect(p.Position.X - 5, p.Position.Y - 5, 10, 10);
            if (rect.Contains(pos))
            {
                _dragging = p;
                break;
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragging = null;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging is null)
            return;

        var pos = e.GetPosition(_canvas);
        var index = Points.IndexOf(_dragging);
        if (index >= 0)
        {
            Points[index] = _dragging with { Position = new ShimSkiaSharp.SKPoint((float)pos.X, (float)pos.Y) };
            _dragging = Points[index];
        }
    }

    private void ColorPicker_OnColorChanged(object? sender, ColorChangedEventArgs e)
    {
        if (sender is ColorPicker picker && picker.DataContext is GradientMeshPoint point)
        {
            var index = Points.IndexOf(point);
            if (index >= 0)
            {
                var c = e.NewColor;
                var sk = new SKColor(c.R, c.G, c.B, c.A);
                Points[index] = point with { Color = sk };
            }
        }
    }

    public GradientMesh Result
    {
        get
        {
            var mesh = new GradientMesh();
            mesh.Points.AddRange(Points);
            return mesh;
        }
    }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}

public class SkColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SKColor color)
            return new Color(color.Alpha, color.Red, color.Green, color.Blue);
        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color c)
            return new SKColor(c.R, c.G, c.B, c.A);
        return SKColor.Empty;
    }
}
