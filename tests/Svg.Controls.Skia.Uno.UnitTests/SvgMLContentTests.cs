using System.Linq;
using Microsoft.UI.Xaml.Markup;
using Xunit;

namespace Uno.Svg.Skia.UnitTests;

public class SvgMLContentTests
{
    [Fact]
    public void TextElementContentProperty_UsesMixedContentNodes()
    {
        var attribute = typeof(SvgML.text)
            .GetCustomAttributes(typeof(ContentPropertyAttribute), inherit: true)
            .OfType<ContentPropertyAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(nameof(SvgML.element.ContentNodes), attribute!.Name);
    }

    [Fact]
    public void TspanContentProperty_UsesText()
    {
        var attribute = typeof(SvgML.tspan)
            .GetCustomAttributes(typeof(ContentPropertyAttribute), inherit: true)
            .OfType<ContentPropertyAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(nameof(SvgML.text_base.Text), attribute!.Name);
    }

    [Fact]
    public void TextPathContentProperty_UsesText()
    {
        var attribute = typeof(SvgML.textPath)
            .GetCustomAttributes(typeof(ContentPropertyAttribute), inherit: true)
            .OfType<ContentPropertyAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(nameof(SvgML.text_base.Text), attribute!.Name);
    }

    [Fact]
    public void EnumBridge_FormatsSvgEnumsWithoutTypeDescriptor()
    {
        Assert.Equal("visiblePainted", new SvgML.SvgPointerEventsValue(global::Svg.SvgPointerEvents.VisiblePainted).ToString());
        Assert.Equal("whenNotActive", new SvgML.SvgAnimationRestartValue(global::Svg.SvgAnimationRestart.WhenNotActive).ToString());
        Assert.Equal(global::Svg.SvgPointerEvents.VisiblePainted, SvgML.SvgPointerEventsValue.Parse("visiblePainted").Value);
    }

    [Fact]
    public void EnumBridge_FormatsManualEnumsWithoutTypeDescriptor()
    {
        Assert.Equal("color-dodge", new SvgML.BlendModeValue(SvgML.blend_mode.color_dodge).ToString());
        Assert.Equal(SvgML.blend_mode.color_dodge, SvgML.BlendModeValue.Parse("color-dodge").Value);
        Assert.Equal("hueRotate", new SvgML.TypeFeColorMatrixValue(SvgML.type_feColorMatrix.hueRotate).ToString());
    }
}
