using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ShimSkiaSharp;
using Svg;
using Svg.JavaScript;
using Svg.Model.Services;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgJavaScriptRuntimeTests
{
    [Fact]
    public void FromSvg_DoesNotExecuteScriptsByDefault()
    {
        using var svg = new SKSvg();

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script>document.getElementById('target').setAttribute('fill', 'green');</script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Red);
    }

    [Fact]
    public void FromSvg_ExecutesInlineScriptsWhenEnabled()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script><![CDATA[
                document.getElementById('target').setAttribute('fill', 'green');
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_SkipsUnsupportedScriptTypes()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script type="application/noSuchLanguage">
                document.getElementById('target').setAttribute('fill', 'green');
              </script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Red);
    }

    [Fact]
    public void FromSvg_ExecutesRootOnLoadWhenEnabled()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20"
                 onload="document.getElementById('target').setAttribute('fill', 'green')">
              <rect id="target" width="10" height="10" fill="red" />
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void Runtime_StyleRemoveProperty_RestoresUpdatedUnderlyingAttribute()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
            </svg>
            """)!;

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        var target = runtime.GetElement(document.GetElementById("target")!);
        target.style.fill = "green";
        AssertVisualFill(document, "target", Color.Green);

        target.setAttribute("fill", "blue");
        AssertVisualFill(document, "target", Color.Green);

        Assert.Equal("green", target.style.removeProperty("fill"));
        AssertVisualFill(document, "target", Color.Blue);
    }

    [Fact]
    public void Runtime_SetStyleAttribute_AppliesCascadeAndRestoresUpdatedUnderlyingAttributeOnRemoval()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
            </svg>
            """)!;

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        var target = runtime.GetElement(document.GetElementById("target")!);
        target.setAttribute("style", "fill: green");
        AssertVisualFill(document, "target", Color.Green);

        target.setAttribute("fill", "blue");
        AssertVisualFill(document, "target", Color.Green);

        target.removeAttribute("style");
        AssertVisualFill(document, "target", Color.Blue);
    }

    [Fact]
    public void FromSvg_SetTimeoutFunctionCallback_ExecutesHandler()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script><![CDATA[
                setTimeout(function () {
                  document.getElementById('target').setAttribute('fill', 'green');
                }, 0);
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void Load_ExecutesExternalScriptsWhenEnabled()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var scriptPath = Path.Combine(tempDirectory, "update.js");
            var svgPath = Path.Combine(tempDirectory, "test.svg");
            File.WriteAllText(scriptPath, "document.getElementById('target').setAttribute('fill', 'green');");
            File.WriteAllText(svgPath, """
                <svg xmlns="http://www.w3.org/2000/svg"
                     xmlns:xlink="http://www.w3.org/1999/xlink"
                     width="20"
                     height="20">
                  <rect id="target" width="10" height="10" fill="red" />
                  <script xlink:href="update.js" />
                </svg>
                """);

            using var svg = new SKSvg();
            svg.Settings.EnableJavaScript = true;
            svg.Load(svgPath);

            AssertFill(svg, "target", Color.Green);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void DispatchPointerReleased_ExecutesClickHandlerAndRefreshesDocument()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="20" height="20" fill="red"
                    onclick="evt.target.ownerDocument.getElementById('target').style.fill = 'green'" />
            </svg>
            """);

        var dispatcher = new SvgInteractionDispatcher();
        var input = new SvgPointerInput(
            new SKPoint(5, 5),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "test");

        Assert.Equal("target", svg.HitTestTopmostElement(input.PicturePoint)?.ID);
        var target = Assert.IsType<SvgRectangle>(svg.SourceDocument!.Descendants().Single(element => element.ID == "target"));
        Assert.True(target.TryGetAttribute("onclick", out _));

        dispatcher.DispatchPointerPressed(svg, input);
        dispatcher.DispatchPointerReleased(svg, input);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void DispatchPointerReleased_HonorsJavaScriptStopPropagation()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g onclick="document.getElementById('target').setAttribute('fill', 'blue')">
                <rect id="target" width="20" height="20" fill="red"
                      onclick="evt.stopPropagation(); this.setAttribute('fill', 'green')" />
              </g>
            </svg>
            """);

        var dispatcher = new SvgInteractionDispatcher();
        var input = new SvgPointerInput(
            new SKPoint(5, 5),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "test");

        dispatcher.DispatchPointerPressed(svg, input);
        dispatcher.DispatchPointerReleased(svg, input);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void DispatchPointerReleased_ReturnFalseMarksEventHandled()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="20" height="20" fill="red" onclick="return false;" />
            </svg>
            """);

        var dispatcher = new SvgInteractionDispatcher();
        var input = new SvgPointerInput(
            new SKPoint(5, 5),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "test");

        dispatcher.DispatchPointerPressed(svg, input);
        var result = dispatcher.DispatchPointerReleased(svg, input);

        Assert.True(result.Handled);
    }

    [Fact]
    public void Clone_DispatchPointerReleased_ExecutesClickHandler()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="20" height="20" fill="red"
                    onclick="this.setAttribute('fill', 'green')" />
            </svg>
            """);

        using var clone = svg.Clone();
        var dispatcher = new SvgInteractionDispatcher();
        var input = new SvgPointerInput(
            new SKPoint(5, 5),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "test");

        dispatcher.DispatchPointerPressed(clone, input);
        dispatcher.DispatchPointerReleased(clone, input);

        AssertFill(svg, "target", Color.Red);
        AssertFill(clone, "target", Color.Green);
    }

    [Fact]
    public void DispatchPointerReleased_UseInstanceClick_ExposesCorrespondingInstanceTargets()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="480" height="360">
              <defs>
                <rect id="reference" x="240" y="1" width="239" height="358"/>
                <script><![CDATA[
                  function test(event) {
                    var reference = document.getElementById('reference');
                    var use = document.getElementById('use');
                    var pass = 0;
                    var colors = ['red', 'orange', 'green'];

                    if (event.target.correspondingUseElement === use) {
                      document.getElementById('assertion_1').setAttribute('fill', 'green');
                      pass++;
                    }

                    if (event.target.correspondingElement === reference) {
                      document.getElementById('assertion_2').setAttribute('fill', 'green');
                      pass++;
                    }

                    use.setAttribute('fill', colors[pass]);
                  }
                ]]></script>
              </defs>
              <use id="use" xlink:href="#reference" onclick="test(evt)" fill="grey"/>
              <text id="assertion_1" fill="red" x="5" y="80">Test for correspondingUseElement</text>
              <text id="assertion_2" fill="red" x="5" y="110">Test for correspondingElement</text>
            </svg>
            """);

        var dispatcher = new SvgInteractionDispatcher();
        var input = new SvgPointerInput(
            new SKPoint(470, 180),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "test");

        Assert.Equal("use", svg.HitTestTopmostElement(input.PicturePoint)?.ID);

        dispatcher.DispatchPointerPressed(svg, input);
        dispatcher.DispatchPointerReleased(svg, input);

        AssertVisualFill(svg.SourceDocument!, "use", Color.Green);
        AssertVisualFill(svg.SourceDocument!, "assertion_1", Color.Green);
        AssertVisualFill(svg.SourceDocument!, "assertion_2", Color.Green);
    }

    [Fact]
    public void Runtime_CheckIntersection_UsesHiddenRenderableGeometry()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg">
              <g visibility="hidden">
                <circle id="c1" cx="40" cy="40" r="10" fill="blue" stroke="lime"/>
                <circle id="c2" cx="10" cy="50" r="10" fill="red" stroke="lime"/>
                <circle id="c3" cx="20" cy="20" r="20" fill="green" stroke="lime"/>
                <line id="l1" x1="5" y1="5" x2="40" y2="20" stroke="black"/>
                <line id="l2" x1="20" y1="20" x2="40" y2="30" stroke="red"/>
                <rect id="r1" x="10" y="10" width="50" height="50" fill="none" stroke="red"/>
                <circle id="c4" cx="80" cy="50" r="10" fill="yellow"/>
              </g>
            </svg>
            """);

        var runtime = new SvgJavaScriptRuntime(document!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        var root = runtime.GetElement(document!);
        var rect = root.createSVGRect();
        rect.x = 10;
        rect.y = 10;
        rect.width = 50;
        rect.height = 50;

        var expectedIntersections = new Dictionary<string, bool>
        {
            ["c1"] = true,
            ["c2"] = true,
            ["c3"] = true,
            ["l1"] = true,
            ["l2"] = true,
            ["r1"] = true,
            ["c4"] = false
        };

        foreach (var pair in expectedIntersections)
        {
            var element = runtime.GetElement(document!.GetElementById(pair.Key)!);
            Assert.Equal(pair.Value, root.checkIntersection(element, rect));
        }

        var list = root.getIntersectionList(rect, null);
        var expectedOrder = new[] { "c1", "c2", "c3", "l1", "l2", "r1" };
        Assert.Equal(expectedOrder.Length, list.length);

        for (var i = 0; i < expectedOrder.Length; i++)
        {
            var item = Assert.IsType<SvgJavaScriptElement>(list.item(i));
            Assert.Equal(expectedOrder[i], item.id);
        }
    }

    [Fact]
    public void Load_StructDom13Fixture_AppendsPassedVerificationRows()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Load(GetW3CSvgPath("struct-dom-13-f"));

        var body = Assert.IsType<SvgGroup>(svg.SourceDocument!.GetElementById("test-body-content")!);
        var verificationGroups = body.Children
            .OfType<SvgGroup>()
            .Where(group => group.Children.OfType<SvgRectangle>().Any() && group.Children.OfType<SvgText>().Any())
            .ToList();

        Assert.Equal(17, verificationGroups.Count);

        var failedChecks = new List<string>();
        foreach (var group in verificationGroups)
        {
            var rect = Assert.Single(group.Children.OfType<SvgRectangle>());
            var text = Assert.Single(group.Children.OfType<SvgText>());

            var fill = Assert.IsType<SvgColourServer>(rect.Fill);
            if (fill.Colour.ToArgb() != Color.Lime.ToArgb() || !text.Text.Contains("PASSED"))
            {
                failedChecks.Add(text.Text);
            }
        }

        Assert.Equal(new[] { "length: FAILED" }, failedChecks);
    }

    [Fact]
    public void Runtime_DispatchEvent_BubblesThroughRecursiveUseInstanceTree()
    {
        var document = SvgDocument.Open<SvgDocument>(GetW3CSvgPath("struct-dom-20-f"));

        var runtime = new SvgJavaScriptRuntime(document!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        runtime.ExecuteDocumentScripts();

        var use = Assert.IsType<SvgUse>(document.GetElementById("use3")!);
        var instanceRoot = runtime.GetElement(use).instanceRoot;
        Assert.NotNull(instanceRoot);
        Assert.Equal(4, instanceRoot.childNodes.length);
        Assert.IsType<SvgJavaScriptTextNode>(((SvgJavaScriptElementInstance)instanceRoot.childNodes.item(0)!).correspondingElement);
        Assert.Equal("defscircle3", Assert.IsType<SvgJavaScriptElement>(((SvgJavaScriptElementInstance)instanceRoot.childNodes.item(1)!).correspondingElement).id);
        Assert.IsType<SvgJavaScriptTextNode>(((SvgJavaScriptElementInstance)instanceRoot.childNodes.item(2)!).correspondingElement);

        var nestedUseInstance = Assert.IsType<SvgJavaScriptElementInstance>(instanceRoot.childNodes.item(3));
        var nestedCircleInstance = Assert.IsType<SvgJavaScriptElementInstance>(nestedUseInstance.childNodes.item(0));
        Assert.Same(runtime.GetElement(use), nestedCircleInstance.correspondingUseElement);

        AssertFill(document, "defscircle3", Color.Lime);
        AssertFill(document, "defscircle4", Color.Lime);
    }

    [Fact]
    public void Load_StructDom20Fixture_ExecutesRecursiveUseDispatchHandlers()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Load(GetW3CSvgPath("struct-dom-20-f"));

        AssertFill(svg.SourceDocument!, "defscircle3", Color.Lime);
        AssertFill(svg.SourceDocument!, "defscircle4", Color.Lime);
    }

    [Fact]
    public void Runtime_InstanceRootChildNodes_PreserveStableTraversalObjects()
    {
        var document = SvgDocument.Open<SvgDocument>(GetW3CSvgPath("struct-dom-14-f"));

        var runtime = new SvgJavaScriptRuntime(document!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        var use = Assert.IsType<SvgUse>(document.GetElementById("use1")!);
        var instanceRoot = runtime.GetElement(use).instanceRoot;
        Assert.NotNull(instanceRoot);

        var firstChild = Assert.IsType<SvgJavaScriptElementInstance>(instanceRoot.firstChild);
        var firstListChild = Assert.IsType<SvgJavaScriptElementInstance>(instanceRoot.childNodes.item(0));
        Assert.Same(firstChild, firstListChild);
        Assert.Same(firstChild.correspondingElement, firstListChild.correspondingElement);

        var nextSibling = Assert.IsType<SvgJavaScriptElementInstance>(firstChild.nextSibling);
        var secondListChild = Assert.IsType<SvgJavaScriptElementInstance>(instanceRoot.childNodes.item(1));
        Assert.Same(nextSibling, secondListChild);
        Assert.Same(nextSibling.correspondingElement, secondListChild.correspondingElement);

        var lastChild = Assert.IsType<SvgJavaScriptElementInstance>(instanceRoot.lastChild);
        var lastListChild = Assert.IsType<SvgJavaScriptElementInstance>(instanceRoot.childNodes.item(instanceRoot.childNodes.length - 1));
        Assert.Same(lastChild, lastListChild);
        Assert.Same(lastChild.correspondingElement, lastListChild.correspondingElement);
    }

    [Fact]
    public void Runtime_IntersectionAndEnclosureLists_ReturnExpectedMixedRenderableOrder()
    {
        var document = SvgDocument.Open<SvgDocument>(GetW3CSvgPath("struct-dom-18-f"));

        var runtime = new SvgJavaScriptRuntime(document!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        var testSvg = runtime.GetElement(document.GetElementById("testSVG")!);
        var expectedIds = new[]
        {
            "testCircle",
            "testEllipse",
            "testLine",
            "testPath",
            "testPolyline",
            "testPolygon",
            "testRect",
            "testUse",
            "testImage",
            "testText"
        };

        var intersectionRect = testSvg.createSVGRect();
        intersectionRect.x = 10;
        intersectionRect.y = 0;
        intersectionRect.width = 130;
        intersectionRect.height = 98;
        Assert.Equal(expectedIds, GetNodeListIds(testSvg.getIntersectionList(intersectionRect, null)));

        var enclosureRect = testSvg.createSVGRect();
        enclosureRect.x = 0;
        enclosureRect.y = 0;
        enclosureRect.width = 200;
        enclosureRect.height = 200;
        Assert.Equal(expectedIds, GetNodeListIds(testSvg.getEnclosureList(enclosureRect, null)));
    }

    [Fact]
    public void Runtime_RecursiveUseInstanceLists_ReportExpectedChildCounts()
    {
        var document = SvgDocument.Open<SvgDocument>(GetW3CSvgPath("struct-dom-19-f"));

        var runtime = new SvgJavaScriptRuntime(document!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        var firstUse = Assert.IsType<SvgUse>(document.GetElementById("testUse1")!);
        var secondUse = Assert.IsType<SvgUse>(document.GetElementById("testUse2")!);

        var firstInstanceRoot = runtime.GetElement(firstUse).instanceRoot;
        var secondInstanceRoot = runtime.GetElement(secondUse).instanceRoot;

        Assert.NotNull(firstInstanceRoot);
        Assert.NotNull(secondInstanceRoot);
        Assert.Equal(0, firstInstanceRoot!.childNodes.length);
        Assert.Equal(1, secondInstanceRoot!.childNodes.length);
    }

    [Fact]
    public void Runtime_GetBBox_CoversRemovedNodesUseInstancesAndEmptyGroups()
    {
        var document = SvgDocument.Open<SvgDocument>(GetW3CSvgPath("types-dom-08-f"));

        var runtime = new SvgJavaScriptRuntime(document!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        AssertRect(runtime.GetElement(document.GetElementById("group1")!).getBBox(), -70, -60, 230, 200);
        AssertRect(runtime.GetElement(document.GetElementById("rect1")!).getBBox(), 10, 10, 50, 50);
        AssertRect(runtime.GetElement(document.GetElementById("rect2")!).getBBox(), 10, 10, 100, 100);
        AssertRect(runtime.GetElement(document.GetElementById("group2")!).getBBox(), -80, -80, 230, 200);
        AssertRect(runtime.GetElement(document.GetElementById("rect3")!).getBBox(), 0, 10, 150, 50);
        AssertRect(runtime.GetElement(document.GetElementById("circle1")!).getBBox(), -80, -80, 200, 200);
        AssertRect(runtime.GetElement(document.GetElementById("rect4")!).getBBox(), 10, 10, 400, 0);
        AssertRect(runtime.GetElement(document.GetElementById("myUse")!).getBBox(), -30, -20, 60, 40);
        AssertRect(runtime.GetElement(document.GetElementById("thickLine")!).getBBox(), 0, 0, 100, 0);
        Assert.Null(runtime.GetElement(document.GetElementById("emptyG")!).getBBox());

        var body = runtime.GetElement(document.GetElementById("body")!);
        var detachedCircle = runtime.GetElement(document.GetElementById("circle2")!);
        body.removeChild(detachedCircle);
        AssertRect(detachedCircle.getBBox(), -80, -80, 200, 200);
    }

    [Fact]
    public void Runtime_UseInstanceChildNodes_CanMutateCorrespondingElements()
    {
        var document = SvgDocument.Open<SvgDocument>(GetW3CSvgPath("struct-dom-07-f"));

        var runtime = new SvgJavaScriptRuntime(document!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        runtime.ExecuteDocumentScripts();

        var drawRects = Assert.IsType<SvgGroup>(document.GetElementById("drawRects")!);
        var rectangles = drawRects.Children.OfType<SvgRectangle>().ToList();
        Assert.Equal(3, rectangles.Count);

        foreach (var rectangle in rectangles)
        {
            var fill = Assert.IsType<SvgColourServer>(rectangle.Fill);
            Assert.Equal(Color.Green.ToArgb(), fill.Colour.ToArgb());
        }
    }

    [Fact]
    public void Runtime_AnimatedDomWrappers_RemainLiveAfterAttributeMutation()
    {
        var document = SvgDocument.Open<SvgDocument>(GetW3CSvgPath("types-dom-04-b"));

        var runtime = new SvgJavaScriptRuntime(document!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        var text = runtime.GetElement(document.GetElementById("text")!);
        var circle = runtime.GetElement(document.GetElementById("circle")!);
        var marker = runtime.GetElement(document.GetElementById("marker")!);
        var svg = runtime.GetElement(document.GetElementById("svg")!);
        var feTurbulence = runtime.GetElement(document.GetElementById("feTurbulence")!);
        var xList = Assert.IsType<SvgJavaScriptAnimatedLengthList>(text.x);

        Assert.Equal(3, text.rotate.baseVal.numberOfItems);
        text.setAttribute("rotate", "0 20");
        Assert.Equal(2, text.rotate.baseVal.numberOfItems);

        Assert.Equal(50d, circle.r.baseVal.value);
        circle.setAttribute("r", "100");
        Assert.Equal(100d, circle.r.baseVal.value);

        Assert.Equal(2, xList.baseVal.numberOfItems);
        text.setAttribute("x", "10");
        Assert.Equal(1, xList.baseVal.numberOfItems);

        Assert.Equal(30d, marker.orientAngle.baseVal.value);
        marker.setAttribute("orient", "60");
        Assert.Equal(60d, marker.orientAngle.baseVal.value);

        Assert.Equal(10d, svg.viewBox.baseVal.x);
        svg.setAttribute("viewBox", "20 30 40 50");
        Assert.Equal(20d, svg.viewBox.baseVal.x);

        Assert.Equal(2, circle.transform.baseVal.numberOfItems);
        circle.setAttribute("transform", "scale(1)");
        Assert.Equal(1, circle.transform.baseVal.numberOfItems);

        Assert.Equal(1, svg.preserveAspectRatio.baseVal.align);
        svg.setAttribute("preserveAspectRatio", "xMidYMid");
        Assert.Equal(6, svg.preserveAspectRatio.baseVal.align);

        Assert.False(svg.externalResourcesRequired.baseVal);
        svg.setAttribute("externalResourcesRequired", "true");
        Assert.True(svg.externalResourcesRequired.baseVal);

        Assert.Equal("one", circle.className.baseVal);
        circle.setAttribute("class", "two");
        Assert.Equal("two", circle.className.baseVal);

        Assert.Equal(1, text.lengthAdjust.baseVal);
        text.setAttribute("lengthAdjust", "spacingAndGlyphs");
        Assert.Equal(2, text.lengthAdjust.baseVal);

        Assert.Equal(2, feTurbulence.numOctaves.baseVal);
        feTurbulence.setAttribute("numOctaves", "1");
        Assert.Equal(1, feTurbulence.numOctaves.baseVal);

        Assert.Equal(3d, feTurbulence.baseFrequencyY.baseVal);
        feTurbulence.setAttribute("baseFrequency", "4 5");
        Assert.Equal(5d, feTurbulence.baseFrequencyY.baseVal);
    }

    [Fact]
    public void Runtime_StringLists_DuplicateInsertedValuesInsteadOfMovingThem()
    {
        var document = SvgDocument.Open<SvgDocument>(GetW3CSvgPath("types-dom-06-f"));

        var runtime = new SvgJavaScriptRuntime(document!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        var r1 = runtime.GetElement(document.GetElementById("r1")!);
        var r2 = runtime.GetElement(document.GetElementById("r2")!);
        var r3 = runtime.GetElement(document.GetElementById("r3")!);

        var i0 = r1.requiredFeatures.getItem(0);
        var i1 = r1.requiredFeatures.getItem(1);

        Assert.Equal("http://www.w3.org/TR/SVG11/feature#Shape", i0);
        Assert.Equal("this.is.a.bogus.feature.string", i1);
        Assert.Equal(2, r1.requiredFeatures.numberOfItems);

        r2.requiredFeatures.appendItem(i1);
        Assert.Equal(2, r1.requiredFeatures.numberOfItems);
        Assert.Equal(1, r2.requiredFeatures.numberOfItems);
        Assert.Equal("this.is.a.bogus.feature.string", r2.requiredFeatures.getItem(0));

        r3.requiredFeatures.insertItemBefore(i0, 0);
        Assert.Equal(2, r3.requiredFeatures.numberOfItems);
        Assert.Equal("http://www.w3.org/TR/SVG11/feature#Shape", r3.requiredFeatures.getItem(0));
        Assert.Equal("http://www.w3.org/TR/SVG11/feature#Shape", r3.requiredFeatures.getItem(1));
        Assert.Equal(2, r1.requiredFeatures.numberOfItems);
    }

    private static void AssertFill(SKSvg svg, string id, Color expected)
    {
        var rect = Assert.IsType<SvgRectangle>(svg.SourceDocument!.Descendants().Single(element => element.ID == id));
        var fill = Assert.IsType<SvgColourServer>(rect.Fill);
        Assert.Equal(expected.ToArgb(), fill.Colour.ToArgb());
    }

    private static void AssertVisualFill(SvgDocument document, string id, Color expected)
    {
        var element = Assert.IsAssignableFrom<SvgVisualElement>(document.Descendants().Single(node => node.ID == id));
        var fill = Assert.IsType<SvgColourServer>(element.Fill);
        Assert.Equal(expected.ToArgb(), fill.Colour.ToArgb());
    }

    private static void AssertFill(SvgDocument document, string id, Color expected)
    {
        var element = Assert.IsType<SvgCircle>(document.Descendants().Single(node => node.ID == id));
        var fill = Assert.IsType<SvgColourServer>(element.Fill);
        Assert.Equal(expected.ToArgb(), fill.Colour.ToArgb());
    }

    private static string GetW3CSvgPath(string name)
    {
        return Path.Combine("..", "..", "..", "..", "..", "externals", "W3C_SVG_11_TestSuite", "W3C_SVG_11_TestSuite", "svg", $"{name}.svg");
    }

    private static IReadOnlyList<string> GetNodeListIds(SvgJavaScriptNodeList list)
    {
        return Enumerable.Range(0, list.length)
            .Select(index => list.item(index))
            .Select(item => item switch
            {
                SvgJavaScriptElement element => element.id,
                SvgJavaScriptElementInstance instance when instance.correspondingUseElement is { } useElement => useElement.id,
                SvgJavaScriptElementInstance instance => Assert.IsType<SvgJavaScriptElement>(instance.correspondingElement).id,
                _ => throw new Xunit.Sdk.XunitException($"Unexpected node list item type: {item?.GetType().FullName ?? "<null>"}")
            })
            .ToArray();
    }

    private static void AssertRect(SvgJavaScriptRect? rect, double x, double y, double width, double height, double epsilon = 1e-4)
    {
        Assert.NotNull(rect);
        Assert.InRange(rect!.x, x - epsilon, x + epsilon);
        Assert.InRange(rect.y, y - epsilon, y + epsilon);
        Assert.InRange(rect.width, width - epsilon, width + epsilon);
        Assert.InRange(rect.height, height - epsilon, height + epsilon);
    }
}
