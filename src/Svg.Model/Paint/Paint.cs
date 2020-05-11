namespace Svg.Model
{
    public class Paint
    {
        public PaintStyle Style { get; set; }
        public bool IsAntialias { get; set; }
        public BlendMode BlendMode { get; set; }
        public FilterQuality FilterQuality { get; set; }
        public float StrokeWidth { get; set; }
        public float StrokeMiter { get; set; }
        public StrokeJoin StrokeJoin { get; set; }
        public StrokeCap StrokeCap { get; set; }
        public Typeface? Typeface { get; set; }
        public float TextSize { get; set; }
        public TextAlign TextAlign { get; set; }
        public Color? Color { get; set; }
        public Shader? Shader { get; set; }
        public ColorFilter? ColorFilter { get; set; }
        public ImageFilter? ImageFilter { get; set; }
        public PathEffect? PathEffect { get; set; }
    }
}
