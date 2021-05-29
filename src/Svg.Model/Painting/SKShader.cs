using Svg.Model.Painting.Shaders;
using Svg.Model.Primitives;

namespace Svg.Model.Painting
{
    public abstract class SKShader
    {
        public static SKShader CreateColor(SKColor color, SKColorSpace colorSpace)
        {
            return new ColorShader
            {
                Color = color,
                ColorSpace = colorSpace
            };
        }

        public static SKShader CreateLinearGradient(SKPoint start, SKPoint end, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode)
        {
            return new LinearGradientShader
            {
                Start = start,
                End = end,
                Colors = colors,
                ColorSpace = colorSpace,
                ColorPos = colorPos,
                Mode = mode
            };
        }

        public static SKShader CreateLinearGradient(SKPoint start, SKPoint end, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode, SKMatrix localMatrix)
        {
            return new LinearGradientShader
            {
                Start = start,
                End = end,
                Colors = colors,
                ColorSpace = colorSpace,
                ColorPos = colorPos,
                Mode = mode,
                LocalMatrix = localMatrix
            };
        }

        public static SKShader CreatePerlinNoiseFractalNoise(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, SKPointI tileSize)
        {
            return new PerlinNoiseFractalNoiseShader
            {
                BaseFrequencyX = baseFrequencyX,
                BaseFrequencyY = baseFrequencyY,
                NumOctaves = numOctaves,
                Seed = seed,
                TileSize = tileSize
            };
        }

        public static SKShader CreatePerlinNoiseTurbulence(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, SKPointI tileSize)
        {
            return new PerlinNoiseTurbulenceShader
            {
                BaseFrequencyX = baseFrequencyX,
                BaseFrequencyY = baseFrequencyY,
                NumOctaves = numOctaves,
                Seed = seed,
                TileSize = tileSize
            };
        }

        public static SKShader CreatePicture(SKPicture src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKMatrix localMatrix, SKRect tile)
        {
            return new PictureShader
            {
                Src = src,
                TmX = tmx,
                TmY = tmy,
                LocalMatrix = localMatrix,
                Tile = tile
            };
        }

        public static SKShader CreateTwoPointConicalGradient(SKPoint start, float startRadius, SKPoint end, float endRadius, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode)
        {
            return new TwoPointConicalGradientShader
            {
                Start = start,
                StartRadius = startRadius,
                End = end,
                EndRadius = endRadius,
                Colors = colors,
                ColorSpace = colorSpace,
                ColorPos = colorPos,
                Mode = mode
            };
        }

        public static SKShader CreateTwoPointConicalGradient(SKPoint start, float startRadius, SKPoint end, float endRadius, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode, SKMatrix localMatrix)
        {
            return new TwoPointConicalGradientShader
            {
                Start = start,
                StartRadius = startRadius,
                End = end,
                EndRadius = endRadius,
                Colors = colors,
                ColorSpace = colorSpace,
                ColorPos = colorPos,
                Mode = mode,
                LocalMatrix = localMatrix
            };
        }
    }
}
