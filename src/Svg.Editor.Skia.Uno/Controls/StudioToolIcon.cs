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
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(nameof(Symbol), typeof(string), typeof(StudioToolIcon),
        new PropertyMetadata(string.Empty, (sender, _) => ((StudioToolIcon)sender).UpdateGeometry()));
    private readonly Microsoft.UI.Xaml.Shapes.Path _path = new() { StrokeThickness = 1.3, Width = 20, Height = 20 };
    public SvgEditorToolKind Kind { get => (SvgEditorToolKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    /// <summary>Vector glyph for a non-drawing interaction; an empty name uses Kind.</summary>
    public string Symbol { get => (string)GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }
    public StudioToolIcon()
    {
        Width = Height = 20;
        IsHitTestVisible = false;
        IsTabStop = false;
        _path.SetBinding(Shape.StrokeProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
        Content = _path;
        UpdateGeometry();
    }
    private void UpdateGeometry()
    {
        var group = new GeometryGroup();
        void Polygon(params Point[] points)
        {
            var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = Symbol.Length == 0 && Kind == SvgEditorToolKind.Select };
            foreach (var point in points.Skip(1))
                figure.Segments.Add(new LineSegment { Point = point });
            var path = new PathGeometry();
            path.Figures.Add(figure);
            group.Children.Add(path);
        }
        void Line(double x1, double y1, double x2, double y2)
        {
            var figure = new PathFigure { StartPoint = new Point(x1, y1), IsFilled = false };
            figure.Segments.Add(new LineSegment { Point = new Point(x2, y2) });
            var path = new PathGeometry();
            path.Figures.Add(figure);
            group.Children.Add(path);
        }
        void Circle(double x, double y, double radius)
            => group.Children.Add(new EllipseGeometry { Center = new Point(x, y), RadiusX = radius, RadiusY = radius });
        void Curve(Point start, Point control1, Point control2, Point end)
        {
            var figure = new PathFigure { StartPoint = start, IsFilled = false };
            figure.Segments.Add(new BezierSegment { Point1 = control1, Point2 = control2, Point3 = end });
            var path = new PathGeometry();
            path.Figures.Add(figure);
            group.Children.Add(path);
        }
        void Box(double x, double y, double width, double height)
            => group.Children.Add(new RectangleGeometry { Rect = new Rect(x, y, width, height) });
        if (Symbol.Length > 0)
            switch (Symbol)
            {
                case "Hand":
                    Polygon(new(5, 11), new(5, 6), new(7, 6), new(7, 2), new(9, 2), new(9, 8), new(10, 8), new(10, 3), new(12, 3), new(12, 8), new(13, 8), new(13, 5), new(15, 5), new(15, 12), new(13, 17), new(7, 17), new(3, 12), new(3, 9));
                    break;
                case "Zoom":
                    Circle(8, 8, 5);
                    Line(12, 12, 18, 18);
                    Line(5, 8, 11, 8);
                    Line(8, 5, 8, 11);
                    break;
                case "Rotate":
                    Curve(new(15, 5), new(4, -3), new(-1, 15), new(9, 17));
                    Curve(new(9, 17), new(13, 18), new(17, 14), new(17, 10));
                    Line(15, 1, 15, 6);
                    Line(15, 6, 10, 6);
                    break;
                case "Reflect":
                    Line(10, 1, 10, 19);
                    Polygon(new(2, 4), new(7, 15), new(2, 15));
                    Polygon(new(18, 4), new(13, 15), new(18, 15));
                    break;
                case "Scale":
                    Box(2, 9, 8, 8);
                    Box(2, 2, 15, 15);
                    Line(7, 12, 15, 4);
                    Line(11, 4, 15, 4);
                    Line(15, 4, 15, 8);
                    break;
                case "FreeTransform":
                    Polygon(new(5, 3), new(17, 5), new(15, 17), new(3, 14));
                    Box(3, 1, 3, 3);
                    Box(15, 3, 3, 3);
                    Box(13, 15, 3, 3);
                    Box(1, 12, 3, 3);
                    break;
                case "Gradient":
                    Box(2, 4, 16, 12);
                    Line(5, 5, 5, 15);
                    Line(7, 5, 7, 15);
                    Line(9, 5, 9, 15);
                    Line(12, 5, 12, 15);
                    break;
                case "Eyedropper":
                    Polygon(new(3, 17), new(5, 12), new(13, 4), new(16, 7), new(8, 15));
                    Line(10, 3, 17, 10);
                    Polygon(new(14, 2), new(18, 6), new(16, 8), new(12, 4));
                    break;
                case "Artboard":
                    Line(5, 1, 5, 18);
                    Line(1, 5, 18, 5);
                    Line(15, 2, 15, 19);
                    Line(2, 15, 19, 15);
                    break;
                case "Paintbrush":
                    Polygon(new(6, 11), new(15, 2), new(18, 5), new(10, 13));
                    Curve(new(6, 11), new(1, 10), new(5, 16), new(2, 18));
                    Curve(new(2, 18), new(9, 20), new(12, 15), new(10, 13));
                    break;
                case "Scissors":
                    Circle(4, 15, 3);
                    Circle(15, 15, 3);
                    Line(6, 13, 16, 2);
                    Line(13, 13, 3, 2);
                    Circle(9.5, 9.5, 1);
                    break;
                case "Eraser":
                    Polygon(new(2, 12), new(11, 3), new(18, 10), new(10, 18), new(7, 18));
                    Line(6, 8, 13, 15);
                    Line(7, 18, 18, 18);
                    break;
                case "Knife":
                    Polygon(new(2, 18), new(7, 9), new(11, 13));
                    Polygon(new(7, 9), new(16, 2), new(18, 3), new(11, 13));
                    Line(6, 8, 12, 14);
                    break;
                case "Lasso":
                    Curve(new(5, 15), new(-3, 8), new(5, 1), new(12, 3));
                    Curve(new(12, 3), new(23, 5), new(18, 18), new(6, 16));
                    Curve(new(6, 16), new(11, 10), new(12, 20), new(4, 19));
                    break;
                case "MagicWand":
                    Line(2, 18, 13, 7);
                    Line(3, 19, 14, 8);
                    Line(6, 2, 6, 7);
                    Line(3.5, 4.5, 8.5, 4.5);
                    Line(15, 1, 15, 5);
                    Line(13, 3, 17, 3);
                    Line(16, 11, 16, 17);
                    Line(13, 14, 19, 14);
                    break;
                case "AddAnchor":
                case "DeleteAnchor":
                    Polygon(new(2, 18), new(4, 9), new(12, 3), new(16, 7), new(10, 16));
                    Line(2, 18, 9, 11);
                    Circle(10, 10, 1.5);
                    Line(13, 3, 19, 3);
                    if (Symbol == "AddAnchor")
                        Line(16, 0, 16, 6);
                    break;
                case "AnchorPoint":
                    Line(2, 15, 10, 4);
                    Line(10, 4, 18, 15);
                    Box(8, 2, 4, 4);
                    break;
                case "RoundedRectangle":
                    Line(5, 4, 15, 4);
                    Curve(new(15, 4), new(17, 4), new(18, 5), new(18, 7));
                    Line(18, 7, 18, 13);
                    Curve(new(18, 13), new(18, 15), new(17, 16), new(15, 16));
                    Line(15, 16, 5, 16);
                    Curve(new(5, 16), new(3, 16), new(2, 15), new(2, 13));
                    Line(2, 13, 2, 7);
                    Curve(new(2, 7), new(2, 5), new(3, 4), new(5, 4));
                    break;
                case "RegularPolygon":
                    Polygon(new(6, 3), new(14, 3), new(18, 10), new(14, 17), new(6, 17), new(2, 10));
                    break;
                case "Star":
                    Polygon(Enumerable.Range(0, 10).Select(index =>
                    { var angle = index * Math.PI / 5 - Math.PI / 2; var radius = index % 2 == 0 ? 8 : 3.5; return new Point(10 + radius * Math.Cos(angle), 10 + radius * Math.Sin(angle)); }).ToArray());
                    break;
                case "RectangularGrid":
                case "Mesh":
                    Box(2, 3, 16, 14);
                    for (var index = 1; index < 3; index++)
                    { Line(2, 3 + index * 14d / 3, 18, 3 + index * 14d / 3); Line(2 + index * 16d / 3, 3, 2 + index * 16d / 3, 17); }
                    break;
                case "PolarGrid":
                    Circle(10, 10, 8);
                    Circle(10, 10, 4);
                    Line(2, 10, 18, 10);
                    Line(10, 2, 10, 18);
                    Line(4, 4, 16, 16);
                    Line(4, 16, 16, 4);
                    break;
                case "Spiral":
                    Curve(new(10, 10), new(3, 3), new(3, 16), new(11, 15));
                    Curve(new(11, 15), new(23, 14), new(16, -2), new(7, 3));
                    Curve(new(7, 3), new(-1, 7), new(1, 18), new(11, 18));
                    break;
                case "Width":
                    Curve(new(3, 3), new(11, 7), new(11, 13), new(3, 17));
                    Curve(new(17, 3), new(9, 7), new(9, 13), new(17, 17));
                    Line(2, 10, 18, 10);
                    Circle(10, 10, 2);
                    break;
                case "Blend":
                    Box(1, 3, 6, 6);
                    Box(13, 12, 6, 6);
                    Line(7, 6, 15, 12);
                    Line(4, 9, 13, 15);
                    break;
                case "ShapeBuilder":
                    Circle(7, 10, 5);
                    Box(8, 4, 9, 12);
                    Line(7, 10, 17, 2);
                    break;
                case "Symbol":
                    Circle(10, 6, 4);
                    Circle(6, 12, 4);
                    Circle(14, 12, 4);
                    Line(10, 11, 10, 19);
                    Line(7, 19, 13, 19);
                    break;
                case "More":
                    Circle(4, 10, 1.5);
                    Circle(10, 10, 1.5);
                    Circle(16, 10, 1.5);
                    break;
                default:
                    Box(3, 3, 14, 14);
                    break;
            }
        else
            switch (Kind)
            {
                case SvgEditorToolKind.Select:
                case SvgEditorToolKind.PathSelect:
                case SvgEditorToolKind.MultiSelect:
                    Polygon(new(3, 2), new(16, 10), new(10, 11), new(13, 17), new(10, 18), new(7, 12), new(3, 16));
                    break;
                case SvgEditorToolKind.Rect:
                    group.Children.Add(new RectangleGeometry { Rect = new Rect(3, 4, 14, 12) });
                    break;
                case SvgEditorToolKind.Ellipse:
                    group.Children.Add(new EllipseGeometry { Center = new Point(10, 10), RadiusX = 7, RadiusY = 5 });
                    break;
                case SvgEditorToolKind.Circle:
                    Circle(10, 10, 7);
                    break;
                case SvgEditorToolKind.Line:
                case SvgEditorToolKind.PathLine:
                    Line(3, 17, 17, 3);
                    break;
                case SvgEditorToolKind.Polygon:
                case SvgEditorToolKind.PolygonSelect:
                    Polygon(new(10, 2), new(18, 8), new(15, 17), new(5, 17), new(2, 8));
                    break;
                case SvgEditorToolKind.Polyline:
                case SvgEditorToolKind.PolylineSelect:
                    Line(2, 15, 7, 5);
                    Line(7, 5, 13, 14);
                    Line(13, 14, 18, 4);
                    break;
                case SvgEditorToolKind.PathCubic:
                case SvgEditorToolKind.PathQuadratic:
                case SvgEditorToolKind.PathArc:
                case SvgEditorToolKind.PathMove:
                    Polygon(new(3, 17), new(5, 8), new(14, 2), new(18, 6), new(12, 15));
                    Line(3, 17, 10, 10);
                    Circle(11, 9, 1.5);
                    break;
                case SvgEditorToolKind.Freehand:
                    Polygon(new(3, 17), new(4, 12), new(13, 3), new(17, 7), new(8, 16));
                    Line(11, 5, 15, 9);
                    Line(4, 12, 8, 16);
                    break;
                case SvgEditorToolKind.Text:
                case SvgEditorToolKind.TextArea:
                case SvgEditorToolKind.TextPath:
                    Line(3, 4, 17, 4);
                    Line(10, 4, 10, 17);
                    Line(6, 17, 14, 17);
                    Line(3, 4, 3, 7);
                    Line(17, 4, 17, 7);
                    if (Kind == SvgEditorToolKind.TextArea)
                        Box(1, 1, 18, 18);
                    if (Kind == SvgEditorToolKind.TextPath)
                        Curve(new(1, 18), new(7, 12), new(12, 21), new(19, 15));
                    break;
                case SvgEditorToolKind.Image:
                    group.Children.Add(new RectangleGeometry { Rect = new Rect(2, 3, 16, 14) });
                    Circle(13.5, 7, 1.5);
                    Line(3, 15, 7, 10);
                    Line(7, 10, 12, 16);
                    Line(10, 13, 13, 11);
                    Line(13, 11, 17, 16);
                    break;
                default:
                    group.Children.Add(new RectangleGeometry { Rect = new Rect(3, 3, 14, 14) });
                    Line(3, 3, 17, 17);
                    break;
            }
        _path.Data = group;
        if (Symbol.Length == 0 && Kind == SvgEditorToolKind.Select)
            _path.SetBinding(Shape.FillProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
        else
        { _path.ClearValue(Shape.FillProperty); _path.Fill = null; }
    }
}

public sealed class StudioTabButton : Microsoft.UI.Xaml.Controls.Primitives.ToggleButton
{
    public StudioTabButton() => DefaultStyleKey = typeof(StudioTabButton);
}
