namespace Svg.Picture
{
    public sealed class LinearGradientShader : Shader
    {
        public Point Start { get; set; }
        public Point End { get; set; }
        public ColorF[]? Colors { get; set; }
        public ColorSpace ColorSpace { get; set; }
        public float[]? ColorPos { get; set; }
        public ShaderTileMode Mode { get; set; }
        public Matrix? LocalMatrix { get; set; }
    }
}
