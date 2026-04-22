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

}
