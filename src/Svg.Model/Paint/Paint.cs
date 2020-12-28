using System;

namespace Svg.Model.Paint
{
    public sealed class Paint : IDisposable
    {
        public PaintStyle Style { get; set; }
        public bool IsAntialias { get; set; }
        public float StrokeWidth { get; set; }
        public StrokeCap StrokeCap { get; set; }
        public StrokeJoin StrokeJoin { get; set; }
        public float StrokeMiter { get; set; }
        public Typeface? Typeface { get; set; }
        public float TextSize { get; set; }
        public TextAlign TextAlign { get; set; }
        public bool LcdRenderText { get; set; }
        public bool SubpixelText { get; set; }
        public TextEncoding TextEncoding { get; set; }
        public Color? Color { get; set; }
        public Shader? Shader { get; set; }
        public ColorFilter? ColorFilter { get; set; }
        public ImageFilter? ImageFilter { get; set; }
        public PathEffect? PathEffect { get; set; }
        public BlendMode BlendMode { get; set; }
        public FilterQuality FilterQuality { get; set; }

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
