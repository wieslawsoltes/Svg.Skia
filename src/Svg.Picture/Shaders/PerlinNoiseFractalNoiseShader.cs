namespace Svg.Picture
{
    public class PerlinNoiseFractalNoiseShader : Shader
    {
        public float BaseFrequencyX;
        public float BaseFrequencyY;
        public int NumOctaves;
        public float Seed;
        public PointI TileSize;
    }
}
