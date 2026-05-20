using System;
using System.Collections.Generic;
using System.IO;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

[Collection(SvgExternalResourceStateCollection.Name)]
public class SvgServiceExternalImageTests
{
    [Fact]
    public void GetImage_ReturnsNullAndDoesNotLoadLocalFile_WhenExternalImagesDisabled()
    {
        var previousResolveExternalImages = SvgDocument.ResolveExternalImages;
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            SvgDocument.ResolveExternalImages = ExternalType.None;
            var imagePath = Path.Combine(tempDirectory.FullName, "image.png");
            File.WriteAllBytes(imagePath, new byte[] { 1, 2, 3, 4 });

            var assetLoader = new CountingAssetLoader();
            var image = SvgService.GetImage(new Uri(Path.GetFullPath(imagePath)).AbsoluteUri, new SvgDocument(), assetLoader);

            Assert.Null(image);
            Assert.Equal(0, assetLoader.LoadImageCallCount);
        }
        finally
        {
            SvgDocument.ResolveExternalImages = previousResolveExternalImages;
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetImage_LoadsLocalFile_WhenLocalImagesAllowed()
    {
        var previousResolveExternalImages = SvgDocument.ResolveExternalImages;
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            SvgDocument.ResolveExternalImages = ExternalType.Local;
            var imagePath = Path.Combine(tempDirectory.FullName, "image.png");
            File.WriteAllBytes(imagePath, new byte[] { 1, 2, 3, 4 });

            var assetLoader = new CountingAssetLoader();
            var image = SvgService.GetImage(new Uri(Path.GetFullPath(imagePath)).AbsoluteUri, new SvgDocument(), assetLoader);

            Assert.IsType<SKImage>(image);
            Assert.Equal(1, assetLoader.LoadImageCallCount);
        }
        finally
        {
            SvgDocument.ResolveExternalImages = previousResolveExternalImages;
            tempDirectory.Delete(recursive: true);
        }
    }

    private sealed class CountingAssetLoader : ISvgAssetLoader
    {
        public int LoadImageCallCount { get; private set; }

        public bool EnableSvgFonts => false;

        public SKImage LoadImage(Stream stream)
        {
            LoadImageCallCount++;
            return new SKImage
            {
                Data = SKImage.FromStream(stream),
                Width = 1f,
                Height = 1f
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
