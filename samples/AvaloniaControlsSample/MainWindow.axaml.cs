using Avalonia;
using Avalonia.Controls;
using SkiaSharp;

namespace AvaloniaControlsSample;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        CanvasControl.Draw += (_, e) =>
        {
            e.Canvas.DrawRect(SKRect.Create(0f, 0f, 100f, 100f), new SKPaint { Color = SKColors.Aqua });
        };
    }
}
