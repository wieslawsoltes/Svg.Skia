using System.Linq;
using Microsoft.UI.Xaml.Markup;
using Xunit;
using WindowsMarkup = System.Windows.Markup;

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

    [Fact]
    public void ElementContentProperty_HasTextContentWrapper()
    {
        var attribute = typeof(SvgML.tspan)
            .GetCustomAttributes(typeof(WindowsMarkup.ContentWrapperAttribute), inherit: true)
            .OfType<WindowsMarkup.ContentWrapperAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(typeof(SvgML.content), attribute!.ContentWrapper);
    }

    [Fact]
    public void ContentNodesCollection_PreservesWhitespaceMetadata()
    {
        var attribute = typeof(SvgML.SvgContentCollection)
            .GetCustomAttributes(typeof(WindowsMarkup.WhitespaceSignificantCollectionAttribute), inherit: true)
            .OfType<WindowsMarkup.WhitespaceSignificantCollectionAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
    }
}
