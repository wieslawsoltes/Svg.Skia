using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Svg.Editor.Core;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

/// <summary>Resolution-independent tool artwork with inherited theme foreground.</summary>
public sealed class StudioToolIcon : UserControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(SvgEditorToolKind), typeof(StudioToolIcon),
        new PropertyMetadata(SvgEditorToolKind.Select, (sender, _) => ((StudioToolIcon)sender).UpdateGeometry()));
    private readonly Microsoft.UI.Xaml.Shapes.Path _path = new() { StrokeThickness = 1.3, Width = 20, Height = 20 };
    public SvgEditorToolKind Kind { get => (SvgEditorToolKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public StudioToolIcon()
    {
        Width = Height = 20; IsHitTestVisible = false; IsTabStop = false;
        _path.SetBinding(Shape.StrokeProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
        Content = _path; UpdateGeometry();
    }
    private void UpdateGeometry()
    {
        var group = new GeometryGroup();
        void Polygon(params Point[] points)
        {
            var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = Kind == SvgEditorToolKind.Select };
            foreach (var point in points.Skip(1)) figure.Segments.Add(new LineSegment { Point = point });
            var path = new PathGeometry(); path.Figures.Add(figure); group.Children.Add(path);
        }
        void Line(double x1, double y1, double x2, double y2)
        {
            var figure = new PathFigure { StartPoint = new Point(x1, y1), IsFilled = false };
            figure.Segments.Add(new LineSegment { Point = new Point(x2, y2) });
            var path = new PathGeometry(); path.Figures.Add(figure); group.Children.Add(path);
        }
        void Circle(double x, double y, double radius)
            => group.Children.Add(new EllipseGeometry { Center = new Point(x, y), RadiusX = radius, RadiusY = radius });
        switch (Kind)
        {
            case SvgEditorToolKind.Select:
            case SvgEditorToolKind.PathSelect:
            case SvgEditorToolKind.MultiSelect:
                Polygon(new(3, 2), new(16, 10), new(10, 11), new(13, 17), new(10, 18), new(7, 12), new(3, 16));
                break;
            case SvgEditorToolKind.Rect:
                group.Children.Add(new RectangleGeometry { Rect = new Rect(3, 4, 14, 12) }); break;
            case SvgEditorToolKind.Ellipse:
                group.Children.Add(new EllipseGeometry { Center = new Point(10, 10), RadiusX = 7, RadiusY = 5 }); break;
            case SvgEditorToolKind.Circle:
                Circle(10, 10, 7); break;
            case SvgEditorToolKind.Line:
            case SvgEditorToolKind.PathLine:
                Line(3, 17, 17, 3); break;
            case SvgEditorToolKind.Polygon:
            case SvgEditorToolKind.PolygonSelect:
                Polygon(new(10, 2), new(18, 8), new(15, 17), new(5, 17), new(2, 8)); break;
            case SvgEditorToolKind.Polyline:
            case SvgEditorToolKind.PolylineSelect:
                Line(2, 15, 7, 5); Line(7, 5, 13, 14); Line(13, 14, 18, 4); break;
            case SvgEditorToolKind.PathCubic:
            case SvgEditorToolKind.PathQuadratic:
            case SvgEditorToolKind.PathArc:
            case SvgEditorToolKind.PathMove:
                Polygon(new(3, 17), new(5, 8), new(14, 2), new(18, 6), new(12, 15));
                Line(3, 17, 10, 10); Circle(11, 9, 1.5); break;
            case SvgEditorToolKind.Freehand:
                Polygon(new(3, 17), new(4, 12), new(13, 3), new(17, 7), new(8, 16));
                Line(11, 5, 15, 9); Line(4, 12, 8, 16); break;
            case SvgEditorToolKind.Text:
            case SvgEditorToolKind.TextArea:
            case SvgEditorToolKind.TextPath:
                Line(3, 4, 17, 4); Line(10, 4, 10, 17); Line(6, 17, 14, 17); Line(3, 4, 3, 7); Line(17, 4, 17, 7); break;
            case SvgEditorToolKind.Image:
                group.Children.Add(new RectangleGeometry { Rect = new Rect(2, 3, 16, 14) });
                Circle(13.5, 7, 1.5); Line(3, 15, 7, 10); Line(7, 10, 12, 16); Line(10, 13, 13, 11); Line(13, 11, 17, 16); break;
            default:
                group.Children.Add(new RectangleGeometry { Rect = new Rect(3, 3, 14, 14) }); Line(3, 3, 17, 17); break;
        }
        _path.Data = group;
        if (Kind == SvgEditorToolKind.Select)
            _path.SetBinding(Shape.FillProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
        else { _path.ClearValue(Shape.FillProperty); _path.Fill = null; }
    }
}

public sealed class StudioTabButton : Microsoft.UI.Xaml.Controls.Primitives.ToggleButton
{
    public StudioTabButton() => DefaultStyleKey = typeof(StudioTabButton);
}
