using System.Linq;
using ShimSkiaSharp;
using Svg;
using Svg.Model.Services;
using Xunit;

namespace Svg.Skia.UnitTests;

public class Issue99Tests
{
    [Fact]
    public void NestedSvgPercentageSizeResolvesAgainstViewport()
    {
        const string SvgMarkup = """
<?xml version="1.0" encoding="utf-8"?>
<svg version="1.1" xmlns="http://www.w3.org/2000/svg" width="100" height="100">
  <circle cx="100" cy="100" r="100" fill="gray" />
  <svg width="50%" height="50%">
    <circle cx="100" cy="100" r="100" fill="gold" />
  </svg>
</svg>
""";

        var svgDocument = SvgService.FromSvg(SvgMarkup);
        Assert.NotNull(svgDocument);

        var documentSize = SvgService.GetDimensions(svgDocument!);
        var parentViewport = SKRect.Create(documentSize);

        var nestedFragment = svgDocument!.Children.OfType<SvgFragment>().First();
        var resolvedSize = SvgService.GetDimensions(nestedFragment, parentViewport);

        Assert.Equal(50f, resolvedSize.Width, 3);
        Assert.Equal(50f, resolvedSize.Height, 3);
    }
}
