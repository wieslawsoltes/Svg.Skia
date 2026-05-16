using System;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class Svg2StaticHrefTests
{
    [Fact]
    public void LoadOptions_DefaultToStaticSvg2HrefContract()
    {
        var options = new SvgDocumentLoadOptions();

        Assert.Equal(SvgProcessingMode.Static, options.ProcessingMode);
        Assert.Equal(SvgExternalResourcePolicy.Enabled, options.ExternalResources);
        Assert.True(options.PreserveUnknownElements);
        Assert.True(options.PreferSvg2Href);
    }

    [Fact]
    public void TryGetEffectiveHref_PrefersUnnamespacedHrefOverXlinkHref()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink">
              <image id="asset" href="modern.png" xlink:href="legacy.png" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        var image = Assert.IsType<SvgImage>(document!.GetElementById("asset"));

        Assert.True(image.TryGetEffectiveHrefString(out var hrefText));
        Assert.Equal("modern.png", hrefText);
        Assert.True(image.TryGetEffectiveHref(out var href));
        Assert.Equal(new Uri("modern.png", UriKind.RelativeOrAbsolute), href);
    }

    [Fact]
    public void TryGetEffectiveHref_CanPreferLegacyXlinkHrefWhenRequested()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink">
              <image id="asset" href="modern.png" xlink:href="legacy.png" />
            </svg>
            """;
        var parameters = new SvgParameters(
            null,
            null,
            null,
            new SvgDocumentLoadOptions { PreferSvg2Href = false });

        var document = SvgService.FromSvg(svg, parameters);
        var image = Assert.IsType<SvgImage>(document!.GetElementById("asset"));

        Assert.True(image.TryGetEffectiveHrefString(out var hrefText));
        Assert.Equal("legacy.png", hrefText);
    }

    [Fact]
    public void TryGetEffectiveHref_FallsBackToXlinkHref()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink">
              <image id="asset" xlink:href="legacy.png" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        var image = Assert.IsType<SvgImage>(document!.GetElementById("asset"));

        Assert.True(image.TryGetEffectiveHrefString(out var hrefText));
        Assert.Equal("legacy.png", hrefText);
        Assert.True(image.TryGetEffectiveHref(out var href));
        Assert.Equal(new Uri("legacy.png", UriKind.RelativeOrAbsolute), href);
    }

    [Fact]
    public void TryGetEffectiveHref_EmptyUnnamespacedHrefDoesNotFallBackToXlinkHref()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink">
              <image id="asset" href="" xlink:href="legacy.png" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        var image = Assert.IsType<SvgImage>(document!.GetElementById("asset"));

        Assert.True(image.TryGetEffectiveHrefString(out var hrefText));
        Assert.Equal(string.Empty, hrefText);
        Assert.False(image.TryGetEffectiveHref(out _));
    }

    [Fact]
    public void TryGetEffectiveHref_WhitespaceUnnamespacedHrefDoesNotFallBackToXlinkHref()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink">
              <image id="asset" href="   " xlink:href="legacy.png" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        var image = Assert.IsType<SvgImage>(document!.GetElementById("asset"));

        Assert.True(image.TryGetEffectiveHrefString(out var hrefText));
        Assert.Equal("   ", hrefText);
        Assert.False(image.TryGetEffectiveHref(out _));
    }

    [Fact]
    public void TryGetEffectiveHref_UsesProgrammaticHrefAfterParsing()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink">
              <use id="asset" href="#modern" xlink:href="#legacy" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        var use = Assert.IsType<SvgUse>(document!.GetElementById("asset"));

        use.ReferencedElement = new Uri("#programmatic", UriKind.RelativeOrAbsolute);

        Assert.True(use.TryGetEffectiveHrefString(out var hrefText));
        Assert.Equal("#programmatic", hrefText);
        Assert.True(use.TryGetEffectiveHref(out var href));
        Assert.Equal(new Uri("#programmatic", UriKind.RelativeOrAbsolute), href);
    }

    [Fact]
    public void TryGetEffectiveHref_InvalidUriReturnsFalse()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg">
              <image id="asset" href="http://[invalid" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        var image = Assert.IsType<SvgImage>(document!.GetElementById("asset"));

        Assert.True(image.TryGetEffectiveHrefString(out var hrefText));
        Assert.Equal("http://[invalid", hrefText);
        Assert.False(image.TryGetEffectiveHref(out _));
    }

    [Fact]
    public void TryGetEffectiveHref_UppercaseHrefAttributeDoesNotOverrideXlinkHref()
    {
        const string svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink">
              <image id="asset" Href="uppercase.png" xlink:href="legacy.png" />
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        var image = Assert.IsType<SvgImage>(document!.GetElementById("asset"));

        Assert.True(image.TryGetEffectiveHrefString(out var hrefText));
        Assert.Equal("legacy.png", hrefText);
    }

    [Fact]
    public void TryGetEffectiveHref_ReadsNamespaceQualifiedXlinkCustomAttribute()
    {
        var image = new SvgImage();
        image.CustomAttributes[SvgNamespaces.XLinkNamespace + ":href"] = "custom-legacy.png";

        Assert.True(image.TryGetEffectiveHrefString(out var hrefText));
        Assert.Equal("custom-legacy.png", hrefText);
        Assert.True(image.TryGetEffectiveHref(out var href));
        Assert.Equal(new Uri("custom-legacy.png", UriKind.RelativeOrAbsolute), href);
    }

    [Theory]
    [InlineData("linearGradient")]
    [InlineData("radialGradient")]
    [InlineData("pattern")]
    public void PaintServerInheritance_UsesSvg2HrefPrecedenceWhenXlinkHrefAppearsLast(string elementName)
    {
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink">
              <defs>
                <{{elementName}} id="modern" />
                <{{elementName}} id="legacy" />
                <{{elementName}} id="target" href="#modern" xlink:href="#legacy" />
              </defs>
            </svg>
            """;

        var document = SvgService.FromSvg(svg);
        var target = Assert.IsAssignableFrom<SvgPaintServer>(document!.GetElementById("target"));

        var href = target switch
        {
            SvgGradientServer gradient => gradient.InheritGradient,
            SvgPatternServer pattern => pattern.InheritGradient,
            _ => null
        };

        Assert.NotNull(href);
        Assert.Equal("#modern", href!.DeferredId);
    }

    [Fact]
    public void SvgParameters_ThreeArgumentNullCallRemainsCurrentColorCompatible()
    {
        var parameters = new SvgParameters(null, null, null);

        Assert.Null(parameters.CurrentColor);
        Assert.Null(parameters.LoadOptions);
    }
}
