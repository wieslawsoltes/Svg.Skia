namespace Svg.Picture
{
    public class LinearGradientShader : Shader
    {
        public Point Start { get; set; }
        public Point End { get; set; }
        public Color[]? Colors { get; set; }
        public float[]? ColorPos { get; set; }
        public ShaderTileMode Mode { get; set; }
        public Matrix? LocalMatrix { get; set; }
    }
}
