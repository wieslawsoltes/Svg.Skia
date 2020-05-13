namespace Svg.Model
{
    public class MatrixConvolutionImageFilter : ImageFilter
    {
        public SizeI KernelSize { get; set; }
        public float[]? Kernel { get; set; }
        public float Gain { get; set; }
        public float Bias { get; set; }
        public PointI KernelOffset { get; set; }
        public MatrixConvolutionTileMode TileMode { get; set; }
        public bool ConvolveAlpha { get; set; }
        public ImageFilter? Input { get; set; }
        public CropRect? CropRect { get; set; }
    }
}
