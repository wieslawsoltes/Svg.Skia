using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Small editor indicators drawn without relying on a platform font's symbol coverage.
/// Existing string marks remain supported; unrecognized custom marks retain text rendering.
/// </summary>
public sealed class StudioGlyph : UserControl
{
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(nameof(Symbol), typeof(string), typeof(StudioGlyph),
        new PropertyMetadata(string.Empty, (sender, _) => ((StudioGlyph)sender).UpdateGeometry()));

    private readonly Microsoft.UI.Xaml.Shapes.Path _path = new() { Width = 12, Height = 12, StrokeThickness = 1 };
    private readonly TextBlock _fallback = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    public string Symbol { get => (string)GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }

    public StudioGlyph()
    {
        Width = Height = 12;
        IsHitTestVisible = false;
        IsTabStop = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        _path.SetBinding(Shape.StrokeProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
        _path.SetBinding(Shape.FillProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
        _fallback.SetBinding(TextBlock.ForegroundProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
        var artwork = new Grid { Width = 12, Height = 12 };
        artwork.Children.Add(_path);
        artwork.Children.Add(_fallback);
        Content = new Viewbox { Child = artwork, Stretch = Stretch.Uniform };
        UpdateGeometry();
    }

    private void UpdateGeometry()
    {
        var geometry = new PathGeometry();
        void Line(params Point[] points)
        {
            var figure = new PathFigure { StartPoint = points[0], IsFilled = false };
            foreach (var point in points.Skip(1))
                figure.Segments.Add(new LineSegment { Point = point });
            geometry.Figures.Add(figure);
        }
        void Polygon(params Point[] points)
        {
            var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
            foreach (var point in points.Skip(1))
                figure.Segments.Add(new LineSegment { Point = point });
            geometry.Figures.Add(figure);
        }
        void Circle(double x, double y, double radius, bool filled = false)
        {
            const double tangent = 0.5522847498307936;
            var offset = radius * tangent;
            var figure = new PathFigure { StartPoint = new Point(x + radius, y), IsClosed = true, IsFilled = filled };
            figure.Segments.Add(new BezierSegment { Point1 = new(x + radius, y + offset), Point2 = new(x + offset, y + radius), Point3 = new(x, y + radius) });
            figure.Segments.Add(new BezierSegment { Point1 = new(x - offset, y + radius), Point2 = new(x - radius, y + offset), Point3 = new(x - radius, y) });
            figure.Segments.Add(new BezierSegment { Point1 = new(x - radius, y - offset), Point2 = new(x - offset, y - radius), Point3 = new(x, y - radius) });
            figure.Segments.Add(new BezierSegment { Point1 = new(x + offset, y - radius), Point2 = new(x + radius, y - offset), Point3 = new(x + radius, y) });
            geometry.Figures.Add(figure);
        }

        var symbol = Symbol ?? string.Empty;
        var known = true;
        switch (symbol)
        {
            case "":
                break;
            case "ChevronDown":
            case "⌄":
                Line(new(2, 4), new(6, 8), new(10, 4));
                break;
            case "ChevronUp":
            case "⌃":
                Line(new(2, 8), new(6, 4), new(10, 8));
                break;
            case "Up":
            case "↑":
                Line(new(6, 10), new(6, 2));
                Line(new(2, 6), new(6, 2), new(10, 6));
                break;
            case "Down":
            case "↓":
                Line(new(6, 2), new(6, 10));
                Line(new(2, 6), new(6, 10), new(10, 6));
                break;
            case "Spin":
            case "↕":
                Line(new(6, 1), new(6, 11));
                Line(new(3, 4), new(6, 1), new(9, 4));
                Line(new(3, 8), new(6, 11), new(9, 8));
                break;
            case "Swap":
            case "⇄":
            case "⇆":
                Line(new(1, 3), new(10, 3));
                Line(new(8, 1), new(10, 3), new(8, 5));
                Line(new(11, 9), new(2, 9));
                Line(new(4, 7), new(2, 9), new(4, 11));
                break;
            case "None":
            case "∅":
                Circle(6, 6, 4);
                Line(new(2, 10), new(10, 2));
                break;
            case "Linked":
            case "↗":
                Line(new(2, 10), new(10, 2));
                Line(new(4, 2), new(10, 2), new(10, 8));
                break;
            case "Plus":
            case "＋":
            case "+":
                Line(new(2, 6), new(10, 6));
                Line(new(6, 2), new(6, 10));
                break;
            case "Minus":
            case "−":
            case "—":
                Line(new(2, 6), new(10, 6));
                break;
            case "More":
            case "⋯":
                Circle(2, 6, 0.6, true);
                Circle(6, 6, 0.6, true);
                Circle(10, 6, 0.6, true);
                break;
            case "Panels":
            case "☷":
                for (var row = 3; row <= 9; row += 3)
                {
                    Line(new(1, row), new(3, row));
                    Line(new(5, row), new(11, row));
                }
                break;
            case "Alternate":
            case "◢":
                Polygon(new(2, 10), new(10, 2), new(10, 10));
                break;
            case "Play":
                Polygon(new(3, 2), new(10, 6), new(3, 10));
                break;
            case "Pause":
                Polygon(new(3, 2), new(4, 2), new(4, 10), new(3, 10));
                Polygon(new(8, 2), new(9, 2), new(9, 10), new(8, 10));
                break;
            case "Rewind":
                Line(new(2, 2), new(2, 10));
                Polygon(new(9, 2), new(3, 6), new(9, 10));
                break;
            default:
                known = false;
                break;
        }

        _path.Data = geometry;
        _path.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
        _fallback.Text = known ? string.Empty : symbol;
        _fallback.Visibility = known ? Visibility.Collapsed : Visibility.Visible;
    }
}
