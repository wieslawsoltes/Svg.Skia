using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Linq;

namespace AvaloniaSvgSkiaStylingSample;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        ApplyButton.AddHandler(Button.ClickEvent, OnApply);
    }

    private void OnApply(object sender, EventArgs e) 
    {
        this.SetValue(Avalonia.Svg.Skia.Svg.StyleProperty, ".Black { fill: #AAAAFF; }");
    }
}
