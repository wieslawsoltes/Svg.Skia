using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace Svg.Editor.Skia.UnitTests;

public class SvgFilterGraphTests
{
    [Fact]
    public void CatalogCreatesEveryPrimitiveAndItsNestedDefaults()
    {
        Assert.Equal(17, SvgFilterCatalog.Primitives.Count);
        foreach (var name in SvgFilterCatalog.Primitives)
        {
            var graph = new SvgFilterGraph();
            Assert.Equal(0, graph.Add(name));
            Assert.Empty(graph.Validate());
            Assert.Equal(name, Assert.Single(graph.Nodes).Name.LocalName);
        }
    }

    [Fact]
    public void ImportPreservesUnknownNamespacesCommentsAndMetadata()
    {
        var xml = "<filter xmlns='http://www.w3.org/2000/svg' xmlns:x='urn:studio' x:note='keep'><title>Custom</title><!-- keep --><feOffset dx='4' x:extra='yes'/><x:metadata>keep</x:metadata></filter>";
        var original = XElement.Parse(xml, LoadOptions.PreserveWhitespace);
        var graph = SvgFilterGraph.Parse(xml);
        Assert.True(XNode.DeepEquals(original, graph.ToElement()));
        graph.SetAttribute(0, "dy", "5");
        Assert.Contains("x:extra", graph.GetXml());
        Assert.Contains("<!-- keep -->", graph.GetXml());
        Assert.Contains("x:metadata", graph.GetXml());
    }

    [Fact]
    public void DependenciesIncludeImplicitInputsSecondInputsAndMergeChildren()
    {
        var graph = SvgFilterGraph.Parse("<filter xmlns='http://www.w3.org/2000/svg'><feGaussianBlur result='blur'/><feOffset/><feBlend in='blur' in2='SourceAlpha'/><feMerge><feMergeNode in='blur'/><feMergeNode/></feMerge></filter>");
        var connections = graph.Connections;
        Assert.Equal(6, connections.Count);
        Assert.Equal(-1, connections[0].Source);
        Assert.Equal(0, connections[1].Source);
        Assert.Equal(0, connections[2].Source);
        Assert.Equal(-1, connections[3].Source);
        Assert.Equal(0, connections[4].Source);
        Assert.Equal(2, connections[5].Source);
        Assert.Empty(graph.Validate());
    }

    [Fact]
    public void RenameUsesNearestPrecedingDefinitionRatherThanGlobalStringReplacement()
    {
        var graph = SvgFilterGraph.Parse("<filter xmlns='http://www.w3.org/2000/svg'><feOffset result='same'/><feOffset in='same'/><feOffset result='same'/><feOffset in='same'/></filter>");
        graph.RenameResult(0, "first");
        Assert.Equal("first", (string?)graph.Nodes[1].Attribute("in"));
        Assert.Equal("same", (string?)graph.Nodes[3].Attribute("in"));
        Assert.Empty(graph.Validate());
    }

    [Fact]
    public void ReorderFreezesImplicitBindingsAndRejectsForwardEdgesAtomically()
    {
        var graph = SvgFilterGraph.Parse("<filter xmlns='http://www.w3.org/2000/svg'><feOffset/><feGaussianBlur/></filter>");
        var before = graph.GetXml();
        Assert.Throws<InvalidOperationException>(() => graph.Move(0, 1));
        Assert.Equal(before, graph.GetXml());
        graph.SetAttribute(1, "in", "SourceAlpha");
        graph.Move(1, 0);
        Assert.Equal("feGaussianBlur", graph.Nodes[0].Name.LocalName);
        Assert.Equal("SourceGraphic", (string?)graph.Nodes[1].Attribute("in"));
        Assert.Empty(graph.Validate());
    }

    [Fact]
    public void BypassReconnectsConsumersToActualInput()
    {
        var graph = SvgFilterGraph.Parse("<filter xmlns='http://www.w3.org/2000/svg'><feFlood result='paint'/><feOffset in='SourceAlpha' result='shift'/><feComposite in='shift' in2='paint'/></filter>");
        var before = graph.GetXml();
        Assert.Throws<InvalidOperationException>(() => graph.Remove(1));
        Assert.Equal(before, graph.GetXml());
        graph.Remove(1, true);
        Assert.Equal("SourceAlpha", (string?)graph.Nodes[1].Attribute("in"));
        Assert.Equal("paint", (string?)graph.Nodes[1].Attribute("in2"));
        Assert.Empty(graph.Validate());
    }

    [Fact]
    public void DuplicateKeepsExistingConsumersAndDoesNotDuplicateXmlIds()
    {
        var graph = SvgFilterGraph.Parse("<filter xmlns='http://www.w3.org/2000/svg'><feDiffuseLighting id='light' result='lit'><feDistantLight id='sun'/></feDiffuseLighting><feOffset in='lit'/></filter>");
        graph.Duplicate(0);
        Assert.Equal("lit", (string?)graph.Nodes[2].Attribute("in"));
        Assert.NotEqual("lit", (string?)graph.Nodes[1].Attribute("result"));
        Assert.Equal("light-copy", (string?)graph.Nodes[1].Attribute("id"));
        Assert.Equal("sun-copy", (string?)graph.Nodes[1].Elements().Single().Attribute("id"));
        Assert.Empty(graph.Validate());
    }

    [Fact]
    public void AnimatedFilterValuesAreNotTreatedAsStaticNumericLists()
    {
        var graph = SvgFilterGraph.Parse("<filter xmlns='http://www.w3.org/2000/svg'><feGaussianBlur stdDeviation='2'><animate id='pulse' attributeName='stdDeviation' values='0;10;0' dur='2s'/></feGaussianBlur><feFlood flood-color='red'><animate attributeName='flood-color' values='red;blue' dur='2s'/></feFlood></filter>");
        Assert.Empty(graph.Validate());
        Assert.Contains("values=\"0;10;0\"", graph.GetXml());
        Assert.Contains("values=\"red;blue\"", graph.GetXml());
    }

    [Fact]
    public void DuplicateRewritesOnlyInternalAnimationAndHrefReferences()
    {
        var graph = SvgFilterGraph.Parse("<filter xmlns='http://www.w3.org/2000/svg'><feGaussianBlur id='blur' stdDeviation='2'><animate id='pulse' href='#blur' attributeName='stdDeviation' begin='pulse.end;external.click' values='0;10;0' dur='2s'/></feGaussianBlur></filter>");
        graph.Duplicate(0);
        var animation = graph.Nodes[1].Elements().Single();
        Assert.Equal("#blur-copy", (string?)animation.Attribute("href"));
        Assert.Equal("pulse-copy.end;external.click", (string?)animation.Attribute("begin"));
        graph.Duplicate(0);
        Assert.Equal("blur-copy2", (string?)graph.Nodes[1].Attribute("id"));
        Assert.Empty(graph.Validate());
    }

    [Fact]
    public void NestedTransferChannelsAndLightingAreEditable()
    {
        var graph = new SvgFilterGraph();
        graph.Add("feComponentTransfer");
        graph.SetAttribute(0, "type", "gamma", 0);
        graph.SetAttribute(0, "exponent", "2.2", 0);
        Assert.Throws<InvalidOperationException>(() => graph.AddChild(0, "feFuncR"));
        graph.RemoveChild(0, 0);
        graph.AddChild(0, "feFuncR");
        graph.Add("feDiffuseLighting");
        graph.AddChild(1, "feSpotLight");
        Assert.Equal("feSpotLight", Assert.Single(graph.Nodes[1].Elements()).Name.LocalName);
        Assert.Empty(graph.Validate());
    }

    [Theory]
    [InlineData("<feGaussianBlur stdDeviation='NaN'/>")]
    [InlineData("<feGaussianBlur stdDeviation='-1'/>")]
    [InlineData("<feOffset in='missing'/>")]
    [InlineData("<feOffset in='later'/><feOffset result='later'/>")]
    [InlineData("<feColorMatrix type='matrix' values='1 0 0'/>")]
    [InlineData("<feConvolveMatrix order='3' kernelMatrix='1 2'/>")]
    [InlineData("<feTurbulence numOctaves='1000000'/>")]
    public void InvalidDraftsCannotBeApplied(string primitives)
    {
        var graph = SvgFilterGraph.Parse("<filter xmlns='http://www.w3.org/2000/svg'>" + primitives + "</filter>");
        Assert.NotEmpty(graph.Validate());
        Assert.Throws<InvalidOperationException>(graph.ThrowIfInvalid);
    }

    [Fact]
    public void XmlEntitiesAndOversizedGraphsAreRejected()
    {
        Assert.Throws<XmlException>(() => SvgFilterGraph.Parse("<!DOCTYPE filter [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><filter xmlns='http://www.w3.org/2000/svg'>&x;</filter>"));
        var graph = new SvgFilterGraph();
        for (var i = 0; i < SvgFilterGraph.MaximumPrimitives; i++) graph.Add("feOffset");
        var before = graph.GetXml();
        Assert.Throws<ArgumentException>(() => graph.Add("feOffset"));
        Assert.Equal(before, graph.GetXml());
    }

    [Fact]
    public void ExposedXmlCopiesCannotMutateDraft()
    {
        var graph = new SvgFilterGraph();
        graph.Add("feOffset");
        graph.Nodes[0].SetAttributeValue("dx", "999");
        graph.ToElement().RemoveNodes();
        Assert.Equal("8", (string?)Assert.Single(graph.Nodes).Attribute("dx"));
    }

    [Fact]
    public void InlineCssEditingPreservesQuotedSemicolonsCommentsAndCustomProperties()
    {
        var style = "fill:url('data:image/svg+xml;a;b');/* old */ FILTER:url(#old) !important;--label:'a;b';stroke:red";
        var edited = SvgInlineStyleService.SetProperty(style, "filter", "url(#new)", true);
        Assert.Contains("data:image/svg+xml;a;b", edited);
        Assert.Contains("--label:'a;b'", edited);
        Assert.Contains("stroke:red", edited);
        Assert.DoesNotContain("#old", edited);
        Assert.EndsWith("filter:url(#new) !important", edited);
    }
}
