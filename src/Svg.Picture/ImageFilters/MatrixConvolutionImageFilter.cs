namespace Svg.Picture
{
    public class MatrixConvolutionImageFilter : ImageFilter
    {
        public SizeI KernelSize;
        public float[]? Kernel;
        public float Gain;
        public float Bias;
        public PointI KernelOffset;
        public MatrixConvolutionTileMode TileMode;
        public bool ConvolveAlpha;
        public ImageFilter? Input;
        public CropRect? CropRect;
    }
}
