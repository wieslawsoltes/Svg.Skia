using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AvaloniaSvgSkiaStylingSample;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        ApplySvgStyleButton.Click += ApplySvgStyleButtonClick;
        ApplySvgImageStyleButton.Click += ApplySvgImageStyleButtonClick;
    }

    private void ApplySvgStyleButtonClick(object sender, EventArgs e)
    {
        SvgControl.SetCurrentValue(Avalonia.Svg.Skia.Svg.CssProperty, ".Black { fill: #AAAAFF; }");
    }

    private void ApplySvgImageStyleButtonClick(object sender, EventArgs e)
    {
        SvgImageButton.SetCurrentValue(Avalonia.Svg.Skia.Svg.CssProperty, ".Black { fill: #AAAAFF; }");
    }
}
