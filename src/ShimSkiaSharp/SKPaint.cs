namespace ShimSkiaSharp
{
    public sealed class SKPaint
    {
        public SKPaintStyle Style { get; set; }

        public bool IsAntialias { get; set; }

        public float StrokeWidth { get; set; }

        public SKStrokeCap StrokeCap { get; set; }

        public SKStrokeJoin StrokeJoin { get; set; }

        public float StrokeMiter { get; set; }

        public SKTypeface? Typeface { get; set; }

        public float TextSize { get; set; }

        public SKTextAlign TextAlign { get; set; }

        public bool LcdRenderText { get; set; }

        public bool SubpixelText { get; set; }

        public SKTextEncoding TextEncoding { get; set; }

        public SKColor? Color { get; set; }

        public SKShader? Shader { get; set; }

        public SKColorFilter? ColorFilter { get; set; }

        public SKImageFilter? ImageFilter { get; set; }

        public SKPathEffect? PathEffect { get; set; }

        public SKBlendMode BlendMode { get; set; }

        public SKFilterQuality FilterQuality { get; set; }

        public SKPaint()
        {
            Style = SKPaintStyle.Fill;
            IsAntialias = false;
            StrokeWidth = 0;
            StrokeCap = SKStrokeCap.Butt;
            StrokeJoin = SKStrokeJoin.Miter;
            StrokeMiter = 4;
            Typeface = null;
            TextSize = 12;
            TextAlign = SKTextAlign.Left;
            LcdRenderText = false;
            SubpixelText = false;
            TextEncoding = SKTextEncoding.Utf8;
            Color = new SKColor(0x00, 0x00, 0x00, 0xFF);
            Shader = null;
            ColorFilter = null;
            ImageFilter = null;
            PathEffect = null;
            BlendMode = SKBlendMode.SrcOver;
            FilterQuality = SKFilterQuality.None;
        }
    }
}
