using System;

namespace Svg.Picture
{
    public abstract class Shader : IDisposable
    {
        public void Dispose()
        {
        }

        public static Shader CreateColor(Color color)
        {
            return new ColorShader()
            {
                Color = color
            };
        }

        public static Shader CreateLinearGradient(Point start, Point end, Color[] colors, float[] colorPos, ShaderTileMode mode)
        {
            return new LinearGradientShader()
            {
                Start = start,
                End = end,
                Colors = colors,
                ColorPos = colorPos,
                Mode = mode
            };
        }

        public static Shader CreateLinearGradient(Point start, Point end, Color[] colors, float[] colorPos, ShaderTileMode mode, Matrix localMatrix)
        {
            return new LinearGradientShader()
            {
                Start = start,
                End = end,
                Colors = colors,
                ColorPos = colorPos,
                Mode = mode,
                LocalMatrix = localMatrix
            };
        }

        public static Shader CreatePerlinNoiseFractalNoise(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, PointI tileSize)
        {
            return new PerlinNoiseFractalNoiseShader()
            {
                BaseFrequencyX = baseFrequencyX,
                BaseFrequencyY = baseFrequencyY,
                NumOctaves = numOctaves,
                Seed = seed,
                TileSize = tileSize
            };
        }

        public static Shader CreatePerlinNoiseTurbulence(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, PointI tileSize)
        {
            return new PerlinNoiseTurbulenceShader()
            {
                BaseFrequencyX = baseFrequencyX,
                BaseFrequencyY = baseFrequencyY,
                NumOctaves = numOctaves,
                Seed = seed,
                TileSize = tileSize
            };
        }

        public static Shader CreatePicture(Picture src, ShaderTileMode tmx, ShaderTileMode tmy, Matrix localMatrix, Rect tile)
        {
            return new PictureShader()
            {
                Src = src,
                TmX = tmx,
                TmY = tmy,
                LocalMatrix = localMatrix,
                Tile = tile
            };
        }

        public static Shader CreateTwoPointConicalGradient(Point start, float startRadius, Point end, float endRadius, Color[] colors, float[] colorPos, ShaderTileMode mode)
        {
            return new TwoPointConicalGradientShader()
            {
                Start = start,
                StartRadius = startRadius,
                End = end,
                EndRadius = endRadius,
                Colors = colors,
                ColorPos = colorPos,
                Mode = mode
            };
        }

        public static Shader CreateTwoPointConicalGradient(Point start, float startRadius, Point end, float endRadius, Color[] colors, float[] colorPos, ShaderTileMode mode, Matrix localMatrix)
        {
            return new TwoPointConicalGradientShader()
            {
                Start = start,
                StartRadius = startRadius,
                End = end,
                EndRadius = endRadius,
                Colors = colors,
                ColorPos = colorPos,
                Mode = mode,
                LocalMatrix = localMatrix
            };
        }
    }
}
