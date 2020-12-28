using Svg.Model.Painting;
using Svg.Model.Primitives;

namespace Svg.Model.Shaders
{
    public sealed class PictureShader : Shader
    {
        public Picture.Picture? Src { get; set; }
        public ShaderTileMode TmX { get; set; }
        public ShaderTileMode TmY { get; set; }
        public Matrix LocalMatrix { get; set; }
        public Rect Tile { get; set; }
    }
}
