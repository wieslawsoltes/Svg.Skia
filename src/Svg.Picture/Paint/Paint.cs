using System;

namespace Svg.Picture
{
    public class Paint : IDisposable
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
        public bool LcdRenderText;
        public bool SubpixelText;
        public TextEncoding TextEncoding;
        public Color? Color;
        public Shader? Shader;
        public ColorFilter? ColorFilter;
        public ImageFilter? ImageFilter;
        public PathEffect? PathEffect;
        public BlendMode BlendMode;
        public FilterQuality FilterQuality;

        public Paint()
        {
            Style = PaintStyle.Fill;
            IsAntialias = false;
            StrokeWidth = 0;
            StrokeCap = StrokeCap.Butt;
            StrokeJoin = StrokeJoin.Miter;
            StrokeMiter = 4;
            Typeface = null;
            TextSize = 12;
            TextAlign = TextAlign.Left;
            LcdRenderText = false;
            SubpixelText = false;
            TextEncoding = TextEncoding.Utf8;
            Color = new Color(0x00, 0x00, 0x00, 0xFF);
            Shader = null;
            ColorFilter = null;
            ImageFilter = null;
            PathEffect = null;
            BlendMode = BlendMode.SrcOver;
            FilterQuality = FilterQuality.None;
        }

        public void Dispose()
        {
        }
    }
}
