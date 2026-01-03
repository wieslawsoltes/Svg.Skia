using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class CloneShaderTests
{
    [Fact]
    public void SKShader_DeepClone_ClonesColorShader()
    {
        SKShader shader = new ColorShader(new SKColor(1, 2, 3, 4), SKColorSpace.Srgb);

        var clone = shader.DeepClone();
        var typed = Assert.IsType<ColorShader>(clone);

        Assert.NotSame(shader, clone);
        Assert.Equal(new SKColor(1, 2, 3, 4), typed.Color);
        Assert.Equal(SKColorSpace.Srgb, typed.ColorSpace);
    }

    [Fact]
    public void SKShader_DeepClone_ClonesLinearGradientShader()
    {
        var shader = CloneTestData.CreateLinearGradientShader();

        var clone = shader.DeepClone();
        var typed = Assert.IsType<LinearGradientShader>(clone);

        Assert.NotSame(shader, clone);
        Assert.NotSame(shader.Colors, typed.Colors);
        Assert.NotSame(shader.ColorPos, typed.ColorPos);
        Assert.Equal(shader.Colors, typed.Colors);
        Assert.Equal(shader.ColorPos, typed.ColorPos);
        Assert.Equal(shader.Start, typed.Start);
        Assert.Equal(shader.End, typed.End);
        Assert.Equal(shader.ColorSpace, typed.ColorSpace);
        Assert.Equal(shader.Mode, typed.Mode);
        Assert.Equal(shader.LocalMatrix, typed.LocalMatrix);
    }

    [Fact]
    public void SKShader_DeepClone_ClonesPerlinNoiseFractal()
    {
        SKShader shader = new PerlinNoiseFractalNoiseShader(1f, 2f, 3, 4f, new SKPointI(5, 6));

        var clone = shader.DeepClone();
        var typed = Assert.IsType<PerlinNoiseFractalNoiseShader>(clone);

        Assert.NotSame(shader, clone);
        Assert.Equal(1f, typed.BaseFrequencyX);
        Assert.Equal(2f, typed.BaseFrequencyY);
        Assert.Equal(3, typed.NumOctaves);
        Assert.Equal(4f, typed.Seed);
        Assert.Equal(new SKPointI(5, 6), typed.TileSize);
    }

    [Fact]
    public void SKShader_DeepClone_ClonesPerlinNoiseTurbulence()
    {
        SKShader shader = new PerlinNoiseTurbulenceShader(1f, 2f, 3, 4f, new SKPointI(5, 6));

        var clone = shader.DeepClone();
        var typed = Assert.IsType<PerlinNoiseTurbulenceShader>(clone);

        Assert.NotSame(shader, clone);
        Assert.Equal(1f, typed.BaseFrequencyX);
        Assert.Equal(2f, typed.BaseFrequencyY);
        Assert.Equal(3, typed.NumOctaves);
        Assert.Equal(4f, typed.Seed);
        Assert.Equal(new SKPointI(5, 6), typed.TileSize);
    }

    [Fact]
    public void SKShader_DeepClone_ClonesPictureShader()
    {
        var picture = CloneTestData.CreatePicture();
        var shader = new PictureShader(picture, SKShaderTileMode.Clamp, SKShaderTileMode.Repeat, SKMatrix.CreateTranslation(1, 2), SKRect.Create(0, 0, 10, 10));

        var clone = shader.DeepClone();
        var typed = Assert.IsType<PictureShader>(clone);

        Assert.NotSame(shader, clone);
        Assert.NotSame(picture, typed.Src);
        Assert.NotSame(picture.Commands, typed.Src!.Commands);
        Assert.Equal(picture.CullRect, typed.Src.CullRect);
        Assert.Equal(shader.TmX, typed.TmX);
        Assert.Equal(shader.TmY, typed.TmY);
        Assert.Equal(shader.Tile, typed.Tile);
        Assert.Equal(shader.LocalMatrix, typed.LocalMatrix);
    }

    [Fact]
    public void SKShader_DeepClone_ClonesRadialGradientShader()
    {
        var shader = new RadialGradientShader(
            new SKPoint(1, 2),
            3f,
            new[] { new SKColorF(1f, 0f, 0f, 1f), new SKColorF(0f, 1f, 0f, 1f) },
            SKColorSpace.Srgb,
            new[] { 0f, 1f },
            SKShaderTileMode.Mirror,
            SKMatrix.CreateScale(2, 3));

        var clone = shader.DeepClone();
        var typed = Assert.IsType<RadialGradientShader>(clone);

        Assert.NotSame(shader, clone);
        Assert.NotSame(shader.Colors, typed.Colors);
        Assert.NotSame(shader.ColorPos, typed.ColorPos);
        Assert.Equal(shader.Colors, typed.Colors);
        Assert.Equal(shader.ColorPos, typed.ColorPos);
        Assert.Equal(shader.Center, typed.Center);
        Assert.Equal(shader.Radius, typed.Radius);
        Assert.Equal(shader.ColorSpace, typed.ColorSpace);
        Assert.Equal(shader.Mode, typed.Mode);
        Assert.Equal(shader.LocalMatrix, typed.LocalMatrix);
    }

    [Fact]
    public void SKShader_DeepClone_ClonesTwoPointConicalGradientShader()
    {
        var shader = new TwoPointConicalGradientShader(
            new SKPoint(1, 2),
            3f,
            new SKPoint(4, 5),
            6f,
            new[] { new SKColorF(1f, 0f, 0f, 1f), new SKColorF(0f, 1f, 0f, 1f) },
            SKColorSpace.Srgb,
            new[] { 0f, 1f },
            SKShaderTileMode.Repeat,
            SKMatrix.CreateTranslation(2, 3));

        var clone = shader.DeepClone();
        var typed = Assert.IsType<TwoPointConicalGradientShader>(clone);

        Assert.NotSame(shader, clone);
        Assert.NotSame(shader.Colors, typed.Colors);
        Assert.NotSame(shader.ColorPos, typed.ColorPos);
        Assert.Equal(shader.Colors, typed.Colors);
        Assert.Equal(shader.ColorPos, typed.ColorPos);
        Assert.Equal(shader.Start, typed.Start);
        Assert.Equal(shader.StartRadius, typed.StartRadius);
        Assert.Equal(shader.End, typed.End);
        Assert.Equal(shader.EndRadius, typed.EndRadius);
        Assert.Equal(shader.ColorSpace, typed.ColorSpace);
        Assert.Equal(shader.Mode, typed.Mode);
        Assert.Equal(shader.LocalMatrix, typed.LocalMatrix);
    }
}
