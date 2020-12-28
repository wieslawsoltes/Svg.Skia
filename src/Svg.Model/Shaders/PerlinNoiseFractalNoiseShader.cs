namespace Svg.Model
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
