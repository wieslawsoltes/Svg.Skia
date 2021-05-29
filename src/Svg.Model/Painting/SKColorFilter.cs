using Svg.Model.Painting.ColorFilters;

namespace Svg.Model.Painting
{
    public abstract class SKColorFilter
    {
        public static SKColorFilter CreateColorMatrix(float[] matrix)
        {
            return new ColorMatrixColorFilter
            {
                Matrix = matrix
            };
        }

        public static SKColorFilter CreateTable(byte[]? tableA, byte[]? tableR, byte[]? tableG, byte[]? tableB)
        {
            return new TableColorFilter
            {
                TableA = tableA,
                TableB = tableB,
                TableG = tableG,
                TableR = tableR
            };
        }

        public static SKColorFilter CreateBlendMode(SKColor c, SKBlendMode mode)
        {
            return new BlendModeColorFilter
            {
                Color = c,
                Mode = mode
            };
        }

        public static SKColorFilter CreateLumaColor()
        {
            return new LumaColorColorFilter
            {
            };
        }
    }
}
