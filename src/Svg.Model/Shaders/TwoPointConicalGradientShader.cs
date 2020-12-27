namespace Svg.Model
{
    public sealed class TwoPointConicalGradientShader : Shader
    {
        public Point Start { get; set; }
        public float StartRadius { get; set; }
        public Point End { get; set; }
        public float EndRadius { get; set; }
        public ColorF[]? Colors { get; set; }
        public ColorSpace ColorSpace { get; set; }
        public float[]? ColorPos { get; set; }
        public ShaderTileMode Mode { get; set; }
        public Matrix? LocalMatrix { get; set; }
    }
}
