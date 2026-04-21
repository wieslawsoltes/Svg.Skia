using Microsoft.Maui.Controls;

namespace SvgML.Maui.Demo;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BoundSvgHost.Content = CreateBoundFillSvg();
    }

    private global::SvgML.svg CreateBoundFillSvg()
    {
        var svg = new global::SvgML.svg
        {
            HeightRequest = 220,
            Stretch = Stretch.Uniform,
            viewBox = "0 0 220 120",
            BindingContext = CircleFill
        };

        var defs = new global::SvgML.defs();
        var gradient = new global::SvgML.linearGradient
        {
            id = "gradient",
            x1 = Unit("0%"),
            y1 = Unit("0%"),
            x2 = Unit("0"),
            y2 = Unit("100%")
        };

        gradient.Children.Add(new global::SvgML.stop
        {
            offset = Unit("0%"),
            style = "stop-color:skyblue;"
        });
        gradient.Children.Add(new global::SvgML.stop
        {
            offset = Unit("100%"),
            style = "stop-color:seagreen;"
        });
        defs.Children.Add(gradient);

        svg.Children.Add(defs);
        svg.Children.Add(new global::SvgML.rect
        {
            x = Unit("0"),
            y = Unit("0"),
            width = Unit("100%"),
            height = Unit("100%"),
            fill = "url(#gradient)"
        });

        var boundCircle = new global::SvgML.circle
        {
            cx = Unit("50"),
            cy = Unit("50"),
            r = Unit("40")
        };
        boundCircle.SetBinding(global::SvgML.element.fillProperty, new Binding(nameof(Entry.Text), BindingMode.TwoWay));
        svg.Children.Add(boundCircle);

        svg.Children.Add(new global::SvgML.circle
        {
            cx = Unit("150"),
            cy = Unit("50"),
            r = Unit("40"),
            fill = "black",
            opacity = 0.3f
        });

        return svg;
    }

    private static global::Svg.SvgUnit Unit(string value)
    {
        return global::Svg.SvgUnitConverter.Parse(value.AsSpan());
    }
}
