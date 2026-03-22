using ShimSkiaSharp;
using Svg;
using Svg.Model.Services;
using Xunit;
namespace Svg.Model.UnitTests;

public class BlendModeServiceTests
{
    [Fact]
    public void SetBlendModeToken_StoresStandardModesInBothAttributes()
    {
        var rectangle = new SvgRectangle();

        BlendModeService.SetBlendModeToken(rectangle, BlendModeService.OverlayToken);

        Assert.Equal(BlendModeService.OverlayToken, rectangle.CustomAttributes[BlendModeService.EditorBlendModeAttribute]);
        Assert.Equal(BlendModeService.OverlayToken, rectangle.CustomAttributes[BlendModeService.CssBlendModeAttribute]);
        Assert.Equal(BlendModeService.OverlayToken, BlendModeService.GetBlendModeToken(rectangle));
        Assert.Equal(SKBlendMode.Overlay, BlendModeService.GetBlendPaint(rectangle)!.BlendMode);
    }

    [Fact]
    public void SetBlendModeToken_StoresPassThroughOnlyAsEditorMetadata()
    {
        var group = new SvgGroup();

        BlendModeService.SetBlendModeToken(group, BlendModeService.PassThroughToken);

        Assert.Equal(BlendModeService.PassThroughToken, group.CustomAttributes[BlendModeService.EditorBlendModeAttribute]);
        Assert.False(group.CustomAttributes.ContainsKey(BlendModeService.CssBlendModeAttribute));
        Assert.Equal(BlendModeService.PassThroughToken, BlendModeService.GetBlendModeToken(group));
        Assert.Null(BlendModeService.GetBlendPaint(group));
    }

    [Theory]
    [InlineData("Plus Lighter", BlendModeService.PlusLighterToken, SKBlendMode.Plus)]
    [InlineData("plus-darker", BlendModeService.PlusDarkerToken, SKBlendMode.Darken)]
    [InlineData("soft light", BlendModeService.SoftLightToken, SKBlendMode.SoftLight)]
    [InlineData("ColorBurn", BlendModeService.ColorBurnToken, SKBlendMode.ColorBurn)]
    public void NormalizeAndMapBlendModes(string rawValue, string expectedToken, SKBlendMode expectedMode)
    {
        Assert.Equal(expectedToken, BlendModeService.NormalizeToken(rawValue));
        Assert.Equal(expectedMode, BlendModeService.ToBlendMode(rawValue));
    }

    [Fact]
    public void ClearBlendModeToken_RemovesBothAttributes()
    {
        var rectangle = new SvgRectangle();
        BlendModeService.SetBlendModeToken(rectangle, BlendModeService.ScreenToken);

        BlendModeService.ClearBlendModeToken(rectangle);

        Assert.Empty(rectangle.CustomAttributes);
        Assert.Null(BlendModeService.GetBlendModeToken(rectangle));
        Assert.Null(BlendModeService.GetBlendPaint(rectangle));
    }

    [Fact]
    public void GetBlendModeToken_IgnoresRawCssAttributeWithoutEditorMetadata()
    {
        var rectangle = new SvgRectangle();
        rectangle.CustomAttributes[BlendModeService.CssBlendModeAttribute] = BlendModeService.OverlayToken;

        Assert.Null(BlendModeService.GetBlendModeToken(rectangle));
        Assert.Null(BlendModeService.GetBlendPaint(rectangle));
    }

}
