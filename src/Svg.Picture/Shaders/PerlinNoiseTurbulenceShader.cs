namespace Svg.Picture
{
    public class PerlinNoiseTurbulenceShader : Shader
    {
        public float BaseFrequencyX;
        public float BaseFrequencyY;
        public int NumOctaves;
        public float Seed;
        public PointI TileSize;
    }
}
