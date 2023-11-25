namespace ShimSkiaSharp;

public class PathClip
{
    public SKPath? Path { get; set; }

    public SKMatrix? Transform { get; set; }

    public ClipPath? Clip { get; set; }
}
