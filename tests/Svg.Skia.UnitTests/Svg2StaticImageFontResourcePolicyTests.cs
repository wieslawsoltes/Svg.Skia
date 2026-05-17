using System.Collections.Generic;
using System.IO;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;
using Xunit;

namespace Svg.Skia.UnitTests;

public class Svg2StaticImageFontResourcePolicyTests
{
    [Fact]
    public void ImageFontResourcePolicy_ImageWithoutExplicitDimensionsUsesIntrinsicSize()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <image id="asset" href="data:image/png;base64,AQIDBA==" />
            </svg>
            """);
        var assetLoader = new IntrinsicImageAssetLoader(width: 12f, height: 8f);

        var compiled = SvgSceneCompiler.TryCompile(
            document,
            SKRect.Create(0f, 0f, 100f, 100f),
            assetLoader,
            DrawAttributes.None,
            out var sceneDocument);

        Assert.True(compiled);
        Assert.NotNull(sceneDocument);
        Assert.True(sceneDocument!.TryGetNodeById("asset", out var imageNode));
        Assert.Equal(SKRect.Create(0f, 0f, 12f, 8f), imageNode!.GeometryBounds);
    }

    [Fact]
    public void ImageFontResourcePolicy_ImageWithOneExplicitDimensionPreservesIntrinsicAspectRatio()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <image id="asset" href="data:image/png;base64,AQIDBA==" width="24" />
            </svg>
            """);
        var assetLoader = new IntrinsicImageAssetLoader(width: 12f, height: 8f);

        var compiled = SvgSceneCompiler.TryCompile(
            document,
            SKRect.Create(0f, 0f, 100f, 100f),
            assetLoader,
            DrawAttributes.None,
            out var sceneDocument);

        Assert.True(compiled);
        Assert.NotNull(sceneDocument);
        Assert.True(sceneDocument!.TryGetNodeById("asset", out var imageNode));
        Assert.Equal(SKRect.Create(0f, 0f, 24f, 16f), imageNode!.GeometryBounds);
    }

    [Fact]
    public void ImageFontResourcePolicy_ForeignObjectRemainsMeasuredDeferredContent()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40">
              <foreignObject id="html" x="3" y="4" width="12" height="10">
                <body xmlns="http://www.w3.org/1999/xhtml">Deferred</body>
              </foreignObject>
            </svg>
            """);

        var compiled = SvgSceneCompiler.TryCompile(
            document,
            SKRect.Create(0f, 0f, 40f, 40f),
            new IntrinsicImageAssetLoader(width: 1f, height: 1f),
            DrawAttributes.None,
            out var sceneDocument);

        Assert.True(compiled);
        Assert.NotNull(sceneDocument);
        Assert.True(sceneDocument!.TryGetNodeById("html", out var foreignObjectNode));
        Assert.Equal(SvgSceneNodeKind.Container, foreignObjectNode!.Kind);
        Assert.False(foreignObjectNode.IsRenderable);
        Assert.Equal(SKRect.Create(3f, 4f, 12f, 10f), foreignObjectNode.GeometryBounds);
    }

    private sealed class IntrinsicImageAssetLoader : ISvgAssetLoader
    {
        private readonly float _width;
        private readonly float _height;

        public IntrinsicImageAssetLoader(float width, float height)
        {
            _width = width;
            _height = height;
        }

        public bool EnableSvgFonts => false;

        public SKImage LoadImage(Stream stream)
        {
            return new SKImage
            {
                Data = SKImage.FromStream(stream),
                Width = _width,
                Height = _height
            };
        }

        public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
            => new();

        public SKFontMetrics GetFontMetrics(SKPaint paint)
            => default;

        public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
            => 0f;

        public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y)
            => new SKPath();
    }
}
