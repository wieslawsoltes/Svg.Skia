namespace Svg.Editor.Skia.Uno;

public sealed class ZoomPercentRequestedEventArgs : EventArgs
{
    public ZoomPercentRequestedEventArgs(double zoomPercent)
    {
        ZoomPercent = zoomPercent;
    }

    public double ZoomPercent { get; }
}
