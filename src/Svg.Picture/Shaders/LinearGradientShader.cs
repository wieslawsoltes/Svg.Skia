namespace Svg.Picture
{
    public class LinearGradientShader : Shader
    {
        public Point Start;
        public Point End;
        public Color[]? Colors;
        public float[]? ColorPos;
        public ShaderTileMode Mode;
        public Matrix? LocalMatrix;
    }
}
