using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace Svg.Controls.ColorPicker.Uno.Controls;

public sealed class PickerIcon : Viewbox
{
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind),
            typeof(PickerIconKind),
            typeof(PickerIcon),
            new PropertyMetadata(PickerIconKind.Add, OnIconPropertyChanged));

    public static readonly DependencyProperty IconStrokeProperty =
        DependencyProperty.Register(
            nameof(IconStroke),
            typeof(Brush),
            typeof(PickerIcon),
            new PropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 17, 24, 39)), OnIconPropertyChanged));

    public static readonly DependencyProperty IconFillProperty =
        DependencyProperty.Register(
            nameof(IconFill),
            typeof(Brush),
            typeof(PickerIcon),
            new PropertyMetadata(null, OnIconPropertyChanged));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(PickerIcon),
            new PropertyMetadata(1.4d, OnIconPropertyChanged));

    public PickerIcon()
    {
        Stretch = Stretch.Uniform;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Width = 16;
        Height = 16;
        Rebuild();
    }

    public PickerIconKind Kind
    {
        get => (PickerIconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Brush IconStroke
    {
        get => (Brush)GetValue(IconStrokeProperty);
        set => SetValue(IconStrokeProperty, value);
    }

    public Brush? IconFill
    {
        get => (Brush?)GetValue(IconFillProperty);
        set => SetValue(IconFillProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    private static void OnIconPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PickerIcon)d).Rebuild();
    }

    private void Rebuild()
    {
        Child = BuildIcon();
    }

    private UIElement BuildIcon()
    {
        var canvas = new Canvas
        {
            Width = 16,
            Height = 16
        };

        switch (Kind)
        {
            case PickerIconKind.Add:
                canvas.Children.Add(CreateLine(8.0, 2.5, 8.0, 13.5));
                canvas.Children.Add(CreateLine(2.5, 8.0, 13.5, 8.0));
                break;
            case PickerIconKind.Close:
                canvas.Children.Add(CreateLine(3.0, 3.0, 13.0, 13.0));
                canvas.Children.Add(CreateLine(13.0, 3.0, 3.0, 13.0));
                break;
            case PickerIconKind.SolidPaint:
                canvas.Children.Add(CreateRectangle(3.0, 3.0, 10.0, 10.0, 2.4, FillBrushOrTransparent(), StrokeBrush()));
                break;
            case PickerIconKind.GradientLinear:
                canvas.Children.Add(CreateRectangle(2.8, 3.4, 10.4, 9.2, 2.2));
                canvas.Children.Add(CreateLine(4.2, 11.0, 11.8, 5.0, 1.1));
                break;
            case PickerIconKind.GradientRadial:
                canvas.Children.Add(CreateEllipse(2.6, 2.6, 10.8, 10.8));
                canvas.Children.Add(CreateEllipse(5.2, 5.2, 5.6, 5.6));
                break;
            case PickerIconKind.ImagePaint:
                canvas.Children.Add(CreateRectangle(2.6, 3.2, 10.8, 9.6, 1.8));
                canvas.Children.Add(CreateCircle(5.2, 6.0, 1.1));
                canvas.Children.Add(CreatePolyline(
                    new Point(4.0, 11.2),
                    new Point(7.0, 8.4),
                    new Point(9.2, 10.2),
                    new Point(11.8, 7.4)));
                break;
            case PickerIconKind.VideoPaint:
                canvas.Children.Add(CreateRectangle(2.4, 3.6, 9.2, 8.8, 1.8));
                canvas.Children.Add(CreatePolygon(
                    [new Point(7.0, 8.0), new Point(11.2, 5.4), new Point(11.2, 10.6)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                break;
            case PickerIconKind.Image:
                canvas.Children.Add(CreateRectangle(2.4, 3.2, 11.2, 9.6, 1.8));
                canvas.Children.Add(CreateCircle(5.0, 6.0, 1.1));
                canvas.Children.Add(CreatePolyline(
                    new Point(3.8, 11.2),
                    new Point(7.2, 7.8),
                    new Point(9.2, 9.6),
                    new Point(12.2, 6.2)));
                break;
            case PickerIconKind.Droplet:
                canvas.Children.Add(CreatePolygon(
                    [new Point(8.0, 2.2), new Point(11.3, 6.7), new Point(10.8, 10.8), new Point(8.0, 13.4), new Point(5.2, 10.8), new Point(4.7, 6.7)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                break;
            case PickerIconKind.DropletOff:
                canvas.Children.Add(CreatePolygon(
                    [new Point(8.0, 2.2), new Point(11.3, 6.7), new Point(10.8, 10.8), new Point(8.0, 13.4), new Point(5.2, 10.8), new Point(4.7, 6.7)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                canvas.Children.Add(CreateLine(3.0, 13.0, 13.0, 3.0, 1.3));
                break;
            case PickerIconKind.Group:
                canvas.Children.Add(CreateRectangle(2.0, 2.0, 4.2, 4.2, 0.8));
                canvas.Children.Add(CreateRectangle(9.8, 2.0, 4.2, 4.2, 0.8));
                canvas.Children.Add(CreateRectangle(2.0, 9.8, 4.2, 4.2, 0.8));
                canvas.Children.Add(CreateRectangle(9.8, 9.8, 4.2, 4.2, 0.8));
                break;
        }

        return canvas;
    }

    private Brush StrokeBrush() => IconStroke;

    private Brush FillBrushOrTransparent() => IconFill ?? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

    private Line CreateLine(double x1, double y1, double x2, double y2, double? strokeThickness = null)
    {
        return new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = StrokeBrush(),
            StrokeThickness = strokeThickness ?? StrokeThickness
        };
    }

    private Rectangle CreateRectangle(double x, double y, double width, double height, double radius, Brush? fill = null, Brush? stroke = null)
    {
        var rectangle = new Rectangle
        {
            Width = width,
            Height = height,
            RadiusX = radius,
            RadiusY = radius,
            Fill = fill,
            Stroke = stroke ?? StrokeBrush(),
            StrokeThickness = StrokeThickness
        };
        Canvas.SetLeft(rectangle, x);
        Canvas.SetTop(rectangle, y);
        return rectangle;
    }

    private Ellipse CreateEllipse(double x, double y, double width, double height)
    {
        var ellipse = new Ellipse
        {
            Width = width,
            Height = height,
            Stroke = StrokeBrush(),
            StrokeThickness = StrokeThickness,
            Fill = FillBrushOrTransparent()
        };
        Canvas.SetLeft(ellipse, x);
        Canvas.SetTop(ellipse, y);
        return ellipse;
    }

    private Ellipse CreateCircle(double centerX, double centerY, double radius)
    {
        var diameter = radius * 2.0;
        var ellipse = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Stroke = StrokeBrush(),
            StrokeThickness = StrokeThickness,
            Fill = FillBrushOrTransparent()
        };
        Canvas.SetLeft(ellipse, centerX - radius);
        Canvas.SetTop(ellipse, centerY - radius);
        return ellipse;
    }

    private Polygon CreatePolygon(Point[] points, Brush fill, Brush stroke, double strokeThickness)
    {
        return new Polygon
        {
            Points = new PointCollection(points),
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = strokeThickness
        };
    }

    private Polyline CreatePolyline(params Point[] points)
    {
        return new Polyline
        {
            Points = new PointCollection(points),
            Stroke = StrokeBrush(),
            StrokeThickness = StrokeThickness
        };
    }
}
