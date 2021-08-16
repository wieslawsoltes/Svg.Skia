namespace ShimSkiaSharp
{
    public abstract record SKShader
    {
        public static SKShader CreateColor(SKColor color, SKColorSpace colorSpace) 
            => new ColorShader(color, colorSpace);

        public static SKShader CreateLinearGradient(SKPoint start, SKPoint end, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode) 
            => new LinearGradientShader(start, end, colors, colorSpace, colorPos, mode, null);

        public static SKShader CreateLinearGradient(SKPoint start, SKPoint end, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode, SKMatrix localMatrix) 
            => new LinearGradientShader(start, end, colors, colorSpace, colorPos, mode, localMatrix);

        public static SKShader CreatePerlinNoiseFractalNoise(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, SKPointI tileSize) 
            => new PerlinNoiseFractalNoiseShader(baseFrequencyX, baseFrequencyY, numOctaves, seed, tileSize);

        public static SKShader CreatePerlinNoiseTurbulence(float baseFrequencyX, float baseFrequencyY, int numOctaves, float seed, SKPointI tileSize) 
            => new PerlinNoiseTurbulenceShader(baseFrequencyX, baseFrequencyY, numOctaves, seed, tileSize);

        public static SKShader CreatePicture(SKPicture src, SKShaderTileMode tmx, SKShaderTileMode tmy, SKMatrix localMatrix, SKRect tile) 
            => new PictureShader(src, tmx, tmy, localMatrix, tile);

        public static SKShader CreateRadialGradient(SKPoint center, float radius, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode) 
            => new RadialGradientShader(center, radius, colors, colorSpace, colorPos, mode, null);

        public static SKShader CreateRadialGradient(SKPoint center, float radius, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode, SKMatrix localMatrix) 
            => new RadialGradientShader(center, radius, colors, colorSpace, colorPos, mode, localMatrix);
        
        public static SKShader CreateTwoPointConicalGradient(SKPoint start, float startRadius, SKPoint end, float endRadius, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode) 
            => new TwoPointConicalGradientShader(start, startRadius, end, endRadius, colors, colorSpace, colorPos, mode, null);

        public static SKShader CreateTwoPointConicalGradient(SKPoint start, float startRadius, SKPoint end, float endRadius, SKColorF[] colors, SKColorSpace colorSpace, float[] colorPos, SKShaderTileMode mode, SKMatrix localMatrix) 
            => new TwoPointConicalGradientShader(start, startRadius, end, endRadius, colors, colorSpace, colorPos, mode, localMatrix);
    }

    public record ColorShader(SKColor Color, SKColorSpace ColorSpace) : SKShader;

    public record LinearGradientShader(SKPoint Start, SKPoint End, SKColorF[]? Colors, SKColorSpace ColorSpace, float[]? ColorPos, SKShaderTileMode Mode, SKMatrix? LocalMatrix) : SKShader;

    public record PerlinNoiseFractalNoiseShader(float BaseFrequencyX, float BaseFrequencyY, int NumOctaves, float Seed, SKPointI TileSize) : SKShader;

    public record PerlinNoiseTurbulenceShader(float BaseFrequencyX, float BaseFrequencyY, int NumOctaves, float Seed, SKPointI TileSize) : SKShader;

    public record PictureShader(SKPicture? Src, SKShaderTileMode TmX, SKShaderTileMode TmY, SKMatrix LocalMatrix, SKRect Tile) : SKShader;

    public record RadialGradientShader(SKPoint Center, float Radius, SKColorF[]? Colors, SKColorSpace ColorSpace, float[]? ColorPos, SKShaderTileMode Mode, SKMatrix? LocalMatrix) : SKShader;

    public record TwoPointConicalGradientShader(SKPoint Start, float StartRadius, SKPoint End, float EndRadius, SKColorF[]? Colors, SKColorSpace ColorSpace, float[]? ColorPos, SKShaderTileMode Mode, SKMatrix? LocalMatrix) : SKShader;
}
