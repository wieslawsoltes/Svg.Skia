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

    public GradientMesh Result
    {
        get
        {
            var mesh = new GradientMesh();
            mesh.Points.AddRange(Points);
            return mesh;
        }
    }
}
