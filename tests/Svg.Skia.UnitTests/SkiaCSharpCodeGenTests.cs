using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using Svg.CodeGen.Skia;
using Svg.Model.Services;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SkiaCSharpCodeGenTests
{
    [Fact]
    public void ToStringArray_EscapesVerbatimStringQuotes()
    {
        var code = new[] { "\"Black Ops One\"" }.ToStringArray().ToString();

        Assert.Equal("new string[1] { @\"\"\"Black Ops One\"\"\",  }", code);
    }

    [Fact]
    public void ToFloatArray_EmitsValidSpecialFloatConstants()
    {
        var code = new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity }.ToFloatArray().ToString();

        Assert.Equal("new float[3] { float.NaN, float.PositiveInfinity, float.NegativeInfinity,  }", code);
    }

    [Fact]
    public void Generate_UsesNullGradientPositionsWhenShaderContainsNonFiniteStops()
    {
        var paint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(34.39571f, 97.36665f),
                4.233333f,
                new[]
                {
                    new SKColorF(0.3529412f, 0.3529412f, 0.3529412f, 1f),
                    new SKColorF(0f, 0f, 0f, 0.9960785f)
                },
                SKColorSpace.Srgb,
                new[] { float.NaN, float.NaN },
                SKShaderTileMode.Clamp,
                new SKMatrix
                {
                    ScaleX = 0.0007958741f,
                    SkewX = 0.9999996f,
                    TransX = -67.76066f,
                    SkewY = -7.999998f,
                    ScaleY = 1.9758927E-07f,
                    TransY = 376.7656f,
                    Persp2 = 1f
                })
        };
        var path = new SKPath();
        path.AddRect(SKRect.Create(0f, 0f, 10f, 10f));
        var picture = new SKPicture(SKRect.Create(0f, 0f, 10f, 10f), new List<CanvasCommand>
        {
            new DrawPathCanvasCommand(path, paint)
        });

        var code = SkiaCSharpCodeGen.Generate(picture, "Svg", "Generated");

        Assert.DoesNotContain("NaNf", code);
        Assert.Contains("SKShader.CreateRadialGradient(", code);
        Assert.Contains("    null,", code);
    }

    [Fact]
    public void Generate_DoesNotEmitNaNGradientStopsForZeroWidthGradientBounds()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <linearGradient id="g" gradientUnits="userSpaceOnUse">
                  <stop offset="0%" stop-color="#666666" />
                  <stop offset="100%" stop-color="#000000" />
                </linearGradient>
              </defs>
              <line x1="50" y1="0" x2="50" y2="100" stroke="url(#g)" stroke-width="10" />
            </svg>
            """;
        var document = SvgService.FromSvg(svgMarkup);
        var assetLoader = new SkiaSvgAssetLoader(new SkiaModel(new SKSvgSettings()));
        var picture = SvgSceneRuntime.CreateModel(document, assetLoader);

        Assert.NotNull(picture);
        var shader = Assert.Single(EnumerateShaders(picture).OfType<LinearGradientShader>());
        Assert.NotNull(shader.ColorPos);
        Assert.Equal(0f, shader.ColorPos![0]);
        Assert.Equal(1f, shader.ColorPos[1]);

        var code = SkiaCSharpCodeGen.Generate(picture!, "Svg", "Generated");

        Assert.DoesNotContain("NaN", code);
    }

    [Fact]
    public void Generate_UsesExplicitSamplingOptionsForDrawImage()
    {
        var image = new SKImage { Data = new byte[] { 1, 2, 3, 4 }, Width = 1, Height = 1 };
        var picture = new SKPicture(SKRect.Create(0f, 0f, 1f, 1f), new List<CanvasCommand>
        {
            new DrawImageCanvasCommand(
                image,
                SKRect.Create(0f, 0f, 1f, 1f),
                SKRect.Create(0f, 0f, 1f, 1f),
                null,
                new SKSamplingOptions(SKCubicResampler.CatmullRom))
        });

        var code = SkiaCSharpCodeGen.Generate(picture, "Svg", "Generated");

        Assert.Contains("new SKSamplingOptions(SKCubicResampler.CatmullRom)", code);
        Assert.DoesNotContain("FilterQuality", code);
    }

    [Fact]
    public void Generate_MapsLegacyFilterQualityToSamplingOptionsForDrawImage()
    {
        var image = new SKImage { Data = new byte[] { 1, 2, 3, 4 }, Width = 1, Height = 1 };
        var paint = new SKPaint { FilterQuality = SKFilterQuality.Medium };
        var picture = new SKPicture(SKRect.Create(0f, 0f, 1f, 1f), new List<CanvasCommand>
        {
            new DrawImageCanvasCommand(
                image,
                SKRect.Create(0f, 0f, 1f, 1f),
                SKRect.Create(0f, 0f, 1f, 1f),
                paint)
        });

        var code = SkiaCSharpCodeGen.Generate(picture, "Svg", "Generated");

        Assert.Contains("new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)", code);
        Assert.DoesNotContain(".FilterQuality", code);
    }

    [Fact]
    public void Generate_UsesTextBlobFontForPositionedText()
    {
        var font = new SKFont(null, 18f)
        {
            Edging = SKFontEdging.Alias,
            Subpixel = true
        };
        var textBlob = SKTextBlob.CreatePositioned("Text", font, new[] { new SKPoint(1f, 2f) });
        var picture = new SKPicture(SKRect.Create(0f, 0f, 10f, 10f), new List<CanvasCommand>
        {
            new DrawTextBlobCanvasCommand(textBlob, 3f, 4f, new SKPaint())
        });

        var code = SkiaCSharpCodeGen.Generate(picture, "Svg", "Generated");

        Assert.Contains("new SKFont(SKTypeface.Default, 18f, 1f, 0f)", code);
        Assert.Contains(".Edging = SKFontEdging.Alias;", code);
        Assert.Contains(".Subpixel = true;", code);
        Assert.Contains("SKTextBlob.CreatePositioned(\"Text\", skFont0", code);
        Assert.DoesNotContain(".ToFont()", code);
    }

    [Fact]
    public void Generate_UsesAliasFontEdgingForAliasedTextPaint()
    {
        var paint = new SKPaint
        {
            IsAntialias = false,
            LcdRenderText = true,
            TextSize = 18f
        };
        var picture = new SKPicture(SKRect.Create(0f, 0f, 40f, 20f), new List<CanvasCommand>
        {
            new DrawTextCanvasCommand("Text", 1f, 18f, paint)
        });

        var code = SkiaCSharpCodeGen.Generate(picture, "Svg", "Generated");

        Assert.Contains(".Edging = SKFontEdging.Alias;", code);
        Assert.DoesNotContain(".Edging = SKFontEdging.SubpixelAntialias;", code);
    }

    [Fact]
    public void Generate_UsesSpotLitSpecularShininessForFinalArgument()
    {
        var paint = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateSpotLitSpecular(
                new SKPoint3(1f, 2f, 3f),
                new SKPoint3(4f, 5f, 6f),
                7f,
                8f,
                new SKColor(9, 10, 11, 255),
                12f,
                13f,
                14f)
        };
        var path = new SKPath();
        path.AddRect(SKRect.Create(0f, 0f, 10f, 10f));
        var picture = new SKPicture(SKRect.Create(0f, 0f, 10f, 10f), new List<CanvasCommand>
        {
            new DrawPathCanvasCommand(path, paint)
        });

        var code = SkiaCSharpCodeGen.Generate(picture, "Svg", "Generated");

        Assert.Contains("SKImageFilter.CreateSpotLitSpecular(", code);
        Assert.Contains("    7f,", code);
        Assert.Contains("    14f,", code);
    }

    [Fact]
    public void SkiaModel_ToSKShader_CreatesGradientsWithOptionalColorPositions()
    {
        var model = new SkiaModel(new SKSvgSettings());
        var shaderModels = CreateGradientShaders(null)
            .Concat(CreateGradientShaders(new[] { float.NaN, float.NaN }));

        foreach (var shaderModel in shaderModels)
        {
            using var shader = model.ToSKShader(shaderModel);

            Assert.NotNull(shader);
        }
    }

    private static IEnumerable<SKShader> CreateGradientShaders(float[]? colorPos)
    {
        var colors = new[]
        {
            new SKColorF(1f, 0f, 0f, 1f),
            new SKColorF(0f, 0f, 1f, 1f)
        };

        yield return new LinearGradientShader(
            new SKPoint(0f, 0f),
            new SKPoint(10f, 0f),
            colors,
            SKColorSpace.Srgb,
            colorPos,
            SKShaderTileMode.Clamp,
            null);
        yield return new RadialGradientShader(
            new SKPoint(5f, 5f),
            5f,
            colors,
            SKColorSpace.Srgb,
            colorPos,
            SKShaderTileMode.Clamp,
            null);
        yield return new TwoPointConicalGradientShader(
            new SKPoint(2f, 2f),
            1f,
            new SKPoint(5f, 5f),
            5f,
            colors,
            SKColorSpace.Srgb,
            colorPos,
            SKShaderTileMode.Clamp,
            null);
    }

    private static IEnumerable<SKShader> EnumerateShaders(SKPicture? picture)
    {
        if (picture?.Commands is null)
        {
            yield break;
        }

        foreach (var command in picture.Commands)
        {
            switch (command)
            {
                case DrawPathCanvasCommand { Paint.Shader: { } shader }:
                    yield return shader;
                    break;
                case DrawTextBlobCanvasCommand { Paint.Shader: { } shader }:
                    yield return shader;
                    break;
                case DrawTextCanvasCommand { Paint.Shader: { } shader }:
                    yield return shader;
                    break;
                case DrawTextOnPathCanvasCommand { Paint.Shader: { } shader }:
                    yield return shader;
                    break;
                case DrawImageCanvasCommand { Paint.Shader: { } shader }:
                    yield return shader;
                    break;
                case SaveLayerCanvasCommand { Paint.Shader: { } shader }:
                    yield return shader;
                    break;
                case DrawPictureCanvasCommand { Picture: { } nestedPicture }:
                    foreach (var nestedShader in EnumerateShaders(nestedPicture))
                    {
                        yield return nestedShader;
                    }
                    break;
            }
        }
    }
}
