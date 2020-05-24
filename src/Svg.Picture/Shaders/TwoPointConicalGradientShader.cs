namespace Svg.Picture
{
    public class TwoPointConicalGradientShader : Shader
    {
        public Point Start { get; set; }
        public float StartRadius { get; set; }
        public Point End { get; set; }
        public float EndRadius { get; set; }
        public Color[]? Colors { get; set; }
        public float[]? ColorPos { get; set; }
        public ShaderTileMode Mode { get; set; }
        public Matrix? LocalMatrix { get; set; }
    }
}
