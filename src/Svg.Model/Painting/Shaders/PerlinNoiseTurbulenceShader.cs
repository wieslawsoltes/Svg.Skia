using Svg.Model.Primitives;

namespace Svg.Model.Painting.Shaders
{
    public sealed class PerlinNoiseTurbulenceShader : SKShader
    {
        public float BaseFrequencyX { get; set; }
        public float BaseFrequencyY { get; set; }
        public int NumOctaves { get; set; }
        public float Seed { get; set; }
        public SKPointI TileSize { get; set; }
    }
}
