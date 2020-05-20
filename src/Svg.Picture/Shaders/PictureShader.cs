namespace Svg.Picture
{
    public class PictureShader : Shader
    {
        public Picture? Src;
        public ShaderTileMode TmX;
        public ShaderTileMode TmY;
        public Matrix LocalMatrix;
        public Rect Tile;
    }
}
