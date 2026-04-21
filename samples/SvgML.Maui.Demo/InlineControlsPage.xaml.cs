using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace SvgML.Maui.Demo;

public partial class InlineControlsPage : ContentPage
{
    public InlineControlsPage()
    {
        InitializeComponent();
        HostedSvgHost.Content = CreateHostedControlsSvg();
    }

    private static global::SvgML.svg CreateHostedControlsSvg()
    {
        var svg = new global::SvgML.svg
        {
            HeightRequest = 340,
            Stretch = Stretch.Uniform,
            viewBox = "0 0 520 260"
        };

        svg.Children.Add(new global::SvgML.rect
        {
            x = Unit("0"),
            y = Unit("0"),
            width = Unit("520"),
            height = Unit("260"),
            fill = "#F8FAFC"
        });
        svg.Children.Add(new global::SvgML.rect
        {
            x = Unit("18"),
            y = Unit("18"),
            width = Unit("484"),
            height = Unit("224"),
            fill = "#FFFFFF",
            opacity = 0.92f
        });

        svg.Children.Add(CreateTextRow("32", "76",
            CreateForeignObject(124, 40, CreateButton("Publish", 112, true, new Thickness(0, 0, 12, 0))),
            CreateForeignObject(112, 40, CreateButton("Preview", 112))));
        svg.Children.Add(CreateTextRow("32", "138",
            CreateForeignObject(192, 38, CreateEntry("Design systems", 180, new Thickness(0, 0, 12, 0))),
            CreateForeignObject(150, 38, CreateEntry("Q2 launch", 150))));
        svg.Children.Add(CreateTextRow("32", "200",
            CreateForeignObject(132, 40, CreateButton("Assign reviewer", 120, margin: new Thickness(0, 0, 12, 0))),
            CreateForeignObject(120, 40, CreateButton("Create task", 120))));

        return svg;
    }

    private static global::SvgML.text CreateTextRow(string x, string y, params global::SvgML.element[] children)
    {
        var row = new global::SvgML.text
        {
            x = x,
            y = y
        };

        foreach (var child in children)
        {
            row.Children.Add(child);
        }

        return row;
    }

    private static global::SvgML.foreignObject CreateForeignObject(double width, double height, View child)
    {
        return new global::SvgML.foreignObject
        {
            width = Unit(width.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            height = Unit(height.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Child = child
        };
    }

    private static Button CreateButton(string text, double width, bool primary = false, Thickness margin = default)
    {
        var button = new Button
        {
            Text = text,
            WidthRequest = width,
            HeightRequest = 40,
            Padding = new Thickness(16, 8),
            Margin = margin
        };

        if (primary)
        {
            button.BackgroundColor = Color.FromArgb("#2563EB");
            button.TextColor = Colors.White;
        }

        return button;
    }

    private static Entry CreateEntry(string text, double width, Thickness margin = default)
    {
        return new Entry
        {
            Text = text,
            WidthRequest = width,
            HeightRequest = 38,
            FontSize = 14,
            Margin = margin
        };
    }

    private static global::Svg.SvgUnit Unit(string value)
    {
        return global::Svg.SvgUnitConverter.Parse(value.AsSpan());
    }
}
