using System.Linq;
using Microsoft.UI.Xaml.Markup;
using Xunit;

namespace Uno.Svg.Skia.UnitTests;

public class SvgMLContentTests
{
    [Fact]
    public void ElementContentProperty_UsesMixedContentNodes()
    {
        var attribute = typeof(SvgML.tspan)
            .GetCustomAttributes(typeof(ContentPropertyAttribute), inherit: true)
            .OfType<ContentPropertyAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(nameof(SvgML.element.ContentNodes), attribute!.Name);
    }

}
