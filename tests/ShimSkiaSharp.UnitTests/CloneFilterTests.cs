using ShimSkiaSharp;
using Xunit;

namespace ShimSkiaSharp.UnitTests;

public class CloneFilterTests
{
    [Fact]
    public void SKColorFilter_DeepClone_ClonesBlendMode()
    {
        SKColorFilter filter = new BlendModeColorFilter(new SKColor(1, 2, 3, 4), SKBlendMode.Src);

        var clone = filter.DeepClone();
        var typed = Assert.IsType<BlendModeColorFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.Equal(new SKColor(1, 2, 3, 4), typed.Color);
        Assert.Equal(SKBlendMode.Src, typed.Mode);
    }

    [Fact]
    public void SKColorFilter_DeepClone_ClonesColorMatrix()
    {
        var matrix = new float[] { 1f, 0f, 0.5f, 1f };
        SKColorFilter filter = new ColorMatrixColorFilter(matrix);

        var clone = filter.DeepClone();
        var typed = Assert.IsType<ColorMatrixColorFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(matrix, typed.Matrix);
        Assert.Equal(matrix, typed.Matrix);
    }

    [Fact]
    public void SKColorFilter_DeepClone_ClonesLuma()
    {
        SKColorFilter filter = new LumaColorColorFilter();

        var clone = filter.DeepClone();

        Assert.NotSame(filter, clone);
        Assert.IsType<LumaColorColorFilter>(clone);
    }

    [Fact]
    public void SKColorFilter_DeepClone_ClonesTable()
    {
        var tableA = new byte[] { 1, 2 };
        var tableR = new byte[] { 3, 4 };
        var tableG = new byte[] { 5, 6 };
        var tableB = new byte[] { 7, 8 };
        SKColorFilter filter = new TableColorFilter(tableA, tableR, tableG, tableB);

        var clone = filter.DeepClone();
        var typed = Assert.IsType<TableColorFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(tableA, typed.TableA);
        Assert.NotSame(tableR, typed.TableR);
        Assert.NotSame(tableG, typed.TableG);
        Assert.NotSame(tableB, typed.TableB);
        Assert.Equal(tableA, typed.TableA);
        Assert.Equal(tableR, typed.TableR);
        Assert.Equal(tableG, typed.TableG);
        Assert.Equal(tableB, typed.TableB);
    }

    [Fact]
    public void SKPathEffect_DeepClone_ClonesDash()
    {
        var intervals = new float[] { 1f, 2f };
        SKPathEffect effect = new DashPathEffect(intervals, 0.5f);

        var clone = effect.DeepClone();
        var typed = Assert.IsType<DashPathEffect>(clone);

        Assert.NotSame(effect, clone);
        Assert.NotSame(intervals, typed.Intervals);
        Assert.Equal(intervals, typed.Intervals);
        Assert.Equal(0.5f, typed.Phase);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesArithmetic()
    {
        var background = CloneTestData.CreateLeafImageFilter();
        var foreground = CloneTestData.CreateLeafImageFilter();
        var clip = SKRect.Create(1, 2, 3, 4);
        var filter = SKImageFilter.CreateArithmetic(1f, 2f, 3f, 4f, true, background, foreground, clip);

        var clone = filter.DeepClone();
        var typed = Assert.IsType<ArithmeticImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(background, typed.Background);
        Assert.NotSame(foreground, typed.Foreground);
        Assert.Equal(clip, typed.Clip);
        Assert.Equal(1f, typed.K1);
        Assert.Equal(2f, typed.K2);
        Assert.Equal(3f, typed.K3);
        Assert.Equal(4f, typed.K4);
        Assert.True(typed.EforcePMColor);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesBlendMode()
    {
        var background = CloneTestData.CreateLeafImageFilter();
        var foreground = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateBlendMode(SKBlendMode.SrcOver, background, foreground, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<BlendModeImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(background, typed.Background);
        Assert.NotSame(foreground, typed.Foreground);
        Assert.Equal(SKBlendMode.SrcOver, typed.Mode);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesBlur()
    {
        var input = CloneTestData.CreateLeafImageFilter();
        var clip = SKRect.Create(1, 2, 3, 4);
        var filter = SKImageFilter.CreateBlur(1f, 2f, input, clip);

        var clone = filter.DeepClone();
        var typed = Assert.IsType<BlurImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(1f, typed.SigmaX);
        Assert.Equal(2f, typed.SigmaY);
        Assert.Equal(clip, typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesColorFilter()
    {
        var colorFilter = CloneTestData.CreateColorMatrixFilter();
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateColorFilter(colorFilter, input, SKRect.Create(2, 3, 4, 5));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<ColorFilterImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(colorFilter, typed.ColorFilter);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(SKRect.Create(2, 3, 4, 5), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesDilate()
    {
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateDilate(2, 3, input, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<DilateImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(2, typed.RadiusX);
        Assert.Equal(3, typed.RadiusY);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesDisplacementMap()
    {
        var displacement = CloneTestData.CreateLeafImageFilter();
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateDisplacementMapEffect(SKColorChannel.A, SKColorChannel.B, 2f, displacement, input, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<DisplacementMapEffectImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(displacement, typed.Displacement);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(SKColorChannel.A, typed.XChannelSelector);
        Assert.Equal(SKColorChannel.B, typed.YChannelSelector);
        Assert.Equal(2f, typed.Scale);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesDistantLitDiffuse()
    {
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateDistantLitDiffuse(new SKPoint3(1, 2, 3), new SKColor(1, 2, 3, 4), 1f, 2f, input, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<DistantLitDiffuseImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(new SKPoint3(1, 2, 3), typed.Direction);
        Assert.Equal(new SKColor(1, 2, 3, 4), typed.LightColor);
        Assert.Equal(1f, typed.SurfaceScale);
        Assert.Equal(2f, typed.Kd);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesDistantLitSpecular()
    {
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateDistantLitSpecular(new SKPoint3(1, 2, 3), new SKColor(1, 2, 3, 4), 1f, 2f, 3f, input, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<DistantLitSpecularImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(new SKPoint3(1, 2, 3), typed.Direction);
        Assert.Equal(new SKColor(1, 2, 3, 4), typed.LightColor);
        Assert.Equal(1f, typed.SurfaceScale);
        Assert.Equal(2f, typed.Ks);
        Assert.Equal(3f, typed.Shininess);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesErode()
    {
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateErode(2, 3, input, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<ErodeImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(2, typed.RadiusX);
        Assert.Equal(3, typed.RadiusY);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesImage()
    {
        var image = CloneTestData.CreateImage();
        var filter = SKImageFilter.CreateImage(image, SKRect.Create(0, 0, 10, 10), SKRect.Create(1, 1, 2, 2), SKFilterQuality.Medium);

        var clone = filter.DeepClone();
        var typed = Assert.IsType<ImageImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(image, typed.Image);
        Assert.NotSame(image.Data, typed.Image!.Data);
        Assert.Equal(image.Width, typed.Image.Width);
        Assert.Equal(image.Height, typed.Image.Height);
        Assert.Equal(SKRect.Create(0, 0, 10, 10), typed.Src);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Dst);
        Assert.Equal(SKFilterQuality.Medium, typed.FilterQuality);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesMatrixConvolution()
    {
        var kernel = new float[] { 1f, 0f, 0f, 1f };
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateMatrixConvolution(new SKSizeI(2, 2), kernel, 1f, 2f, new SKPointI(1, 1), SKShaderTileMode.Clamp, true, input, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<MatrixConvolutionImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(kernel, typed.Kernel);
        Assert.Equal(kernel, typed.Kernel);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(new SKSizeI(2, 2), typed.KernelSize);
        Assert.Equal(1f, typed.Gain);
        Assert.Equal(2f, typed.Bias);
        Assert.Equal(new SKPointI(1, 1), typed.KernelOffset);
        Assert.Equal(SKShaderTileMode.Clamp, typed.TileMode);
        Assert.True(typed.ConvolveAlpha);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesMerge()
    {
        var filters = new[] { CloneTestData.CreateLeafImageFilter(), CloneTestData.CreateLeafImageFilter() };
        var filter = SKImageFilter.CreateMerge(filters, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<MergeImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(filters, typed.Filters);
        Assert.NotSame(filters[0], typed.Filters![0]);
        Assert.NotSame(filters[1], typed.Filters[1]);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesOffset()
    {
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateOffset(3f, 4f, input, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<OffsetImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(3f, typed.Dx);
        Assert.Equal(4f, typed.Dy);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesPaint()
    {
        var paint = CloneTestData.CreatePaint();
        var filter = SKImageFilter.CreatePaint(paint, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<PaintImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(paint, typed.Paint);
        Assert.NotSame(paint.Typeface, typed.Paint!.Typeface);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesShader()
    {
        var shader = CloneTestData.CreateLinearGradientShader();
        var filter = SKImageFilter.CreateShader(shader, true, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<ShaderImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(shader, typed.Shader);
        Assert.True(typed.Dither);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesPicture()
    {
        var picture = CloneTestData.CreatePicture();
        var filter = SKImageFilter.CreatePicture(picture, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<PictureImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(picture, typed.Picture);
        Assert.NotSame(picture.Commands, typed.Picture!.Commands);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesPointLitDiffuse()
    {
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreatePointLitDiffuse(new SKPoint3(1, 2, 3), new SKColor(1, 2, 3, 4), 1f, 2f, input, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<PointLitDiffuseImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(new SKPoint3(1, 2, 3), typed.Location);
        Assert.Equal(new SKColor(1, 2, 3, 4), typed.LightColor);
        Assert.Equal(1f, typed.SurfaceScale);
        Assert.Equal(2f, typed.Kd);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesPointLitSpecular()
    {
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreatePointLitSpecular(new SKPoint3(1, 2, 3), new SKColor(1, 2, 3, 4), 1f, 2f, 3f, input, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<PointLitSpecularImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(new SKPoint3(1, 2, 3), typed.Location);
        Assert.Equal(new SKColor(1, 2, 3, 4), typed.LightColor);
        Assert.Equal(1f, typed.SurfaceScale);
        Assert.Equal(2f, typed.Ks);
        Assert.Equal(3f, typed.Shininess);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesSpotLitDiffuse()
    {
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateSpotLitDiffuse(new SKPoint3(1, 2, 3), new SKPoint3(4, 5, 6), 1f, 2f, new SKColor(1, 2, 3, 4), 3f, 4f, input, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<SpotLitDiffuseImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(1f, typed.SpecularExponent);
        Assert.Equal(2f, typed.CutoffAngle);
        Assert.Equal(new SKPoint3(1, 2, 3), typed.Location);
        Assert.Equal(new SKPoint3(4, 5, 6), typed.Target);
        Assert.Equal(new SKColor(1, 2, 3, 4), typed.LightColor);
        Assert.Equal(3f, typed.SurfaceScale);
        Assert.Equal(4f, typed.Kd);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesSpotLitSpecular()
    {
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateSpotLitSpecular(new SKPoint3(1, 2, 3), new SKPoint3(4, 5, 6), 1f, 2f, new SKColor(1, 2, 3, 4), 3f, 4f, 5f, input, SKRect.Create(1, 1, 2, 2));

        var clone = filter.DeepClone();
        var typed = Assert.IsType<SpotLitSpecularImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(1f, typed.SpecularExponent);
        Assert.Equal(2f, typed.CutoffAngle);
        Assert.Equal(new SKPoint3(1, 2, 3), typed.Location);
        Assert.Equal(new SKPoint3(4, 5, 6), typed.Target);
        Assert.Equal(new SKColor(1, 2, 3, 4), typed.LightColor);
        Assert.Equal(3f, typed.SurfaceScale);
        Assert.Equal(4f, typed.Ks);
        Assert.Equal(5f, typed.Shininess);
        Assert.Equal(SKRect.Create(1, 1, 2, 2), typed.Clip);
    }

    [Fact]
    public void SKImageFilter_DeepClone_ClonesTile()
    {
        var input = CloneTestData.CreateLeafImageFilter();
        var filter = SKImageFilter.CreateTile(SKRect.Create(1, 2, 3, 4), SKRect.Create(5, 6, 7, 8), input);

        var clone = filter.DeepClone();
        var typed = Assert.IsType<TileImageFilter>(clone);

        Assert.NotSame(filter, clone);
        Assert.NotSame(input, typed.Input);
        Assert.Equal(SKRect.Create(1, 2, 3, 4), typed.Src);
        Assert.Equal(SKRect.Create(5, 6, 7, 8), typed.Dst);
    }
}
