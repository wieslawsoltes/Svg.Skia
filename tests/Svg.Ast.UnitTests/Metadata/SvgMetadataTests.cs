// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using Svg.Ast.Metadata;
using Xunit;

namespace Svg.Ast.UnitTests.Metadata;

public class SvgMetadataTests
{
    [Theory]
    [InlineData("linearGradient")]
    [InlineData("rect")]
    [InlineData("path")]
    public void TryGetElement_Returns_Metadata(string name)
    {
        Assert.True(SvgMetadata.TryGetElement(name, out var metadata));
        Assert.Equal(name, metadata.Name);
    }

    [Fact]
    public void Flags_Presentation_Attributes()
    {
        Assert.True(SvgMetadata.TryGetElement("rect", out var rectMetadata));
        Assert.Contains("fill", rectMetadata.PresentationAttributes);
        Assert.Contains("stroke", rectMetadata.PresentationAttributes);
    }

    [Fact]
    public void Attributes_Override_Behavior()
    {
        Assert.True(SvgMetadata.TryGetAttribute("fill", out var attribute));
        Assert.Equal("Paint", attribute.DataType);
        Assert.True(attribute.Animatable);
    }

    [Theory]
    [InlineData("svg", "width")]
    [InlineData("svg", "height")]
    [InlineData("svg", "viewBox")]
    [InlineData("rect", "width")]
    [InlineData("rect", "height")]
    [InlineData("rect", "rx")]
    [InlineData("linearGradient", "x1")]
    [InlineData("linearGradient", "x2")]
    [InlineData("linearGradient", "gradientUnits")]
    [InlineData("stop", "offset")]
    [InlineData("stop", "stop-color")]
    public void ElementMetadata_Allows_Core_Attributes(string elementName, string attributeName)
    {
        Assert.True(SvgMetadata.TryGetElement(elementName, out var metadata));
        Assert.True(metadata.AllowsAttribute(attributeName));
    }
}
