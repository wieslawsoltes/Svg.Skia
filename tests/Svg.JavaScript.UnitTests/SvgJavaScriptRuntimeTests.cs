using System.Drawing;
using System.Linq;
using System.Xml;
using Svg;
using Svg.JavaScript;
using Svg.Model.Services;
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
    public void ExecuteDocumentScripts_DispatchesLoadForElementsInPostOrder()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20"
                 onload="mark(evt, 'svg')">
              <rect id="target" width="10" height="10" fill="red" data-order="" />
              <g onload="mark(evt, 'g')">
                <rect id="child" width="10" height="10" onload="mark(evt, 'rect')" />
              </g>
              <script>
                function mark(evt, name) {
                  var target = document.getElementById('target');
                  target.setAttribute('data-order', target.getAttribute('data-order') + name + ':' + evt.bubbles + ';');
                }
              </script>
            </svg>
            """, captureJavaScriptDomState: true);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        runtime.ExecuteDocumentScripts();

        var target = runtime.GetElement(document.Descendants().Single(element => element.ID == "target"));
        Assert.Equal("rect:false;g:false;svg:false;", target.getAttribute("data-order"));
    }

    [Fact]
    public void ExecuteDocumentScripts_DispatchesNonBubblingImageLoad()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <image id="image" href="missing.png" width="1" height="1"
                     onload="if (!evt.bubbles &amp;&amp; evt.target == this) document.getElementById('target').setAttribute('fill', 'green')" />
            </svg>
            """);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        runtime.ExecuteDocumentScripts();

        AssertFill(document, "target", Color.Green);
    }

    [Fact]
    public void NodeLists_AreLiveAcrossDomMutations()
    {
        var document = LoadDocument("""<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" />""");
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var root = runtime.GetElement(document);
        var documentFacade = root.ownerDocument;
        var rects = documentFacade.getElementsByTagName("rect");
        var childNodes = root.childNodes;

        Assert.Equal(0, rects.length);
        Assert.Equal(0, childNodes.length);

        var rect = documentFacade.createElementNS("http://www.w3.org/2000/svg", "rect");
        root.appendChild(rect);

        Assert.Equal(1, rects.length);
        Assert.Equal(1, childNodes.length);
        Assert.Same(rect, rects.item(0));
        Assert.Same(rect, root.lastChild);
        Assert.True(root.hasChildNodes());

        root.removeChild(rect);

        Assert.Equal(0, rects.length);
        Assert.Equal(0, childNodes.length);
        Assert.False(root.hasChildNodes());
    }

    [Fact]
    public void GetElementsByTagNameNS_ReturnsLiveForeignNamespaceElements()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:bd="http://example.org/ExampleBusinessData" width="20" height="20">
              <bd:Results>
                <bd:Region><bd:RegionName>East</bd:RegionName></bd:Region>
              </bd:Results>
            </svg>
            """);
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var documentFacade = runtime.GetElement(document).ownerDocument;
        var regions = documentFacade.getElementsByTagNameNS("http://example.org/ExampleBusinessData", "Region");
        var names = documentFacade.getElementsByTagNameNS("http://example.org/ExampleBusinessData", "RegionName");

        Assert.Equal(1, regions.length);
        Assert.Equal(1, names.length);

        var region = Assert.IsType<SvgJavaScriptElement>(regions.item(0));
        var name = Assert.IsType<SvgJavaScriptElement>(names.item(0));
        Assert.Equal("Region", region.localName);
        Assert.Equal("http://example.org/ExampleBusinessData", region.namespaceURI);
        Assert.Equal("East", Assert.IsType<SvgJavaScriptTextNode>(name.firstChild).nodeValue);
    }

    [Fact]
    public void SetAttributeNS_MapsOnlyRealXLinkHrefToSvgHref()
    {
        var document = LoadDocument("""<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" />""");
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var image = runtime.GetElement(document).ownerDocument.createElementNS("http://www.w3.org/2000/svg", "image");

        image.setAttributeNS("http://www.w3.org/1999/xlink", "randomPrefix:href", "expected.png");
        image.setAttributeNS("http://www.this.is.not.an/xlink", "xlink:href", "ignored.png");

        Assert.Equal("expected.png", image.getAttribute("href"));
        Assert.Equal("expected.png", image.getAttributeNS("http://www.w3.org/1999/xlink", "href"));
        Assert.Equal("ignored.png", image.getAttributeNS("http://www.this.is.not.an/xlink", "xlink:href"));
    }

    [Fact]
    public void ParsedForeignNamespaceXLinkPrefix_DoesNotBindSvgHref()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g xmlns:xlink="http://www.this.is.not.an/xlink">
                <image id="image" width="10" height="10" xlink:href="ignored.png" />
              </g>
            </svg>
            """, captureJavaScriptDomState: true);

        var image = Assert.IsType<SvgImage>(document.Descendants().Single(element => element.ID == "image"));
        Assert.True(string.IsNullOrEmpty(image.Href));
    }

    [Fact]
    public void SvgLengthValue_ResolvesPercentAgainstCurrentViewportAfterReparent()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%" viewBox="0 0 480 360">
              <svg id="testroot" width="480" height="360">
                <svg id="testSVG1" />
                <svg id="testSVG2" />
                <svg id="subSVG" width="300" height="175" />
              </svg>
            </svg>
            """);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var testSvg1 = runtime.GetElement(document.Descendants().Single(element => element.ID == "testSVG1"));
        var testSvg2 = runtime.GetElement(document.Descendants().Single(element => element.ID == "testSVG2"));
        var subSvg = runtime.GetElement(document.Descendants().Single(element => element.ID == "subSVG"));

        var length = Assert.IsType<SvgJavaScriptAnimatedLength>(testSvg1.width).baseVal;
        Assert.Equal(480d, length.value);
        Assert.Equal(100d, length.valueInSpecifiedUnits);

        length.value = 240d;
        Assert.Equal(240d, length.value);
        Assert.Equal(50d, length.valueInSpecifiedUnits);
        Assert.Equal("50%", length.valueAsString);

        subSvg.appendChild(testSvg1);
        Assert.Equal(150d, length.value);
        Assert.Equal(50d, length.valueInSpecifiedUnits);

        subSvg.appendChild(testSvg2);
        var defaultLength = Assert.IsType<SvgJavaScriptAnimatedLength>(testSvg2.width).baseVal;
        Assert.Equal(300d, defaultLength.value);
        Assert.Equal(100d, defaultLength.valueInSpecifiedUnits);
    }

    [Fact]
    public void CurrentScaleAndTranslate_UseViewerHostForOutermostSvg()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <svg id="nested" width="10" height="10" />
            </svg>
            """);
        var host = new TestViewerHost
        {
            CurrentScale = 2d,
            CurrentTranslateX = 4f,
            CurrentTranslateY = 5f
        };
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true })
        {
            ViewerHost = host
        };

        var root = runtime.GetElement(document);
        var nested = runtime.GetElement(document.Descendants().Single(element => element.ID == "nested"));

        Assert.Equal(2d, root.currentScale);
        Assert.Equal(4f, root.currentTranslate.x);
        Assert.Equal(5f, root.currentTranslate.y);

        root.currentScale = 3d;
        root.currentTranslate.x = 7f;
        root.currentTranslate.y = 8f;
        root.currentScale = -1d;

        Assert.Equal(3d, host.CurrentScale);
        Assert.Equal(7f, host.CurrentTranslateX);
        Assert.Equal(8f, host.CurrentTranslateY);
        Assert.Equal(3d, root.currentScale);
        Assert.Equal(7f, root.currentTranslate.x);
        Assert.Equal(8f, root.currentTranslate.y);

        nested.currentScale = 9d;
        nested.currentTranslate.x = 10f;

        Assert.Equal(3d, host.CurrentScale);
        Assert.Equal(7f, host.CurrentTranslateX);
        Assert.Equal(9d, nested.currentScale);
        Assert.Equal(10f, nested.currentTranslate.x);
    }

    [Fact]
    public void ElementFocusAndBlur_DispatchesFocusInAndFocusOut()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20" data-log="">
              <rect id="first" width="10" height="10" tabindex="0"
                    onfocusin="append('first-in:' + related(evt))"
                    onfocusout="append('first-out:' + related(evt))" />
              <rect id="second" x="20" width="10" height="10" tabindex="0"
                    onfocusin="append('second-in:' + related(evt))"
                    onfocusout="append('second-out:' + related(evt))" />
              <script><![CDATA[
                function related(evt) {
                  return evt.relatedTarget ? evt.relatedTarget.id : 'null';
                }
                function append(value) {
                  var root = document.documentElement;
                  root.setAttribute('data-log', root.getAttribute('data-log') + value + ';');
                }
              ]]></script>
            </svg>
            """, captureJavaScriptDomState: true);
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        runtime.ExecuteDocumentScripts();
        var first = runtime.GetElement(document.Descendants().Single(element => element.ID == "first"));
        var second = runtime.GetElement(document.Descendants().Single(element => element.ID == "second"));

        first.focus();
        second.focus();
        second.blur();

        Assert.Null(runtime.FocusedElement);
        Assert.Equal(
            "first-in:null;first-out:second;second-in:first;second-out:null;",
            runtime.GetElement(document).getAttribute("data-log"));
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
    public void InteractionHost_DispatchesPointerMouseSequenceThroughHitTestAndBubbling()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" data-log="">
              <g id="group"
                 onmousedown="append('g-down:' + evt.target.id + ':' + evt.currentTarget.id)"
                 onclick="append('g-click')">
                <rect id="target"
                      width="50"
                      height="50"
                      fill="red"
                      onmouseover="append('over:' + (evt.relatedTarget ? evt.relatedTarget.id : 'null') + ':' + evt.target.id + ':' + evt.currentTarget.id)"
                      onpointerdown="append('pointerdown:' + evt.target.id + ':' + evt.currentTarget.id)"
                      onmousedown="append('down:' + evt.clientX + ':' + evt.button + ':' + evt.altKey + ':' + evt.shiftKey + ':' + evt.ctrlKey)"
                      onclick="append('click:' + evt.detail); evt.preventDefault(); evt.stopPropagation(); this.setAttribute('fill', 'green')" />
              </g>
              <script><![CDATA[
                function append(value) {
                  var root = document.documentElement;
                  root.setAttribute('data-log', root.getAttribute('data-log') + value + ';');
                }

                document.getElementById('group').addEventListener('mousedown', function (evt) {
                  append('capture:' + evt.target.id + ':' + evt.currentTarget.id);
                }, true);

                document.getElementById('group').addEventListener('mousedown', function (evt) {
                  append('bubble:' + evt.target.id + ':' + evt.currentTarget.id);
                }, false);
              ]]></script>
            </svg>
            """, captureJavaScriptDomState: true);
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        runtime.ExecuteDocumentScripts();
        var host = new SvgJavaScriptInteractionHost(runtime);
        var input = new SvgJavaScriptEventInput(11, 12, SvgJavaScriptMouseButton.Left, 2, 0, altKey: true, shiftKey: false, ctrlKey: true);

        var move = host.DispatchPointerMoved(input);
        var press = host.DispatchPointerPressed(input);
        var release = host.DispatchPointerReleased(input);

        Assert.Equal("target", move.TargetElement?.id);
        Assert.Equal("target", host.HoveredElement?.id);
        Assert.Equal("target", press.TargetElement?.id);
        Assert.Null(host.CapturedElement);
        Assert.True(release.DefaultPrevented);
        Assert.True(release.CancelBubble);
        Assert.True(release.Mutated);
        Assert.Equal(
            "over:null:target:target;" +
            "pointerdown:target:target;" +
            "capture:target:group;" +
            "down:11:0:true:false:true;" +
            "g-down:target:group;" +
            "bubble:target:group;" +
            "click:2;",
            runtime.GetElement(document).getAttribute("data-log"));
        AssertFill(document, "target", Color.Green);
    }

    [Fact]
    public void InteractionHost_PointerPressFocusesFocusableElement()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="60" height="20" data-log="">
              <rect id="first" width="20" height="20" fill="red" tabindex="0"
                    onfocusin="append('first-in:' + related(evt))"
                    onfocusout="append('first-out:' + related(evt))" />
              <rect id="second" x="30" width="20" height="20" fill="blue" tabindex="0"
                    onfocusin="append('second-in:' + related(evt))" />
              <script><![CDATA[
                function related(evt) {
                  return evt.relatedTarget ? evt.relatedTarget.id : 'null';
                }
                function append(value) {
                  var root = document.documentElement;
                  root.setAttribute('data-log', root.getAttribute('data-log') + value + ';');
                }
              ]]></script>
            </svg>
            """, captureJavaScriptDomState: true);
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        runtime.ExecuteDocumentScripts();
        var host = new SvgJavaScriptInteractionHost(runtime);

        var first = host.DispatchPointerPressed(new SvgJavaScriptEventInput(5, 5, SvgJavaScriptMouseButton.Left, 1, 0, false, false, false));
        var second = host.DispatchPointerPressed(new SvgJavaScriptEventInput(35, 5, SvgJavaScriptMouseButton.Left, 1, 0, false, false, false));

        Assert.Equal("first", first.FocusedElement?.id);
        Assert.True(first.DefaultActionActivated);
        Assert.Equal("second", host.FocusedElement?.id);
        Assert.Equal("second", second.FocusedElement?.id);
        Assert.Equal(
            "first-in:null;first-out:second;second-in:first;",
            runtime.GetElement(document).getAttribute("data-log"));
    }

    [Fact]
    public void InteractionHost_MousedownPreventDefaultSuppressesFocusDefaultAction()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" data-log="">
              <rect id="target" width="20" height="20" tabindex="0"
                    onmousedown="evt.preventDefault()"
                    onfocusin="document.documentElement.setAttribute('data-log', 'focused')" />
            </svg>
            """, captureJavaScriptDomState: true);
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var host = new SvgJavaScriptInteractionHost(runtime);

        var result = host.DispatchPointerPressed(new SvgJavaScriptEventInput(5, 5, SvgJavaScriptMouseButton.Left, 1, 0, false, false, false));

        Assert.True(result.DefaultPrevented);
        Assert.False(result.DefaultActionActivated);
        Assert.Null(host.FocusedElement);
        Assert.Equal(string.Empty, runtime.GetElement(document).getAttribute("data-log"));
    }

    [Fact]
    public void InteractionHost_CapturesReleaseToPressedElementAndSuppressesClickOutside()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100" data-log="">
              <rect id="background" width="100" height="100" fill="white"
                    onmouseover="document.documentElement.setAttribute('data-log', document.documentElement.getAttribute('data-log') + 'background-over;')" />
              <rect id="target" width="20" height="20" fill="red"
                    onmouseup="document.documentElement.setAttribute('data-log', document.documentElement.getAttribute('data-log') + 'target-up;')"
                    onclick="document.documentElement.setAttribute('data-log', document.documentElement.getAttribute('data-log') + 'target-click;')" />
            </svg>
            """, captureJavaScriptDomState: true);
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var host = new SvgJavaScriptInteractionHost(runtime);
        var pressInput = new SvgJavaScriptEventInput(10, 10, SvgJavaScriptMouseButton.Left, 1, 0, false, false, false);
        var outsideInput = new SvgJavaScriptEventInput(80, 80, SvgJavaScriptMouseButton.Left, 1, 0, false, false, false);

        host.DispatchPointerPressed(pressInput);
        Assert.Equal("target", host.CapturedElement?.id);

        host.DispatchPointerMoved(outsideInput);
        Assert.Equal("target", host.CapturedElement?.id);
        Assert.Equal("target", host.HoveredElement?.id);

        var release = host.DispatchPointerReleased(outsideInput);

        Assert.Null(host.CapturedElement);
        Assert.Equal("background", host.HoveredElement?.id);
        Assert.Equal("background", release.TargetElement?.id);
        Assert.Equal("target-up;background-over;", runtime.GetElement(document).getAttribute("data-log"));
    }

    [Fact]
    public void InteractionHost_UseInstanceClickTargetsCorrespondingInstanceButRunsUseHandler()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="40"
                 height="20"
                 data-target="">
              <defs>
                <rect id="template" width="10" height="10" />
              </defs>
              <use id="use"
                   xlink:href="#template"
                   onclick="document.documentElement.setAttribute('data-target', evt.target.correspondingElement.id + ':' + (evt.target.correspondingUseElement === this))" />
            </svg>
            """, captureJavaScriptDomState: true);
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var host = new SvgJavaScriptInteractionHost(runtime);
        var input = new SvgJavaScriptEventInput(5, 5, SvgJavaScriptMouseButton.Left, 1, 0, false, false, false);

        var result = host.DispatchMouseEventAt("click", input);

        Assert.Equal("use", result.TargetElement?.id);
        Assert.Equal("template:true", runtime.GetElement(document).getAttribute("data-target"));
        Assert.IsType<SvgJavaScriptElementInstance>(Assert.Single(result.Events).TargetNode);
    }

    [Fact]
    public void InteractionHost_UseInstanceMouseOverRunsReferencedHandlerBeforeUseHandler()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg"
                 xmlns:xlink="http://www.w3.org/1999/xlink"
                 width="40"
                 height="20"
                 data-log="">
              <defs>
                <rect id="template"
                      width="10"
                      height="10"
                      onmouseover="document.documentElement.setAttribute('data-log', document.documentElement.getAttribute('data-log') + 'template:' + evt.target.correspondingElement.id + ';')" />
              </defs>
              <use id="use"
                   xlink:href="#template"
                   onmouseover="document.documentElement.setAttribute('data-log', document.documentElement.getAttribute('data-log') + 'use:' + evt.target.correspondingUseElement.id + ';')" />
            </svg>
            """, captureJavaScriptDomState: true);
        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var host = new SvgJavaScriptInteractionHost(runtime);
        var input = new SvgJavaScriptEventInput(5, 5, SvgJavaScriptMouseButton.None, 0, 0, false, false, false);

        _ = host.DispatchPointerMoved(input);

        Assert.Equal("template:template;use:use;", runtime.GetElement(document).getAttribute("data-log"));
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
    public void SvgAngleValueAsString_PreservesAssignedUnitType()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <marker id="target" orient="20rad" />
            </svg>
            """, captureJavaScriptDomState: true);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var targetElement = document.Descendants().Single(element => element.ID == "target");
        var target = runtime.GetElement(targetElement);

        Assert.Equal(3, target.orientAngle.baseVal.unitType);

        target.orientAngle.baseVal.valueAsString = "2grad";

        Assert.Equal("2grad", target.orientAngle.baseVal.valueAsString);
        Assert.Equal(4, target.orientAngle.baseVal.unitType);

        target.setAttribute("data-empty", string.Empty);
        Assert.True(target.hasAttribute("data-empty"));
        Assert.Equal(string.Empty, target.getAttribute("data-empty"));

        var clone = Assert.IsType<SvgDocument>(document.DeepCopy());
        var cloneRuntime = new SvgJavaScriptRuntime(clone, new SvgJavaScriptSettings { ThrowOnError = true });
        var cloneTarget = cloneRuntime.GetElement(clone.Descendants().Single(element => element.ID == "target"));

        Assert.Equal("2grad", cloneTarget.orientAngle.baseVal.valueAsString);
        Assert.Equal(4, cloneTarget.orientAngle.baseVal.unitType);
        Assert.True(cloneTarget.hasAttribute("data-empty"));

        var elementClone = targetElement.DeepCopy();
        var elementCloneTarget = runtime.GetElement(elementClone);

        Assert.Equal("2grad", elementCloneTarget.orientAngle.baseVal.valueAsString);
        Assert.Equal(4, elementCloneTarget.orientAngle.baseVal.unitType);
        Assert.True(elementCloneTarget.hasAttribute("data-empty"));
    }

    [Fact]
    public void ParsedEmptyAttributes_PreserveJavaScriptDomState()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" style="" data-space="   " />
              <marker id="marker" orient="" />
            </svg>
            """, captureJavaScriptDomState: true);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var target = runtime.GetElement(document.Descendants().Single(element => element.ID == "target"));
        var marker = runtime.GetElement(document.Descendants().Single(element => element.ID == "marker"));

        Assert.True(target.hasAttribute("style"));
        Assert.Equal(string.Empty, target.getAttribute("style"));
        Assert.True(target.hasAttribute("data-space"));
        Assert.Equal("   ", target.getAttribute("data-space"));
        Assert.True(marker.hasAttribute("orient"));
        Assert.Equal(string.Empty, marker.getAttribute("orient"));
    }

    [Fact]
    public void StylePropertyMutations_UpdateRawStyleAttribute()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" style="fill: red" />
            </svg>
            """, captureJavaScriptDomState: true);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var target = runtime.GetElement(document.Descendants().Single(element => element.ID == "target"));

        Assert.Equal("fill: red", target.getAttribute("style"));

        target.style.setProperty("fill", "green");

        Assert.Equal("fill: green", target.getAttribute("style"));

        Assert.Equal("green", target.style.removeProperty("fill"));
        Assert.True(target.hasAttribute("style"));
        Assert.Equal(string.Empty, target.getAttribute("style"));

        target.removeAttribute("style");

        Assert.False(target.hasAttribute("style"));
        Assert.Equal(string.Empty, target.getAttribute("style"));
    }

    [Fact]
    public void StyleDeclaration_CssTextLengthItemPriorityAndCamelCaseMutateRawStyle()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" style="fill: red; stroke-width: 2px" />
              <script><![CDATA[
                var target = document.getElementById('target');
                var style = target.style;
                var before = style.length + ':' + style.item(0) + ':' + style.item(1);
                style.strokeWidth = '4px';
                style.setProperty('opacity', '0.5', 'important');
                var removed = style.removeProperty('fill');
                style.cssText = style.cssText + '; fill: green';
                target.setAttribute('data-result', [
                  before,
                  style.length,
                  style.item(0),
                  style.getPropertyValue('strokeWidth'),
                  style.getPropertyValue('opacity'),
                  style.getPropertyPriority('opacity'),
                  removed,
                  style.cssText,
                  target.getAttribute('style')
                ].join('|'));
              ]]></script>
            </svg>
            """, captureJavaScriptDomState: true);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });

        runtime.ExecuteDocumentScripts();

        var target = runtime.GetElement(document.Descendants().Single(element => element.ID == "target"));
        Assert.Equal(
            "2:fill:stroke-width|3|stroke-width|4px|0.5|important|red|stroke-width: 4px; opacity: 0.5 !important; fill: green|stroke-width: 4px; opacity: 0.5 !important; fill: green",
            target.getAttribute("data-result"));
        Assert.True(runtime.MutationVersion > 0);
    }

    [Fact]
    public void ComputedStyle_ResolvesInlinePresentationCssDefaultsAndInheritance()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <style>.accent { stroke: purple; fill-opacity: 0.25 }</style>
              <g id="group" color="blue" fill="green" font-size="20px" display="table">
                <rect id="target" class="accent primary" style="fill: red; stroke-width: 2px" opacity="0.5" />
                <circle id="child" fill="inherit" />
              </g>
            </svg>
            """, captureJavaScriptDomState: true);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var target = runtime.GetElement(document.Descendants().Single(element => element.ID == "target"));
        var child = runtime.GetElement(document.Descendants().Single(element => element.ID == "child"));
        var targetStyle = target.ownerDocument.defaultView.getComputedStyle(target, null);
        var childStyle = target.ownerDocument.defaultView.getComputedStyle(child, null);

        Assert.Equal("red", targetStyle.getPropertyValue("fill"));
        Assert.Equal("purple", targetStyle.stroke);
        Assert.Equal("2px", targetStyle.strokeWidth);
        Assert.Equal("0.25", targetStyle.fillOpacity);
        Assert.Equal("blue", targetStyle.color);
        Assert.Equal("20px", targetStyle.fontSize);
        Assert.Equal("0.5", targetStyle.opacity);
        Assert.Equal("inline", targetStyle.display);
        Assert.Contains("fill", Enumerable.Range(0, targetStyle.length).Select(targetStyle.item));
        Assert.Contains("stroke-width: 2px", targetStyle.cssText);

        Assert.Equal("green", childStyle.fill);
        Assert.Equal("inline", childStyle.display);
    }

    [Fact]
    public void ClassLookup_IsLiveAndPreservesCreatedElementNamespaceAndLocalName()
    {
        var document = LoadDocument("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g id="parent">
                <rect id="target" class="primary active" />
              </g>
            </svg>
            """, captureJavaScriptDomState: true);

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings { ThrowOnError = true });
        var root = runtime.GetElement(document);
        var parent = runtime.GetElement(document.Descendants().Single(element => element.ID == "parent"));
        var documentFacade = root.ownerDocument;
        var active = documentFacade.getElementsByClassName("primary active");
        var scoped = parent.getElementsByClassName("primary");

        Assert.Equal(1, active.length);
        Assert.Same(active.item(0), scoped.item(0));

        var foreign = documentFacade.createElementNS("http://example.org/custom", "custom:Widget");
        foreign.setAttribute("class", "primary active");
        parent.appendChild(foreign);

        Assert.Equal(2, active.length);
        Assert.Equal("Widget", foreign.localName);
        Assert.Equal("Widget", foreign.tagName);
        Assert.Equal("http://example.org/custom", foreign.namespaceURI);
        Assert.Same(foreign, documentFacade.getElementsByTagNameNS("http://example.org/custom", "Widget").item(0));
        Assert.Same(foreign, documentFacade.getElementsByTagName("Widget").item(0));
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

    private static SvgDocument LoadDocument(string svg, bool captureJavaScriptDomState = false)
    {
        if (captureJavaScriptDomState)
        {
            return SvgService.FromSvg(svg, captureCompatibilityStyleState: true)!;
        }

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

    private sealed class TestViewerHost : ISvgJavaScriptViewerHost
    {
        public double CurrentScale { get; set; } = 1d;

        public float CurrentTranslateX { get; set; }

        public float CurrentTranslateY { get; set; }
    }
}
