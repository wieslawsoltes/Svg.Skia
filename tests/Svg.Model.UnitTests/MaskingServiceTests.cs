using ShimSkiaSharp;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class MaskingServiceTests
{
    [Fact]
    public void GetClipRect_ParsesWhitespaceSyntaxUnitsAndAuto()
    {
        var rect = MaskingService.GetClipRect("rect(10px auto 30px 5px)", SKRect.Create(2f, 3f, 100f, 80f));

        Assert.NotNull(rect);
        Assert.Equal(7f, rect!.Value.Left);
        Assert.Equal(13f, rect.Value.Top);
        Assert.Equal(102f, rect.Value.Right);
        Assert.Equal(33f, rect.Value.Bottom);
    }

    [Fact]
    public void GetClipRect_PreservesLegacyCommaSyntaxAsOffsets()
    {
        var rect = MaskingService.GetClipRect("rect(10, 40, 30, 5)", SKRect.Create(2f, 3f, 100f, 80f));

        Assert.NotNull(rect);
        Assert.Equal(7f, rect!.Value.Left);
        Assert.Equal(13f, rect.Value.Top);
        Assert.Equal(62f, rect.Value.Right);
        Assert.Equal(53f, rect.Value.Bottom);
    }
}
