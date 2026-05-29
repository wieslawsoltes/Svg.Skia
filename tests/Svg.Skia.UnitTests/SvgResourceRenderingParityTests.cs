using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Svg;
using Svg.FilterEffects;
using Svg.Model;
using Svg.Model.Services;
using Xunit;
using SkiaAlphaType = SkiaSharp.SKAlphaType;
using SkiaBitmap = SkiaSharp.SKBitmap;
using SkiaColor = SkiaSharp.SKColor;
using SkiaColors = SkiaSharp.SKColors;
using SkiaColorType = SkiaSharp.SKColorType;

namespace Svg.Skia.UnitTests;

public class SvgResourceRenderingParityTests
{
    [Fact]
    public void RetainedSceneGraph_PreservesOutOfCircleRadialGradientFocalPoint()
    {
        const string radialGradientSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
              <defs>
                <radialGradient id="grad"
                                gradientUnits="userSpaceOnUse"
                                cx="40"
                                cy="40"
                                r="20"
                                fx="100"
                                fy="40">
                  <stop offset="0" stop-color="red" />
                  <stop offset="1" stop-color="blue" />
                </radialGradient>
              </defs>
              <rect id="target" width="80" height="80" fill="url(#grad)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(radialGradientSvg);

        var command = Assert.Single(
            svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("target"),
            static command => command.Paint?.Style == SKPaintStyle.Fill);
        var shader = Assert.IsType<TwoPointConicalGradientShader>(command.Paint!.Shader);

        AssertApproximately(100f, shader.Start.X);
        AssertApproximately(40f, shader.Start.Y);
        AssertApproximately(0f, shader.StartRadius);
        AssertApproximately(40f, shader.End.X);
        AssertApproximately(40f, shader.End.Y);
        AssertApproximately(20f, shader.EndRadius);
    }

    [Fact]
    public void RetainedSceneGraph_BreaksNonStartGradientHrefCycles()
    {
        const string gradientCycleSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="20">
              <defs>
                <linearGradient id="a" href="#b" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="80" y2="0" />
                <linearGradient id="b" href="#c" />
                <linearGradient id="c" href="#b">
                  <stop offset="0" stop-color="red" />
                  <stop offset="1" stop-color="blue" />
                </linearGradient>
              </defs>
              <rect id="target" width="80" height="20" fill="url(#a)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(gradientCycleSvg);

        var command = Assert.Single(
            svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("target"),
            static command => command.Paint?.Style == SKPaintStyle.Fill);
        Assert.IsType<LinearGradientShader>(command.Paint!.Shader);
    }

    [Fact]
    public void RetainedSceneGraph_BreaksNonStartPatternHrefCycles()
    {
        const string patternCycleSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32">
              <defs>
                <pattern id="a" href="#b" width="8" height="8" patternUnits="userSpaceOnUse" />
                <pattern id="b" href="#c" />
                <pattern id="c" href="#b">
                  <rect width="8" height="8" fill="#00ff00" />
                </pattern>
              </defs>
              <rect id="target" width="32" height="32" fill="url(#a)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(patternCycleSvg);

        var command = Assert.Single(
            svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("target"),
            static command => command.Paint?.Style == SKPaintStyle.Fill);
        Assert.IsType<PictureShader>(command.Paint!.Shader);
    }

    [Fact]
    public void RetainedSceneGraph_RecursivePatternUsesPaintServerFallback()
    {
        const string recursivePatternSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <pattern id="self" width="10" height="10" patternUnits="userSpaceOnUse">
                  <rect width="10" height="10" fill="url(#self) #00ff00" />
                </pattern>
              </defs>
              <rect id="target" width="20" height="20" fill="url(#self) #ff0000" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(recursivePatternSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);
        var pixel = bitmap.GetPixel(5, 5);

        Assert.True(
            pixel.Green > 200 && pixel.Red < 40 && pixel.Blue < 40 && pixel.Alpha > 200,
            $"Expected recursive pattern fallback to render green but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_NonFiniteColorMatrixFallsBackToIdentity()
    {
        const string colorMatrixSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <filter id="f" color-interpolation-filters="sRGB">
                  <feColorMatrix type="matrix" values="1 0 0 0 NaN 0 1 0 0 0 0 0 1 0 0 0 0 0 1 0" />
                </filter>
              </defs>
              <rect id="target" width="20" height="20" fill="#123456" filter="url(#f)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(colorMatrixSvg);

        var colorFilter = AssertFilter<ColorFilterImageFilter>(svg, "target");
        var colorMatrix = Assert.IsType<ColorMatrixColorFilter>(colorFilter.ColorFilter);
        Assert.Equal(CreateIdentityColorMatrix(), colorMatrix.Matrix);
    }

    [Theory]
    [InlineData("""
        <feGaussianBlur stdDeviation="NaN" />
        """, typeof(BlurImageFilter))]
    [InlineData("""
        <feGaussianBlur stdDeviation="-1" />
        """, typeof(BlurImageFilter))]
    [InlineData("""
        <feMorphology radius="NaN" operator="dilate" />
        """, typeof(DilateImageFilter))]
    [InlineData("""
        <feMorphology radius="Infinity" operator="erode" />
        """, typeof(ErodeImageFilter))]
    [InlineData("""
        <feMorphology radius="-1" operator="dilate" />
        """, typeof(DilateImageFilter))]
    [InlineData("""
        <feDisplacementMap in2="SourceGraphic" scale="Infinity" />
        """, typeof(DisplacementMapEffectImageFilter))]
    [InlineData("""
        <feTurbulence baseFrequency="NaN" />
        """, typeof(PaintImageFilter))]
    public void RetainedSceneGraph_RejectsInvalidFilterNumbers(string primitiveMarkup, Type rejectedFilterType)
    {
        var filterSvg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <filter id="f" color-interpolation-filters="sRGB">
                  {{primitiveMarkup}}
                </filter>
              </defs>
              <rect id="target" width="20" height="20" fill="#123456" filter="url(#f)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(filterSvg);

        Assert.False(ContainsImageFilter(svg, "target", rejectedFilterType));
    }

    [Fact]
    public void RetainedSceneGraph_RejectsProgrammaticNonFiniteOffset()
    {
        const string offsetSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <filter id="f" color-interpolation-filters="sRGB">
                  <feOffset id="offset-node" dx="1" dy="0" />
                </filter>
              </defs>
              <rect id="target" width="20" height="20" fill="#123456" filter="url(#f)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(offsetSvg);
        var offset = Assert.IsType<SvgOffset>(svg.SourceDocument!.GetElementById("offset-node"));
        offset.Dx = new SvgUnit(float.NaN);

        svg.RefreshFromSourceDocument();

        Assert.False(ContainsImageFilter(svg, "target", typeof(OffsetImageFilter)));
    }

    [Fact]
    public void RetainedSceneGraph_RejectsProgrammaticNonFiniteDropShadowOffset()
    {
        const string dropShadowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <filter id="f" color-interpolation-filters="sRGB">
                  <feDropShadow id="drop-shadow-node" dx="1" dy="0" stdDeviation="1" />
                </filter>
              </defs>
              <rect id="target" width="20" height="20" fill="#123456" filter="url(#f)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(dropShadowSvg);
        var dropShadow = Assert.IsType<SvgDropShadow>(svg.SourceDocument!.GetElementById("drop-shadow-node"));
        dropShadow.Dx = new SvgUnit(float.PositiveInfinity);

        svg.RefreshFromSourceDocument();

        Assert.False(ContainsImageFilter(svg, "target", typeof(MergeImageFilter)));
    }

    [Fact]
    public void RetainedSceneGraph_ObjectBoundingBoxPrimitiveUnitsScaleOffsets()
    {
        const string objectBoundingBoxOffsetSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="60">
              <defs>
                <filter id="f" x="0" y="0" width="1" height="1"
                        filterUnits="objectBoundingBox"
                        primitiveUnits="objectBoundingBox"
                        color-interpolation-filters="sRGB">
                  <feOffset dx=".25" dy=".5" />
                </filter>
              </defs>
              <rect id="target" x="10" y="20" width="40" height="20" fill="#ff0000" filter="url(#f)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(objectBoundingBoxOffsetSvg);

        var offset = AssertFilter<OffsetImageFilter>(svg, "target");
        AssertApproximately(10f, offset.Dx);
        AssertApproximately(10f, offset.Dy);
    }

    [Fact]
    public void RetainedSceneGraph_FeImageZeroSizeDecodeReturnsTransparentInput()
    {
        const string invalidImageSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="20" height="20" color-interpolation-filters="sRGB">
                  <feImage href="data:image/png;base64,AQIDBA==" result="img" />
                  <feMerge>
                    <feMergeNode in="img" />
                    <feMergeNode in="SourceGraphic" />
                  </feMerge>
                </filter>
              </defs>
              <rect id="target" width="20" height="20" fill="#ff0000" filter="url(#f)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(invalidImageSvg);

        AssertFilter<MergeImageFilter>(svg, "target");
        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void RetainedSceneGraph_NonAxisFeImageUsesGlobalFilterLayer()
    {
        const string nonAxisFeImageSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
              <defs>
                <filter id="f" x="0" y="0" width="1" height="1">
                  <feImage href="#source" />
                </filter>
                <rect id="source" x="-60" y="0" width="120" height="120" fill="#00ff00" />
              </defs>
              <rect id="target" x="40" y="40" width="120" height="120" fill="#ff0000"
                    filter="url(#f)" transform="skewX(50) translate(-90)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(nonAxisFeImageSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.True(node!.FilterUsesGlobalLayer);
        Assert.NotNull(node.FilterGlobalClip);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        AssertMostlyGreen(
            bitmap.GetPixel(80, 150),
            "Expected inputless feImage output to extend through the global filter region instead of clipping to the skewed source shape.");
    }

    [Fact]
    public void RetainedSceneGraph_FeImageMissingExternalResourceProducesTransparentPrimitive()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var sourcePath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(sourcePath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
                  <defs>
                    <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="20" height="20" color-interpolation-filters="sRGB">
                      <feImage href="missing.png" result="img" />
                      <feMerge>
                        <feMergeNode in="img" />
                        <feMergeNode in="SourceGraphic" />
                      </feMerge>
                    </filter>
                  </defs>
                  <rect id="target" width="20" height="20" fill="#ff0000" filter="url(#f)" />
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Load(sourcePath);

            AssertFilter<MergeImageFilter>(svg, "target");
            Assert.NotNull(svg.Picture);
            using var bitmap = ToBitmap(svg, svg.Picture!);
            AssertMostlyRed(bitmap.GetPixel(10, 10), "Expected missing external feImage to behave as transparent black.");
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void RetainedSceneGraph_FeImageBlockedExternalResourceProducesTransparentPrimitive()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var imagePath = Path.Combine(tempDirectory.FullName, "blocked.png");
            File.WriteAllBytes(imagePath, CreateSolidPng(SkiaColors.Lime));

            var sourcePath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(sourcePath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
                  <defs>
                    <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="20" height="20" color-interpolation-filters="sRGB">
                      <feImage href="blocked.png" result="img" />
                      <feMerge>
                        <feMergeNode in="img" />
                        <feMergeNode in="SourceGraphic" />
                      </feMerge>
                    </filter>
                  </defs>
                  <rect id="target" width="20" height="20" fill="#ff0000" filter="url(#f)" />
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Load(
                sourcePath,
                new SvgParameters(
                    null,
                    null,
                    null,
                    new SvgDocumentLoadOptions
                    {
                        ExternalResources = SvgExternalResourcePolicy.SameDocumentAndDataOnly
                    }));

            AssertFilter<MergeImageFilter>(svg, "target");
            Assert.NotNull(svg.Picture);
            using var bitmap = ToBitmap(svg, svg.Picture!);
            AssertMostlyRed(bitmap.GetPixel(10, 10), "Expected policy-blocked external feImage to behave as transparent black.");
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void RetainedSceneGraph_FeImageExternalSvgLoadsNestedRaster()
    {
        var previousResolveExternalImages = SvgDocument.ResolveExternalImages;
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            SvgDocument.ResolveExternalImages = ExternalType.Local | ExternalType.Remote;
            var imagePath = Path.Combine(tempDirectory.FullName, "nested.png");
            File.WriteAllBytes(imagePath, CreateSolidPng(SkiaColors.Lime));

            var nestedSvgPath = Path.Combine(tempDirectory.FullName, "nested.svg");
            File.WriteAllText(nestedSvgPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <image href="nested.png" width="16" height="16" />
                </svg>
                """);

            var sourcePath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(sourcePath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
                  <defs>
                    <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="20" height="20" color-interpolation-filters="sRGB">
                      <feImage href="nested.svg" />
                    </filter>
                  </defs>
                  <rect id="target" width="20" height="20" fill="#ff0000" filter="url(#f)" />
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Load(sourcePath);

            Assert.NotNull(svg.Picture);
            using var bitmap = ToBitmap(svg, svg.Picture!);
            AssertMostlyGreen(bitmap.GetPixel(10, 10), "Expected external SVG feImage to render its nested raster image.");
        }
        finally
        {
            SvgDocument.ResolveExternalImages = previousResolveExternalImages;
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void RetainedSceneGraph_NestedImplicitSvgImageUsesImageViewport()
    {
        var previousResolveExternalImages = SvgDocument.ResolveExternalImages;
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            SvgDocument.ResolveExternalImages = ExternalType.Local | ExternalType.Remote;

            var level2Path = Path.Combine(tempDirectory.FullName, "level2.svg");
            File.WriteAllText(level2Path, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <rect width="100%" height="100%" fill="#00ff00" />
                </svg>
                """);

            var level1Path = Path.Combine(tempDirectory.FullName, "level1.svg");
            File.WriteAllText(level1Path, """
                <svg xmlns="http://www.w3.org/2000/svg">
                  <image href="level2.svg" width="100%" height="100%" />
                </svg>
                """);

            var sourcePath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(sourcePath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
                  <image href="level1.svg" width="20" height="20" />
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Load(sourcePath);

            Assert.NotNull(svg.Picture);
            using var bitmap = ToBitmap(svg, svg.Picture!);
            AssertMostlyGreen(bitmap.GetPixel(10, 10), "Expected nested implicit SVG image to resolve percentage dimensions against the image viewport.");
        }
        finally
        {
            SvgDocument.ResolveExternalImages = previousResolveExternalImages;
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void RetainedSceneGraph_FeImageExternalSvgCycleProducesTransparentNestedInput()
    {
        var previousResolveExternalImages = SvgDocument.ResolveExternalImages;
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            SvgDocument.ResolveExternalImages = ExternalType.Local | ExternalType.Remote;

            var sourcePath = Path.Combine(tempDirectory.FullName, "source.svg");
            var childPath = Path.Combine(tempDirectory.FullName, "child.svg");

            File.WriteAllText(childPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
                  <defs>
                    <filter id="child-filter" filterUnits="userSpaceOnUse" x="0" y="0" width="20" height="20" color-interpolation-filters="sRGB">
                      <feImage href="source.svg" result="loop" />
                      <feFlood flood-color="#00ff00" result="green" />
                      <feMerge>
                        <feMergeNode in="loop" />
                        <feMergeNode in="green" />
                      </feMerge>
                    </filter>
                  </defs>
                  <rect width="20" height="20" fill="#ff0000" filter="url(#child-filter)" />
                </svg>
                """);

            File.WriteAllText(sourcePath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
                  <defs>
                    <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="20" height="20" color-interpolation-filters="sRGB">
                      <feImage href="child.svg" />
                    </filter>
                  </defs>
                  <rect id="target" width="20" height="20" fill="#ff0000" filter="url(#f)" />
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Load(sourcePath);

            Assert.NotNull(svg.Picture);
            using var bitmap = ToBitmap(svg, svg.Picture!);
            AssertMostlyGreen(bitmap.GetPixel(10, 10), "Expected recursive external feImage edge to be transparent while sibling filter content renders.");
        }
        finally
        {
            SvgDocument.ResolveExternalImages = previousResolveExternalImages;
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void RetainedSceneGraph_FeImageCachesRepeatedExternalRasterInSingleFilter()
    {
        var previousResolveExternalImages = SvgDocument.ResolveExternalImages;
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            SvgDocument.ResolveExternalImages = ExternalType.Local | ExternalType.Remote;
            var imagePath = Path.Combine(tempDirectory.FullName, "shared.png");
            File.WriteAllBytes(imagePath, CreateSolidPng(SkiaColors.Lime));

            var sourcePath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(sourcePath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
                  <defs>
                    <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="20" height="20" color-interpolation-filters="sRGB">
                      <feImage href="shared.png" result="first" />
                      <feImage href="shared.png" result="second" />
                      <feMerge>
                        <feMergeNode in="first" />
                        <feMergeNode in="second" />
                      </feMerge>
                    </filter>
                  </defs>
                  <rect id="target" width="20" height="20" fill="#ff0000" filter="url(#f)" />
                </svg>
                """);

            var document = SvgService.Open(sourcePath);
            var assetLoader = new CountingImageAssetLoader();

            Assert.True(SvgSceneRuntime.TryCompile(document, assetLoader, DrawAttributes.None, out var sceneDocument));
            Assert.NotNull(sceneDocument);
            Assert.Equal(1, assetLoader.LoadImageCallCount);
        }
        finally
        {
            SvgDocument.ResolveExternalImages = previousResolveExternalImages;
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void RetainedSceneGraph_ClipPathUseSuppressesReferencedMarkers()
    {
        const string clipMarkerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
              <defs>
                <marker id="m" markerWidth="20" markerHeight="20" refX="10" refY="10" markerUnits="userSpaceOnUse">
                  <rect width="20" height="20" fill="black" />
                </marker>
                <path id="clip-shape" d="M30 30H50V50H30Z" marker-start="url(#m)" />
                <clipPath id="clip">
                  <use href="#clip-shape" />
                </clipPath>
              </defs>
              <rect id="target" width="80" height="80" fill="#ff0000" clip-path="url(#clip)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(clipMarkerSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        Assert.Equal(new SkiaColor(0x00, 0x00, 0x00, 0x00), bitmap.GetPixel(22, 22));
        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(35, 35));
    }

    [Fact]
    public void RetainedSceneGraph_ObjectBoundingBoxFilterCanReadBackgroundImage()
    {
        const string backgroundImageSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="48" height="32">
              <defs>
                <filter id="f" x="0" y="0" width="1" height="1" filterUnits="objectBoundingBox" color-interpolation-filters="sRGB">
                  <feOffset in="BackgroundImage" dx="0" dy="0" />
                </filter>
              </defs>
              <g enable-background="new">
                <rect x="18" y="8" width="12" height="12" fill="#00ff00" />
                <rect id="target" x="18" y="8" width="12" height="12" fill="#ff0000" filter="url(#f)" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(backgroundImageSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        var pixel = bitmap.GetPixel(22, 12);

        Assert.True(
            pixel.Green > 200 && pixel.Red < 40 && pixel.Blue < 40 && pixel.Alpha > 200,
            $"Expected objectBoundingBox BackgroundImage to render the retained background but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_ObjectBoundingBoxFilterCanReadBackgroundAlpha()
    {
        const string backgroundAlphaSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="48" height="32">
              <defs>
                <filter id="f" x="0" y="0" width="1" height="1" filterUnits="objectBoundingBox" color-interpolation-filters="sRGB">
                  <feFlood flood-color="#0000ff" result="blue" />
                  <feComposite in="blue" in2="BackgroundAlpha" operator="in" />
                </filter>
              </defs>
              <g enable-background="new">
                <rect x="18" y="8" width="12" height="12" fill="#00ff00" />
                <rect id="target" x="18" y="8" width="12" height="12" fill="#ff0000" filter="url(#f)" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(backgroundAlphaSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        var pixel = bitmap.GetPixel(22, 12);

        Assert.True(
            pixel.Blue > 200 && pixel.Red < 40 && pixel.Green < 40 && pixel.Alpha > 200,
            $"Expected objectBoundingBox BackgroundAlpha to mask the flood but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_EnableBackgroundUsesCascadedStyle()
    {
        const string backgroundStyleSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="48" height="32">
              <defs>
                <filter id="f" x="0" y="0" width="1" height="1" filterUnits="objectBoundingBox" color-interpolation-filters="sRGB">
                  <feOffset in="BackgroundImage" dx="0" dy="0" />
                </filter>
              </defs>
              <g style="enable-background:new">
                <rect x="18" y="8" width="12" height="12" fill="#00ff00" />
                <rect id="target" x="18" y="8" width="12" height="12" fill="#ff0000" filter="url(#f)" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(backgroundStyleSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        var pixel = bitmap.GetPixel(22, 12);

        Assert.True(
            pixel.Green > 200 && pixel.Red < 40 && pixel.Blue < 40 && pixel.Alpha > 200,
            $"Expected cascaded enable-background to create a background layer but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_InvalidEnableBackgroundClipDoesNotCreateLayer()
    {
        const string invalidBackgroundSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="48" height="32">
              <defs>
                <filter id="f" x="0" y="0" width="48" height="32" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                  <feOffset in="BackgroundImage" dx="18" dy="0" />
                </filter>
              </defs>
              <g enable-background="new NaN 0 48 32">
                <rect x="0" y="8" width="12" height="12" fill="#00ff00" />
                <rect id="target" x="18" y="8" width="12" height="12" fill="#ff0000" filter="url(#f)" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(invalidBackgroundSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        var pixel = bitmap.GetPixel(22, 12);

        Assert.True(
            pixel.Alpha < 20,
            $"Expected invalid enable-background clip to keep BackgroundImage transparent but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_FeMergeDefaultRegionUsesMergeNodeUnion()
    {
        const string mergeRegionSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="24">
              <defs>
                <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="64" height="24" color-interpolation-filters="sRGB">
                  <feFlood x="10" y="4" width="8" height="8" flood-color="#00ff00" result="left" />
                  <feFlood x="32" y="8" width="12" height="8" flood-color="#0000ff" result="right" />
                  <feMerge>
                    <feMergeNode in="left" />
                    <feMergeNode in="right" />
                  </feMerge>
                </filter>
              </defs>
              <rect id="target" width="64" height="24" fill="#ff0000" filter="url(#f)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(mergeRegionSvg);

        var merge = AssertFilter<MergeImageFilter>(svg, "target");
        Assert.NotNull(merge.Clip);
        AssertRectApproximately(SKRect.Create(10f, 4f, 34f, 12f), merge.Clip!.Value);
    }

    [Fact]
    public void RetainedSceneGraph_PrimitiveRegionClipsSourceInputAndOutput()
    {
        const string clippedBlurSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="48" height="24">
              <defs>
                <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="48" height="24" color-interpolation-filters="sRGB">
                  <feGaussianBlur x="20" y="4" width="12" height="12" stdDeviation="2" />
                </filter>
              </defs>
              <rect id="target" x="0" y="0" width="48" height="24" fill="#ff0000" filter="url(#f)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(clippedBlurSvg);

        var blur = AssertFilter<BlurImageFilter>(svg, "target");
        var expectedRegion = SKRect.Create(20f, 4f, 12f, 12f);
        Assert.NotNull(blur.Clip);
        AssertRectApproximately(expectedRegion, blur.Clip!.Value);

        var inputCrop = Assert.IsType<ColorFilterImageFilter>(blur.Input);
        Assert.NotNull(inputCrop.Clip);
        AssertRectApproximately(expectedRegion, inputCrop.Clip!.Value);
    }

    [Fact]
    public void RetainedSceneGraph_DefaultPrimitiveRegionKeepsUnspecifiedEdgesFromInput()
    {
        const string mixedDefaultRegionSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="24">
              <defs>
                <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="64" height="24" color-interpolation-filters="sRGB">
                  <feFlood x="10" y="4" width="12" height="8" flood-color="#00ff00" result="left" />
                  <feOffset in="left" y="10" height="6" dx="0" dy="0" />
                </filter>
              </defs>
              <rect id="target" width="64" height="24" fill="#ff0000" filter="url(#f)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(mixedDefaultRegionSvg);

        var offset = AssertFilter<OffsetImageFilter>(svg, "target");
        Assert.NotNull(offset.Clip);
        AssertRectApproximately(SKRect.Create(10f, 10f, 12f, 6f), offset.Clip!.Value);
    }

    [Fact]
    public void RetainedSceneGraph_PrimitiveRegionClipsBackgroundImageInput()
    {
        const string clippedBackgroundSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="48" height="24">
              <defs>
                <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="48" height="24" color-interpolation-filters="sRGB">
                  <feOffset in="BackgroundImage" x="20" y="4" width="12" height="12" dx="0" dy="0" />
                </filter>
              </defs>
              <g enable-background="new">
                <rect width="48" height="24" fill="#00ff00" />
                <rect id="target" width="48" height="24" fill="#ff0000" filter="url(#f)" />
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(clippedBackgroundSvg);

        var offset = AssertFilter<OffsetImageFilter>(svg, "target");
        var expectedRegion = SKRect.Create(20f, 4f, 12f, 12f);
        Assert.NotNull(offset.Clip);
        AssertRectApproximately(expectedRegion, offset.Clip!.Value);

        var inputCrop = Assert.IsType<ColorFilterImageFilter>(offset.Input);
        Assert.NotNull(inputCrop.Clip);
        AssertRectApproximately(expectedRegion, inputCrop.Clip!.Value);
    }

    [Fact]
    public void RetainedSceneGraph_BackgroundImageStopsAtCurrentBackgroundLayer()
    {
        const string backgroundImageStackSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="48" height="24">
              <defs>
                <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="48" height="24" color-interpolation-filters="sRGB">
                  <feOffset in="BackgroundImage" dx="24" dy="0" />
                </filter>
              </defs>
              <g enable-background="new">
                <rect x="0" y="4" width="12" height="12" fill="#00ff00" />
                <g id="target" enable-background="new" filter="url(#f)">
                  <rect x="24" y="4" width="12" height="12" fill="#ff0000" />
                </g>
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(backgroundImageStackSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        var pixel = bitmap.GetPixel(28, 8);

        Assert.True(
            pixel.Alpha < 20,
            $"Expected the current enable-background layer to hide the parent BackgroundImage stack but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_BackgroundAlphaStopsAtCurrentBackgroundLayer()
    {
        const string backgroundAlphaStackSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="48" height="24">
              <defs>
                <filter id="f" filterUnits="userSpaceOnUse" x="0" y="0" width="48" height="24" color-interpolation-filters="sRGB">
                  <feOffset in="BackgroundAlpha" dx="24" dy="0" result="alpha" />
                  <feFlood flood-color="#0000ff" result="blue" />
                  <feComposite in="blue" in2="alpha" operator="in" />
                </filter>
              </defs>
              <g enable-background="new">
                <rect x="0" y="4" width="12" height="12" fill="#00ff00" />
                <g id="target" enable-background="new" filter="url(#f)">
                  <rect x="24" y="4" width="12" height="12" fill="#ff0000" />
                </g>
              </g>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(backgroundAlphaStackSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        var pixel = bitmap.GetPixel(28, 8);

        Assert.True(
            pixel.Alpha < 20,
            $"Expected the current enable-background layer to hide the parent BackgroundAlpha stack but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterFunctionsComposeInOrder()
    {
        const string cssFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32">
              <rect id="target" x="8" y="8" width="16" height="16" fill="#204060"
                    style="filter: brightness(200%) opacity(50%)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        var opacity = Assert.IsType<ColorFilterImageFilter>(node.Filter!.ImageFilter);
        var brightness = Assert.IsType<ColorFilterImageFilter>(opacity.Input);
        var opacityMatrix = Assert.IsType<ColorMatrixColorFilter>(opacity.ColorFilter).Matrix;
        var brightnessMatrix = Assert.IsType<ColorMatrixColorFilter>(brightness.ColorFilter).Matrix;

        Assert.NotNull(opacityMatrix);
        Assert.NotNull(brightnessMatrix);
        AssertApproximately(0.5f, opacityMatrix![18]);
        AssertApproximately(2f, brightnessMatrix![0]);
        AssertApproximately(2f, brightnessMatrix[6]);
        AssertApproximately(2f, brightnessMatrix[12]);
    }

    [Fact]
    public void RetainedSceneGraph_CssBlurFilterInflatesClip()
    {
        const string cssBlurSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <rect id="target" x="10" y="10" width="10" height="10" fill="#ff0000" style="filter: blur(2px)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssBlurSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        var blur = Assert.IsType<BlurImageFilter>(node.Filter!.ImageFilter);
        AssertApproximately(2f, blur.SigmaX);
        AssertApproximately(2f, blur.SigmaY);

        Assert.NotNull(node.FilterClip);
        Assert.True(node.FilterClip!.Value.Left < 10f);
        Assert.True(node.FilterClip.Value.Top < 10f);
        Assert.True(node.FilterClip.Value.Right > 20f);
        Assert.True(node.FilterClip.Value.Bottom > 20f);
    }

    [Fact]
    public void RetainedSceneGraph_CssDropShadowMergesShadowAndSource()
    {
        const string cssDropShadowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <rect id="target" x="10" y="10" width="10" height="10" fill="#ff0000"
                    style="filter: drop-shadow(4px 3px 2px #0000ff80)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssDropShadowSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        var merge = Assert.IsType<MergeImageFilter>(node.Filter!.ImageFilter);
        Assert.NotNull(merge.Filters);
        Assert.Equal(2, merge.Filters!.Length);
        var shadow = Assert.IsType<ColorFilterImageFilter>(merge.Filters[0]);
        Assert.IsType<ColorFilterImageFilter>(merge.Filters[1]);
        var shadowMatrix = Assert.IsType<ColorMatrixColorFilter>(shadow.ColorFilter).Matrix;
        Assert.NotNull(shadowMatrix);
        AssertApproximately(0x80 / 255f, shadowMatrix![18]);

        Assert.NotNull(node.FilterClip);
        Assert.True(node.FilterClip!.Value.Right > 24f);
        Assert.True(node.FilterClip.Value.Bottom > 23f);
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterComposesUrlAndFunctionChain()
    {
        const string cssUrlFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <defs>
                <filter id="f" x="0" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                  <feFlood flood-color="#00ff00" />
                </filter>
              </defs>
              <rect id="target" width="24" height="24" fill="#ff0000" style="filter: url(#f) opacity(50%)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssUrlFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        var opacity = Assert.IsType<ColorFilterImageFilter>(node.Filter!.ImageFilter);
        var opacityMatrix = Assert.IsType<ColorMatrixColorFilter>(opacity.ColorFilter).Matrix;
        Assert.NotNull(opacityMatrix);
        AssertApproximately(0.5f, opacityMatrix![18]);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        var pixel = bitmap.GetPixel(12, 12);
        Assert.True(
            pixel.Green > 120 && pixel.Red < 20 && pixel.Blue < 20 && pixel.Alpha is > 110 and < 150,
            $"Expected url filter result to be post-processed by CSS opacity but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterComposesFunctionBeforeUrl()
    {
        const string cssPreUrlFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <defs>
                <filter id="f" x="0" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                  <feGaussianBlur stdDeviation="1" />
                </filter>
              </defs>
              <rect id="target" width="24" height="24" fill="#ff0000" style="filter: opacity(50%) url(#f)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssPreUrlFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        var blur = Assert.IsType<BlurImageFilter>(node.Filter!.ImageFilter);
        var opacity = Assert.IsType<ColorFilterImageFilter>(blur.Input);
        var opacityMatrix = Assert.IsType<ColorMatrixColorFilter>(opacity.ColorFilter).Matrix;
        Assert.NotNull(opacityMatrix);
        AssertApproximately(0.5f, opacityMatrix![18]);
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterComposesMultipleUrls()
    {
        const string cssMultiUrlFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <defs>
                <filter id="blur" x="0" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                  <feGaussianBlur stdDeviation="1" />
                </filter>
                <filter id="opacity" x="0" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                  <feColorMatrix type="matrix" values="1 0 0 0 0  0 1 0 0 0  0 0 1 0 0  0 0 0 0.5 0" />
                </filter>
              </defs>
              <rect id="target" width="24" height="24" fill="#ff0000" filter="url(#blur) url(#opacity)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssMultiUrlFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        var opacity = Assert.IsType<ColorFilterImageFilter>(node.Filter!.ImageFilter);
        Assert.True(
            ContainsImageFilter(opacity.Input, typeof(BlurImageFilter)),
            "Expected the second URL filter to consume the first URL filter result as SourceGraphic.");
    }

    [Fact]
    public void RetainedSceneGraph_InvalidCssFilterDeclarationDoesNotOverrideEarlierValidFilter()
    {
        const string invalidCssFilterFallbackSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <defs>
                <filter id="valid" x="0" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                  <feFlood flood-color="#00ff00" />
                </filter>
              </defs>
              <rect id="target" width="24" height="24" fill="#ff0000" style="filter: url(#valid); filter: blur(-1px)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(invalidCssFilterFallbackSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        var pixel = bitmap.GetPixel(12, 12);
        Assert.True(
            pixel.Green > 200 && pixel.Red < 40 && pixel.Blue < 40 && pixel.Alpha > 200,
            $"Expected invalid CSS filter declaration to be ignored in favor of the earlier valid filter but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_MissingOnlyFilterUrlIsNoOp()
    {
        const string missingOnlyFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <rect width="24" height="24" fill="#00ff00" />
              <rect id="target" width="24" height="24" fill="#ff0000" filter="url(#missing)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(missingOnlyFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.False(node!.SuppressSubtreeRendering);
        Assert.Null(node.Filter);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(12, 12));
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterParsesPhysicalAndFontRelativeLengths()
    {
        const string cssLengthFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="32">
              <rect id="physical" x="4" y="4" width="16" height="16" fill="#ff0000" style="filter: blur(1mm)" />
              <rect id="fontRelative" x="36" y="4" width="16" height="16" fill="#00ff00" font-size="20"
                    style="filter: blur(0.5em)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssLengthFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("physical", out var physicalNode));
        Assert.True(scene.TryGetNodeById("fontRelative", out var fontRelativeNode));

        var physicalBlur = Assert.IsType<BlurImageFilter>(physicalNode!.Filter!.ImageFilter);
        var fontRelativeBlur = Assert.IsType<BlurImageFilter>(fontRelativeNode!.Filter!.ImageFilter);

        AssertApproximately(96f / 25.4f, physicalBlur.SigmaX, tolerance: 0.01f);
        AssertApproximately(10f, fontRelativeBlur.SigmaX);
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterResolvesVarAndCalcExpressions()
    {
        const string cssExpressionFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="32">
              <rect id="target" x="4" y="4" width="16" height="16" fill="#ff0000"
                    style="--blur: 2px; --alpha: 50%; filter: blur(calc(1px + var(--blur))) opacity(calc(var(--alpha) + 25%))" />
              <rect id="shadow" x="36" y="4" width="16" height="16" fill="#00ff00"
                    style="--shadow-color: #0000ff80; filter: drop-shadow(calc(1px + 1px) calc(2px * 2) calc(6px / 3) var(--shadow-color))" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssExpressionFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var targetNode));
        Assert.True(scene.TryGetNodeById("shadow", out var shadowNode));

        var opacity = Assert.IsType<ColorFilterImageFilter>(targetNode!.Filter!.ImageFilter);
        var blur = Assert.IsType<BlurImageFilter>(opacity.Input);
        var opacityMatrix = Assert.IsType<ColorMatrixColorFilter>(opacity.ColorFilter).Matrix;
        Assert.NotNull(opacityMatrix);

        AssertApproximately(3f, blur.SigmaX);
        AssertApproximately(0.75f, opacityMatrix![18]);

        var merge = Assert.IsType<MergeImageFilter>(shadowNode!.Filter!.ImageFilter);
        var shadow = Assert.IsType<ColorFilterImageFilter>(merge.Filters![0]);
        var offset = Assert.IsType<OffsetImageFilter>(shadow.Input);
        var shadowBlur = Assert.IsType<BlurImageFilter>(offset.Input);
        var shadowMatrix = Assert.IsType<ColorMatrixColorFilter>(shadow.ColorFilter).Matrix;

        AssertApproximately(2f, offset.Dx);
        AssertApproximately(4f, offset.Dy);
        AssertApproximately(2f, shadowBlur.SigmaX);
        AssertApproximately(0x80 / 255f, shadowMatrix![18]);
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterEvaluatesMathFunctions()
    {
        const string cssMathFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="96" height="32">
              <rect id="lengthMath" x="4" y="4" width="16" height="16" fill="#ff0000"
                    style="filter: blur(min(5px, max(2px, clamp(1px, 3px, 4px))))" />
              <rect id="factorMath" x="36" y="4" width="16" height="16" fill="#00ff00"
                    style="filter: opacity(clamp(25%, max(75%, 60%), 100%))" />
              <rect id="shadowMath" x="68" y="4" width="16" height="16" fill="#0000ff"
                    style="filter: drop-shadow(min(3px, 5px) max(2px, 4px) clamp(1px, 2px, 3px) #00000080)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssMathFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("lengthMath", out var lengthNode));
        Assert.True(scene.TryGetNodeById("factorMath", out var factorNode));
        Assert.True(scene.TryGetNodeById("shadowMath", out var shadowNode));

        var blur = Assert.IsType<BlurImageFilter>(lengthNode!.Filter!.ImageFilter);
        AssertApproximately(3f, blur.SigmaX);

        var opacity = Assert.IsType<ColorFilterImageFilter>(factorNode!.Filter!.ImageFilter);
        var opacityMatrix = Assert.IsType<ColorMatrixColorFilter>(opacity.ColorFilter).Matrix;
        AssertApproximately(0.75f, opacityMatrix![18]);

        var merge = Assert.IsType<MergeImageFilter>(shadowNode!.Filter!.ImageFilter);
        var shadow = Assert.IsType<ColorFilterImageFilter>(merge.Filters![0]);
        var offset = Assert.IsType<OffsetImageFilter>(shadow.Input);
        var shadowBlur = Assert.IsType<BlurImageFilter>(offset.Input);
        var shadowMatrix = Assert.IsType<ColorMatrixColorFilter>(shadow.ColorFilter).Matrix;

        AssertApproximately(3f, offset.Dx);
        AssertApproximately(4f, offset.Dy);
        AssertApproximately(2f, shadowBlur.SigmaX);
        AssertApproximately(0x80 / 255f, shadowMatrix![18]);
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterResolvesNestedVarFallbacksAndModernColors()
    {
        const string cssFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="32">
              <rect id="target" x="8" y="8" width="12" height="12" fill="#ff0000"
                    style="--alpha: var(--missing-alpha, 25%);
                           --shadow: var(--missing-shadow, hsl(240 100% 50% / 50%));
                           filter: opacity(calc(var(--missing-opacity, var(--alpha)) + 25%))
                                   drop-shadow(1px 2px 3px var(--shadow));" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        var merge = Assert.IsType<MergeImageFilter>(node.Filter!.ImageFilter);
        var shadow = Assert.IsType<ColorFilterImageFilter>(merge.Filters![0]);
        var source = Assert.IsType<ColorFilterImageFilter>(merge.Filters[1]);
        var shadowMatrix = Assert.IsType<ColorMatrixColorFilter>(shadow.ColorFilter).Matrix;
        var sourceMatrix = Assert.IsType<ColorMatrixColorFilter>(source.ColorFilter).Matrix;

        AssertApproximately(0.5f, sourceMatrix![18]);
        AssertApproximately(0f, shadowMatrix![3]);
        AssertApproximately(0f, shadowMatrix[8]);
        AssertApproximately(1f, shadowMatrix[13]);
        AssertApproximately(128f / 255f, shadowMatrix[18]);
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterParsesDirectModernDropShadowColor()
    {
        const string cssFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="32">
              <rect id="target" x="8" y="8" width="12" height="12" fill="#ff0000"
                    style="filter: drop-shadow(1px 2px 3px hsl(240 100% 50% / 50%));" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        var merge = Assert.IsType<MergeImageFilter>(node.Filter!.ImageFilter);
        var shadow = Assert.IsType<ColorFilterImageFilter>(merge.Filters![0]);
        var shadowMatrix = Assert.IsType<ColorMatrixColorFilter>(shadow.ColorFilter).Matrix;

        AssertApproximately(0f, shadowMatrix![3]);
        AssertApproximately(0f, shadowMatrix[8]);
        AssertApproximately(1f, shadowMatrix[13]);
        AssertApproximately(128f / 255f, shadowMatrix[18]);
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterParsesDirectHwbDropShadowColor()
    {
        const string cssFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="32">
              <rect id="target" x="8" y="8" width="12" height="12" fill="#ff0000"
                    style="filter: drop-shadow(1px 2px 3px hwb(240 0% 0% / 50%));" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        var merge = Assert.IsType<MergeImageFilter>(node.Filter!.ImageFilter);
        var shadow = Assert.IsType<ColorFilterImageFilter>(merge.Filters![0]);
        var shadowMatrix = Assert.IsType<ColorMatrixColorFilter>(shadow.ColorFilter).Matrix;

        AssertApproximately(0f, shadowMatrix![3]);
        AssertApproximately(0f, shadowMatrix[8]);
        AssertApproximately(1f, shadowMatrix[13]);
        AssertApproximately(128f / 255f, shadowMatrix[18]);
    }

    [Fact]
    public void RetainedSceneGraph_CssColor4PaintPropertiesRenderDeterministicSrgb()
    {
        const string cssColorSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="72" height="24">
              <rect id="rgb" x="0" y="0" width="24" height="24" style="fill: rgb(0 255 0 / 75%)" />
              <rect id="hsl" x="24" y="0" width="24" height="24" fill="hsl(240 100% 50% / 50%)" />
              <rect id="hwb" x="48" y="0" width="24" height="24" fill="hwb(120 0% 0% / 25%)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(cssColorSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);

        var rgb = bitmap.GetPixel(12, 12);
        Assert.True(
            rgb.Green > 240 && rgb.Red < 10 && rgb.Blue < 10 && rgb.Alpha is >= 188 and <= 193,
            $"Expected modern rgb() slash-alpha fill to render translucent green but was {rgb}.");

        var hsl = bitmap.GetPixel(36, 12);
        Assert.True(
            hsl.Blue > 240 && hsl.Red < 10 && hsl.Green < 10 && hsl.Alpha is >= 126 and <= 130,
            $"Expected modern hsl() slash-alpha fill to render translucent blue but was {hsl}.");

        var hwb = bitmap.GetPixel(60, 12);
        Assert.True(
            hwb.Green > 240 && hwb.Red < 10 && hwb.Blue < 10 && hwb.Alpha is >= 62 and <= 66,
            $"Expected hwb() slash-alpha fill to render translucent green but was {hwb}.");
    }

    [Fact]
    public void RetainedSceneGraph_ResourceFilterColorsAcceptTransparentAndCurrentColor()
    {
        const string filterColorSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="48" height="24">
              <defs>
                <filter id="transparent" x="0" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                  <feFlood flood-color="transparent" />
                </filter>
                <filter id="current" x="24" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB"
                        color="hsl(120 100% 50% / 50%)">
                  <feFlood flood-color="currentColor" />
                </filter>
              </defs>
              <rect x="0" y="0" width="24" height="24" fill="#ff0000" filter="url(#transparent)" />
              <rect x="24" y="0" width="24" height="24" fill="#ff0000" filter="url(#current)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(filterColorSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);

        var transparent = bitmap.GetPixel(12, 12);
        Assert.True(
            transparent.Alpha < 5 && transparent.Red < 5 && transparent.Green < 5 && transparent.Blue < 5,
            $"Expected transparent flood color to resolve to transparent black but was {transparent}.");

        var current = bitmap.GetPixel(36, 12);
        Assert.True(
            current.Green > 240 && current.Red < 10 && current.Blue < 10 && current.Alpha is >= 126 and <= 130,
            $"Expected currentColor flood color to resolve through modern hsl() color but was {current}.");
    }

    [Fact]
    public void RetainedSceneGraph_InvalidCssColor4PaintDeclarationDoesNotOverrideEarlierValidPaint()
    {
        const string invalidColorSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <rect width="24" height="24" style="fill: #00ff00; fill: hwb(120 0%);" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(invalidColorSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        AssertMostlyGreen(
            bitmap.GetPixel(12, 12),
            "Expected malformed hwb() paint declaration to be ignored in favor of earlier valid fill.");
    }

    [Theory]
    [InlineData("drop-shadow(1px 2px 3px calc(2px))")]
    [InlineData("drop-shadow(red blue 1px 2px)")]
    public void RetainedSceneGraph_InvalidDirectCssDropShadowDoesNotOverrideEarlierValidFilter(string invalidFilter)
    {
        var invalidCssFilterFallbackSvg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <defs>
                <filter id="valid" x="0" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                  <feFlood flood-color="#00ff00" />
                </filter>
              </defs>
              <rect id="target" width="24" height="24" fill="#ff0000" style="filter: url(#valid); filter: {{invalidFilter}}" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(invalidCssFilterFallbackSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        var pixel = bitmap.GetPixel(12, 12);
        Assert.True(
            pixel.Green > 200 && pixel.Red < 40 && pixel.Blue < 40 && pixel.Alpha > 200,
            $"Expected invalid direct CSS drop-shadow declaration to be ignored in favor of the earlier valid filter but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterInvalidVarComputesToNone()
    {
        const string invalidFilterSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <defs>
                <filter id="green" x="0" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                  <feFlood flood-color="#00ff00" />
                </filter>
              </defs>
              <rect id="target" width="24" height="24" fill="#ff0000" filter="url(#green)"
                    style="--bad-radius: -1px; filter: blur(var(--bad-radius));" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(invalidFilterSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.Null(node!.Filter);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(12, 12));
    }

    [Fact]
    public void RetainedSceneGraph_CssFilterRejectsNonZeroUnitlessLengths()
    {
        const string invalidLengthSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24">
              <defs>
                <filter id="green" x="0" y="0" width="24" height="24" filterUnits="userSpaceOnUse" color-interpolation-filters="sRGB">
                  <feFlood flood-color="#00ff00" />
                </filter>
              </defs>
              <rect id="target" width="24" height="24" fill="#ff0000" filter="url(#green)"
                    style="--bad-radius: 2; filter: blur(var(--bad-radius));" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(invalidLengthSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var node));
        Assert.NotNull(node);
        Assert.Null(node!.Filter);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(12, 12));
    }

    [Fact]
    public void RetainedSceneGraph_MaskElementCanBeMaskedByAnotherMask()
    {
        const string maskOnMaskSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <mask id="inner" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="20" height="20">
                  <rect x="0" y="0" width="10" height="20" fill="#ffffff" />
                  <rect x="10" y="0" width="10" height="20" fill="#000000" />
                </mask>
                <mask id="outer" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="20" height="20" mask="url(#inner)">
                  <rect width="20" height="20" fill="#ffffff" />
                </mask>
              </defs>
              <rect id="target" width="20" height="20" fill="#ff0000" mask="url(#outer)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(maskOnMaskSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.NotNull(targetNode!.MaskNode);
        Assert.NotNull(targetNode.MaskNode!.MaskNode);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(5, 10));
        var maskedPixel = bitmap.GetPixel(15, 10);
        Assert.True(
            maskedPixel.Alpha < 20,
            $"Expected mask-on-mask to hide the right half but was {maskedPixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_SelfMaskedMaskSkipsRecursiveEdgeOnly()
    {
        const string selfMaskedMaskSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <mask id="self" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="20" height="20" mask="url(#self)">
                  <rect x="0" y="0" width="10" height="20" fill="#ffffff" />
                </mask>
              </defs>
              <rect id="target" width="20" height="20" fill="#ff0000" mask="url(#self)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(selfMaskedMaskSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.NotNull(targetNode!.MaskNode);
        Assert.Null(targetNode.MaskNode!.MaskNode);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(5, 10));
        var maskedPixel = bitmap.GetPixel(15, 10);
        Assert.True(
            maskedPixel.Alpha < 20,
            $"Expected self-referenced mask to keep non-recursive content only but was {maskedPixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_MaskChildSelfReferenceSkipsRecursiveEdgeOnly()
    {
        const string childSelfMaskedSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <mask id="self" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="20" height="20">
                  <rect id="mask-child" x="0" y="0" width="10" height="20" fill="#ffffff" mask="url(#self)" />
                </mask>
              </defs>
              <rect id="target" width="20" height="20" fill="#ff0000" mask="url(#self)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(childSelfMaskedSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.NotNull(targetNode!.MaskNode);
        var recursiveMaskContentNode = Assert.Single(targetNode.MaskNode!.Children, static child => child.ElementId == "mask-child");
        Assert.Null(recursiveMaskContentNode.MaskNode);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(5, 10));
        var maskedPixel = bitmap.GetPixel(15, 10);
        Assert.True(
            maskedPixel.Alpha < 20,
            $"Expected child self-reference to be skipped without dropping mask content but was {maskedPixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_MaskUseSelfReferenceSkipsReferencedResourceMaskOnly()
    {
        const string useSelfMaskedSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <g id="masked-source" mask="url(#self)">
                  <rect id="source-fill" x="0" y="0" width="10" height="20" fill="#ffffff" />
                </g>
                <mask id="self" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="20" height="20">
                  <use id="mask-use" href="#masked-source" />
                </mask>
              </defs>
              <rect id="target" width="20" height="20" fill="#ff0000" mask="url(#self)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(useSelfMaskedSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.NotNull(targetNode!.MaskNode);
        var maskUseNode = Assert.Single(targetNode.MaskNode!.Children, static child => child.ElementId == "mask-use");
        var referencedGroupNode = Assert.Single(maskUseNode.Children, static child => child.ElementId == "masked-source");
        Assert.Null(referencedGroupNode.MaskNode);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(5, 10));
        var maskedPixel = bitmap.GetPixel(15, 10);
        Assert.True(
            maskedPixel.Alpha < 20,
            $"Expected referenced resource recursion to skip only the recursive mask edge but was {maskedPixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_MutualMaskCycleKeepsFirstNonRecursiveMask()
    {
        const string mutualMaskCycleSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <mask id="left" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="20" height="20" mask="url(#top)">
                  <rect width="20" height="20" fill="#ffffff" />
                </mask>
                <mask id="top" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="20" height="20" mask="url(#left)">
                  <rect x="0" y="0" width="20" height="10" fill="#ffffff" />
                </mask>
              </defs>
              <rect id="target" width="20" height="20" fill="#ff0000" mask="url(#left)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(mutualMaskCycleSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.NotNull(targetNode!.MaskNode);
        Assert.NotNull(targetNode.MaskNode!.MaskNode);
        Assert.Null(targetNode.MaskNode.MaskNode!.MaskNode);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(5, 5));
        var maskedPixel = bitmap.GetPixel(5, 15);
        Assert.True(
            maskedPixel.Alpha < 20,
            $"Expected mutual cycle to keep the non-recursive top mask and hide the bottom half but was {maskedPixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_MutualMaskCycleOnMaskChildrenKeepsNonRecursiveChildMask()
    {
        const string childMutualMaskCycleSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <mask id="left" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="20" height="20">
                  <rect id="left-child" x="0" y="0" width="10" height="20" fill="#ffffff" mask="url(#top)" />
                </mask>
                <mask id="top" maskUnits="userSpaceOnUse" maskContentUnits="userSpaceOnUse" x="0" y="0" width="20" height="20">
                  <rect id="top-child" x="0" y="0" width="20" height="10" fill="#ffffff" mask="url(#left)" />
                </mask>
              </defs>
              <rect id="target" width="20" height="20" fill="#ff0000" mask="url(#left)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(childMutualMaskCycleSvg);

        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById("target", out var targetNode));
        Assert.NotNull(targetNode);
        Assert.NotNull(targetNode!.MaskNode);
        var leftChild = Assert.Single(targetNode.MaskNode!.Children, static child => child.ElementId == "left-child");
        Assert.NotNull(leftChild.MaskNode);
        var topChild = Assert.Single(leftChild.MaskNode!.Children, static child => child.ElementId == "top-child");
        Assert.Null(topChild.MaskNode);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        Assert.Equal(SkiaColors.Red, bitmap.GetPixel(5, 5));
        Assert.True(
            bitmap.GetPixel(5, 15).Alpha < 20,
            $"Expected child mutual cycle to hide the lower-left pixel but was {bitmap.GetPixel(5, 15)}.");
        Assert.True(
            bitmap.GetPixel(15, 5).Alpha < 20,
            $"Expected child mutual cycle to hide the upper-right pixel but was {bitmap.GetPixel(15, 5)}.");
    }

    [Fact]
    public void RetainedSceneGraph_LuminanceMaskMultipliesMaskAlpha()
    {
        const string luminanceAlphaMaskSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <mask id="half-mask"
                      maskUnits="userSpaceOnUse"
                      maskContentUnits="userSpaceOnUse"
                      x="0" y="0" width="20" height="20">
                  <rect x="0" y="0" width="20" height="20" fill="#ffffff" opacity="0.5" />
                </mask>
              </defs>
              <rect id="target" width="20" height="20" fill="#ff0000" mask="url(#half-mask)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(luminanceAlphaMaskSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(
            pixel.Red > 200 && pixel.Alpha is >= 96 and <= 160,
            $"Expected luminance mask coverage to be multiplied by mask alpha but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_AlphaMaskIgnoresLuminanceButUsesMaskAlpha()
    {
        const string alphaMaskSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <mask id="half-mask"
                      maskUnits="userSpaceOnUse"
                      maskContentUnits="userSpaceOnUse"
                      x="0" y="0" width="20" height="20"
                      mask-type="alpha">
                  <rect x="0" y="0" width="20" height="20" fill="#000000" opacity="0.5" />
                </mask>
              </defs>
              <rect id="target" width="20" height="20" fill="#ff0000" mask="url(#half-mask)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(alphaMaskSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        var pixel = bitmap.GetPixel(10, 10);
        Assert.True(
            pixel.Red > 200 && pixel.Alpha is >= 96 and <= 160,
            $"Expected alpha mask-type to use source alpha even for black mask content but was {pixel}.");
    }

    [Fact]
    public void RetainedSceneGraph_UserSpaceMaskRegionClipsMaskContent()
    {
        const string maskBoxSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <mask id="left-mask"
                      maskUnits="userSpaceOnUse"
                      maskContentUnits="userSpaceOnUse"
                      x="0" y="0" width="10" height="20">
                  <rect x="0" y="0" width="20" height="20" fill="#ffffff" />
                </mask>
              </defs>
              <rect id="target" width="20" height="20" fill="#ff0000" mask="url(#left-mask)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(maskBoxSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        AssertMostlyRed(bitmap.GetPixel(5, 10), "Expected content inside the mask region to reveal the target.");
        Assert.True(
            bitmap.GetPixel(15, 10).Alpha < 20,
            $"Expected content outside the mask region to be clipped but was {bitmap.GetPixel(15, 10)}.");
    }

    [Fact]
    public void RetainedSceneGraph_ObjectBoundingBoxMaskRegionClipsMaskContent()
    {
        const string maskBoxSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <mask id="middle-mask"
                      maskUnits="objectBoundingBox"
                      maskContentUnits="userSpaceOnUse"
                      x="0.25" y="0" width="0.5" height="1">
                  <rect x="0" y="0" width="20" height="20" fill="#ffffff" />
                </mask>
              </defs>
              <rect id="target" width="20" height="20" fill="#ff0000" mask="url(#middle-mask)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(maskBoxSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        Assert.True(bitmap.GetPixel(4, 10).Alpha < 20, $"Expected left side outside objectBoundingBox mask region to be clipped but was {bitmap.GetPixel(4, 10)}.");
        AssertMostlyRed(bitmap.GetPixel(10, 10), "Expected objectBoundingBox mask region to reveal the middle of the target.");
        Assert.True(bitmap.GetPixel(16, 10).Alpha < 20, $"Expected right side outside objectBoundingBox mask region to be clipped but was {bitmap.GetPixel(16, 10)}.");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("overflow=\"hidden\"", false)]
    [InlineData("overflow=\"visible\"", true)]
    [InlineData("style=\"overflow: hidden\"", false)]
    [InlineData("style=\"overflow: visible\"", true)]
    public void RetainedSceneGraph_PatternOverflowControlsTileClipping(string overflowAttribute, bool isVisibleOverflow)
    {
        var patternOverflowSvg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="12">
              <defs>
                <pattern id="p" patternUnits="userSpaceOnUse" x="0" y="0" width="10" height="10" {{overflowAttribute}}>
                  <rect x="8" y="0" width="4" height="10" fill="#00ff00" />
                </pattern>
              </defs>
              <rect width="24" height="12" fill="#ff0000" />
              <rect id="target" width="24" height="10" fill="url(#p)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(patternOverflowSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);
        var insideTilePixel = bitmap.GetPixel(9, 5);
        var overflowPixel = bitmap.GetPixel(11, 5);
        var command = Assert.Single(
            svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("target"),
            static command => command.Paint?.Style == SKPaintStyle.Fill);
        var shader = Assert.IsType<PictureShader>(command.Paint!.Shader);
        var hasPatternViewportClip = shader.Src?.Commands?.Any(
            static command => command is ClipRectCanvasCommand clip &&
                              Math.Abs(clip.Rect.Left) < 0.001f &&
                              Math.Abs(clip.Rect.Top) < 0.001f &&
                              Math.Abs(clip.Rect.Right - 10f) < 0.001f &&
                              Math.Abs(clip.Rect.Bottom - 10f) < 0.001f) == true;

        Assert.True(
            insideTilePixel.Green > 200 && insideTilePixel.Red < 40,
            $"Expected in-tile pattern content to render green but was {insideTilePixel}.");

        if (isVisibleOverflow)
        {
            Assert.False(hasPatternViewportClip);
            Assert.True(
                overflowPixel.Green > 200 && overflowPixel.Red < 40,
                $"Expected visible pattern overflow to bleed into the next tile but was {overflowPixel}.");
        }
        else
        {
            Assert.True(hasPatternViewportClip);
            Assert.True(
                overflowPixel.Red > 200 && overflowPixel.Green < 40,
                $"Expected hidden pattern overflow to reveal the red backdrop but was {overflowPixel}.");
        }
    }

    [Fact]
    public void RetainedSceneGraph_PatternVisibleOverflowWrapsContentFromNeighboringTiles()
    {
        const string patternOverflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="10">
              <defs>
                <pattern id="p" patternUnits="userSpaceOnUse" x="0" y="0" width="10" height="10" overflow="visible">
                  <rect x="12" y="0" width="4" height="10" fill="#00ff00" />
                </pattern>
              </defs>
              <rect width="24" height="10" fill="#ff0000" />
              <rect id="target" width="24" height="10" fill="url(#p)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(patternOverflowSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);

        AssertMostlyGreen(
            bitmap.GetPixel(2, 5),
            "Expected visible pattern overflow from the previous repeated tile to render in the current tile.");
        AssertMostlyRed(
            bitmap.GetPixel(8, 5),
            "Expected pixels outside the visible overflow band to keep the backdrop.");
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("overflow=\"hidden\"", false)]
    [InlineData("style=\"overflow: hidden\"", false)]
    public void RetainedSceneGraph_PatternOverflowCascadesThroughHref(string derivedOverflow, bool isVisibleOverflow)
    {
        var patternOverflowSvg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="12">
              <style>
                #base { overflow: visible; }
              </style>
              <defs>
                <pattern id="base" patternUnits="userSpaceOnUse" width="10" height="10">
                  <rect x="8" y="0" width="4" height="10" fill="#00ff00" />
                </pattern>
                <pattern id="p" href="#base" {{derivedOverflow}} />
              </defs>
              <rect width="24" height="12" fill="#ff0000" />
              <rect id="target" width="24" height="10" fill="url(#p)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(patternOverflowSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);

        if (isVisibleOverflow)
        {
            AssertMostlyGreen(
                bitmap.GetPixel(11, 5),
                "Expected visible overflow inherited through href and stylesheet cascade to bleed into the next tile.");
        }
        else
        {
            AssertMostlyRed(
                bitmap.GetPixel(11, 5),
                "Expected derived pattern overflow to override the referenced visible overflow.");
        }
    }

    [Fact]
    public void RetainedSceneGraph_PatternViewBoxOverflowClipsTileViewport()
    {
        const string patternViewBoxOverflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="40">
              <defs>
                <pattern id="p" patternUnits="userSpaceOnUse" width="20" height="40" viewBox="0 0 20 20">
                  <rect x="0" y="-10" width="20" height="40" fill="#00ff00" />
                </pattern>
              </defs>
              <rect width="20" height="40" fill="#ff0000" />
              <rect id="target" width="20" height="40" fill="url(#p)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(patternViewBoxOverflowSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);

        AssertMostlyGreen(bitmap.GetPixel(10, 5), "Expected pattern overflow to keep the top of the tile visible.");
        AssertMostlyGreen(bitmap.GetPixel(10, 35), "Expected pattern overflow to keep the bottom of the tile visible.");
    }

    [Fact]
    public void RetainedSceneGraph_PatternInheritsViewBoxAndPreserveAspectRatio()
    {
        const string inheritedViewBoxSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <pattern id="base" patternUnits="userSpaceOnUse" width="20" height="20"
                         viewBox="0 0 10 20" preserveAspectRatio="none" />
                <pattern id="p" href="#base">
                  <rect width="10" height="20" fill="#00ff00" />
                </pattern>
              </defs>
              <rect width="20" height="20" fill="#ff0000" />
              <rect id="target" width="20" height="20" fill="url(#p)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(inheritedViewBoxSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);

        AssertMostlyGreen(bitmap.GetPixel(15, 10), "Expected inherited viewBox and preserveAspectRatio to scale pattern content.");
    }

    [Fact]
    public void RetainedSceneGraph_PatternContentUnitsObjectBoundingBoxScaleFromTargetBounds()
    {
        const string objectBoundingBoxContentSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="50" height="30">
              <defs>
                <pattern id="p" patternUnits="userSpaceOnUse" patternContentUnits="objectBoundingBox"
                         width="20" height="20">
                  <rect x="0.25" y="0.25" width="0.5" height="0.5" fill="#00ff00" />
                </pattern>
              </defs>
              <rect width="50" height="30" fill="#ff0000" />
              <rect id="target" x="10" y="5" width="40" height="20" fill="url(#p)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(objectBoundingBoxContentSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);

        AssertMostlyGreen(
            bitmap.GetPixel(15, 10),
            "Expected patternContentUnits objectBoundingBox content to scale from the target bounds.");
        AssertMostlyRed(bitmap.GetPixel(28, 10), "Expected objectBoundingBox content to stay local to the repeated tile.");
    }

    [Fact]
    public void RetainedSceneGraph_RepeatsPatternTileAcrossPaintedShape()
    {
        const string repeatedPatternSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="26" height="8">
              <defs>
                <pattern id="p" patternUnits="userSpaceOnUse" width="8" height="8">
                  <rect x="0" y="0" width="4" height="8" fill="#00ff00" />
                </pattern>
              </defs>
              <rect width="26" height="8" fill="#ff0000" />
              <rect id="target" width="26" height="8" fill="url(#p)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(repeatedPatternSvg);

        Assert.NotNull(svg.Picture);
        using var bitmap = ToBitmap(svg, svg.Picture!);

        AssertMostlyGreen(bitmap.GetPixel(2, 4), "Expected the first pattern tile to render.");
        AssertMostlyRed(bitmap.GetPixel(6, 4), "Expected the first pattern tile gap to reveal the backdrop.");
        AssertMostlyGreen(bitmap.GetPixel(10, 4), "Expected the second pattern tile to repeat.");
        AssertMostlyRed(bitmap.GetPixel(14, 4), "Expected the second pattern tile gap to reveal the backdrop.");
    }

    [Fact]
    public void RetainedSceneGraph_MarkerStyleOverflowControlsViewportClipping()
    {
        const string markerOverflowSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="12">
              <defs>
                <marker id="marker"
                        markerWidth="4"
                        markerHeight="4"
                        refX="0"
                        refY="2"
                        markerUnits="userSpaceOnUse"
                        style="overflow: visible">
                  <rect x="0" y="0" width="12" height="4" fill="#00ff00" />
                </marker>
              </defs>
              <rect width="24" height="12" fill="#ff0000" />
              <path id="target" d="M4 6 L20 6" fill="none" stroke="none" marker-start="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerOverflowSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        AssertMostlyGreen(
            bitmap.GetPixel(10, 6),
            "Expected CSS marker overflow to keep content outside the marker viewport visible.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("overflow=\"hidden\"")]
    [InlineData("style=\"overflow: hidden\"")]
    public void RetainedSceneGraph_MarkerHiddenOverflowClipsViewport(string overflowAttribute)
    {
        var markerOverflowSvg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="12">
              <defs>
                <marker id="marker"
                        markerWidth="4"
                        markerHeight="4"
                        refX="0"
                        refY="2"
                        markerUnits="userSpaceOnUse"
                        {{overflowAttribute}}>
                  <rect x="0" y="0" width="12" height="4" fill="#00ff00" />
                </marker>
              </defs>
              <rect width="24" height="12" fill="#ff0000" />
              <path id="target" d="M4 6 L20 6" fill="none" stroke="none" marker-start="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerOverflowSvg);

        using var retainedPicture = svg.CreateRetainedSceneGraphPicture();
        Assert.NotNull(retainedPicture);
        using var bitmap = ToBitmap(svg, retainedPicture!);

        AssertMostlyGreen(bitmap.GetPixel(6, 6), "Expected marker content inside the viewport to render.");
        AssertMostlyRed(bitmap.GetPixel(10, 6), "Expected hidden marker overflow to clip content outside the viewport.");
    }

    [Fact]
    public void RetainedSceneGraph_PlacesMarkerMidAtQuadraticEndpoint()
    {
        const string markerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="80">
              <defs>
                <marker id="marker" markerWidth="4" markerHeight="4" refX="0" refY="0" orient="auto" markerUnits="userSpaceOnUse">
                  <path d="M0,0 L4,0" fill="none" stroke="red" />
                </marker>
              </defs>
              <path id="target"
                    d="M10 50 Q20 50 30 50 L50 50"
                    fill="none"
                    stroke="black"
                    marker-start="url(#marker)"
                    marker-mid="url(#marker)"
                    marker-end="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerSvg);

        var markerNodes = GetTargetMarkerNodes(svg.RetainedSceneGraph, "target");

        Assert.Equal(3, markerNodes.Count);
        AssertMarkerTransform(markerNodes[0], 10f, 50f, 0f);
        AssertMarkerTransform(markerNodes[1], 30f, 50f, 0f);
        AssertMarkerTransform(markerNodes[2], 50f, 50f, 0f);
    }

    [Fact]
    public void RetainedSceneGraph_UsesArcTangentsForAutoMarkerOrientation()
    {
        const string markerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <marker id="marker" markerWidth="4" markerHeight="4" refX="0" refY="0" orient="auto" markerUnits="userSpaceOnUse">
                  <path d="M0,0 L4,0" fill="none" stroke="red" />
                </marker>
              </defs>
              <path id="target"
                    d="M50 20 A30 30 0 0 1 80 50"
                    fill="none"
                    stroke="black"
                    marker-start="url(#marker)"
                    marker-end="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerSvg);

        var markerNodes = GetTargetMarkerNodes(svg.RetainedSceneGraph, "target");

        Assert.Equal(2, markerNodes.Count);
        AssertMarkerTransform(markerNodes[0], 50f, 20f, 0f);
        AssertMarkerTransform(markerNodes[1], 80f, 50f, 90f);
    }

    [Fact]
    public void RetainedSceneGraph_DoesNotBridgeSubpathMarkerTangentsAndKeepsZeroLengthMarkers()
    {
        const string markerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <marker id="marker" markerWidth="4" markerHeight="4" refX="0" refY="0" orient="auto" markerUnits="userSpaceOnUse">
                  <path d="M0,0 L4,0" fill="none" stroke="red" />
                </marker>
              </defs>
              <path id="target"
                    d="M10 10 L20 10 M40 40 L40 60 M70 40 L70 40"
                    fill="none"
                    stroke="black"
                    marker-start="url(#marker)"
                    marker-mid="url(#marker)"
                    marker-end="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerSvg);

        var markerNodes = GetTargetMarkerNodes(svg.RetainedSceneGraph, "target");

        Assert.Equal(6, markerNodes.Count);
        AssertMarkerTransform(markerNodes[2], 40f, 40f, 90f);
        AssertMarkerTransform(markerNodes[4], 70f, 40f, 0f);
        AssertMarkerTransform(markerNodes[5], 70f, 40f, 0f);
    }

    [Fact]
    public void RetainedSceneGraph_UsesNeighborTangentsForZeroLengthEndpointMarkers()
    {
        const string markerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="60">
              <defs>
                <marker id="marker" markerWidth="4" markerHeight="4" refX="0" refY="0" orient="auto" markerUnits="userSpaceOnUse">
                  <path d="M0,0 L4,0" fill="none" stroke="red" />
                </marker>
              </defs>
              <path id="target"
                    d="M10 20 L10 20 L40 20 M60 40 L90 40 L90 40"
                    fill="none"
                    stroke="black"
                    marker-start="url(#marker)"
                    marker-end="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerSvg);

        var markerNodes = GetTargetMarkerNodes(svg.RetainedSceneGraph, "target");

        Assert.Equal(2, markerNodes.Count);
        AssertMarkerTransform(markerNodes[0], 10f, 20f, 0f);
        AssertMarkerTransform(markerNodes[1], 90f, 40f, 0f);
    }

    [Fact]
    public void RetainedSceneGraph_AveragesAutoMarkerAnglesAcrossWrapBoundary()
    {
        const string markerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <marker id="marker" markerWidth="4" markerHeight="4" refX="0" refY="0" orient="auto" markerUnits="userSpaceOnUse">
                  <path d="M0,0 L4,0" fill="none" stroke="red" />
                </marker>
              </defs>
              <path id="target"
                    d="M60 48.237 L50 50 L40 48.237"
                    fill="none"
                    stroke="black"
                    marker-mid="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerSvg);

        var markerNode = Assert.Single(GetTargetMarkerNodes(svg.RetainedSceneGraph, "target"));
        AssertMarkerTransform(markerNode, 50f, 50f, 180f);
    }

    [Fact]
    public void RetainedSceneGraph_AppliesMarkerViewBoxPreserveAspectRatio()
    {
        const string markerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <defs>
                <marker id="marker"
                        markerWidth="20"
                        markerHeight="20"
                        refX="10"
                        refY="10"
                        markerUnits="userSpaceOnUse"
                        viewBox="3 6 14 20"
                        preserveAspectRatio="none">
                  <rect id="marker-box" x="3" y="6" width="14" height="20" fill="#00ff00" />
                </marker>
              </defs>
              <path id="target" d="M50 50 L80 50" fill="none" stroke="black" marker-start="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerSvg);

        var markerNode = Assert.Single(GetTargetMarkerNodes(svg.RetainedSceneGraph, "target"));
        AssertMarkerTransform(markerNode, 35.714f, 40f, 0f, tolerance: 0.01f);
    }

    [Fact]
    public void RetainedSceneGraph_RendersMultipleMarkerVisualChildren()
    {
        const string markerSvg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="24">
              <defs>
                <marker id="marker" markerWidth="8" markerHeight="8" refX="4" refY="4" markerUnits="userSpaceOnUse">
                  <rect id="marker-left" x="0" y="0" width="4" height="8" fill="#ff0000" />
                  <rect id="marker-right" x="4" y="0" width="4" height="8" fill="#0000ff" />
                </marker>
              </defs>
              <path id="target" d="M12 12 L32 12" fill="none" stroke="black" marker-start="url(#marker)" />
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(markerSvg);

        Assert.Single(svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("marker-left"));
        Assert.Single(svg.Model!.FindCommandsBySourceElementId<DrawPathCanvasCommand>("marker-right"));
    }

    private static T AssertFilter<T>(SKSvg svg, string elementId)
        where T : SKImageFilter
    {
        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById(elementId, out var node));
        Assert.NotNull(node);
        Assert.NotNull(node!.Filter);

        var filter = FindImageFilter<T>(node.Filter!.ImageFilter);
        Assert.NotNull(filter);
        return filter!;
    }

    private static bool ContainsImageFilter(SKSvg svg, string elementId, Type filterType)
    {
        var scene = svg.RetainedSceneGraph;
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById(elementId, out var node));
        Assert.NotNull(node);

        return ContainsImageFilter(node!.Filter?.ImageFilter, filterType);
    }

    private static T? FindImageFilter<T>(SKImageFilter? filter)
        where T : SKImageFilter
    {
        if (filter is T typed)
        {
            return typed;
        }

        foreach (var child in GetChildFilters(filter))
        {
            if (FindImageFilter<T>(child) is { } childFilter)
            {
                return childFilter;
            }
        }

        return null;
    }

    private static bool ContainsImageFilter(SKImageFilter? filter, Type filterType)
    {
        if (filter is null)
        {
            return false;
        }

        if (filter.GetType() == filterType)
        {
            return true;
        }

        return GetChildFilters(filter).Any(child => ContainsImageFilter(child, filterType));
    }

    private static List<SvgSceneNode> GetTargetMarkerNodes(SvgSceneDocument? scene, string elementId)
    {
        Assert.NotNull(scene);
        Assert.True(scene!.TryGetNodeById(elementId, out var targetNode));
        Assert.NotNull(targetNode);

        return targetNode!.Children
            .Where(static node => node.Kind == SvgSceneNodeKind.Marker)
            .ToList();
    }

    private static void AssertMarkerTransform(
        SvgSceneNode markerNode,
        float expectedX,
        float expectedY,
        float expectedAngle,
        float tolerance = 0.001f)
    {
        var origin = markerNode.TotalTransform.MapPoint(new SKPoint(0f, 0f));
        var unitX = markerNode.TotalTransform.MapPoint(new SKPoint(1f, 0f));
        var actualAngle = NormalizeAngle((float)(Math.Atan2(unitX.Y - origin.Y, unitX.X - origin.X) * 180.0 / Math.PI));

        AssertApproximately(expectedX, origin.X, tolerance);
        AssertApproximately(expectedY, origin.Y, tolerance);
        AssertApproximately(NormalizeAngle(expectedAngle), actualAngle, tolerance);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return angle < 0f ? angle + 360f : angle;
    }

    private static IEnumerable<SKImageFilter?> GetChildFilters(SKImageFilter? filter)
    {
        return filter switch
        {
            ArithmeticImageFilter value => new[] { value.Background, value.Foreground },
            BlendModeImageFilter value => new[] { value.Background, value.Foreground },
            BlurImageFilter value => new[] { value.Input },
            ColorFilterImageFilter value => new[] { value.Input },
            DilateImageFilter value => new[] { value.Input },
            DisplacementMapEffectImageFilter value => new[] { value.Displacement, value.Input },
            DistantLitDiffuseImageFilter value => new[] { value.Input },
            DistantLitSpecularImageFilter value => new[] { value.Input },
            ErodeImageFilter value => new[] { value.Input },
            MatrixConvolutionImageFilter value => new[] { value.Input },
            MergeImageFilter value => value.Filters ?? Array.Empty<SKImageFilter>(),
            OffsetImageFilter value => new[] { value.Input },
            PointLitDiffuseImageFilter value => new[] { value.Input },
            PointLitSpecularImageFilter value => new[] { value.Input },
            SpotLitDiffuseImageFilter value => new[] { value.Input },
            SpotLitSpecularImageFilter value => new[] { value.Input },
            TileImageFilter value => new[] { value.Input },
            _ => Array.Empty<SKImageFilter?>()
        };
    }

    private static float[] CreateIdentityColorMatrix()
        =>
        [
            1, 0, 0, 0, 0,
            0, 1, 0, 0, 0,
            0, 0, 1, 0, 0,
            0, 0, 0, 1, 0
        ];

    private static void AssertApproximately(float expected, float actual, float tolerance = 0.001f)
    {
        Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"Expected {actual} to be within {tolerance} of {expected}.");
    }

    private static void AssertRectApproximately(SKRect expected, SKRect actual, float tolerance = 0.001f)
    {
        AssertApproximately(expected.Left, actual.Left, tolerance);
        AssertApproximately(expected.Top, actual.Top, tolerance);
        AssertApproximately(expected.Right, actual.Right, tolerance);
        AssertApproximately(expected.Bottom, actual.Bottom, tolerance);
    }

    private static void AssertMostlyGreen(SkiaColor pixel, string message)
    {
        Assert.True(
            pixel.Green > 200 && pixel.Red < 40 && pixel.Blue < 40 && pixel.Alpha > 200,
            $"{message} Pixel was {pixel}.");
    }

    private static void AssertMostlyRed(SkiaColor pixel, string message)
    {
        Assert.True(
            pixel.Red > 200 && pixel.Green < 40 && pixel.Blue < 40 && pixel.Alpha > 200,
            $"{message} Pixel was {pixel}.");
    }

    private static byte[] CreateSolidPng(SkiaColor color, int width = 1, int height = 1)
    {
        using var bitmap = new SkiaBitmap(width, height);
        bitmap.Erase(color);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SkiaBitmap ToBitmap(SKSvg svg, SkiaSharp.SKPicture picture)
    {
        var bitmap = picture.ToBitmap(
            SkiaColors.Transparent,
            1f,
            1f,
            SkiaColorType.Rgba8888,
            SkiaAlphaType.Unpremul,
            svg.Settings.Srgb);

        return Assert.IsType<SkiaBitmap>(bitmap);
    }

    private sealed class CountingImageAssetLoader : ISvgAssetLoader, ISvgImageAssetLoader
    {
        public int LoadImageCallCount { get; private set; }

        public bool EnableSvgFonts => false;

        public SKImage LoadImage(Stream stream)
        {
            LoadImageCallCount++;
            return LoadImageCore(stream);
        }

        public SKImage LoadImage(Stream stream, SvgImageLoadContext context)
            => LoadImage(stream);

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
            => new();

        public SKFontMetrics GetFontMetrics(SKPaint paint)
            => default;

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
            => 0f;

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y)
            => new SKPath();

        private static SKImage LoadImageCore(Stream stream)
        {
            var data = SKImage.FromStream(stream);
            using var image = data is { Length: > 0 } ? SkiaSharp.SKImage.FromEncodedData(data) : null;
            return new SKImage
            {
                Data = data,
                Width = image?.Width ?? 0,
                Height = image?.Height ?? 0
            };
        }
    }
}
