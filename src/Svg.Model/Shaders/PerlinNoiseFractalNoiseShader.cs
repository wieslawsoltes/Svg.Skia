using Svg.Model.Painting;
using Svg.Model.Primitives;

namespace Svg.Model.Shaders
{
    public sealed class PerlinNoiseFractalNoiseShader : Shader
    {
        public float BaseFrequencyX { get; set; }
        public float BaseFrequencyY { get; set; }
        public int NumOctaves { get; set; }
        public float Seed { get; set; }
        public PointI TileSize { get; set; }
    }
}
