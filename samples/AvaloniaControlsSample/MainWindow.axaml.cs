using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Skia;
using Avalonia.Markup.Xaml;
using SkiaSharp;

namespace AvaloniaControlsSample;

public class MainWindow : Window
{
    private SKCanvasControl _canvas;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        _canvas = this.FindControl<SKCanvasControl>("Canvas");
        if (_canvas is { })
        {
            _canvas.Draw += (_, e) =>
            {
                e.Canvas.DrawRect(SKRect.Create(0f, 0f, 100f, 100f), new SKPaint { Color = SKColors.Aqua });
            };
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
