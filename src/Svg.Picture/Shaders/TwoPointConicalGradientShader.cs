namespace Svg.Picture
{
    public class TwoPointConicalGradientShader : Shader
    {
        public Point Start;
        public float StartRadius;
        public Point End;
        public float EndRadius;
        public Color[]? Colors;
        public float[]? ColorPos;
        public ShaderTileMode Mode;
        public Matrix? LocalMatrix;
    }
}
