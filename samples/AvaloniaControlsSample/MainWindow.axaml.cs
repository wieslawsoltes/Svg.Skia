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

        // initialize bitmap control
        var bitmap = new SKBitmap(100, 100);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.Red, IsAntialias = true };
            canvas.DrawCircle(50, 50, 40, paint);
        }
        BitmapControl.Bitmap = bitmap;

        // initialize path control
        var path = new SKPath();
        path.MoveTo(10, 10);
        path.LineTo(90, 10);
        path.LineTo(50, 90);
        path.Close();
        PathControl.Path = path;
        PathControl.Paint = new SKPaint { Color = SKColors.Green, IsAntialias = true, Style = SKPaintStyle.Fill };

        // initialize picture control
        var recorder = new SKPictureRecorder();
        var pictureCanvas = recorder.BeginRecording(new SKRect(0, 0, 100, 100));
        using (var paint = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Stroke, StrokeWidth = 5, IsAntialias = true })
        {
            pictureCanvas.DrawRect(new SKRect(10, 10, 90, 90), paint);
        }
        var picture = recorder.EndRecording();
        PictureControl.Picture = picture;
    }
}
