using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
        Resources["ColorStringConverter"] = new ColorStringConverter();
        _canvas = this.FindControl<Canvas>("MeshCanvas");
        foreach (var p in mesh.Points)
            Points.Add(new GradientMeshPoint(p.Position, p.Color));
        _canvas.PointerPressed += OnPointerPressed;
        _canvas.PointerReleased += OnPointerReleased;
        _canvas.PointerMoved += OnPointerMoved;
        DataContext = this;
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
        _dragging.Position = new ShimSkiaSharp.SKPoint((float)pos.X, (float)pos.Y);
    }

    public GradientMesh Result { get; private set; } = new();

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Result.Points.Clear();
        foreach (var p in Points)
            Result.Points.Add(new GradientMeshPoint(p.Position, p.Color));
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
