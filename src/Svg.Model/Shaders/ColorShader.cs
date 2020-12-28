using Svg.Model.Paint;

namespace Svg.Model.Shaders
{
    public sealed class ColorShader : Shader
    {
        public Color Color { get; set; }
        public ColorSpace ColorSpace { get; set; }
    }
}
