// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Linq;
using Svg.Ast;
using Xunit;

namespace Svg.Ast.UnitTests.Namespaces;

public class SvgAstBuilderNamespaceTests
{
    [Fact]
    public void Preserves_Default_And_Custom_Namespaces()
    {
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink">
              <defs>
                <linearGradient id="grad" xlink:href="#base" />
              </defs>
            </svg>
            """;

        var document = SvgAstBuilder.Build(SvgSourceText.FromString(svg));

        Assert.NotNull(document.RootElement);
        Assert.Equal("http://www.w3.org/2000/svg", document.RootElement!.Name.NamespaceUri);

        var defs = Assert.IsType<SvgAstElement>(document.RootElement.Children[0]);
        Assert.Equal("http://www.w3.org/2000/svg", defs.Name.NamespaceUri);

        var gradient = Assert.IsType<SvgAstElement>(defs.Children[0]);
        var hrefAttribute = Assert.Single(gradient.Attributes, a => a.Name.LocalName == "href");
        Assert.Equal("http://www.w3.org/1999/xlink", hrefAttribute.Name.NamespaceUri);
        Assert.Equal("xlink", hrefAttribute.Name.Prefix);
    }

    [Fact]
    public void Propagates_XmlSpace_To_Text_Nodes()
    {
        var svg = "<svg><g xml:space=\"preserve\"><text>  content  </text></g></svg>";

        var document = SvgAstBuilder.Build(SvgSourceText.FromString(svg));
        Assert.NotNull(document.RootElement);

        var group = Assert.IsType<SvgAstElement>(document.RootElement!.Children[0]);
        var xmlSpaceAttribute = Assert.Single(group.Attributes, a => a.Name.LocalName == "space");
        Assert.Equal("xml", xmlSpaceAttribute.Name.Prefix);
        Assert.Equal(SvgXmlSpace.Preserve, group.XmlSpace);

        var textElement = Assert.IsType<SvgAstElement>(group.Children.OfType<SvgAstElement>().First(e => e.Name.LocalName == "text"));
        var textNode = Assert.IsType<SvgAstText>(textElement.Children.OfType<SvgAstText>().First());
        Assert.Equal(SvgXmlSpace.Preserve, textNode.XmlSpace);
    }
}
