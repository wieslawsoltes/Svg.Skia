using System.Collections.Generic;
using System.Drawing;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class SvgSystemColorProviderTests
{
    [Fact]
    public void DefaultProvider_ResolvesSvg11SystemColorsDeterministically()
    {
        Assert.True(SvgSystemColorResolver.TryGetColor("Window", out var window));
        Assert.True(SvgSystemColorResolver.TryGetColor("windowtext", out var windowText));
        Assert.True(SvgSystemColorResolver.TryGetColor("Highlight", out var highlight));
        Assert.True(SvgSystemColorResolver.TryGetColor("ThreeDShadow", out var threeDShadow));

        Assert.Equal(Color.FromArgb(255, 255, 255, 255).ToArgb(), window.ToArgb());
        Assert.Equal(Color.FromArgb(255, 0, 0, 0).ToArgb(), windowText.ToArgb());
        Assert.Equal(Color.FromArgb(255, 10, 36, 106).ToArgb(), highlight.ToArgb());
        Assert.Equal(Color.FromArgb(255, 128, 128, 128).ToArgb(), threeDShadow.ToArgb());
    }

    [Fact]
    public void SvgService_UsesDeterministicSystemColorsForPaintServers()
    {
        var document = SvgService.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" color="MenuText">
              <rect id="presentation" width="10" height="10" fill="Window" stroke="Highlight" />
              <rect id="styled" width="10" height="10" style="fill: ButtonFace; stroke: ThreeDDarkShadow" />
            </svg>
            """,
            null);

        var presentation = Assert.IsType<SvgRectangle>(document!.GetElementById("presentation"));
        var styled = Assert.IsType<SvgRectangle>(document.GetElementById("styled"));
        styled.FlushStyles();

        AssertColor(presentation.Fill, Color.FromArgb(255, 255, 255, 255));
        AssertColor(presentation.Stroke, Color.FromArgb(255, 10, 36, 106));
        AssertColor(styled.Fill, Color.FromArgb(255, 212, 208, 200));
        AssertColor(styled.Stroke, Color.FromArgb(255, 64, 64, 64));
        AssertColor(document.Color, Color.Black);
    }

    [Fact]
    public void ScopedProvider_OverridesDefaultSystemColorsForCurrentLoad()
    {
        var provider = new SvgDictionarySystemColorProvider(new Dictionary<string, Color>
        {
            ["Window"] = Color.FromArgb(255, 1, 2, 3),
            ["Highlight"] = Color.FromArgb(255, 4, 5, 6)
        });

        SvgDocument document;
        using (SvgSystemColorResolver.PushProvider(provider))
        {
            document = SvgService.FromSvg(
                """
                <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
                  <rect id="shape" width="10" height="10" fill="Window" stroke="Highlight" />
                </svg>
                """,
                null)!;
        }

        var shape = Assert.IsType<SvgRectangle>(document.GetElementById("shape"));
        AssertColor(shape.Fill, Color.FromArgb(255, 1, 2, 3));
        AssertColor(shape.Stroke, Color.FromArgb(255, 4, 5, 6));
        Assert.True(SvgSystemColorResolver.TryGetColor("Window", out var window));
        Assert.Equal(Color.White.ToArgb(), window.ToArgb());
    }

    [Fact]
    public void ColorProfileElement_IsPreservedAsUnsupportedOptionalPolicy()
    {
        var document = SvgService.FromSvg(
            """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="100" height="100">
              <defs>
                <color-profile id="changeColor" name="changeColor" xlink:href="../images/changeColor.ICM" />
              </defs>
              <image id="profiled" color-profile="changeColor" xlink:href="../images/colorprof.png" />
            </svg>
            """,
            null);

        var profile = document!.GetElementById("changeColor");
        Assert.NotNull(profile);
        Assert.IsType<SvgUnknownElement>(profile);
        Assert.True(profile.TryGetAttribute("name", out var profileName));
        Assert.Equal("changeColor", profileName?.ToString());

        var image = Assert.IsType<SvgImage>(document.GetElementById("profiled"));
        Assert.True(image.TryGetAttribute("color-profile", out var colorProfile));
        Assert.Equal("changeColor", colorProfile?.ToString());
        Assert.Equal("../images/colorprof.png", image.Href);
    }

    private static void AssertColor(SvgPaintServer server, Color expected)
    {
        var color = Assert.IsType<SvgColourServer>(server);
        Assert.Equal(expected.ToArgb(), color.Colour.ToArgb());
    }
}
