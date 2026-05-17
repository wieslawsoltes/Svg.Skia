using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using ShimSkiaSharp;
using Svg.Model;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

[Collection(SvgExternalResourceStateCollection.Name)]
public class Svg2StaticResourcePolicyTests
{
    private const string MinimalSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
          <rect id="shape" width="16" height="16" />
        </svg>
        """;

    [Fact]
    public void FromSvg_MapsDefaultLoadOptionsToDocumentState()
    {
        var document = SvgService.FromSvg(MinimalSvg);

        var options = SvgService.GetDocumentLoadOptions(document!);

        Assert.Equal(SvgProcessingMode.Static, options.ProcessingMode);
        Assert.Equal(SvgExternalResourcePolicy.Enabled, options.ExternalResources);
        Assert.True(options.PreserveUnknownElements);
        Assert.True(options.PreferSvg2Href);
    }

    [Fact]
    public void FromSvg_MapsParametersLoadOptionsToDocumentState()
    {
        var loadOptions = new SvgDocumentLoadOptions
        {
            ProcessingMode = SvgProcessingMode.SecureStatic,
            ExternalResources = SvgExternalResourcePolicy.SameDocumentAndDataOnly,
            PreserveUnknownElements = false,
            PreferSvg2Href = false
        };
        var parameters = new SvgParameters(null, null, null, loadOptions);

        var document = SvgService.FromSvg(MinimalSvg, parameters);

        var options = SvgService.GetDocumentLoadOptions(document!);
        Assert.Equal(SvgProcessingMode.SecureStatic, options.ProcessingMode);
        Assert.Equal(SvgExternalResourcePolicy.SameDocumentAndDataOnly, options.ExternalResources);
        Assert.False(options.PreserveUnknownElements);
        Assert.False(options.PreferSvg2Href);
    }

    [Fact]
    public void AllowsExternalResource_SameDocumentAndDataOnlyAllowsSameDocumentAndDataUris()
    {
        var document = CreateDocument(SvgExternalResourcePolicy.SameDocumentAndDataOnly);
        document.BaseUri = new Uri("https://example.test/assets/source.svg");

        Assert.True(SvgService.AllowsExternalResource(document, new Uri("#shape", UriKind.RelativeOrAbsolute)));
        Assert.True(SvgService.AllowsExternalResource(document, new Uri("data:image/png;base64,AQIDBA==", UriKind.Absolute)));
        Assert.True(SvgService.AllowsExternalResource(document, new Uri("https://example.test/assets/source.svg#shape")));
    }

    [Fact]
    public void AllowsExternalResource_SameDocumentAndDataOnlyBlocksExternalUris()
    {
        var document = CreateDocument(SvgExternalResourcePolicy.SameDocumentAndDataOnly);
        document.BaseUri = new Uri("https://example.test/assets/source.svg");

        Assert.False(SvgService.AllowsExternalResource(document, new Uri("image.png", UriKind.RelativeOrAbsolute)));
        Assert.False(SvgService.AllowsExternalResource(document, new Uri("https://example.test/assets/other.svg#shape")));
        Assert.False(SvgService.AllowsExternalResource(document, new Uri("https://cdn.example.test/image.png")));
    }

    [Fact]
    public void AllowsExternalResource_SameOriginAllowsFileResourcesUnderDocumentDirectory()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var documentPath = Path.Combine(tempDirectory.FullName, "source.svg");
            var imagePath = Path.Combine(tempDirectory.FullName, "images", "image.png");
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            File.WriteAllText(documentPath, MinimalSvg);
            File.WriteAllBytes(imagePath, new byte[] { 1, 2, 3, 4 });

            var document = CreateDocument(SvgExternalResourcePolicy.SameOrigin);
            document.BaseUri = new Uri(Path.GetFullPath(documentPath));

            Assert.True(SvgService.AllowsExternalResource(document, new Uri(Path.GetFullPath(imagePath))));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void AllowsExternalResource_SameOriginBlocksFileResourcesOutsideDocumentDirectory()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var documentDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "document"));
            var documentPath = Path.Combine(documentDirectory.FullName, "source.svg");
            var outsidePath = Path.Combine(tempDirectory.FullName, "outside.png");
            File.WriteAllText(documentPath, MinimalSvg);
            File.WriteAllBytes(outsidePath, new byte[] { 1, 2, 3, 4 });

            var document = CreateDocument(SvgExternalResourcePolicy.SameOrigin);
            document.BaseUri = new Uri(Path.GetFullPath(documentPath));

            Assert.False(SvgService.AllowsExternalResource(document, new Uri(Path.GetFullPath(outsidePath))));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetImage_SameDocumentAndDataOnlyBlocksExternalFileBeforeAssetLoader()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var imagePath = Path.Combine(tempDirectory.FullName, "image.png");
            File.WriteAllBytes(imagePath, new byte[] { 1, 2, 3, 4 });
            var imageUri = new Uri(Path.GetFullPath(imagePath)).AbsoluteUri;
            var svg = $$"""
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <image id="asset" href="{{imageUri}}" width="16" height="16" />
                </svg>
                """;
            var document = SvgService.FromSvg(
                svg,
                new SvgParameters(null, null, null, CreateLoadOptions(SvgExternalResourcePolicy.SameDocumentAndDataOnly)));
            var image = Assert.IsType<SvgImage>(document!.GetElementById("asset"));
            Assert.True(image.TryGetEffectiveHrefString(out var href));

            var assetLoader = new CountingAssetLoader();
            var loadedImage = SvgService.GetImage(href, image, assetLoader);

            Assert.Null(loadedImage);
            Assert.Equal(0, assetLoader.LoadImageCallCount);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetImage_SameDocumentAndDataOnlyAllowsDataImage()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
              <image id="asset" href="data:image/png;base64,AQIDBA==" width="16" height="16" />
            </svg>
            """;
        var document = SvgService.FromSvg(
            svg,
            new SvgParameters(null, null, null, CreateLoadOptions(SvgExternalResourcePolicy.SameDocumentAndDataOnly)));
        var image = Assert.IsType<SvgImage>(document!.GetElementById("asset"));
        Assert.True(image.TryGetEffectiveHrefString(out var href));

        var assetLoader = new CountingAssetLoader();
        var loadedImage = SvgService.GetImage(href, image, assetLoader);

        Assert.IsType<SKImage>(loadedImage);
        Assert.Equal(1, assetLoader.LoadImageCallCount);
    }

    [Fact]
    public void OpenSvg_DisabledResourcePolicyBlocksCssImportsDuringParse()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var cssPath = Path.Combine(tempDirectory.FullName, "imported.css");
            File.WriteAllText(cssPath, "#shape { fill: #ff0000; }");
            var svgPath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <style>@import "imported.css";</style>
                  <rect id="shape" width="16" height="16" fill="#000000" />
                </svg>
                """);
            var parameters = new SvgParameters(
                null,
                null,
                null,
                CreateLoadOptions(SvgExternalResourcePolicy.Disabled));

            var document = SvgService.OpenSvg(svgPath, parameters);
            var shape = Assert.IsType<SvgRectangle>(document!.GetElementById("shape"));
            var fill = Assert.IsType<SvgColourServer>(shape.Fill);

            Assert.Equal(Color.Black.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void OpenSvg_SameOriginResourcePolicyAllowsCssImportsUnderDocumentDirectory()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var cssPath = Path.Combine(tempDirectory.FullName, "imported.css");
            File.WriteAllText(cssPath, "#shape { fill: #ff0000; }");
            var svgPath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <style>@import "imported.css";</style>
                  <rect id="shape" width="16" height="16" fill="#000000" />
                </svg>
                """);
            var parameters = new SvgParameters(
                null,
                null,
                null,
                CreateLoadOptions(SvgExternalResourcePolicy.SameOrigin));

            var document = SvgService.OpenSvg(svgPath, parameters);
            var shape = Assert.IsType<SvgRectangle>(document!.GetElementById("shape"));
            var fill = Assert.IsType<SvgColourServer>(shape.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void OpenSvg_SameDocumentAndDataOnlyResourcePolicyAllowsDataCssImports()
    {
        const string cssDataUri = "data:text/css,%23shape%20%7B%20fill%3A%20%23ff0000%3B%20%7D";
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
              <style>@import "{{cssDataUri}}";</style>
              <rect id="shape" width="16" height="16" fill="#000000" />
            </svg>
            """;
        var parameters = new SvgParameters(
            null,
            null,
            null,
            CreateLoadOptions(SvgExternalResourcePolicy.SameDocumentAndDataOnly));

        var document = SvgService.FromSvg(svg, parameters);
        var shape = Assert.IsType<SvgRectangle>(document!.GetElementById("shape"));
        var fill = Assert.IsType<SvgColourServer>(shape.Fill);

        Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void OpenSvg_SameOriginResourcePolicyAllowsXmlStylesheetUnderDocumentDirectory()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var cssPath = Path.Combine(tempDirectory.FullName, "linked.css");
            File.WriteAllText(cssPath, "#shape { fill: #ff0000; }");
            var svgPath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(svgPath, """
                <?xml-stylesheet type="text/css" href="linked.css"?>
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <rect id="shape" width="16" height="16" fill="#000000" />
                </svg>
                """);
            var parameters = new SvgParameters(
                null,
                null,
                null,
                CreateLoadOptions(SvgExternalResourcePolicy.SameOrigin));

            var document = SvgService.OpenSvg(svgPath, parameters);
            var shape = Assert.IsType<SvgRectangle>(document!.GetElementById("shape"));
            var fill = Assert.IsType<SvgColourServer>(shape.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void OpenSvg_DisabledResourcePolicyBlocksXmlStylesheetDuringParse()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var cssPath = Path.Combine(tempDirectory.FullName, "linked.css");
            File.WriteAllText(cssPath, "#shape { fill: #ff0000; }");
            var svgPath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(svgPath, """
                <?xml-stylesheet type="text/css" href="linked.css"?>
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <rect id="shape" width="16" height="16" fill="#000000" />
                </svg>
                """);
            var parameters = new SvgParameters(
                null,
                null,
                null,
                CreateLoadOptions(SvgExternalResourcePolicy.Disabled));

            var document = SvgService.OpenSvg(svgPath, parameters);
            var shape = Assert.IsType<SvgRectangle>(document!.GetElementById("shape"));
            var fill = Assert.IsType<SvgColourServer>(shape.Fill);

            Assert.Equal(Color.Black.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void OpenSvg_SameOriginResourcePolicyAllowsLinkStylesheetUnderDocumentDirectory()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var cssPath = Path.Combine(tempDirectory.FullName, "linked.css");
            File.WriteAllText(cssPath, "#shape { fill: #ff0000; }");
            var svgPath = Path.Combine(tempDirectory.FullName, "source.svg");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <link rel="stylesheet" type="text/css" href="linked.css" />
                  <rect id="shape" width="16" height="16" fill="#000000" />
                </svg>
                """);
            var parameters = new SvgParameters(
                null,
                null,
                null,
                CreateLoadOptions(SvgExternalResourcePolicy.SameOrigin));

            var document = SvgService.OpenSvg(svgPath, parameters);
            var shape = Assert.IsType<SvgRectangle>(document!.GetElementById("shape"));
            var fill = Assert.IsType<SvgColourServer>(shape.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void OpenSvg_SameDocumentAndDataOnlyResourcePolicyAllowsDataLinkStylesheet()
    {
        const string cssDataUri = "data:text/css,%23shape%20%7B%20fill%3A%20%23ff0000%3B%20%7D";
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
              <link rel="stylesheet" type="text/css" href="{{cssDataUri}}" />
              <rect id="shape" width="16" height="16" fill="#000000" />
            </svg>
            """;
        var parameters = new SvgParameters(
            null,
            null,
            null,
            CreateLoadOptions(SvgExternalResourcePolicy.SameDocumentAndDataOnly));

        var document = SvgService.FromSvg(svg, parameters);
        var shape = Assert.IsType<SvgRectangle>(document!.GetElementById("shape"));
        var fill = Assert.IsType<SvgColourServer>(shape.Fill);

        Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void GetImage_SameOriginSvgResourceResolvesCssImportAgainstNestedSvgBaseUri()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var cssPath = Path.Combine(tempDirectory.FullName, "nested.css");
            File.WriteAllText(cssPath, "#nested-shape { fill: #ff0000; }");
            var nestedSvgPath = Path.Combine(tempDirectory.FullName, "nested.svg");
            File.WriteAllText(nestedSvgPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <style>@import "nested.css";</style>
                  <rect id="nested-shape" width="16" height="16" fill="#000000" />
                </svg>
                """);
            var parentSvgPath = Path.Combine(tempDirectory.FullName, "parent.svg");
            var nestedSvgUri = new Uri(Path.GetFullPath(nestedSvgPath)).AbsoluteUri;
            var parentSvg = $$"""
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <image id="asset" href="{{nestedSvgUri}}" width="16" height="16" />
                </svg>
                """;
            File.WriteAllText(parentSvgPath, parentSvg);
            var parameters = new SvgParameters(
                null,
                null,
                null,
                CreateLoadOptions(SvgExternalResourcePolicy.SameOrigin));

            var parentDocument = SvgService.FromSvg(parentSvg, parameters);
            parentDocument!.BaseUri = new Uri(Path.GetFullPath(parentSvgPath));
            var image = Assert.IsType<SvgImage>(parentDocument.GetElementById("asset"));
            Assert.True(image.TryGetEffectiveHrefString(out var href));

            var nestedDocument = Assert.IsType<SvgDocument>(SvgService.GetImage(href, image, new CountingAssetLoader()));
            var shape = Assert.IsType<SvgRectangle>(nestedDocument.GetElementById("nested-shape"));
            var fill = Assert.IsType<SvgColourServer>(shape.Fill);

            Assert.Equal(Color.Red.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetImage_NestedDataSvgInheritsResourcePolicy()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var imagePath = Path.Combine(tempDirectory.FullName, "nested.png");
            File.WriteAllBytes(imagePath, new byte[] { 1, 2, 3, 4 });
            var imageUri = new Uri(Path.GetFullPath(imagePath)).AbsoluteUri;
            var nestedSvg = $$"""
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <image id="nested-asset" href="{{imageUri}}" width="16" height="16" />
                </svg>
                """;
            var nestedDataUri = "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(nestedSvg));
            var parentSvg = $$"""
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <image id="asset" href="{{nestedDataUri}}" width="16" height="16" />
                </svg>
                """;
            var parameters = new SvgParameters(
                null,
                null,
                null,
                CreateLoadOptions(SvgExternalResourcePolicy.SameDocumentAndDataOnly));

            var parentDocument = SvgService.FromSvg(parentSvg, parameters);
            var parentImage = Assert.IsType<SvgImage>(parentDocument!.GetElementById("asset"));
            Assert.True(parentImage.TryGetEffectiveHrefString(out var parentHref));

            var nestedDocument = Assert.IsType<SvgDocument>(SvgService.GetImage(parentHref, parentImage, new CountingAssetLoader()));
            var nestedImage = Assert.IsType<SvgImage>(nestedDocument.GetElementById("nested-asset"));
            Assert.True(nestedImage.TryGetEffectiveHrefString(out var nestedHref));
            var assetLoader = new CountingAssetLoader();

            var nestedRaster = SvgService.GetImage(nestedHref, nestedImage, assetLoader);

            Assert.Null(nestedRaster);
            Assert.Equal(0, assetLoader.LoadImageCallCount);
            Assert.Equal(SvgExternalResourcePolicy.SameDocumentAndDataOnly, SvgService.GetDocumentLoadOptions(nestedDocument).ExternalResources);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void OpenSvg_SameOriginResourcePolicyBlocksCssImportsOutsideDocumentDirectory()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var documentDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory.FullName, "document"));
            var cssPath = Path.Combine(tempDirectory.FullName, "outside.css");
            File.WriteAllText(cssPath, "#shape { fill: #ff0000; }");
            var svgPath = Path.Combine(documentDirectory.FullName, "source.svg");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16">
                  <style>@import "../outside.css";</style>
                  <rect id="shape" width="16" height="16" fill="#000000" />
                </svg>
                """);
            var parameters = new SvgParameters(
                null,
                null,
                null,
                CreateLoadOptions(SvgExternalResourcePolicy.SameOrigin));

            var document = SvgService.OpenSvg(svgPath, parameters);
            var shape = Assert.IsType<SvgRectangle>(document!.GetElementById("shape"));
            var fill = Assert.IsType<SvgColourServer>(shape.Fill);

            Assert.Equal(Color.Black.ToArgb(), fill.Colour.ToArgb());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    private static SvgDocument CreateDocument(SvgExternalResourcePolicy externalResourcePolicy)
    {
        return SvgService.FromSvg(
            MinimalSvg,
            new SvgParameters(null, null, null, CreateLoadOptions(externalResourcePolicy)))!;
    }

    private static SvgDocumentLoadOptions CreateLoadOptions(SvgExternalResourcePolicy externalResourcePolicy)
    {
        return new SvgDocumentLoadOptions
        {
            ExternalResources = externalResourcePolicy
        };
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
