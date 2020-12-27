namespace Svg.Model
{
    public sealed class PictureShader : Shader
    {
        public Picture? Src { get; set; }
        public ShaderTileMode TmX { get; set; }
        public ShaderTileMode TmY { get; set; }
        public Matrix LocalMatrix { get; set; }
        public Rect Tile { get; set; }
    }
}
