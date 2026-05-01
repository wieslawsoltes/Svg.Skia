using System.Drawing;
using System.Linq;
using System.Xml;
using Svg;
using Svg.JavaScript;
using Xunit;

namespace Svg.JavaScript.UnitTests;

public class SvgJavaScriptRuntimeTests
{
    [Fact]
    public void ExecuteDocumentScripts_ExecutesInlineScripts()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script>document.getElementById('target').setAttribute('fill', 'green');</script>
            </svg>
            """);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        runtime.ExecuteDocumentScripts();

        AssertFill(document, "target", Color.Green);
        Assert.Equal(1, runtime.MutationVersion);
    }

    [Fact]
    public void ExecuteDocumentScripts_ExecutesOnLoad()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20"
                 onload="document.getElementById('target').setAttribute('fill', 'green')">
              <rect id="target" width="10" height="10" fill="red" />
            </svg>
            """);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        runtime.ExecuteDocumentScripts();

        AssertFill(document, "target", Color.Green);
    }

    [Fact]
    public void ExecuteEventHandler_ProvidesEventInput()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="20" height="20" fill="red"
                    onclick="if (evt.clientX === 4 &amp;&amp; evt.clientY === 5 &amp;&amp; evt.button === 0) evt.target.setAttribute('fill', 'green')" />
            </svg>
            """);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var target = document.Descendants().Single(element => element.ID == "target");
        var input = new SvgJavaScriptEventInput(4, 5, SvgJavaScriptMouseButton.Left, 1, 0, false, false, false);

        var result = runtime.ExecuteEventHandler(target, target, null, "click", "onclick", input);

        Assert.True(result.Mutated);
        AssertFill(document, "target", Color.Green);
    }

    [Fact]
    public void ExecuteEventHandler_UsesCurrentTargetAsThisAndAllowsReturn()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="20" height="20" fill="red"
                    onclick="this.setAttribute('fill', 'green'); return false;" />
            </svg>
            """);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var target = document.Descendants().Single(element => element.ID == "target");

        var result = runtime.ExecuteEventHandler(target, target, null, "click", "onclick", null);

        Assert.True(result.Mutated);
        Assert.True(result.DefaultPrevented);
        AssertFill(document, "target", Color.Green);
    }

    [Fact]
    public void ExecuteDocumentScripts_ExposesDefaultViewAsWindow()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script>
                if (window === document.defaultView) {
                  document.getElementById('target').setAttribute('fill', 'green');
                }
              </script>
            </svg>
            """);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });

        runtime.ExecuteDocumentScripts();

        AssertFill(document, "target", Color.Green);
    }

    [Fact]
    public void AppendChild_MovesTextNodeOutOfPreviousParent()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <text id="source">hello</text>
              <text id="target"></text>
              <script>
                var source = document.getElementById('source');
                var target = document.getElementById('target');
                target.appendChild(source.firstChild);
              </script>
            </svg>
            """);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });

        runtime.ExecuteDocumentScripts();

        var source = Assert.IsType<SvgText>(document.Descendants().Single(element => element.ID == "source"));
        var target = Assert.IsType<SvgText>(document.Descendants().Single(element => element.ID == "target"));
        Assert.Equal(string.Empty, source.Content);
        Assert.Empty(source.Nodes);
        Assert.Equal("hello", target.Content);
        Assert.Single(target.Nodes);
    }

    private static SvgDocument LoadDocument(string svg)
    {
        var xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(svg);
        return SvgDocument.Open(xmlDocument);
    }

    private static void AssertFill(SvgDocument document, string id, Color expected)
    {
        var rect = Assert.IsType<SvgRectangle>(document.Descendants().Single(element => element.ID == id));
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);
        Assert.Equal(expected.ToArgb(), fill.Colour.ToArgb());
    }
}
