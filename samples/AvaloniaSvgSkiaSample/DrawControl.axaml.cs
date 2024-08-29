using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace AvaloniaSvgSkiaSample;

public partial class DrawControl : UserControl
{
    public SvgImage Image { get; set; }

    public DrawControl()
    {
        InitializeComponent();
    }

    public override void Render(DrawingContext context)
    {
        var center = Bounds.Center;
        var width = Image.Size.Width / 2;
        var height = Image.Size.Width / 2;
        context.DrawImage(Image, new Rect(center.X - 0.5 * width, center.Y - 0.5 * height, width, height));
    }
}

