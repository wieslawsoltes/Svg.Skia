using System;
using Svg;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class SvgServiceImageUriTests
{
    [Fact]
    public void GetImageUri_RelativeImageHrefWithoutBaseUri_RemainsRelative()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="32" height="32">
              <image id="missing-image" width="99" height="108" xlink:href="6F03BD87.png" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var image = Assert.IsType<SvgImage>(document!.GetElementById("missing-image"));

        var uri = SvgService.GetImageUri(image.Href, image);

        Assert.False(uri.IsAbsoluteUri);
        Assert.Equal("6F03BD87.png", uri.OriginalString);
    }

    [Fact]
    public void GetImage_RelativeImageHrefWithoutBaseUri_ReturnsNull()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="32" height="32">
              <image id="missing-image" width="99" height="108" xlink:href="6F03BD87.png" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var image = Assert.IsType<SvgImage>(document!.GetElementById("missing-image"));

        var loadedImage = SvgService.GetImage(image.Href, image, new TestAssetLoader());

        Assert.Null(loadedImage);
    }

    [Fact]
    public void GetImageUri_DocumentOwnerWithoutBaseUri_RemainsRelative()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" />
            """;

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);

        var uri = SvgService.GetImageUri("6F03BD87.png", document!);

        Assert.False(uri.IsAbsoluteUri);
        Assert.Equal("6F03BD87.png", uri.OriginalString);
    }

    [Fact]
    public void GetImageUri_DocumentOwnerUsesDocumentBaseUri()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" />
            """;

        var document = SvgService.FromSvg(svg);
        Assert.NotNull(document);
        document!.BaseUri = new Uri("https://example.test/assets/source.svg");

        var uri = SvgService.GetImageUri("6F03BD87.png", document);

        Assert.Equal(new Uri("https://example.test/assets/6F03BD87.png"), uri);
    }
}
