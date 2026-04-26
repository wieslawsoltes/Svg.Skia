using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ShimSkiaSharp;
using Svg;
using Svg.JavaScript;
using Svg.Model.Services;
using Svg.Pathing;
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
    public void FromSvg_RootLoadEventDispatchesRegisteredListeners()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script><![CDATA[
                document.documentElement.addEventListener('load', function (evt) {
                  var passed = evt.target === document.documentElement
                    && evt.currentTarget === document.documentElement;
                  document.getElementById('target').setAttribute('fill', passed ? 'green' : 'red');
                }, false);
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_ReusedInstanceLoadScriptsApplyPendingAnimationTime()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <animate attributeName="display" values="inline;inline" begin="0s" dur="10s" />
            </svg>
            """);
        svg.SetAnimationTime(TimeSpan.FromSeconds(7));

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <animate attributeName="display" values="inline;inline" begin="0s" dur="10s" />
              <script><![CDATA[
                var root = document.documentElement;
                var initialTime = root.getCurrentTime();
                root.setCurrentTime(2);
                var passed = initialTime === 0 && root.getCurrentTime() === 0;
                document.getElementById('target').setAttribute('fill', passed ? 'green' : 'red');
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
        Assert.Equal(TimeSpan.FromSeconds(2), svg.AnimationTime);
    }

    [Fact]
    public void FromSvg_AnimationTimelineDispatchesRegisteredListenersWithoutInlineHandlers()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="beginResult" width="10" height="10" fill="red" />
              <rect id="endResult" x="10" width="10" height="10" fill="red" />
              <animate id="subject" attributeName="display" values="inline;inline" begin="0s" dur="1s" />
              <script><![CDATA[
                var animation = document.getElementById('subject');
                animation.addEventListener('beginEvent', function (evt) {
                  document.getElementById('beginResult').setAttribute(
                    'fill',
                    evt.currentTarget === animation ? 'green' : 'red');
                }, false);
                animation.addEventListener('endEvent', function (evt) {
                  document.getElementById('endResult').setAttribute(
                    'fill',
                    evt.currentTarget === animation ? 'green' : 'red');
                }, false);
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "beginResult", Color.Green);

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        AssertFill(svg, "endResult", Color.Green);
    }

    [Fact]
    public void FromSvg_DocumentTagNameQueriesIncludeRootElement()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script><![CDATA[
                var svgElements = document.getElementsByTagName('svg');
                var allElements = document.getElementsByTagName('*');
                var passed = svgElements.length === 1
                  && svgElements.item(0) === document.documentElement
                  && allElements.length === 3
                  && allElements.item(0) === document.documentElement;
                document.getElementById('target').setAttribute('fill', passed ? 'green' : 'red');
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_DocumentTagNameQueriesUseSvgElementNamesAndCreateTypedElements()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <defs>
                <linearGradient id="paint" />
              </defs>
              <rect id="target" width="10" height="10" fill="red" />
              <script><![CDATA[
                var ns = 'http://www.w3.org/2000/svg';
                var gradient = document.createElementNS(ns, 'linearGradient');
                var span = document.createElementNS(ns, 'tspan');
                var clip = document.createElementNS(ns, 'clipPath');
                var gradients = document.getElementsByTagName('linearGradient');
                var passed = gradient.tagName === 'linearGradient'
                  && span.tagName === 'tspan'
                  && clip.tagName === 'clipPath'
                  && gradients.length === 1
                  && gradients.item(0).tagName === 'linearGradient';
                document.getElementById('target').setAttribute('fill', passed ? 'green' : 'red');
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_ElementOnlyDomDoesNotFabricateTextNodes()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g id="parent"><rect id="child" width="10" height="10" fill="blue" /></g>
              <rect id="target" y="10" width="10" height="10" fill="red" />
              <script><![CDATA[
                var parent = document.getElementById('parent');
                var child = document.getElementById('child');
                var passed = parent.firstChild === child
                  && parent.childNodes.length === 1
                  && parent.childNodes.item(0) === child;
                document.getElementById('target').setAttribute('fill', passed ? 'green' : 'red');
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_ReplaceChildWithSameNodeIsNoOp()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g id="parent">
                <rect id="target" width="10" height="10" fill="red" />
              </g>
              <script><![CDATA[
                var parent = document.getElementById('parent');
                var target = document.getElementById('target');
                var returned = parent.replaceChild(target, target);
                var passed = returned === target
                  && target.parentNode === parent
                  && parent.children.length === 1
                  && parent.children.item(0) === target;
                target.setAttribute('fill', passed ? 'green' : 'red');
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_InsertBeforeRejectsCyclesAndPreservesExistingDomNodes()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g id="parent">
                <rect id="first" width="4" height="4" fill="blue" />
                <g id="child">
                  <rect id="nested" width="4" height="4" fill="blue" />
                </g>
              </g>
              <rect id="target" y="10" width="10" height="10" fill="red" />
              <script><![CDATA[
                var parent = document.getElementById('parent');
                var child = document.getElementById('child');
                var target = document.getElementById('target');

                var appended = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                appended.setAttribute('id', 'appended');
                parent.appendChild(appended);

                var selfBlocked = false;
                try {
                  parent.appendChild(parent);
                } catch (e) {
                  selfBlocked = e.code === DOMException.HIERARCHY_REQUEST_ERR;
                }

                var ancestorBlocked = false;
                try {
                  child.appendChild(parent);
                } catch (e) {
                  ancestorBlocked = e.code === DOMException.HIERARCHY_REQUEST_ERR;
                }

                var firstNodeIndex = -1;
                var childNodeIndex = -1;
                var appendedNodeIndex = -1;
                for (var i = 0; i < parent.childNodes.length; i++) {
                  var node = parent.childNodes.item(i);
                  if (node && node.nodeType === Node.ELEMENT_NODE) {
                    if (node.id === 'first') {
                      firstNodeIndex = i;
                    } else if (node.id === 'child') {
                      childNodeIndex = i;
                    } else if (node.id === 'appended') {
                      appendedNodeIndex = i;
                    }
                  }
                }

                var passed = selfBlocked
                  && ancestorBlocked
                  && parent.children.length === 3
                  && parent.children.item(0).id === 'first'
                  && parent.children.item(1).id === 'child'
                  && parent.children.item(2).id === 'appended'
                  && firstNodeIndex >= 0
                  && childNodeIndex > firstNodeIndex
                  && appendedNodeIndex > childNodeIndex;
                target.setAttribute('fill', passed ? 'green' : 'red');
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_DomMutationRejectsMissingReferencesAndNonChildRemoval()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g id="parent">
                <rect id="child" width="4" height="4" fill="blue" />
              </g>
              <rect id="target" y="10" width="10" height="10" fill="red" />
              <script><![CDATA[
                var ns = 'http://www.w3.org/2000/svg';
                var parent = document.getElementById('parent');
                var newChild = document.createElementNS(ns, 'rect');
                var foreignElement = document.createElementNS(ns, 'rect');
                var foreignText = document.createTextNode('outside');
                var rejectedReference = false;
                var rejectedElementRemoval = false;
                var rejectedTextRemoval = false;

                try {
                  parent.insertBefore(newChild, foreignElement);
                } catch (e) {
                  rejectedReference = e.code === DOMException.NOT_FOUND_ERR;
                }

                try {
                  parent.removeChild(foreignElement);
                } catch (e) {
                  rejectedElementRemoval = e.code === DOMException.NOT_FOUND_ERR;
                }

                try {
                  parent.removeChild(foreignText);
                } catch (e) {
                  rejectedTextRemoval = e.code === DOMException.NOT_FOUND_ERR;
                }

                var passed = rejectedReference
                  && rejectedElementRemoval
                  && rejectedTextRemoval
                  && parent.children.length === 1
                  && parent.children.item(0).id === 'child';
                document.getElementById('target').setAttribute('fill', passed ? 'green' : 'red');
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_TextContentResetDetachesOldTextNode()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <text id="text">old</text>
              <rect id="target" width="10" height="10" fill="red" />
              <script><![CDATA[
                var text = document.getElementById('text');
                var oldText = text.firstChild;
                text.textContent = 'new';
                var passed = oldText.parentNode === null
                  && oldText.nextSibling === null
                  && text.textContent === 'new'
                  && text.firstChild !== oldText;
                document.getElementById('target').setAttribute('fill', passed ? 'green' : 'red');
              ]]></script>
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
    public void Runtime_FirstStyleMutation_PreservesUnchangedSiblingPresentationAttributes()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <rect id="sibling" x="10" width="10" height="10" fill="blue" />
            </svg>
            """)!;

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        var target = runtime.GetElement(document.GetElementById("target")!);
        target.setAttribute("fill", "green");

        AssertVisualFill(document, "target", Color.Green);
        AssertVisualFill(document, "sibling", Color.Blue);
    }

    [Fact]
    public void Runtime_ClassMutation_ReappliesCompatibilityCssFromRawBaseline()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <style>.active { fill: green; }</style>
              <rect id="target" class="active" width="10" height="10" fill="red" />
            </svg>
            """, captureCompatibilityStyleState: true)!;

        Assert.False(IsCompatibilityStyleStateInitialized(document));

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        AssertVisualFill(document, "target", Color.Green);

        var target = runtime.GetElement(document.GetElementById("target")!);
        target.setAttribute("class", string.Empty);

        Assert.True(IsCompatibilityStyleStateInitialized(document));
        AssertVisualFill(document, "target", Color.Red);
    }

    [Fact]
    public void Runtime_ClassMutationFromStream_ReappliesCompatibilityCssFromRawBaseline()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <style>.active { fill: green; }</style>
              <rect id="target" class="active" width="10" height="10" fill="red" />
            </svg>
            """));
        var document = SvgService.Open(stream, parameters: null, captureCompatibilityStyleState: true)!;

        Assert.False(IsCompatibilityStyleStateInitialized(document));

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        AssertVisualFill(document, "target", Color.Green);

        var target = runtime.GetElement(document.GetElementById("target")!);
        target.setAttribute("class", string.Empty);

        Assert.True(IsCompatibilityStyleStateInitialized(document));
        AssertVisualFill(document, "target", Color.Red);
    }

    [Fact]
    public void Runtime_ClassMutationFromTinyReadStream_DetectsCompatibilityCssWithoutEagerSnapshot()
    {
        using var stream = new SingleByteReadMemoryStream(Encoding.UTF8.GetBytes("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <style>.active { fill: green; }</style>
              <rect id="target" class="active" width="10" height="10" fill="red" />
            </svg>
            """));
        var document = SvgService.Open(stream, parameters: null, captureCompatibilityStyleState: true)!;

        Assert.False(IsCompatibilityStyleStateInitialized(document));

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        AssertVisualFill(document, "target", Color.Green);

        var target = runtime.GetElement(document.GetElementById("target")!);
        target.setAttribute("class", string.Empty);

        Assert.True(IsCompatibilityStyleStateInitialized(document));
        AssertVisualFill(document, "target", Color.Red);
    }

    [Fact]
    public void Runtime_PresentationMutationWithoutCss_DoesNotInitializeCompatibilityStyleState()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
            </svg>
            """, captureCompatibilityStyleState: true)!;

        Assert.False(IsCompatibilityStyleStateInitialized(document));

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        var target = runtime.GetElement(document.GetElementById("target")!);
        target.setAttribute("fill", "green");

        Assert.False(IsCompatibilityStyleStateInitialized(document));
        AssertVisualFill(document, "target", Color.Green);
    }

    [Fact]
    public void Runtime_IdMutation_ReappliesCompatibilityCssAcrossSiblingSelectors()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <style>#trigger + rect { fill: green; }</style>
              <rect id="trigger" width="10" height="10" />
              <rect id="target" x="10" width="10" height="10" fill="red" />
            </svg>
            """, captureCompatibilityStyleState: true)!;

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        AssertVisualFill(document, "target", Color.Green);

        var trigger = runtime.GetElement(document.GetElementById("trigger")!);
        trigger.setAttribute("id", "updated-trigger");

        AssertVisualFill(document, "target", Color.Red);
    }

    [Fact]
    public void FromSvg_InsertInlineStyledElement_ReappliesInlineStyleAfterCss()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <style>rect { fill: red; }</style>
              <g id="parent" />
              <script><![CDATA[
                var rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                rect.setAttribute('id', 'target');
                rect.setAttribute('width', '10');
                rect.setAttribute('height', '10');
                rect.setAttribute('style', 'fill: green');
                document.getElementById('parent').appendChild(rect);
              ]]></script>
            </svg>
            """);

        AssertVisualFill(svg.SourceDocument!, "target", Color.Green);
    }

    [Fact]
    public void Runtime_ParsedInlineStyleOnlyElement_ReappliesInlineStyleAfterCss()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="30" height="20">
              <style>rect.off { fill: red; }</style>
              <rect id="candidate" width="10" height="10" fill="blue" />
              <rect id="target" x="10" width="10" height="10" class="on" style="fill: green" />
            </svg>
            """, captureCompatibilityStyleState: true)!;

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        AssertVisualFill(document, "target", Color.Green);

        var target = runtime.GetElement(document.GetElementById("target")!);
        target.setAttribute("class", "off");

        AssertVisualFill(document, "target", Color.Green);
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
    public void FromSvg_SetTimeoutDelayAndClearTimeout_UseDeterministicDueOrder()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="10" height="10" fill="red" />
              <script><![CDATA[
                window.order = '';
                setTimeout("window.order += 'a';", 10);
                var cancelled = setTimeout("window.order += 'x';", 15);
                clearTimeout(cancelled);
                setTimeout("window.order += 'b';", 20);
                setTimeout(function () {
                  document.getElementById('target').setAttribute('fill', window.order === 'ab' ? 'green' : 'red');
                }, 30);
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_ElementTimeControlMethodsReturnUndefinedDuringTimelineBeginCallbacks()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="result" width="10" height="10" fill="red" />
              <animate id="subject" attributeName="display" values="inline; inline" begin="indefinite" dur="10s" />
              <animate attributeName="display" values="inline; inline" dur="10s" onbegin="verify()" />
              <script><![CDATA[
                function verify() {
                  var animation = document.getElementById('subject');
                  var passed = typeof animation.beginElement() == 'undefined'
                    && typeof animation.beginElementAt(1) == 'undefined'
                    && typeof animation.endElement() == 'undefined'
                    && typeof animation.endElementAt(2) == 'undefined';
                  document.getElementById('result').setAttribute('fill', passed ? 'green' : 'red');
                }
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "result", Color.Green);
    }

    [Fact]
    public void FromSvg_GetStartTimeSupportsFutureActiveSyncbaseAndInvalidStateSemantics()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="before" x="0" y="0" width="2" height="2" fill="red" />
              <rect id="during" x="3" y="0" width="2" height="2" fill="red" />
              <rect id="after" x="6" y="0" width="2" height="2" fill="red" />
              <rect id="indefinite" x="9" y="0" width="2" height="2" fill="red" />
              <rect id="syncbase" x="12" y="0" width="2" height="2" fill="red" />

              <animate id="a1" attributeName="display" values="inline; inline" begin="1s" dur="1s" />
              <animate id="a3" attributeName="display" values="inline; inline" begin="indefinite" dur="1s" />
              <animate id="dep" attributeName="display" values="inline; inline" begin="5s" dur="1s" />
              <animate id="sync" attributeName="display" values="inline; inline" begin="dep.begin+2s" dur="1s" />

              <animate attributeName="display" values="inline; inline" begin="0.5s" dur="1s" onbegin="beforeCheck()" />
              <animate attributeName="display" values="inline; inline" begin="1.5s" dur="1s" onbegin="duringCheck()" />
              <animate attributeName="display" values="inline; inline" begin="2.5s" dur="1s" onbegin="afterCheck()" />

              <script><![CDATA[
                function pass(id, ok) {
                  document.getElementById(id).setAttribute('fill', ok ? 'green' : 'red');
                }

                function beforeCheck() {
                  pass('before', document.getElementById('a1').getStartTime() == 1);

                  try {
                    document.getElementById('a3').getStartTime();
                    pass('indefinite', false);
                  } catch (e) {
                    pass('indefinite', e.code == DOMException.INVALID_STATE_ERR);
                  }

                  pass('syncbase', document.getElementById('sync').getStartTime() == 7);
                }

                function duringCheck() {
                  pass('during', document.getElementById('a1').getStartTime() == 1);
                }

                function afterCheck() {
                  try {
                    document.getElementById('a1').getStartTime();
                    pass('after', false);
                  } catch (e) {
                    pass('after', e.code == DOMException.INVALID_STATE_ERR);
                  }
                }
              ]]></script>
            </svg>
            """);

        svg.SetAnimationTime(TimeSpan.FromSeconds(2.5));

        AssertFill(svg, "before", Color.Green);
        AssertFill(svg, "during", Color.Green);
        AssertFill(svg, "after", Color.Green);
        AssertFill(svg, "indefinite", Color.Green);
        AssertFill(svg, "syncbase", Color.Green);
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
    public void DispatchPointerReleased_ReusesEventAcrossInlineHandlerAndListeners()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="20" height="20" fill="red" onclick="evt.preventDefault();" />
              <script><![CDATA[
                document.getElementById('target').addEventListener('click', function (evt) {
                  this.setAttribute('fill', evt.defaultPrevented ? 'green' : 'red');
                }, false);
              ]]></script>
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
        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void DispatchPointerReleased_ReusesEventAcrossRouteHandlers()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g onclick="if (evt.defaultPrevented) document.getElementById('target').setAttribute('fill', 'green')">
                <rect id="target" width="20" height="20" fill="red" onclick="evt.preventDefault();" />
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
        var result = dispatcher.DispatchPointerReleased(svg, input);

        Assert.True(result.Handled);
        AssertFill(svg, "target", Color.Green);
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
    public void Clone_DispatchPointerReleased_ExecutesScriptDefinedGlobalHandler()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="20" height="20" fill="red" onclick="setGreen()" />
              <script><![CDATA[
                function setGreen() {
                  document.getElementById('target').setAttribute('fill', 'green');
                }
              ]]></script>
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
    public void Clone_RebuildsModelAfterDocumentScriptsMutateClone()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <rect id="target" width="20" height="20" fill="red" />
              <script><![CDATA[
                var root = document.documentElement;
                var target = document.getElementById('target');
                if (root.getAttribute('data-cloned') === '1') {
                  target.setAttribute('fill', 'green');
                }

                root.setAttribute('data-cloned', '1');
              ]]></script>
            </svg>
            """);

        using var clone = svg.Clone();

        AssertFill(svg, "target", Color.Red);
        AssertFill(clone, "target", Color.Green);

        using var bitmap = RenderBitmap(clone);
        var pixel = bitmap.GetPixel(10, 10);
        Assert.Equal((byte)0, pixel.Red);
        Assert.Equal((byte)128, pixel.Green);
        Assert.Equal((byte)0, pixel.Blue);
    }

    [Fact]
    public void DispatchPointerReleased_AnimatedHitTargetsMutateSourceDocumentAndRebuildRenderedFrame()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20">
              <rect id="target"
                    x="0"
                    y="0"
                    width="10"
                    height="10"
                    fill="red"
                    onclick="evt.target.setAttribute('stroke', 'black'); document.getElementById('target').setAttribute('fill', 'green')">
                <animate attributeName="x" from="0" to="10" dur="1s" fill="freeze" />
              </rect>
            </svg>
            """);

        Assert.True(svg.HasAnimations);
        svg.SetAnimationTime(TimeSpan.FromSeconds(1));

        var dispatcher = new SvgInteractionDispatcher();
        var input = new SvgPointerInput(
            new SKPoint(15, 5),
            SvgPointerDeviceType.Mouse,
            SvgMouseButton.Left,
            1,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "animated");

        Assert.Equal("target", svg.HitTestTopmostElement(input.PicturePoint)?.ID);

        dispatcher.DispatchPointerPressed(svg, input);
        dispatcher.DispatchPointerReleased(svg, input);

        var target = Assert.IsType<SvgRectangle>(svg.SourceDocument!.GetElementById("target")!);
        Assert.Equal(Color.Green.ToArgb(), Assert.IsType<SvgColourServer>(target.Fill).Colour.ToArgb());
        Assert.Equal(Color.Black.ToArgb(), Assert.IsType<SvgColourServer>(target.Stroke).Colour.ToArgb());

        using var bitmap = RenderBitmap(svg);
        var pixel = bitmap.GetPixel(15, 5);
        Assert.True(pixel.Alpha > 200);
        Assert.True(pixel.Green > 100);
        Assert.True(pixel.Red < 80);
        Assert.True(pixel.Blue < 80);
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
    public void Runtime_IntersectionList_KeepsApplicationDraftWatermarkGroups()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg">
              <title id="test-title">Application draft</title>
              <g id="draft-watermark">
                <rect x="1" y="1" width="478" height="20" fill="red"/>
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

        var list = root.getIntersectionList(rect, null);
        Assert.Equal(1, list.length);
        Assert.Equal("rect", Assert.IsType<SvgJavaScriptElement>(list.item(0)).tagName);
    }

    [Fact]
    public void Load_StructDom13Fixture_AppendsPassedVerificationRows()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Settings.JavaScriptTimeoutMilliseconds = 3000;
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

        Assert.Empty(failedChecks);
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

    [Fact]
    public void Load_SvgDomOverviewFixture_UsesSvgDefaultDomValues()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Settings.JavaScriptTimeoutMilliseconds = 3000;
        svg.Load(GetW3CSvgPath("svgdom-over-01-f"));

        var tt = GetJavaScriptRuntime(svg).GetElement(svg.SourceDocument!.GetElementById("tt")!);
        Assert.Equal(tt.getComputedTextLength(), tt.textLength.baseVal.value);

        var body = Assert.IsType<SvgGroup>(svg.SourceDocument!.GetElementById("test-body-content")!);
        var failedChecks = body.Children
            .OfType<SvgGroup>()
            .Select(group => new
            {
                Rect = group.Children.OfType<SvgRectangle>().FirstOrDefault(),
                Text = group.Children.OfType<SvgText>().FirstOrDefault()?.Text
            })
            .Where(entry => entry.Rect is not null && entry.Text is not null)
            .Where(entry => entry.Rect!.Fill is SvgColourServer fill && fill.Colour.ToArgb() == Color.Red.ToArgb())
            .Select(entry => entry.Text!)
            .ToArray();

        Assert.Empty(failedChecks);
    }

    [Fact]
    public void Load_AnimateScriptElem01Fixture_KeepsScriptHrefAnimValAtBaseValue()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Load(GetW3CSvgPath("animate-script-elem-01-b"));

        svg.SetAnimationTime(TimeSpan.FromSeconds(1.1));

        var runtime = GetJavaScriptRuntime(svg);
        var script = runtime.GetElement(svg.SourceDocument!.GetElementById("s")!);
        Assert.Contains("empty", script.href.baseVal);
        Assert.Contains("empty", script.href.animVal);
        AssertRectFill(svg.SourceDocument!, "r1", Color.Green);
        AssertRectFill(svg.SourceDocument!, "r2", Color.Green);
    }

    [Fact]
    public void Load_PathsDom01Fixture_ExposesPathMetricsAndSegments()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.EnableTextReferences = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);
        svg.Load(GetW3CSvgPath("paths-dom-01-f"));

        var document = svg.SourceDocument!;
        Assert.Equal(300, ParseTrailingInteger(GetText(document, "tl1")));
        Assert.Equal(300, ParseTrailingInteger(GetText(document, "tl2")));
        Assert.Equal("(60, 80)", GetText(document, "tp1"));
        Assert.Equal("(300, 80)", GetText(document, "tp2"));
        Assert.Equal(0, ParseTrailingInteger(GetText(document, "ts1")));
        Assert.Equal(0, ParseTrailingInteger(GetText(document, "ts2")));
        Assert.Equal("m 60 80", Assert.IsAssignableFrom<SvgTextBase>(document.GetElementById("tss1")!).Text);
        Assert.Equal("m 300 80", Assert.IsAssignableFrom<SvgTextBase>(document.GetElementById("tss2")!).Text);
    }

    [Fact]
    public void Load_PathsDom02Fixture_CreatesFlowerPathSegments()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Load(GetW3CSvgPath("paths-dom-02-f"));

        var path = Assert.IsType<SvgPath>(svg.SourceDocument!.GetElementById("mypath")!);
        Assert.NotNull(path.PathData);
        Assert.True(path.PathData.Count >= 8);

        var move = Assert.IsType<SvgMoveToSegment>(path.PathData[0]);
        Assert.InRange(move.End.X, 210f, 270f);
        Assert.InRange(move.End.Y, 140f, 220f);

        var cubic = Assert.IsType<SvgCubicCurveSegment>(path.PathData[1]);
        Assert.InRange(cubic.End.X, 150f, 330f);
        Assert.InRange(cubic.End.Y, 60f, 300f);
    }

    [Fact]
    public void Runtime_PathSegListMutationsNotifyPathAndMarkRuntimeDirty()
    {
        var document = SvgService.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <path id="target" d="M 0 0" />
            </svg>
            """)!;

        var runtime = new SvgJavaScriptRuntime(document, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });
        var path = Assert.IsType<SvgPath>(document.GetElementById("target")!);
        var target = runtime.GetElement(path);
        var changed = 0;
        path.AttributeChanged += (_, args) =>
        {
            if (args.Attribute == "d")
            {
                changed++;
            }
        };

        var initialMutationVersion = runtime.MutationVersion;
        var move = target.createSVGPathSegMovetoAbs(5, 5);
        target.pathSegList.appendItem(move);

        Assert.Equal(1, changed);
        Assert.True(runtime.MutationVersion > initialMutationVersion);
        Assert.Equal(2, path.PathData.Count);

        target.pathSegList.clear();

        Assert.Equal(2, changed);
        Assert.True(runtime.MutationVersion > initialMutationVersion + 1);
        Assert.Empty(path.PathData);
    }

    [Fact]
    public void Runtime_EventListeners_CanRegisterDispatchAndRemove()
    {
        var document = SvgDocument.Open<SvgDocument>(GetW3CSvgPath("interact-dom-01-b"));

        var runtime = new SvgJavaScriptRuntime(document!, new SvgJavaScriptSettings
        {
            ThrowOnError = true
        });

        runtime.ExecuteDocumentScripts();

        var startButton = runtime.GetElement(document.GetElementById("startButton")!);
        var firstClick = new SvgJavaScriptEvent();
        firstClick.initMouseEvent("click", true, true, null, 1, 0f, 0f, 0f, 0f, false, false, false, false, 0, null);
        Assert.True(startButton.dispatchEvent(firstClick));

        AssertRectFill(document, "buttonRect", ColorTranslator.FromHtml("#88ff88"));
        Assert.Equal(1, document.Descendants().OfType<SvgText>().Count(text => text.Text == "Event Listeners supported"));

        var secondClick = new SvgJavaScriptEvent();
        secondClick.initMouseEvent("click", true, true, null, 1, 0f, 0f, 0f, 0f, false, false, false, false, 0, null);
        Assert.True(startButton.dispatchEvent(secondClick));
        Assert.Equal(1, document.Descendants().OfType<SvgText>().Count(text => text.Text == "Event Listeners supported"));
    }

    [Fact]
    public void FromSvg_DispatchEventRunsCaptureListenersAndPreservesEventState()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g id="parent">
                <rect id="target" width="20" height="20" fill="red"
                      onclick="if (window.listenerSawDefaultPrevented &amp;&amp; evt.defaultPrevented) this.setAttribute('fill', 'green')" />
              </g>
              <script><![CDATA[
                var parent = document.getElementById('parent');
                var target = document.getElementById('target');
                parent.addEventListener('click', function (evt) {
                  if (evt.target === target && evt.currentTarget === parent) {
                    evt.preventDefault();
                    window.captureRan = true;
                  }
                }, true);
                target.addEventListener('click', function (evt) {
                  window.listenerSawDefaultPrevented = window.captureRan && evt.defaultPrevented;
                }, false);

                var click = document.createEvent('MouseEvents');
                click.initMouseEvent('click', true, true, window, 1, 0, 0, 0, 0, false, false, false, false, 0, null);
                window.dispatchResult = target.dispatchEvent(click);
                if (window.dispatchResult !== false) {
                  target.setAttribute('fill', 'red');
                }
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_NonBubblingEventInvokesAllTargetListeners()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g id="parent">
                <rect id="target" width="20" height="20" fill="red" />
              </g>
              <script><![CDATA[
                var parent = document.getElementById('parent');
                var target = document.getElementById('target');
                var first = false;
                var second = false;
                var parentRan = false;

                target.addEventListener('custom', function () { first = true; }, false);
                target.addEventListener('custom', function () { second = true; }, false);
                parent.addEventListener('custom', function () { parentRan = true; }, false);

                var evt = document.createEvent('Event');
                evt.initEvent('custom', false, true);
                target.dispatchEvent(evt);

                target.setAttribute('fill', first && second && !parentRan ? 'green' : 'red');
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void FromSvg_TargetCaptureStopPropagationPreservesTargetBubbleAndInlineHandlers()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;

        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20">
              <g id="parent">
                <rect id="target" width="20" height="20" fill="red"
                      onclick="window.inlineRan = window.bubbleRan &amp;&amp; evt.cancelBubble;" />
              </g>
              <script><![CDATA[
                var parent = document.getElementById('parent');
                var target = document.getElementById('target');
                window.captureRan = false;
                window.bubbleRan = false;
                window.inlineRan = false;
                window.parentRan = false;

                target.addEventListener('click', function (evt) {
                  window.captureRan = true;
                  evt.stopPropagation();
                }, true);
                target.addEventListener('click', function (evt) {
                  window.bubbleRan = window.captureRan && evt.cancelBubble;
                }, false);
                parent.addEventListener('click', function () {
                  window.parentRan = true;
                }, false);

                var click = document.createEvent('MouseEvents');
                click.initMouseEvent('click', true, true, window, 1, 0, 0, 0, 0, false, false, false, false, 0, null);
                target.dispatchEvent(click);
                target.setAttribute('fill', window.captureRan && window.bubbleRan && window.inlineRan && !window.parentRan ? 'green' : 'red');
              ]]></script>
            </svg>
            """);

        AssertFill(svg, "target", Color.Green);
    }

    [Fact]
    public void DispatchEvent_RefreshFromSourceDocument_UpdatesRenderedPicture()
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

        var runtime = GetJavaScriptRuntime(svg);
        var target = runtime.GetElement(svg.SourceDocument!.GetElementById("target")!);
        var click = new SvgJavaScriptEvent();
        click.initMouseEvent("click", true, true, null, 1, 0f, 0f, 0f, 0f, false, false, false, false, 0, null);
        Assert.True(target.dispatchEvent(click));

        AssertFill(svg, "target", Color.Green);

        using (var staleBitmap = RenderBitmap(svg))
        {
            var stalePixel = staleBitmap.GetPixel(10, 10);
            Assert.Equal((byte)255, stalePixel.Red);
            Assert.Equal((byte)0, stalePixel.Green);
            Assert.Equal((byte)0, stalePixel.Blue);
        }

        _ = svg.RefreshFromSourceDocument();

        using var refreshedBitmap = RenderBitmap(svg);
        var refreshedPixel = refreshedBitmap.GetPixel(10, 10);
        Assert.Equal((byte)0, refreshedPixel.Red);
        Assert.Equal((byte)128, refreshedPixel.Green);
        Assert.Equal((byte)0, refreshedPixel.Blue);
    }

    [Fact]
    public void Load_TextDom01Fixture_BrowserPath_ExposesTextContentMetricsAndAnimatedValues()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Settings.EnableSvgFonts = false;
        svg.Settings.EnableTextReferences = false;
        svg.Settings.StandaloneViewport = SkiaSharp.SKRect.Create(0f, 0f, 480f, 360f);
        svg.Load(GetW3CSvgPath("text-dom-01-f"));

        var document = svg.SourceDocument!;
        // The exact center hit falls on a font-dependent cluster boundary: macOS/Windows
        // resolve it to the expected W3C character, while Ubuntu's fallback font resolves
        // the same rendered point to the preceding glyph. The API must stay consistent
        // with the shaped host font geometry.
        Assert.InRange(ParseTrailingInteger(GetText(document, "text1")), 29, 30);
        var roundedComputedTextLength = ParseTrailingInteger(GetText(document, "text2"));
        // Browser-path metrics intentionally use the host fallback font when SVG
        // fonts are disabled, so only validate stable DOM and geometry invariants.
        Assert.InRange(roundedComputedTextLength, 300, 430);

        var endPosition = ParseTrailingPair(GetText(document, "text3"));
        Assert.InRange(endPosition.x, 100, 140);
        Assert.Equal(30, endPosition.y);

        var extent = ParseTrailingQuad(GetText(document, "text4"));
        Assert.InRange(extent.x, 100, 140);
        Assert.InRange(extent.y, 10, 25);
        Assert.InRange(extent.width, 5, 15);
        Assert.InRange(extent.height, 10, 25);

        Assert.Equal(54, ParseTrailingInteger(GetText(document, "text5")));
        Assert.Equal(45, ParseTrailingInteger(GetText(document, "text6")));

        var startPosition = ParseTrailingPair(GetText(document, "text7"));
        Assert.InRange(startPosition.x, 100, 140);
        Assert.Equal(30, startPosition.y);
        Assert.True(endPosition.x > startPosition.x);

        var substringLength = ParseTrailingInteger(GetText(document, "text8"));
        Assert.InRange(substringLength, 40, 120);
        Assert.True(substringLength < roundedComputedTextLength);
        Assert.Contains("the word 'the' should be selected", GetText(document, "text9"));
        Assert.Equal(roundedComputedTextLength, ParseTrailingInteger(GetText(document, "text10")));
        Assert.Equal(roundedComputedTextLength, ParseTrailingInteger(GetText(document, "text11")));
        Assert.Equal((1, 1), ParseTrailingPair(GetText(document, "text12")));
    }

    [Fact]
    public void Load_TextDom02Fixture_UsesUtf16CodeUnitCountingAndSubstringLength()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Load(GetW3CSvgPath("text-dom-02-f"));

        AssertRectFill(svg.SourceDocument!, "r1", Color.Green);
        AssertRectFill(svg.SourceDocument!, "r2", Color.Green);
    }

    [Fact]
    public void Load_TextDom03Fixture_SpecPath_UsesIndexSizeErrorSubstringSemantics()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Load(GetW3CSvgPath("text-dom-03-f"));

        AssertRectFill(svg.SourceDocument!, "r1", Color.Green);
        AssertRectFill(svg.SourceDocument!, "r2", Color.Green);
        AssertRectFill(svg.SourceDocument!, "r3", Color.Green);
        AssertRectFill(svg.SourceDocument!, "r4", Color.Green);
        AssertRectFill(svg.SourceDocument!, "r5", Color.Green);
    }

    [Fact]
    public void Load_TextDom04Fixture_SpecPath_UsesLigatureAwareSubstringSemantics()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Load(GetW3CSvgPath("text-dom-04-f"));

        var result = Assert.IsType<SvgText>(svg.SourceDocument!.GetElementById("res")!);
        Assert.True(string.IsNullOrEmpty(result.Text), result.Text);

        var indicator = Assert.IsType<SvgRectangle>(svg.SourceDocument.GetElementById("r")!);
        Assert.True(indicator.Fill is not SvgColourServer fill || fill.Colour.ToArgb() != Color.Red.ToArgb());
    }

    [Fact]
    public void Load_TextDom05Fixture_UsesClusterPositionsExtentsAndHitTesting()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Load(GetW3CSvgPath("text-dom-05-f"));

        var document = svg.SourceDocument!;
        AssertRectFill(document, "r3", Color.Green);
        AssertRectFill(document, "r4", Color.Green);
        AssertRectFill(document, "r5", Color.Green);
        AssertRectFill(document, "r6", Color.Green);
        AssertRectFill(document, "r7", Color.Green);
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

    private static void AssertRectFill(SvgDocument document, string id, Color expected)
    {
        var element = Assert.IsType<SvgRectangle>(document.Descendants().Single(node => node.ID == id));
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

    private static string GetText(SvgDocument document, string id)
    {
        return Assert.IsType<SvgText>(document.GetElementById(id)!).Text;
    }

    private static int ParseTrailingInteger(string text)
    {
        var match = Regex.Match(text, @"(-?\d+)\s*$");
        Assert.True(match.Success, $"Expected trailing integer in '{text}'.");
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static (int x, int y) ParseTrailingPair(string text)
    {
        var match = Regex.Match(text, @"(-?\d+)\s*,\s*(-?\d+)\s*$");
        Assert.True(match.Success, $"Expected trailing pair in '{text}'.");
        return (
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    private static (int x, int y, int width, int height) ParseTrailingQuad(string text)
    {
        var match = Regex.Match(text, @"(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*$");
        Assert.True(match.Success, $"Expected trailing quad in '{text}'.");
        return (
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture));
    }

    private static SkiaSharp.SKBitmap RenderBitmap(SKSvg svg)
    {
        Assert.NotNull(svg.Picture);
        var bitmap = svg.Picture!.ToBitmap(
            SkiaSharp.SKColors.Transparent,
            1f,
            1f,
            SkiaSharp.SKColorType.Rgba8888,
            SkiaSharp.SKAlphaType.Unpremul,
            svg.Settings.Srgb);

        return Assert.IsType<SkiaSharp.SKBitmap>(bitmap);
    }

    private static SvgJavaScriptRuntime GetJavaScriptRuntime(SKSvg svg)
    {
        var field = typeof(SKSvg).GetField("_javaScriptRuntime", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to access SVG JavaScript runtime.");
        return (SvgJavaScriptRuntime?)field.GetValue(svg)
               ?? throw new InvalidOperationException("SVG JavaScript runtime is not initialized.");
    }

    private static bool IsCompatibilityStyleStateInitialized(SvgDocument document)
    {
        var field = typeof(SvgDocument).GetField("_compatibilityStyleStateInitialized", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("Unable to access SVG compatibility style state.");
        return (bool)field.GetValue(document)!;
    }

    private sealed class SingleByteReadMemoryStream(byte[] buffer) : MemoryStream(buffer)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            return base.Read(buffer, offset, Math.Min(count, 1));
        }
    }
}
