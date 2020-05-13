namespace Svg.Model
{
    public class Paint
    {
        public PaintStyle Style;
        public bool IsAntialias;
        public float StrokeWidth;
        public StrokeCap StrokeCap;
        public StrokeJoin StrokeJoin;
        public float StrokeMiter;
        public Typeface? Typeface;
        public float TextSize;
        public TextAlign TextAlign;
        public Color? Color;
        public Shader? Shader;
        public ColorFilter? ColorFilter;
        public ImageFilter? ImageFilter;
        public PathEffect? PathEffect;
        public BlendMode BlendMode;
        public FilterQuality FilterQuality;
    }
}
