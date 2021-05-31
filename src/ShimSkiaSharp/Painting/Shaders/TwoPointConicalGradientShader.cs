using ShimSkiaSharp.Primitives;

namespace ShimSkiaSharp.Painting.Shaders
{
    public sealed class TwoPointConicalGradientShader : SKShader
    {
        public SKPoint Start { get; set; }
        public float StartRadius { get; set; }
        public SKPoint End { get; set; }
        public float EndRadius { get; set; }
        public SKColorF[]? Colors { get; set; }
        public SKColorSpace ColorSpace { get; set; }
        public float[]? ColorPos { get; set; }
        public SKShaderTileMode Mode { get; set; }
        public SKMatrix? LocalMatrix { get; set; }
    }
}
