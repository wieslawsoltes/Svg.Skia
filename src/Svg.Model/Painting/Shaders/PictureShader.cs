using Svg.Model.Primitives;

namespace Svg.Model.Painting.Shaders
{
    public sealed class PictureShader : SKShader
    {
        public SKPicture? Src { get; set; }
        public SKShaderTileMode TmX { get; set; }
        public SKShaderTileMode TmY { get; set; }
        public SKMatrix LocalMatrix { get; set; }
        public SKRect Tile { get; set; }
    }
}
