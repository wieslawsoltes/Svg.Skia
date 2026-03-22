using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed class FigmaIcon : Viewbox
{
    private static readonly FontFamily FallbackSymbolFontFamily = new("ms-appx:///Uno.Fonts.Fluent/Fonts/uno-fluentui-assets.ttf");

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind),
            typeof(FigmaIconKind),
            typeof(FigmaIcon),
            new PropertyMetadata(FigmaIconKind.Rectangle, OnIconPropertyChanged));

    public static readonly DependencyProperty IconStrokeProperty =
        DependencyProperty.Register(
            nameof(IconStroke),
            typeof(Brush),
            typeof(FigmaIcon),
            new PropertyMetadata(new SolidColorBrush(ColorHelper.FromArgb(255, 17, 24, 39)), OnIconPropertyChanged));

    public static readonly DependencyProperty IconFillProperty =
        DependencyProperty.Register(
            nameof(IconFill),
            typeof(Brush),
            typeof(FigmaIcon),
            new PropertyMetadata(null, OnIconPropertyChanged));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(FigmaIcon),
            new PropertyMetadata(1.4d, OnIconPropertyChanged));

    public FigmaIcon()
    {
        Stretch = Stretch.Uniform;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Width = 16;
        Height = 16;
        Rebuild();
    }

    public FigmaIconKind Kind
    {
        get => (FigmaIconKind)GetValue(KindProperty);
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
        ((FigmaIcon)d).Rebuild();
    }

    private enum IconBrushKind
    {
        Stroke,
        Fill
    }

    private readonly record struct ThemedIconDescriptor(
        Symbol? Symbol = null,
        string? Text = null,
        double Rotation = 0,
        bool MirrorX = false,
        bool MirrorY = false,
        IconBrushKind BrushKind = IconBrushKind.Stroke,
        double FontSize = 13.5d);

    private void Rebuild()
    {
        Child = BuildIcon();
    }

    private UIElement BuildIcon()
    {
        if (TryBuildThemedIcon(out var themedIcon))
        {
            return themedIcon;
        }

        var canvas = new Canvas
        {
            Width = 16,
            Height = 16
        };

        switch (Kind)
        {
            case FigmaIconKind.Search:
                canvas.Children.Add(CreateEllipse(2.2, 2.2, 8.0, 8.0));
                canvas.Children.Add(CreateLine(9.6, 9.6, 13.6, 13.6));
                break;
            case FigmaIconKind.Move:
                canvas.Children.Add(CreatePolyline(new Point(2.8, 2.2), new Point(11.8, 7.0), new Point(7.6, 8.2), new Point(10.6, 13.8), new Point(8.4, 14.8), new Point(5.4, 9.2), new Point(2.8, 11.8), new Point(2.8, 2.2)));
                break;
            case FigmaIconKind.Hand:
                canvas.Children.Add(CreatePolyline(new Point(5.2, 8.8), new Point(5.2, 4.4), new Point(6.7, 3.1), new Point(7.8, 4.4), new Point(7.8, 8.8)));
                canvas.Children.Add(CreateLine(7.8, 8.8, 7.8, 2.8));
                canvas.Children.Add(CreateLine(9.6, 8.8, 9.6, 3.8));
                canvas.Children.Add(CreateLine(11.4, 8.8, 11.4, 5.2));
                canvas.Children.Add(CreatePolyline(new Point(4.2, 8.8), new Point(4.2, 11.8), new Point(6.8, 14.2), new Point(10.9, 14.2), new Point(13.2, 11.6), new Point(13.2, 8.0)));
                break;
            case FigmaIconKind.Scale:
                canvas.Children.Add(CreateRectangle(2.4, 3.0, 8.6, 8.6, 1.4));
                canvas.Children.Add(CreateLine(7.0, 9.0, 13.2, 2.8, 1.2));
                canvas.Children.Add(CreateLine(10.0, 2.8, 13.2, 2.8, 1.2));
                canvas.Children.Add(CreateLine(13.2, 2.8, 13.2, 6.0, 1.2));
                canvas.Children.Add(CreateLine(6.0, 14.0, 2.8, 14.0, 1.2));
                canvas.Children.Add(CreateLine(2.8, 14.0, 2.8, 10.8, 1.2));
                break;
            case FigmaIconKind.Comment:
                canvas.Children.Add(CreateRoundedCallout());
                canvas.Children.Add(CreateLine(5.2, 6.2, 10.8, 6.2, 1.1));
                canvas.Children.Add(CreateLine(5.2, 8.8, 9.6, 8.8, 1.1));
                break;
            case FigmaIconKind.Send:
                canvas.Children.Add(CreatePolyline(new Point(2.0, 8.2), new Point(13.4, 2.4), new Point(10.2, 13.6), new Point(8.0, 8.0), new Point(2.0, 8.2)));
                break;
            case FigmaIconKind.Actions:
                canvas.Children.Add(CreateRectangle(2.3, 2.5, 4.2, 4.2, 0.8));
                canvas.Children.Add(CreateEllipse(9.7, 2.7, 3.6, 3.6, StrokeBrush()));
                canvas.Children.Add(CreatePolygon(
                    [new Point(4.1, 11.8), new Point(6.8, 9.1), new Point(9.5, 11.8), new Point(6.8, 14.5)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                canvas.Children.Add(CreateLine(11.8, 8.6, 11.8, 13.4, 1.2));
                canvas.Children.Add(CreateLine(9.4, 11.0, 14.2, 11.0, 1.2));
                break;
            case FigmaIconKind.Add:
                canvas.Children.Add(CreateLine(8, 2, 8, 14));
                canvas.Children.Add(CreateLine(2, 8, 14, 8));
                break;
            case FigmaIconKind.Minus:
                canvas.Children.Add(CreateLine(2.5, 8, 13.5, 8));
                break;
            case FigmaIconKind.Close:
                canvas.Children.Add(CreateLine(3, 3, 13, 13));
                canvas.Children.Add(CreateLine(13, 3, 3, 13));
                break;
            case FigmaIconKind.Back:
                canvas.Children.Add(CreateLine(11.8, 3.0, 4.6, 8.0));
                canvas.Children.Add(CreateLine(4.6, 8.0, 11.8, 13.0));
                break;
            case FigmaIconKind.Menu:
                canvas.Children.Add(CreateLine(3, 4, 13, 4));
                canvas.Children.Add(CreateLine(5, 8, 13, 8));
                canvas.Children.Add(CreateLine(7, 12, 13, 12));
                break;
            case FigmaIconKind.Eye:
                canvas.Children.Add(CreateEllipse(2.0, 4.2, 12.0, 7.6));
                canvas.Children.Add(CreateEllipse(6.1, 6.1, 3.8, 3.8, StrokeBrush()));
                break;
            case FigmaIconKind.EyeOff:
                canvas.Children.Add(CreateEllipse(2.0, 4.2, 12.0, 7.6));
                canvas.Children.Add(CreateEllipse(6.1, 6.1, 3.8, 3.8, StrokeBrush()));
                canvas.Children.Add(CreateLine(3, 13, 13, 3));
                break;
            case FigmaIconKind.Lock:
                canvas.Children.Add(CreateRectangle(4.0, 7.0, 8.0, 6.4, 1.4));
                canvas.Children.Add(CreateArc(4.8, 2.8, 6.4, 6.4, 180, 180));
                break;
            case FigmaIconKind.Unlock:
                canvas.Children.Add(CreateRectangle(4.0, 7.0, 8.0, 6.4, 1.4));
                canvas.Children.Add(CreateArc(5.2, 2.8, 6.2, 6.4, 210, 135));
                canvas.Children.Add(CreateLine(9.8, 4.5, 12.0, 4.5));
                break;
            case FigmaIconKind.Page:
                canvas.Children.Add(CreateRectangle(3.0, 2.0, 10.0, 12.0, 1.6));
                canvas.Children.Add(CreateLine(5.0, 6.0, 11.0, 6.0, 1.1));
                canvas.Children.Add(CreateLine(5.0, 9.0, 10.0, 9.0, 1.1));
                break;
            case FigmaIconKind.Frame:
                canvas.Children.Add(CreateRectangle(3.0, 3.0, 10.0, 10.0, 1.2));
                canvas.Children.Add(CreateLine(8.0, 0.8, 8.0, 4.0, 1.1));
                canvas.Children.Add(CreateLine(8.0, 12.0, 8.0, 15.2, 1.1));
                canvas.Children.Add(CreateLine(0.8, 8.0, 4.0, 8.0, 1.1));
                canvas.Children.Add(CreateLine(12.0, 8.0, 15.2, 8.0, 1.1));
                break;
            case FigmaIconKind.Section:
                canvas.Children.Add(CreateRectangle(2.4, 3.2, 11.2, 9.8, 1.8));
                canvas.Children.Add(CreateLine(2.8, 5.4, 13.2, 5.4, 1.1));
                canvas.Children.Add(CreateLine(5.0, 1.4, 5.0, 5.0, 1.1));
                break;
            case FigmaIconKind.Group:
                canvas.Children.Add(CreateRectangle(2.0, 2.0, 4.2, 4.2, 0.8));
                canvas.Children.Add(CreateRectangle(9.8, 2.0, 4.2, 4.2, 0.8));
                canvas.Children.Add(CreateRectangle(2.0, 9.8, 4.2, 4.2, 0.8));
                canvas.Children.Add(CreateRectangle(9.8, 9.8, 4.2, 4.2, 0.8));
                break;
            case FigmaIconKind.Rectangle:
                canvas.Children.Add(CreateRectangle(2.4, 4.2, 11.2, 7.6, 0.8));
                break;
            case FigmaIconKind.Ellipse:
                canvas.Children.Add(CreateEllipse(2.2, 2.8, 11.6, 10.4));
                break;
            case FigmaIconKind.Line:
                canvas.Children.Add(CreateLine(3.0, 12.5, 13.0, 3.5, 1.7));
                break;
            case FigmaIconKind.Arrow:
                canvas.Children.Add(CreateLine(3.0, 12.2, 12.2, 3.0, 1.6));
                canvas.Children.Add(CreateLine(9.2, 3.0, 12.2, 3.0, 1.6));
                canvas.Children.Add(CreateLine(12.2, 3.0, 12.2, 6.0, 1.6));
                break;
            case FigmaIconKind.Slice:
                canvas.Children.Add(CreateLine(3.0, 12.8, 13.0, 2.8, 1.6));
                canvas.Children.Add(CreateLine(4.6, 11.4, 2.6, 13.4, 1.2));
                canvas.Children.Add(CreateLine(11.4, 4.6, 13.4, 2.6, 1.2));
                break;
            case FigmaIconKind.Polygon:
                canvas.Children.Add(CreatePolygon(
                    [new Point(8.0, 2.4), new Point(13.0, 11.8), new Point(3.0, 11.8)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                break;
            case FigmaIconKind.Star:
                canvas.Children.Add(CreatePolygon(
                    [new Point(8.0, 2.0), new Point(9.9, 6.1), new Point(14.2, 6.4), new Point(10.9, 9.2), new Point(11.9, 13.8), new Point(8.0, 11.2), new Point(4.1, 13.8), new Point(5.1, 9.2), new Point(1.8, 6.4), new Point(6.1, 6.1)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                break;
            case FigmaIconKind.Text:
                return CreateTextGlyph("T");
            case FigmaIconKind.Vector:
                canvas.Children.Add(CreatePolyline(new Point(3, 12), new Point(6, 4), new Point(10, 10), new Point(13, 3)));
                canvas.Children.Add(CreateNode(3, 12));
                canvas.Children.Add(CreateNode(6, 4));
                canvas.Children.Add(CreateNode(10, 10));
                canvas.Children.Add(CreateNode(13, 3));
                break;
            case FigmaIconKind.Pen:
                canvas.Children.Add(CreatePolygon(
                    [new Point(7.8, 1.8), new Point(12.8, 6.6), new Point(10.4, 14.0), new Point(5.2, 11.6), new Point(2.8, 6.8)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                canvas.Children.Add(CreateEllipse(7.2, 6.0, 2.0, 2.0, StrokeBrush()));
                break;
            case FigmaIconKind.Brush:
                canvas.Children.Add(CreatePolyline(new Point(2.8, 11.8), new Point(5.4, 7.0), new Point(8.0, 10.6), new Point(10.8, 4.0), new Point(13.2, 8.8)));
                break;
            case FigmaIconKind.Pencil:
                canvas.Children.Add(CreatePolygon(
                    [new Point(4.0, 12.8), new Point(11.6, 5.2), new Point(13.2, 6.8), new Point(5.6, 14.4), new Point(3.4, 14.8)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                canvas.Children.Add(CreatePolygon(
                    [new Point(11.6, 5.2), new Point(13.8, 3.0), new Point(14.8, 4.0), new Point(13.2, 6.8)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                break;
            case FigmaIconKind.Boolean:
                canvas.Children.Add(CreateRectangle(2.3, 4.0, 6.3, 6.3, 0.8));
                canvas.Children.Add(CreateRectangle(7.4, 5.8, 6.3, 6.3, 0.8));
                break;
            case FigmaIconKind.Image:
                canvas.Children.Add(CreateRectangle(2.0, 3.0, 12.0, 10.0, 1.2));
                canvas.Children.Add(CreateEllipse(4.0, 5.0, 2.0, 2.0, StrokeBrush()));
                canvas.Children.Add(CreatePolyline(new Point(3.5, 11.0), new Point(7.0, 7.2), new Point(9.2, 9.1), new Point(12.5, 5.5)));
                break;
            case FigmaIconKind.Component:
                canvas.Children.Add(CreatePolygon(
                    [new Point(5.0, 1.8), new Point(7.0, 3.8), new Point(5.0, 5.8), new Point(3.0, 3.8)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                canvas.Children.Add(CreatePolygon(
                    [new Point(11.0, 3.0), new Point(13.0, 5.0), new Point(11.0, 7.0), new Point(9.0, 5.0)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                canvas.Children.Add(CreatePolygon(
                    [new Point(5.0, 8.2), new Point(7.0, 10.2), new Point(5.0, 12.2), new Point(3.0, 10.2)],
                    FillBrushOrTransparent(),
                    StrokeBrush(),
                    StrokeThickness));
                canvas.Children.Add(CreateRectangle(8.8, 8.8, 4.0, 4.0, 0.9));
                break;
            case FigmaIconKind.Instance:
                canvas.Children.Add(CreateRectangle(2.0, 4.0, 7.0, 7.0, 0.8));
                canvas.Children.Add(CreateRectangle(7.0, 1.8, 7.0, 7.0, 0.8));
                break;
            case FigmaIconKind.SolidPaint:
                canvas.Children.Add(CreateRectangle(2.0, 2.0, 12.0, 12.0, 2.0, FillBrushOrTransparent(), StrokeBrush()));
                break;
            case FigmaIconKind.GradientLinear:
                canvas.Children.Add(CreateLine(3.0, 13.0, 13.0, 3.0, 1.5));
                canvas.Children.Add(CreateNode(3, 13, true));
                canvas.Children.Add(CreateNode(13, 3, true));
                break;
            case FigmaIconKind.GradientRadial:
                canvas.Children.Add(CreateEllipse(2.5, 2.5, 11.0, 11.0));
                canvas.Children.Add(CreateEllipse(5.0, 5.0, 6.0, 6.0));
                canvas.Children.Add(CreateEllipse(7.0, 7.0, 2.0, 2.0, StrokeBrush()));
                break;
            case FigmaIconKind.ImagePaint:
                canvas.Children.Add(CreateRectangle(2.0, 3.0, 12.0, 10.0, 1.2));
                canvas.Children.Add(CreateEllipse(4.2, 5.0, 2.0, 2.0, StrokeBrush()));
                canvas.Children.Add(CreatePolyline(new Point(3.5, 11.0), new Point(7.0, 7.4), new Point(8.8, 9.0), new Point(12.2, 5.4)));
                break;
            case FigmaIconKind.VideoPaint:
                canvas.Children.Add(CreateRectangle(2.0, 3.0, 12.0, 10.0, 1.4));
                canvas.Children.Add(CreatePolygon([new Point(6.3, 5.5), new Point(11.0, 8.0), new Point(6.3, 10.5)], IconStroke, StrokeBrush(), 1.0));
                break;
            case FigmaIconKind.Droplet:
                canvas.Children.Add(CreatePolygon(
                    [new Point(8.0, 1.8), new Point(11.6, 6.6), new Point(10.8, 11.0), new Point(8.0, 14.0), new Point(5.2, 11.0), new Point(4.4, 6.6)],
                    null,
                    StrokeBrush(),
                    StrokeThickness));
                break;
            case FigmaIconKind.DropletOff:
                canvas.Children.Add(CreatePolygon(
                    [new Point(8.0, 1.8), new Point(11.6, 6.6), new Point(10.8, 11.0), new Point(8.0, 14.0), new Point(5.2, 11.0), new Point(4.4, 6.6)],
                    null,
                    StrokeBrush(),
                    StrokeThickness));
                canvas.Children.Add(CreateLine(3.0, 13.0, 13.0, 3.0));
                break;
            case FigmaIconKind.Library:
                canvas.Children.Add(CreateRectangle(2.2, 3.0, 11.6, 9.8, 1.2));
                canvas.Children.Add(CreateLine(5.2, 3.0, 5.2, 12.8, 1.1));
                canvas.Children.Add(CreateLine(7.8, 5.5, 11.6, 5.5, 1.1));
                canvas.Children.Add(CreateLine(7.8, 8.0, 11.0, 8.0, 1.1));
                break;
            case FigmaIconKind.Team:
                canvas.Children.Add(CreateEllipse(3.0, 3.0, 4.0, 4.0, StrokeBrush()));
                canvas.Children.Add(CreateEllipse(9.0, 3.8, 3.0, 3.0, StrokeBrush()));
                canvas.Children.Add(CreateEllipse(8.2, 9.5, 5.0, 2.8, StrokeBrush()));
                canvas.Children.Add(CreateEllipse(1.8, 8.2, 6.2, 3.4, StrokeBrush()));
                break;
            case FigmaIconKind.Refresh:
                canvas.Children.Add(CreatePolyline(new Point(4.2, 5.2), new Point(5.8, 3.8), new Point(8.2, 3.5), new Point(10.4, 4.4)));
                canvas.Children.Add(CreatePolyline(new Point(11.8, 10.6), new Point(10.2, 12.0), new Point(7.8, 12.3), new Point(5.6, 11.4)));
                canvas.Children.Add(CreatePolyline(new Point(10.4, 2.8), new Point(10.9, 5.1), new Point(13.0, 4.3)));
                canvas.Children.Add(CreatePolyline(new Point(5.7, 13.2), new Point(5.1, 10.9), new Point(3.0, 11.7)));
                break;
            case FigmaIconKind.Package:
                canvas.Children.Add(CreatePolygon(
                    [new Point(8, 1.8), new Point(13.2, 4.6), new Point(8, 7.4), new Point(2.8, 4.6)],
                    null,
                    StrokeBrush(),
                    StrokeThickness));
                canvas.Children.Add(CreatePolygon(
                    [new Point(2.8, 4.6), new Point(2.8, 11.2), new Point(8, 14.0), new Point(8, 7.4)],
                    null,
                    StrokeBrush(),
                    StrokeThickness));
                canvas.Children.Add(CreatePolygon(
                    [new Point(13.2, 4.6), new Point(13.2, 11.2), new Point(8, 14.0), new Point(8, 7.4)],
                    null,
                    StrokeBrush(),
                    StrokeThickness));
                break;
            case FigmaIconKind.Adjust:
                canvas.Children.Add(CreateLine(4.2, 2.0, 4.2, 14.0));
                canvas.Children.Add(CreateLine(11.8, 2.0, 11.8, 14.0));
                canvas.Children.Add(CreateEllipse(2.3, 5.0, 3.8, 3.8, StrokeBrush()));
                canvas.Children.Add(CreateEllipse(9.9, 8.2, 3.8, 3.8, StrokeBrush()));
                break;
            case FigmaIconKind.AlignLeft:
                canvas.Children.Add(CreateLine(3.0, 2.0, 3.0, 14.0, 1.2));
                canvas.Children.Add(CreateLine(6.0, 5.0, 13.0, 5.0, 1.4));
                canvas.Children.Add(CreateLine(6.0, 8.0, 11.0, 8.0, 1.4));
                canvas.Children.Add(CreateLine(6.0, 11.0, 9.0, 11.0, 1.4));
                break;
            case FigmaIconKind.AlignHorizontalCenter:
                canvas.Children.Add(CreateLine(8.0, 2.0, 8.0, 14.0, 1.2));
                canvas.Children.Add(CreateLine(3.0, 5.0, 13.0, 5.0, 1.4));
                canvas.Children.Add(CreateLine(5.0, 8.0, 11.0, 8.0, 1.4));
                canvas.Children.Add(CreateLine(6.0, 11.0, 10.0, 11.0, 1.4));
                break;
            case FigmaIconKind.AlignRight:
                canvas.Children.Add(CreateLine(13.0, 2.0, 13.0, 14.0, 1.2));
                canvas.Children.Add(CreateLine(3.0, 5.0, 10.0, 5.0, 1.4));
                canvas.Children.Add(CreateLine(5.0, 8.0, 10.0, 8.0, 1.4));
                canvas.Children.Add(CreateLine(7.0, 11.0, 10.0, 11.0, 1.4));
                break;
            case FigmaIconKind.AlignTop:
                canvas.Children.Add(CreateLine(2.0, 3.0, 14.0, 3.0, 1.2));
                canvas.Children.Add(CreateLine(5.0, 6.0, 5.0, 13.0, 1.4));
                canvas.Children.Add(CreateLine(8.0, 6.0, 8.0, 11.0, 1.4));
                canvas.Children.Add(CreateLine(11.0, 6.0, 11.0, 9.0, 1.4));
                break;
            case FigmaIconKind.AlignVerticalCenter:
                canvas.Children.Add(CreateLine(2.0, 8.0, 14.0, 8.0, 1.2));
                canvas.Children.Add(CreateLine(5.0, 3.0, 5.0, 13.0, 1.4));
                canvas.Children.Add(CreateLine(8.0, 5.0, 8.0, 11.0, 1.4));
                canvas.Children.Add(CreateLine(11.0, 6.0, 11.0, 10.0, 1.4));
                break;
            case FigmaIconKind.AlignBottom:
                canvas.Children.Add(CreateLine(2.0, 13.0, 14.0, 13.0, 1.2));
                canvas.Children.Add(CreateLine(5.0, 3.0, 5.0, 10.0, 1.4));
                canvas.Children.Add(CreateLine(8.0, 5.0, 8.0, 10.0, 1.4));
                canvas.Children.Add(CreateLine(11.0, 7.0, 11.0, 10.0, 1.4));
                break;
            case FigmaIconKind.DistributeHorizontal:
                canvas.Children.Add(CreateLine(2.5, 4.0, 2.5, 12.0, 1.2));
                canvas.Children.Add(CreateLine(13.5, 4.0, 13.5, 12.0, 1.2));
                canvas.Children.Add(CreateRectangle(4.0, 5.0, 2.2, 6.0, 0.6));
                canvas.Children.Add(CreateRectangle(9.8, 5.0, 2.2, 6.0, 0.6));
                canvas.Children.Add(CreateLine(6.8, 8.0, 8.8, 8.0, 1.2));
                canvas.Children.Add(CreatePolyline(new Point(7.4, 6.8), new Point(6.2, 8.0), new Point(7.4, 9.2)));
                canvas.Children.Add(CreatePolyline(new Point(8.2, 6.8), new Point(9.4, 8.0), new Point(8.2, 9.2)));
                break;
            case FigmaIconKind.DistributeVertical:
                canvas.Children.Add(CreateLine(4.0, 2.5, 12.0, 2.5, 1.2));
                canvas.Children.Add(CreateLine(4.0, 13.5, 12.0, 13.5, 1.2));
                canvas.Children.Add(CreateRectangle(5.0, 4.0, 6.0, 2.2, 0.6));
                canvas.Children.Add(CreateRectangle(5.0, 9.8, 6.0, 2.2, 0.6));
                canvas.Children.Add(CreateLine(8.0, 6.8, 8.0, 8.8, 1.2));
                canvas.Children.Add(CreatePolyline(new Point(6.8, 7.4), new Point(8.0, 6.2), new Point(9.2, 7.4)));
                canvas.Children.Add(CreatePolyline(new Point(6.8, 8.2), new Point(8.0, 9.4), new Point(9.2, 8.2)));
                break;
            case FigmaIconKind.Rotate:
                canvas.Children.Add(CreateArc(3.0, 3.0, 10.0, 10.0, 210, 250));
                canvas.Children.Add(CreatePolyline(new Point(9.8, 1.9), new Point(12.6, 2.5), new Point(11.8, 5.2)));
                break;
            case FigmaIconKind.FlipHorizontal:
                canvas.Children.Add(CreateLine(8.0, 2.0, 8.0, 14.0, 1.1));
                canvas.Children.Add(CreatePolyline(new Point(6.8, 4.0), new Point(3.4, 8.0), new Point(6.8, 12.0)));
                canvas.Children.Add(CreatePolyline(new Point(9.2, 4.0), new Point(12.6, 8.0), new Point(9.2, 12.0)));
                break;
            case FigmaIconKind.FlipVertical:
                canvas.Children.Add(CreateLine(2.0, 8.0, 14.0, 8.0, 1.1));
                canvas.Children.Add(CreatePolyline(new Point(4.0, 6.8), new Point(8.0, 3.4), new Point(12.0, 6.8)));
                canvas.Children.Add(CreatePolyline(new Point(4.0, 9.2), new Point(8.0, 12.6), new Point(12.0, 9.2)));
                break;
            case FigmaIconKind.AutoLayoutHorizontal:
                canvas.Children.Add(CreateRectangle(2.8, 5.0, 2.8, 2.8, 0.6));
                canvas.Children.Add(CreateRectangle(7.4, 5.0, 2.8, 2.8, 0.6));
                canvas.Children.Add(CreateRectangle(12.0, 5.0, 2.8, 2.8, 0.6));
                canvas.Children.Add(CreateLine(3.8, 11.0, 13.8, 11.0, 1.2));
                canvas.Children.Add(CreatePolyline(new Point(11.8, 9.3), new Point(13.8, 11.0), new Point(11.8, 12.7)));
                break;
            case FigmaIconKind.AutoLayoutVertical:
                canvas.Children.Add(CreateRectangle(5.0, 2.8, 2.8, 2.8, 0.6));
                canvas.Children.Add(CreateRectangle(5.0, 7.4, 2.8, 2.8, 0.6));
                canvas.Children.Add(CreateRectangle(5.0, 12.0, 2.8, 2.8, 0.6));
                canvas.Children.Add(CreateLine(11.0, 3.8, 11.0, 13.8, 1.2));
                canvas.Children.Add(CreatePolyline(new Point(9.3, 11.8), new Point(11.0, 13.8), new Point(12.7, 11.8)));
                break;
            case FigmaIconKind.AutoLayoutWrap:
                canvas.Children.Add(CreateRectangle(3.0, 3.4, 2.6, 2.6, 0.6));
                canvas.Children.Add(CreateRectangle(7.0, 3.4, 2.6, 2.6, 0.6));
                canvas.Children.Add(CreateRectangle(11.0, 3.4, 2.6, 2.6, 0.6));
                canvas.Children.Add(CreateRectangle(3.0, 10.0, 2.6, 2.6, 0.6));
                canvas.Children.Add(CreateRectangle(7.0, 10.0, 2.6, 2.6, 0.6));
                canvas.Children.Add(CreatePolyline(new Point(11.5, 9.2), new Point(13.5, 11.0), new Point(11.5, 12.8)));
                canvas.Children.Add(CreateLine(4.0, 8.0, 12.8, 8.0, 1.0));
                break;
            case FigmaIconKind.AutoLayoutGrid:
                canvas.Children.Add(CreateRectangle(2.4, 2.8, 3.2, 3.2, 0.6));
                canvas.Children.Add(CreateRectangle(7.2, 2.8, 3.2, 3.2, 0.6));
                canvas.Children.Add(CreateRectangle(12.0, 2.8, 3.2, 3.2, 0.6));
                canvas.Children.Add(CreateRectangle(2.4, 7.6, 3.2, 3.2, 0.6));
                canvas.Children.Add(CreateRectangle(7.2, 7.6, 3.2, 3.2, 0.6));
                canvas.Children.Add(CreateRectangle(12.0, 7.6, 3.2, 3.2, 0.6));
                canvas.Children.Add(CreateRectangle(2.4, 12.4, 3.2, 3.2, 0.6));
                canvas.Children.Add(CreateRectangle(7.2, 12.4, 3.2, 3.2, 0.6));
                canvas.Children.Add(CreateRectangle(12.0, 12.4, 3.2, 3.2, 0.6));
                break;
            case FigmaIconKind.CornerRadius:
                canvas.Children.Add(CreateRoundedCornerIcon());
                break;
            case FigmaIconKind.Expand:
                canvas.Children.Add(CreateLine(3.0, 6.0, 3.0, 3.0, 1.2));
                canvas.Children.Add(CreateLine(3.0, 3.0, 6.0, 3.0, 1.2));
                canvas.Children.Add(CreateLine(10.0, 3.0, 13.0, 3.0, 1.2));
                canvas.Children.Add(CreateLine(13.0, 3.0, 13.0, 6.0, 1.2));
                canvas.Children.Add(CreateLine(3.0, 10.0, 3.0, 13.0, 1.2));
                canvas.Children.Add(CreateLine(3.0, 13.0, 6.0, 13.0, 1.2));
                canvas.Children.Add(CreateLine(10.0, 13.0, 13.0, 13.0, 1.2));
                canvas.Children.Add(CreateLine(13.0, 10.0, 13.0, 13.0, 1.2));
                break;
            case FigmaIconKind.Design:
                canvas.Children.Add(CreateRectangle(2.2, 3.0, 11.6, 9.8, 1.2));
                canvas.Children.Add(CreateLine(5.4, 3.0, 5.4, 12.8, 1.1));
                canvas.Children.Add(CreateLine(8.0, 6.0, 12.0, 6.0, 1.1));
                canvas.Children.Add(CreateLine(8.0, 8.8, 11.0, 8.8, 1.1));
                canvas.Children.Add(CreatePolyline(new Point(9.2, 1.8), new Point(13.8, 4.8), new Point(11.2, 5.2), new Point(12.0, 7.8), new Point(10.6, 8.6), new Point(9.8, 5.8), new Point(8.4, 4.4), new Point(9.2, 1.8)));
                break;
            case FigmaIconKind.Code:
                canvas.Children.Add(CreateLine(7.1, 3.2, 8.9, 12.8, 1.3));
                canvas.Children.Add(CreatePolyline(new Point(5.2, 4.4), new Point(2.6, 8.0), new Point(5.2, 11.6)));
                canvas.Children.Add(CreatePolyline(new Point(10.8, 4.4), new Point(13.4, 8.0), new Point(10.8, 11.6)));
                break;
        }

        return canvas;
    }

    private bool TryBuildThemedIcon(out UIElement icon)
    {
        var descriptor = GetThemedIconDescriptor(Kind);
        if (descriptor is null)
        {
            icon = null!;
            return false;
        }

        var brush = descriptor.Value.BrushKind == IconBrushKind.Fill && IconFill is not null
            ? IconFill
            : StrokeBrush();

        var text = descriptor.Value.Symbol is Symbol symbol
            ? char.ConvertFromUtf32((int)symbol)
            : descriptor.Value.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            icon = null!;
            return false;
        }

        var glyph = new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontSize = descriptor.Value.FontSize,
            FontFamily = descriptor.Value.Symbol is Symbol ? GetSymbolFontFamily() : new FontFamily("Consolas"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        if (descriptor.Value.Symbol is null)
        {
            glyph.FontWeight = new Windows.UI.Text.FontWeight { Weight = 700 };
        }

        var host = new Grid
        {
            Width = 16,
            Height = 16,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Children =
            {
                glyph
            }
        };

        TransformGroup? transforms = null;

        if (descriptor.Value.MirrorX || descriptor.Value.MirrorY)
        {
            transforms ??= new TransformGroup();
            transforms.Children.Add(new ScaleTransform
            {
                ScaleX = descriptor.Value.MirrorX ? -1 : 1,
                ScaleY = descriptor.Value.MirrorY ? -1 : 1
            });
        }

        if (Math.Abs(descriptor.Value.Rotation) > double.Epsilon)
        {
            transforms ??= new TransformGroup();
            transforms.Children.Add(new RotateTransform
            {
                Angle = descriptor.Value.Rotation
            });
        }

        if (transforms is not null)
        {
            host.RenderTransform = transforms;
        }

        icon = host;
        return true;
    }

    private static ThemedIconDescriptor? GetThemedIconDescriptor(FigmaIconKind kind)
    {
        return kind switch
        {
            FigmaIconKind.Search => new(Symbol.Find),
            FigmaIconKind.Comment => new(Symbol.Comment),
            FigmaIconKind.Send => new(Symbol.Send),
            FigmaIconKind.Actions => new(Symbol.More),
            FigmaIconKind.Move => new(Symbol.SelectAll),
            FigmaIconKind.Hand => new(Symbol.TouchPointer),
            FigmaIconKind.Scale => new(Symbol.FullScreen),
            FigmaIconKind.Add => new(Symbol.Add),
            FigmaIconKind.Minus => new(Symbol.Remove),
            FigmaIconKind.Close => new(Symbol.Cancel),
            FigmaIconKind.Back => new(Symbol.Back),
            FigmaIconKind.Menu => new(Symbol.GlobalNavigationButton),
            FigmaIconKind.Eye => new(Symbol.View),
            FigmaIconKind.EyeOff => new(Symbol.HideBcc),
            FigmaIconKind.Lock => new(Symbol.ProtectedDocument),
            FigmaIconKind.Unlock => new(Symbol.Permissions),
            FigmaIconKind.Page => new(Symbol.Page),
            FigmaIconKind.Frame => new(Symbol.Crop),
            FigmaIconKind.Section => new(Symbol.TwoPage),
            FigmaIconKind.Group => new(Symbol.AllApps),
            FigmaIconKind.Rectangle => new(Symbol.Stop),
            FigmaIconKind.Ellipse => new(Symbol.Target),
            FigmaIconKind.Line => new(Symbol.Remove, Rotation: -45),
            FigmaIconKind.Arrow => new(Symbol.Forward, Rotation: -45),
            FigmaIconKind.Slice => new(Symbol.Crop),
            FigmaIconKind.Polygon => new(Symbol.Stop, Rotation: 45),
            FigmaIconKind.Star => new(Symbol.OutlineStar),
            FigmaIconKind.Text => new(Symbol.Font),
            FigmaIconKind.Vector => new(Symbol.Edit),
            FigmaIconKind.Pen => new(Symbol.Edit),
            FigmaIconKind.Brush => new(Symbol.Highlight),
            FigmaIconKind.Pencil => new(Symbol.Edit),
            FigmaIconKind.Boolean => new(Symbol.SwitchApps),
            FigmaIconKind.Image => new(Symbol.Pictures),
            FigmaIconKind.Component => new(Symbol.Tag),
            FigmaIconKind.Instance => new(Symbol.Copy),
            FigmaIconKind.SolidPaint => new(Symbol.Stop, BrushKind: IconBrushKind.Fill),
            FigmaIconKind.GradientLinear => new(Symbol.Sort),
            FigmaIconKind.GradientRadial => new(Symbol.Target),
            FigmaIconKind.ImagePaint => new(Symbol.Pictures),
            FigmaIconKind.VideoPaint => new(Symbol.Video),
            FigmaIconKind.Droplet => new(Symbol.FontColor),
            FigmaIconKind.DropletOff => new(Symbol.DisableUpdates),
            FigmaIconKind.Library => new(Symbol.Library),
            FigmaIconKind.Team => new(Symbol.People),
            FigmaIconKind.Refresh => new(Symbol.Refresh),
            FigmaIconKind.Package => new(Symbol.Shop),
            FigmaIconKind.Adjust => new(Symbol.Manage),
            FigmaIconKind.AlignLeft => new(Symbol.AlignLeft),
            FigmaIconKind.AlignHorizontalCenter => new(Symbol.AlignCenter),
            FigmaIconKind.AlignRight => new(Symbol.AlignRight),
            FigmaIconKind.AlignTop => new(Symbol.DockBottom, Rotation: 180),
            FigmaIconKind.AlignVerticalCenter => new(Symbol.AlignCenter, Rotation: 90),
            FigmaIconKind.AlignBottom => new(Symbol.DockBottom),
            FigmaIconKind.DistributeHorizontal => new(Symbol.TwoBars),
            FigmaIconKind.DistributeVertical => new(Symbol.TwoBars, Rotation: 90),
            FigmaIconKind.Rotate => new(Symbol.Rotate),
            FigmaIconKind.FlipHorizontal => new(Symbol.Switch),
            FigmaIconKind.FlipVertical => new(Symbol.Switch, Rotation: 90),
            FigmaIconKind.AutoLayoutHorizontal => new(Symbol.DockLeft),
            FigmaIconKind.AutoLayoutVertical => new(Symbol.DockBottom),
            FigmaIconKind.AutoLayoutWrap => new(Symbol.FourBars),
            FigmaIconKind.AutoLayoutGrid => new(Symbol.Calculator),
            FigmaIconKind.CornerRadius => new(Symbol.Placeholder),
            FigmaIconKind.Expand => new(Symbol.FullScreen),
            FigmaIconKind.Design => new(Symbol.Edit),
            FigmaIconKind.Code => new(Text: "</>", FontSize: 8.75d),
            _ => null
        };
    }

    private static FontFamily GetSymbolFontFamily()
    {
        if (Application.Current?.Resources is { } resources &&
            resources.TryGetValue("SymbolThemeFontFamily", out var value))
        {
            if (value is FontFamily family)
            {
                return family;
            }

            if (value is string familyName && !string.IsNullOrWhiteSpace(familyName))
            {
                return new FontFamily(familyName);
            }
        }

        return FallbackSymbolFontFamily;
    }

    private UIElement CreateTextGlyph(string text)
    {
        return new Grid
        {
            Width = 16,
            Height = 16,
            Children =
            {
                new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
                    Foreground = StrokeBrush(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
    }

    private Brush StrokeBrush() => IconStroke ?? new SolidColorBrush(ColorHelper.FromArgb(255, 17, 24, 39));

    private Brush FillBrushOrTransparent() => IconFill ?? new SolidColorBrush(Colors.Transparent);

    private Polygon CreateRoundedCallout()
    {
        return CreatePolygon(
            [
                new Point(4.0, 3.0),
                new Point(12.0, 3.0),
                new Point(13.4, 4.4),
                new Point(13.4, 9.8),
                new Point(12.0, 11.2),
                new Point(8.2, 11.2),
                new Point(5.3, 13.7),
                new Point(5.6, 11.2),
                new Point(4.0, 11.2),
                new Point(2.6, 9.8),
                new Point(2.6, 4.4)
            ],
            null,
            StrokeBrush(),
            StrokeThickness);
    }

    private UIElement CreateRoundedCornerIcon()
    {
        var canvas = new Canvas
        {
            Width = 16,
            Height = 16
        };

        canvas.Children.Add(CreateLine(4.0, 4.0, 4.0, 8.2, 1.2));
        canvas.Children.Add(CreateLine(4.0, 4.0, 8.2, 4.0, 1.2));

        var path = new Microsoft.UI.Xaml.Shapes.Path
        {
            Stroke = StrokeBrush(),
            StrokeThickness = StrokeThickness,
            Data = new PathGeometry
            {
                Figures =
                {
                    new PathFigure
                    {
                        StartPoint = new Point(4.0, 12.2),
                        Segments =
                        {
                            new LineSegment { Point = new Point(8.6, 12.2) },
                            new ArcSegment
                            {
                                Point = new Point(12.2, 8.6),
                                Size = new Size(3.6, 3.6),
                                SweepDirection = SweepDirection.Counterclockwise
                            },
                            new LineSegment { Point = new Point(12.2, 4.0) }
                        },
                        IsClosed = false,
                        IsFilled = false
                    }
                }
            }
        };

        canvas.Children.Add(path);
        return canvas;
    }

    private Line CreateLine(double x1, double y1, double x2, double y2, double? thickness = null)
    {
        return new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = StrokeBrush(),
            StrokeThickness = thickness ?? StrokeThickness
        };
    }

    private Rectangle CreateRectangle(
        double left,
        double top,
        double width,
        double height,
        double radius,
        Brush? fill = null,
        Brush? stroke = null)
    {
        var rectangle = new Rectangle
        {
            Width = width,
            Height = height,
            RadiusX = radius,
            RadiusY = radius,
            Fill = fill ?? new SolidColorBrush(Colors.Transparent),
            Stroke = stroke ?? StrokeBrush(),
            StrokeThickness = StrokeThickness
        };

        Canvas.SetLeft(rectangle, left);
        Canvas.SetTop(rectangle, top);
        return rectangle;
    }

    private Ellipse CreateEllipse(double left, double top, double width, double height, Brush? fill = null)
    {
        var ellipse = new Ellipse
        {
            Width = width,
            Height = height,
            Fill = fill ?? new SolidColorBrush(Colors.Transparent),
            Stroke = StrokeBrush(),
            StrokeThickness = StrokeThickness
        };

        Canvas.SetLeft(ellipse, left);
        Canvas.SetTop(ellipse, top);
        return ellipse;
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

    private Polygon CreatePolygon(IList<Point> points, Brush? fill, Brush? stroke, double thickness)
    {
        return new Polygon
        {
            Points = new PointCollection(points),
            Fill = fill ?? new SolidColorBrush(Colors.Transparent),
            Stroke = stroke ?? StrokeBrush(),
            StrokeThickness = thickness
        };
    }

    private Microsoft.UI.Xaml.Shapes.Path CreateArc(double left, double top, double width, double height, double startAngle, double sweepAngle)
    {
        var radiusX = width / 2.0;
        var radiusY = height / 2.0;
        var centerX = left + radiusX;
        var centerY = top + radiusY;
        var startRadians = System.Math.PI * startAngle / 180.0;
        var endRadians = System.Math.PI * (startAngle + sweepAngle) / 180.0;

        var figure = new PathFigure
        {
            StartPoint = new Point(
                centerX + radiusX * System.Math.Cos(startRadians),
                centerY + radiusY * System.Math.Sin(startRadians)),
            IsClosed = false,
            IsFilled = false
        };

        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(
                centerX + radiusX * System.Math.Cos(endRadians),
                centerY + radiusY * System.Math.Sin(endRadians)),
            Size = new Size(radiusX, radiusY),
            SweepDirection = sweepAngle >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
            IsLargeArc = System.Math.Abs(sweepAngle) > 180.0
        });

        return new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = new PathGeometry
            {
                Figures =
                {
                    figure
                }
            },
            Stroke = StrokeBrush(),
            StrokeThickness = StrokeThickness
        };
    }

    private Ellipse CreateNode(double centerX, double centerY, bool hollow = false)
    {
        var node = new Ellipse
        {
            Width = hollow ? 3.8 : 3.2,
            Height = hollow ? 3.8 : 3.2,
            Fill = hollow ? new SolidColorBrush(Colors.White) : StrokeBrush(),
            Stroke = StrokeBrush(),
            StrokeThickness = 1.1
        };

        Canvas.SetLeft(node, centerX - (hollow ? 1.9 : 1.6));
        Canvas.SetTop(node, centerY - (hollow ? 1.9 : 1.6));
        return node;
    }
}
