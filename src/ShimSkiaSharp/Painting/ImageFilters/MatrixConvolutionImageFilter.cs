using ShimSkiaSharp.Painting.Shaders;
using ShimSkiaSharp.Primitives;

namespace ShimSkiaSharp.Painting.ImageFilters
{
    public sealed class MatrixConvolutionImageFilter : SKImageFilter
    {
        public SKSizeI KernelSize { get; set; }
        public float[]? Kernel { get; set; }
        public float Gain { get; set; }
        public float Bias { get; set; }
        public SKPointI KernelOffset { get; set; }
        public SKShaderTileMode TileMode { get; set; }
        public bool ConvolveAlpha { get; set; }
        public SKImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
