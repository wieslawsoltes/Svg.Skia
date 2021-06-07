namespace ShimSkiaSharp.Painting
{
    public abstract record SKColorFilter
    {
        public static SKColorFilter CreateColorMatrix(float[] matrix) 
            => new ColorMatrixColorFilter(matrix);

        public static SKColorFilter CreateTable(byte[]? tableA, byte[]? tableR, byte[]? tableG, byte[]? tableB) 
            => new TableColorFilter(tableA, tableB, tableG, tableR);

        public static SKColorFilter CreateBlendMode(SKColor c, SKBlendMode mode) 
            => new BlendModeColorFilter(c, mode);

        public static SKColorFilter CreateLumaColor() 
            => new LumaColorColorFilter();
    }

    public record BlendModeColorFilter(SKColor Color, SKBlendMode Mode) : SKColorFilter;

    public record ColorMatrixColorFilter(float[]? Matrix) : SKColorFilter;

    public record LumaColorColorFilter() : SKColorFilter;

    public record TableColorFilter(byte[]? TableA, byte[]? TableR, byte[]? TableG, byte[]? TableB) : SKColorFilter;
}
