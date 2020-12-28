using Svg.Model.Primitives;
using Svg.Model.Painting.Shaders;

namespace Svg.Model.Painting.ImageFilters
{
    public sealed class MatrixConvolutionImageFilter : ImageFilter
    {
        public SizeI KernelSize { get; set; }
        public float[]? Kernel { get; set; }
        public float Gain { get; set; }
        public float Bias { get; set; }
        public PointI KernelOffset { get; set; }
        public ShaderTileMode TileMode { get; set; }
        public bool ConvolveAlpha { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
