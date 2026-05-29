using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShimSkiaSharp;
using Svg;
using Svg.Skia.UnitTests.Common;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgInteractionDispatcherTests : SvgUnitTest
{
    private const string NestedSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <g id="group">
            <rect id="outer" x="0" y="0" width="100" height="100" fill="red" />
            <rect id="inner" x="25" y="25" width="50" height="50" fill="blue" />
          </g>
        </svg>
        """;

    private const string PointerEventsSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <rect id="back" x="0" y="0" width="100" height="100" fill="green" />
          <rect id="front" x="0" y="0" width="100" height="100" fill="red" pointer-events="none" />
        </svg>
        """;

    private const string CursorSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <g id="group" cursor="pointer">
            <rect id="inner" x="10" y="10" width="50" height="50" fill="blue" />
          </g>
        </svg>
        """;

    private const string InheritedCursorSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <g id="group" cursor="pointer">
            <rect id="inner" x="10" y="10" width="50" height="50" fill="blue" cursor="inherit" />
          </g>
        </svg>
        """;

    private const string StrokePointerEventsSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <rect id="back" x="0" y="0" width="100" height="100" fill="green" />
          <rect id="front" x="10" y="10" width="80" height="80" fill="none" stroke="red" stroke-width="10" pointer-events="stroke" />
        </svg>
        """;

    private const string FillPointerEventsSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <rect id="back" x="0" y="0" width="100" height="100" fill="green" />
          <rect id="front" x="10" y="10" width="80" height="80" fill="none" stroke="red" stroke-width="10" pointer-events="fill" />
        </svg>
        """;

    private const string InvalidPaintedFallbackSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <rect id="back" x="10" y="10" width="50" height="50" fill="green" />
          <rect id="front" x="10" y="10" width="50" height="50" fill="url(#missing) none" pointer-events="painted" />
        </svg>
        """;

    private const string ClippedFrontSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <defs>
            <clipPath id="clip">
              <circle cx="50" cy="50" r="20" />
            </clipPath>
          </defs>
          <rect id="back" x="0" y="0" width="100" height="100" fill="green" />
          <rect id="front" x="0" y="0" width="100" height="100" fill="red" clip-path="url(#clip)" />
        </svg>
        """;

    private const string MaskedFrontSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <defs>
            <mask id="empty" maskUnits="userSpaceOnUse" x="0" y="0" width="100" height="100" />
            <mask id="transparent" maskUnits="userSpaceOnUse" x="0" y="0" width="100" height="100" opacity="0">
              <rect x="0" y="0" width="100" height="100" fill="black" />
            </mask>
          </defs>
          <rect id="back" x="0" y="0" width="100" height="100" fill="green" />
          <rect id="emptyMask" x="0" y="0" width="45" height="100" fill="red" mask="url(#empty)" />
          <rect id="transparentMask" x="55" y="0" width="45" height="100" fill="red" mask="url(#transparent)" />
        </svg>
        """;

    private const string UseInstanceSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="40"
             height="20">
          <defs>
            <rect id="template" x="0" y="0" width="10" height="8" fill="forestgreen" />
          </defs>
          <use id="instance" xlink:href="#template" />
        </svg>
        """;

    private const string UseInstanceSmilAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="80"
             height="40"
             viewBox="0 0 80 40">
          <defs>
            <g id="template">
              <rect id="templateChild" x="0" y="0" width="10" height="10" fill="red">
                <set attributeName="fill" begin="mouseover" end="mouseout" to="blue" />
              </rect>
            </g>
          </defs>
          <use id="instance" x="10" y="10" xlink:href="#template" />
          <rect id="groupIndicator" x="40" y="10" width="10" height="10" fill="red">
            <set attributeName="fill" begin="template.mouseover" end="template.mouseout" to="blue" />
          </rect>
        </svg>
        """;

    private const string AnchorSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
          <a id="link" href="https://example.com/docs" target="_blank">
            <rect id="target" x="10" y="10" width="50" height="50" fill="blue" />
          </a>
        </svg>
        """;

    private const string HyperlinkAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40" viewBox="0 0 80 40">
          <rect id="target" x="0" y="0" width="10" height="10" fill="red">
            <animate id="move" attributeName="x" from="0" to="10" begin="indefinite" dur="2s" fill="freeze" />
          </rect>
          <a id="link" href="#move">
            <rect id="button" x="20" y="0" width="20" height="20" fill="blue" />
          </a>
        </svg>
        """;

    private const string BlankTargetHyperlinkAnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40" viewBox="0 0 80 40">
          <rect id="target" x="0" y="0" width="10" height="10" fill="red">
            <animate id="move" attributeName="x" from="0" to="10" begin="indefinite" dur="2s" fill="freeze" />
          </rect>
          <a id="link" href="#move" target="_blank">
            <rect id="button" x="20" y="0" width="20" height="20" fill="blue" />
          </a>
        </svg>
        """;

    private const string ShowNewNavigationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="80"
             height="40"
             viewBox="0 0 80 40">
          <a id="link" xlink:href="page.svg#section" xlink:show="new">
            <rect id="button" x="20" y="0" width="20" height="20" fill="blue" />
          </a>
        </svg>
        """;

    private const string SparseHitSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20">
          <rect id="target" x="10" y="2" width="10" height="8" fill="forestgreen" />
        </svg>
        """;

    private const string AnimatedEventBridgeSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40" viewBox="0 0 40 40">
          <rect id="target" x="0" y="0" width="5" height="5" fill="red">
            <animate attributeName="x" from="0" to="10" dur="2s" fill="freeze" />
          </rect>
          <circle id="trigger" cx="20" cy="20" r="4" fill="blue" />
        </svg>
        """;

    private const string HiddenPointerEventsSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="120" height="70">
          <rect id="back" x="0" y="0" width="120" height="70" fill="green" />
          <rect id="hiddenPainted" x="10" y="10" width="20" height="20" fill="blue" visibility="hidden" pointer-events="painted" />
          <rect id="hiddenFill" x="40" y="10" width="20" height="20" fill="none" visibility="hidden" pointer-events="fill" />
          <rect id="hiddenStroke" x="70" y="10" width="20" height="20" fill="none" stroke="none" stroke-width="8" visibility="hidden" pointer-events="stroke" />
          <rect id="hiddenAll" x="10" y="40" width="20" height="20" fill="none" stroke="none" visibility="hidden" pointer-events="all" />
          <rect id="hiddenVisiblePainted" x="40" y="40" width="20" height="20" fill="blue" visibility="hidden" pointer-events="visiblePainted" />
        </svg>
        """;

    private const string TextPointerEventsSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="120" height="70">
          <rect id="back" x="0" y="0" width="120" height="70" fill="green" />
          <text id="textAll" x="10" y="50" font-family="sans-serif" font-size="40" fill="black" pointer-events="all">O</text>
          <text id="textNone" x="60" y="50" font-family="sans-serif" font-size="40" fill="black" pointer-events="none">O</text>
        </svg>
        """;

    private const string TextPointerEventsOverlaySvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="420" height="140">
          <g transform="translate(40, -10)" font-family="sans-serif" font-size="40">
            <text id="glyph" x="50" y="100" pointer-events="all">O</text>
            <g pointer-events="none">
              <rect id="overlay" x="50" y="65" height="40" width="300" fill="green" fill-opacity="0.5" />
              <rect x="50" y="65" height="40" width="300" fill="none" stroke="black" />
            </g>
          </g>
        </svg>
        """;

    private const string TextPointerEventsScriptSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="160" height="120">
          <script><![CDATA[
            function pass_in(elm) {
              elm.setAttribute("fill", "green");
            }
          ]]></script>
          <text id="glyph" x="50" y="90" font-family="sans-serif" font-size="40" fill="black" pointer-events="all" onmouseover="pass_in(evt.target)">O</text>
        </svg>
        """;

    private const string HiddenTextPointerEventsSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="180" height="80">
          <rect id="back" width="180" height="80" fill="green" />
          <g visibility="hidden" font-family="sans-serif" font-size="40">
            <text id="visiblePainted" x="10" y="50" pointer-events="visiblePainted">O</text>
            <text id="painted" x="60" y="50" pointer-events="painted">O</text>
            <text id="all" x="110" y="50" pointer-events="all">O</text>
          </g>
        </svg>
        """;

    private const string JavaScriptMutationRetestSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40">
          <rect id="back" x="0" y="0" width="80" height="40" fill="green" />
          <rect id="front" x="0" y="0" width="80" height="40" fill="red" onmouseover="evt.target.setAttribute('pointer-events', 'none')" />
        </svg>
        """;

    private const string AnimationMutationRetestSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="80" height="40">
          <rect id="back" x="0" y="0" width="80" height="40" fill="green" />
          <rect id="front" x="0" y="0" width="80" height="40" fill="red" pointer-events="visiblePainted">
            <set attributeName="visibility" to="hidden" begin="mouseover" fill="freeze" />
          </rect>
        </svg>
        """;

    [Fact]
    public void HitTestTopmostElement_ReturnsTopmostLeaf()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSvg);

        var element = svg.HitTestTopmostElement(new SKPoint(30, 30));

        Assert.Equal("inner", element?.ID);
    }

    [Fact]
    public void HitTestTopmostElement_SkipsPointerEventsNone()
    {
        using var svg = new SKSvg();
        svg.FromSvg(PointerEventsSvg);

        var element = svg.HitTestTopmostElement(new SKPoint(10, 10));

        Assert.Equal("back", element?.ID);
    }

    [Fact]
    public void HitTestTopmostElement_UsesStrokeGeometryForStrokePointerEvents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(StrokePointerEventsSvg);

        var strokeElement = svg.HitTestTopmostElement(new SKPoint(12, 50));
        var interiorElement = svg.HitTestTopmostElement(new SKPoint(50, 50));

        Assert.Equal("front", strokeElement?.ID);
        Assert.Equal("back", interiorElement?.ID);
    }

    [Fact]
    public void HitTestTopmostElement_UsesFillGeometryForFillPointerEvents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(FillPointerEventsSvg);

        var fillElement = svg.HitTestTopmostElement(new SKPoint(50, 50));
        var strokeOnlyElement = svg.HitTestTopmostElement(new SKPoint(7, 50));

        Assert.Equal("front", fillElement?.ID);
        Assert.Equal("back", strokeOnlyElement?.ID);
    }

    [Fact]
    public void HitTestTopmostElement_SkipsPaintedElementWithMissingPaintServerAndNoneFallback()
    {
        using var svg = new SKSvg();
        svg.FromSvg(InvalidPaintedFallbackSvg);

        var element = svg.HitTestTopmostElement(new SKPoint(20, 20));

        Assert.Equal("back", element?.ID);
    }

    [Fact]
    public void HitTestTopmostElement_RespectsClipPath()
    {
        using var svg = new SKSvg();
        svg.FromSvg(ClippedFrontSvg);

        var clippedElement = svg.HitTestTopmostElement(new SKPoint(10, 10));
        var visibleElement = svg.HitTestTopmostElement(new SKPoint(50, 50));

        Assert.Equal("back", clippedElement?.ID);
        Assert.Equal("front", visibleElement?.ID);
    }

    [Fact]
    public void HitTestTopmostElement_DoesNotSuppressMaskedTargets()
    {
        using var svg = new SKSvg();
        svg.FromSvg(MaskedFrontSvg);

        var emptyMaskElement = svg.HitTestTopmostElement(new SKPoint(20, 50));
        var transparentMaskElement = svg.HitTestTopmostElement(new SKPoint(80, 50));

        Assert.Equal("emptyMask", emptyMaskElement?.ID);
        Assert.Equal("transparentMask", transparentMaskElement?.ID);
    }

    [Fact]
    public void HitTestTopmostElement_HiddenElementsRespectNonVisiblePointerEventsValues()
    {
        using var svg = new SKSvg();
        svg.FromSvg(HiddenPointerEventsSvg);

        Assert.Equal("hiddenPainted", svg.HitTestTopmostElement(new SKPoint(15, 15))?.ID);
        Assert.Equal("hiddenFill", svg.HitTestTopmostElement(new SKPoint(45, 15))?.ID);
        Assert.Equal("hiddenStroke", svg.HitTestTopmostElement(new SKPoint(70, 20))?.ID);
        Assert.Equal("hiddenAll", svg.HitTestTopmostElement(new SKPoint(15, 45))?.ID);
        Assert.Equal("back", svg.HitTestTopmostElement(new SKPoint(45, 45))?.ID);
    }

    [Fact]
    public void HitTestTopmostElement_AppliesPointerEventsToTextBounds()
    {
        using var svg = new SKSvg();
        svg.FromSvg(TextPointerEventsSvg);

        Assert.Equal("textAll", svg.HitTestTopmostElement(new SKPoint(22, 38))?.ID);
        Assert.Equal("back", svg.HitTestTopmostElement(new SKPoint(72, 38))?.ID);
    }

    [Fact]
    public void HitTestTopmostElement_TextPointerEventsPierceDisabledOverlay()
    {
        using var svg = new SKSvg();
        svg.FromSvg(TextPointerEventsOverlaySvg);

        Assert.Equal("glyph", svg.HitTestTopmostElement(new SKPoint(102, 78))?.ID);
    }

    [Fact]
    public void DispatchPointerMoved_TextMouseOverCanCallGlobalFunctionWithEventTarget()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.FromSvg(TextPointerEventsScriptSvg);
        var dispatcher = new SvgInteractionDispatcher();

        var result = dispatcher.DispatchPointerMoved(
            svg,
            CreateInput(62, 88, SvgMouseButton.None));

        Assert.Equal("glyph", result.TargetElement?.ID);
        var text = Assert.IsType<SvgText>(svg.SourceDocument!.GetElementById("glyph"));
        var fill = Assert.IsType<SvgColourServer>(text.Fill);
        Assert.Equal(System.Drawing.Color.Green.ToArgb(), fill.Colour.ToArgb());
    }

    [Fact]
    public void HitTestTopmostElement_HiddenTextRespectsNonVisiblePointerEvents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(HiddenTextPointerEventsSvg);

        Assert.Equal("back", svg.HitTestTopmostElement(new SKPoint(22, 38))?.ID);
        Assert.Equal("painted", svg.HitTestTopmostElement(new SKPoint(72, 38))?.ID);
        Assert.Equal("all", svg.HitTestTopmostElement(new SKPoint(122, 38))?.ID);
    }

    [Fact]
    public void HitTestTopmostElement_DoesNotReturnStructuralRootForEmptySpace()
    {
        using var svg = new SKSvg();
        svg.FromSvg(SparseHitSvg);

        var element = svg.HitTestTopmostElement(new SKPoint(2, 2));

        Assert.Null(element);
    }

    [Fact]
    public void HitTestTopmostElement_UsesOwningUseElementForGeneratedContent()
    {
        using var svg = new SKSvg();
        svg.FromSvg(UseInstanceSvg);

        var element = svg.HitTestTopmostElement(new SKPoint(2, 2));

        Assert.Equal("instance", element?.ID);
    }

    [Fact]
    public void Dispatcher_RecordsSmilEventsForUseInstanceCorrespondingElements()
    {
        using var svg = new SKSvg();
        svg.FromSvg(UseInstanceSmilAnimationSvg);
        var dispatcher = new SvgInteractionDispatcher();

        var entered = dispatcher.DispatchPointerMoved(svg, CreateInput(12, 12, SvgMouseButton.None));

        Assert.Equal("instance", entered.TargetElement?.ID);
        var active = svg.AnimationController!.CreateAnimatedDocument(TimeSpan.FromMilliseconds(50));
        AssertRectangleFill(active, "templateChild", System.Drawing.Color.Blue);
        AssertRectangleFill(active, "groupIndicator", System.Drawing.Color.Blue);

        svg.SetAnimationTime(TimeSpan.FromMilliseconds(100));
        _ = dispatcher.DispatchPointerMoved(svg, CreateInput(70, 30, SvgMouseButton.None));

        var inactive = svg.AnimationController!.CreateAnimatedDocument(TimeSpan.FromMilliseconds(150));
        AssertRectangleFill(inactive, "templateChild", System.Drawing.Color.Red);
        AssertRectangleFill(inactive, "groupIndicator", System.Drawing.Color.Red);
    }

    [Fact]
    public void Dispatcher_RaisesSharedAndSvgElementEvents()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSvg);

        var inner = svg.HitTestTopmostElement(new SKPoint(30, 30));
        Assert.NotNull(inner);

        var dispatched = new List<SvgPointerEventType>();
        var clickCount = 0;
        var mouseDownCount = 0;
        var mouseUpCount = 0;
        var mouseMoveCount = 0;
        var mouseOverCount = 0;
        var mouseOutCount = 0;

        inner!.Click += (_, _) => clickCount++;
        inner.MouseDown += (_, _) => mouseDownCount++;
        inner.MouseUp += (_, _) => mouseUpCount++;
        inner.MouseMove += (_, _) => mouseMoveCount++;
        inner.MouseOver += (_, _) => mouseOverCount++;
        inner.MouseOut += (_, _) => mouseOutCount++;

        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.Dispatched += (_, args) =>
        {
            if (ReferenceEquals(args.Element, inner))
            {
                dispatched.Add(args.EventType);
            }
        };

        var moveInput = CreateInput(30, 30, SvgMouseButton.None);
        var pressInput = CreateInput(30, 30, SvgMouseButton.Left, clickCount: 1);
        var releaseInput = CreateInput(30, 30, SvgMouseButton.Left, clickCount: 1);
        var exitInput = CreateInput(0, 0, SvgMouseButton.None);

        dispatcher.HandlePointerMoved(svg, moveInput);
        dispatcher.HandlePointerPressed(svg, pressInput);
        dispatcher.HandlePointerReleased(svg, releaseInput);
        dispatcher.HandlePointerExited(exitInput);

        Assert.Contains(SvgPointerEventType.Enter, dispatched);
        Assert.Contains(SvgPointerEventType.Move, dispatched);
        Assert.Contains(SvgPointerEventType.Press, dispatched);
        Assert.Contains(SvgPointerEventType.Release, dispatched);
        Assert.Contains(SvgPointerEventType.Click, dispatched);
        Assert.Contains(SvgPointerEventType.Leave, dispatched);
        Assert.Equal(1, clickCount);
        Assert.Equal(1, mouseDownCount);
        Assert.Equal(1, mouseUpCount);
        Assert.Equal(1, mouseMoveCount);
        Assert.Equal(1, mouseOverCount);
        Assert.Equal(1, mouseOutCount);
    }

    [Fact]
    public void Dispatcher_ActivatesAnchorNavigationHandlerAfterClick()
    {
        var navigationHandler = new TestNavigationHandler();
        using var svg = new SKSvg();
        svg.Settings.NavigationHandler = navigationHandler;
        svg.FromSvg(AnchorSvg);

        var dispatcher = new SvgInteractionDispatcher();
        var input = CreateInput(20, 20, SvgMouseButton.Left, clickCount: 1);

        dispatcher.DispatchPointerPressed(svg, input);
        var result = dispatcher.DispatchPointerReleased(svg, input);

        var request = Assert.Single(navigationHandler.Requests);
        Assert.True(result.Handled);
        Assert.True(result.HyperlinkActivated);
        Assert.True(result.DefaultActionActivated);
        Assert.False(result.DefaultPrevented);
        Assert.Equal("https://example.com/docs", request.Uri.OriginalString);
        Assert.Equal("https://example.com/docs", request.Href);
        Assert.Equal("_blank", request.Target);
        Assert.Equal("link", request.SourceElementId);
        Assert.Same(svg.SourceDocument!.GetElementById("link"), request.SourceElement);
        Assert.Equal(input.PicturePoint, request.PicturePoint);
        Assert.Equal(SvgMouseButton.Left, request.Button);
        Assert.Equal(1, request.ClickCount);
        Assert.Equal("mouse", request.SessionId);
        Assert.Equal("https://example.com/docs", request.ResolvedUri.OriginalString);
        Assert.Null(request.BaseUri);
        Assert.Null(request.Fragment);
        Assert.False(request.IsSameDocumentReference);
        Assert.Null(request.Show);
    }

    [Fact]
    public void Dispatcher_AnchorFragmentStartsIndefiniteAnimation()
    {
        using var svg = new SKSvg();
        svg.FromSvg(HyperlinkAnimationSvg);

        var dispatcher = new SvgInteractionDispatcher();
        var input = CreateInput(25, 5, SvgMouseButton.Left, clickCount: 1);

        dispatcher.DispatchPointerPressed(svg, input);
        var result = dispatcher.DispatchPointerReleased(svg, input);

        Assert.True(result.Handled);
        Assert.True(result.HyperlinkActivated);
        Assert.True(result.DefaultActionActivated);

        var animated = svg.AnimationController!.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var target = Assert.IsType<SvgRectangle>(animated.GetElementById("target"));
        Assert.Equal(5f, target.X.Value, 3);
    }

    [Fact]
    public void Dispatcher_BlankTargetAnchorFragmentUsesNavigationHandler()
    {
        var navigationHandler = new TestNavigationHandler();
        using var svg = new SKSvg();
        svg.Settings.NavigationHandler = navigationHandler;
        svg.FromSvg(BlankTargetHyperlinkAnimationSvg);

        var dispatcher = new SvgInteractionDispatcher();
        var input = CreateInput(25, 5, SvgMouseButton.Left, clickCount: 1);

        dispatcher.DispatchPointerPressed(svg, input);
        var result = dispatcher.DispatchPointerReleased(svg, input);

        var request = Assert.Single(navigationHandler.Requests);
        Assert.True(result.Handled);
        Assert.Equal("_blank", request.Target);
        Assert.Equal("move", request.Fragment);
        Assert.True(request.IsSameDocumentReference);

        var animated = svg.AnimationController!.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var target = Assert.IsType<SvgRectangle>(animated.GetElementById("target"));
        Assert.Equal(0f, target.X.Value, 3);
    }

    [Fact]
    public void Dispatcher_ShowNewAnchorMapsToBlankTargetAndResolvedUri()
    {
        var navigationHandler = new TestNavigationHandler();
        using var svg = new SKSvg();
        svg.Settings.NavigationHandler = navigationHandler;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ShowNewNavigationSvg));
        var baseUri = new Uri("https://example.com/docs/source.svg");
        svg.Load(stream, null, baseUri);

        var dispatcher = new SvgInteractionDispatcher();
        var input = CreateInput(25, 5, SvgMouseButton.Left, clickCount: 1);

        dispatcher.DispatchPointerPressed(svg, input);
        dispatcher.DispatchPointerReleased(svg, input);

        var request = Assert.Single(navigationHandler.Requests);
        Assert.Equal("page.svg#section", request.Href);
        Assert.Equal("page.svg#section", request.Uri.OriginalString);
        Assert.Equal("https://example.com/docs/page.svg#section", request.ResolvedUri.OriginalString);
        Assert.Equal(baseUri, request.BaseUri);
        Assert.Equal("_blank", request.Target);
        Assert.Equal("new", request.Show);
        Assert.Equal("section", request.Fragment);
        Assert.False(request.IsSameDocumentReference);
    }

    [Fact]
    public void BeginAndEndAnimationElement_ScheduleIndefiniteAnimation()
    {
        using var svg = new SKSvg();
        svg.FromSvg(HyperlinkAnimationSvg);

        Assert.True(svg.BeginAnimationElement("move"));

        var active = svg.AnimationController!.CreateAnimatedDocument(TimeSpan.FromSeconds(1));
        var activeTarget = Assert.IsType<SvgRectangle>(active.GetElementById("target"));
        Assert.Equal(5f, activeTarget.X.Value, 3);

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));
        Assert.True(svg.EndAnimationElement("move"));

        var ended = svg.AnimationController.CreateAnimatedDocument(TimeSpan.FromSeconds(1.5));
        var endedTarget = Assert.IsType<SvgRectangle>(ended.GetElementById("target"));
        Assert.Equal(5f, endedTarget.X.Value, 3);
    }

    [Fact]
    public void Dispatcher_PreventDefaultSuppressesAnchorNavigationHandler()
    {
        var navigationHandler = new TestNavigationHandler();
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Settings.NavigationHandler = navigationHandler;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <a id="link" href="https://example.com/docs" onclick="evt.preventDefault()">
                <rect id="target" x="10" y="10" width="50" height="50" fill="blue" />
              </a>
            </svg>
            """);

        var dispatcher = new SvgInteractionDispatcher();
        var input = CreateInput(20, 20, SvgMouseButton.Left, clickCount: 1);

        dispatcher.DispatchPointerPressed(svg, input);
        var result = dispatcher.DispatchPointerReleased(svg, input);

        Assert.True(result.Handled);
        Assert.False(result.HyperlinkActivated);
        Assert.False(result.DefaultActionActivated);
        Assert.Empty(navigationHandler.Requests);
    }

    [Fact]
    public void Dispatcher_StopPropagationDoesNotSuppressAnchorNavigationHandler()
    {
        var navigationHandler = new TestNavigationHandler();
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.Settings.NavigationHandler = navigationHandler;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="100" height="100">
              <a id="link" href="https://example.com/docs" onclick="evt.stopPropagation()">
                <rect id="target" x="10" y="10" width="50" height="50" fill="blue" />
              </a>
            </svg>
            """);

        var dispatcher = new SvgInteractionDispatcher();
        var input = CreateInput(20, 20, SvgMouseButton.Left, clickCount: 1);

        dispatcher.DispatchPointerPressed(svg, input);
        dispatcher.DispatchPointerReleased(svg, input);

        Assert.Single(navigationHandler.Requests);
    }

    [Fact]
    public void Dispatcher_RoutesSharedEventsThroughTunnelTargetAndBubble()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSvg);

        var routed = new List<(string? ElementId, string? TargetId, SvgPointerEventRoutePhase RoutePhase)>();
        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.Dispatched += (_, args) =>
        {
            if (args.EventType == SvgPointerEventType.Press && args.Element?.ID is not null)
            {
                routed.Add((args.Element.ID, args.TargetElement?.ID, args.RoutePhase));
            }
        };

        dispatcher.DispatchPointerPressed(svg, CreateInput(30, 30, SvgMouseButton.Left, clickCount: 1));

        Assert.Collection(
            routed,
            item =>
            {
                Assert.Equal("group", item.ElementId);
                Assert.Equal("inner", item.TargetId);
                Assert.Equal(SvgPointerEventRoutePhase.Tunnel, item.RoutePhase);
            },
            item =>
            {
                Assert.Equal("inner", item.ElementId);
                Assert.Equal("inner", item.TargetId);
                Assert.Equal(SvgPointerEventRoutePhase.Target, item.RoutePhase);
            },
            item =>
            {
                Assert.Equal("group", item.ElementId);
                Assert.Equal("inner", item.TargetId);
                Assert.Equal(SvgPointerEventRoutePhase.Bubble, item.RoutePhase);
            });
    }

    [Fact]
    public void Dispatcher_BubblesSvgElementEventsToAncestors()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSvg);

        var inner = svg.HitTestTopmostElement(new SKPoint(30, 30));
        var group = inner?.Parent;

        Assert.NotNull(inner);
        Assert.NotNull(group);

        var innerMouseDownCount = 0;
        var groupMouseDownCount = 0;

        inner!.MouseDown += (_, _) => innerMouseDownCount++;
        group!.MouseDown += (_, _) => groupMouseDownCount++;

        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.DispatchPointerPressed(svg, CreateInput(30, 30, SvgMouseButton.Left, clickCount: 1));

        Assert.Equal(1, innerMouseDownCount);
        Assert.Equal(1, groupMouseDownCount);
    }

    [Fact]
    public void Dispatcher_KeepsOriginalElementHandlersAfterAnimationCreatesClone()
    {
        using var svg = new SKSvg();
        svg.FromSvg(AnimatedEventBridgeSvg);

        var sourceTrigger = svg.SourceDocument?.GetElementById("trigger");
        Assert.NotNull(sourceTrigger);

        var clickCount = 0;
        sourceTrigger!.Click += (_, _) => clickCount++;

        svg.SetAnimationTime(TimeSpan.FromSeconds(1));
        Assert.NotNull(svg.RetainedSceneGraph);
        Assert.NotSame(svg.SourceDocument, svg.RetainedSceneGraph!.SourceDocument);

        var dispatcher = new SvgInteractionDispatcher();
        var clickInput = CreateInput(20, 20, SvgMouseButton.Left, clickCount: 1);

        dispatcher.DispatchPointerPressed(svg, clickInput);
        dispatcher.DispatchPointerReleased(svg, clickInput);

        Assert.Equal(1, clickCount);
    }

    [Fact]
    public void Dispatcher_HandledStopsBubbleRoute()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSvg);

        var routed = new List<string?>();
        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.Dispatched += (_, args) =>
        {
            if (args.EventType != SvgPointerEventType.Press || args.Element?.ID is null)
            {
                return;
            }

            routed.Add(args.Element.ID);
            if (args.Element.ID == "inner")
            {
                args.Handled = true;
            }
        };

        var result = dispatcher.DispatchPointerPressed(svg, CreateInput(30, 30, SvgMouseButton.Left, clickCount: 1));

        Assert.True(result.Handled);
        Assert.Equal(new[] { "group", "inner" }, routed);
    }

    [Fact]
    public void Dispatcher_HandledStopsRouteWhenAncestorHandlesTunnelPhase()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSvg);

        var routed = new List<(string? ElementId, SvgPointerEventRoutePhase RoutePhase)>();
        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.Dispatched += (_, args) =>
        {
            if (args.EventType != SvgPointerEventType.Press || args.Element?.ID is null)
            {
                return;
            }

            routed.Add((args.Element.ID, args.RoutePhase));
            if (args.Element.ID == "group" && args.RoutePhase == SvgPointerEventRoutePhase.Tunnel)
            {
                args.Handled = true;
            }
        };

        var result = dispatcher.DispatchPointerPressed(svg, CreateInput(30, 30, SvgMouseButton.Left, clickCount: 1));

        Assert.True(result.Handled);
        Assert.Collection(
            routed,
            item =>
            {
                Assert.Equal("group", item.ElementId);
                Assert.Equal(SvgPointerEventRoutePhase.Tunnel, item.RoutePhase);
            });
    }

    [Fact]
    public void Dispatcher_ResolvesCursorFromAncestors()
    {
        using var svg = new SKSvg();
        svg.FromSvg(CursorSvg);

        string? eventCursor = null;
        var cursorChanges = new List<(string? OldCursor, string? NewCursor, string? TargetId)>();
        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.CursorChanged += (_, args) =>
            cursorChanges.Add((args.OldCursor, args.NewCursor, args.TargetElement?.ID));
        dispatcher.Dispatched += (_, args) =>
        {
            if (args.EventType == SvgPointerEventType.Move &&
                args.Element?.ID == "inner" &&
                args.RoutePhase == SvgPointerEventRoutePhase.Target)
            {
                eventCursor = args.Cursor;
            }
        };

        var result = dispatcher.DispatchPointerMoved(svg, CreateInput(20, 20, SvgMouseButton.None));

        Assert.Equal("pointer", result.Cursor);
        Assert.Equal("pointer", dispatcher.CurrentCursor);
        Assert.Equal("pointer", eventCursor);
        Assert.Collection(
            cursorChanges,
            item =>
            {
                Assert.Null(item.OldCursor);
                Assert.Equal("pointer", item.NewCursor);
                Assert.Equal("inner", item.TargetId);
            });
    }

    [Fact]
    public void Dispatcher_PressFocusesFocusableElementAndDispatchesFocusEvents()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="80" height="30" data-log="">
              <rect id="first" x="0" y="0" width="20" height="20" fill="red" tabindex="0"
                    onfocusin="append('first-in:' + related(evt))"
                    onfocusout="append('first-out:' + related(evt))" />
              <rect id="second" x="40" y="0" width="20" height="20" fill="blue" tabindex="0"
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
            """);
        var focusChanges = new List<(string? OldId, string? NewId)>();
        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.FocusChanged += (_, args) => focusChanges.Add((args.OldElement?.ID, args.NewElement?.ID));

        var first = dispatcher.DispatchPointerPressed(svg, CreateInput(5, 5, SvgMouseButton.Left, clickCount: 1));
        var second = dispatcher.DispatchPointerPressed(svg, CreateInput(45, 5, SvgMouseButton.Left, clickCount: 1));

        Assert.Equal("first", first.FocusedElement?.ID);
        Assert.True(first.DefaultActionActivated);
        Assert.Equal("second", second.FocusedElement?.ID);
        Assert.Equal("second", dispatcher.FocusedElement?.ID);
        Assert.Equal(
            "first-in:null;first-out:second;second-in:first;",
            GetAttributeValue(svg.SourceDocument!, "data-log"));
        var expectedChanges = new List<(string? OldId, string? NewId)>
        {
            (null, "first"),
            ("first", "second")
        };
        Assert.Equal(expectedChanges, focusChanges);
    }

    [Fact]
    public void Dispatcher_MousedownPreventDefaultSuppressesFocusDefaultAction()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="20" data-log="">
              <rect id="target" width="20" height="20" tabindex="0"
                    onmousedown="evt.preventDefault()"
                    onfocusin="document.documentElement.setAttribute('data-log', 'focused')" />
            </svg>
            """);
        var dispatcher = new SvgInteractionDispatcher();

        var result = dispatcher.DispatchPointerPressed(svg, CreateInput(5, 5, SvgMouseButton.Left, clickCount: 1));

        Assert.True(result.Handled);
        Assert.True(result.DefaultPrevented);
        Assert.False(result.DefaultActionActivated);
        Assert.Null(result.FocusedElement);
        Assert.Null(dispatcher.FocusedElement);
        Assert.Equal(string.Empty, GetAttributeValue(svg.SourceDocument!, "data-log"));
    }

    [Fact]
    public void Dispatcher_ResolvesInheritedCursorFromAncestor()
    {
        using var svg = new SKSvg();
        svg.FromSvg(InheritedCursorSvg);

        var dispatcher = new SvgInteractionDispatcher();

        var result = dispatcher.DispatchPointerMoved(svg, CreateInput(20, 20, SvgMouseButton.None));

        Assert.Equal("pointer", result.Cursor);
        Assert.Equal("pointer", dispatcher.CurrentCursor);
    }

    [Fact]
    public void Dispatcher_CapturesMoveAndReleaseToPressedElementUntilRelease()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSvg);

        var inner = svg.HitTestTopmostElement(new SKPoint(30, 30));
        Assert.NotNull(inner);

        var clickCount = 0;
        inner!.Click += (_, _) => clickCount++;

        var routed = new List<(SvgPointerEventType EventType, string? ElementId, string? TargetId)>();
        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.Dispatched += (_, args) =>
        {
            if ((args.EventType == SvgPointerEventType.Move || args.EventType == SvgPointerEventType.Release || args.EventType == SvgPointerEventType.Click) &&
                args.RoutePhase == SvgPointerEventRoutePhase.Target)
            {
                routed.Add((args.EventType, args.Element?.ID, args.TargetElement?.ID));
            }
        };

        dispatcher.DispatchPointerPressed(svg, CreateInput(30, 30, SvgMouseButton.Left, clickCount: 1));
        Assert.Equal("inner", dispatcher.CapturedElement?.ID);
        Assert.Equal("inner", dispatcher.HoveredElement?.ID);

        dispatcher.DispatchPointerMoved(svg, CreateInput(10, 10, SvgMouseButton.None));
        Assert.Equal("inner", dispatcher.CapturedElement?.ID);
        Assert.Equal("inner", dispatcher.HoveredElement?.ID);

        dispatcher.DispatchPointerReleased(svg, CreateInput(10, 10, SvgMouseButton.Left, clickCount: 1));

        Assert.Null(dispatcher.CapturedElement);
        Assert.Equal("outer", dispatcher.HoveredElement?.ID);
        Assert.Equal(0, clickCount);
        Assert.Collection(
            routed,
            item =>
            {
                Assert.Equal(SvgPointerEventType.Move, item.EventType);
                Assert.Equal("inner", item.ElementId);
                Assert.Equal("inner", item.TargetId);
            },
            item =>
            {
                Assert.Equal(SvgPointerEventType.Release, item.EventType);
                Assert.Equal("inner", item.ElementId);
                Assert.Equal("inner", item.TargetId);
            });
    }

    [Fact]
    public void Dispatcher_ClickSequencesEnterPressReleaseAndClick()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSvg);

        var routed = new List<SvgPointerEventType>();
        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.Dispatched += (_, args) =>
        {
            if (args.Element?.ID == "inner" && args.RoutePhase == SvgPointerEventRoutePhase.Target)
            {
                routed.Add(args.EventType);
            }
        };

        var result = dispatcher.DispatchPointerClick(svg, CreateInput(30, 30, SvgMouseButton.Left, clickCount: 1));

        Assert.Equal("inner", result.TargetElement?.ID);
        Assert.Equal(
            new[]
            {
                SvgPointerEventType.Enter,
                SvgPointerEventType.Press,
                SvgPointerEventType.Release,
                SvgPointerEventType.Click
            },
            routed);
    }

    [Fact]
    public void Dispatcher_MoveSequencesMouseOutBeforeMouseOverAndMove()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSvg);

        var routed = new List<(SvgPointerEventType EventType, string? ElementId)>();
        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.Dispatched += (_, args) =>
        {
            if (args.RoutePhase == SvgPointerEventRoutePhase.Target)
            {
                routed.Add((args.EventType, args.Element?.ID));
            }
        };

        dispatcher.DispatchPointerMoved(svg, CreateInput(30, 30, SvgMouseButton.None));
        routed.Clear();

        dispatcher.DispatchPointerMoved(svg, CreateInput(10, 10, SvgMouseButton.None));

        var expected = new (SvgPointerEventType EventType, string? ElementId)[]
        {
            (SvgPointerEventType.Leave, "inner"),
            (SvgPointerEventType.Enter, "outer"),
            (SvgPointerEventType.Move, "outer")
        };
        Assert.Equal(expected, routed);
    }

    [Fact]
    public void Dispatcher_RetestsHoverAfterJavaScriptMutationChangesPointerEvents()
    {
        using var svg = new SKSvg();
        svg.Settings.EnableJavaScript = true;
        svg.Settings.ThrowOnJavaScriptError = true;
        svg.FromSvg(JavaScriptMutationRetestSvg);

        var routed = new List<(SvgPointerEventType EventType, string? ElementId)>();
        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.Dispatched += (_, args) =>
        {
            if (args.RoutePhase == SvgPointerEventRoutePhase.Target)
            {
                routed.Add((args.EventType, args.Element?.ID));
            }
        };

        var result = dispatcher.DispatchPointerMoved(svg, CreateInput(20, 20, SvgMouseButton.None));

        Assert.Equal("back", result.TargetElement?.ID);
        Assert.Equal("back", dispatcher.HoveredElement?.ID);
        Assert.Equal("back", svg.HitTestTopmostElement(new SKPoint(20, 20))?.ID);
        var expected = new (SvgPointerEventType EventType, string? ElementId)[]
        {
            (SvgPointerEventType.Enter, "front"),
            (SvgPointerEventType.Leave, "front"),
            (SvgPointerEventType.Enter, "back"),
            (SvgPointerEventType.Move, "back")
        };
        Assert.Equal(expected, routed);
    }

    [Fact]
    public void Dispatcher_RetestsHoverAfterAnimationMutationChangesHitTarget()
    {
        using var svg = new SKSvg();
        svg.FromSvg(AnimationMutationRetestSvg);

        var routed = new List<(SvgPointerEventType EventType, string? ElementId)>();
        var dispatcher = new SvgInteractionDispatcher();
        dispatcher.Dispatched += (_, args) =>
        {
            if (args.RoutePhase == SvgPointerEventRoutePhase.Target)
            {
                routed.Add((args.EventType, args.Element?.ID));
            }
        };

        var result = dispatcher.DispatchPointerMoved(svg, CreateInput(20, 20, SvgMouseButton.None));

        Assert.Equal("back", result.TargetElement?.ID);
        Assert.Equal("back", dispatcher.HoveredElement?.ID);
        var expected = new (SvgPointerEventType EventType, string? ElementId)[]
        {
            (SvgPointerEventType.Enter, "front"),
            (SvgPointerEventType.Leave, "front"),
            (SvgPointerEventType.Enter, "back"),
            (SvgPointerEventType.Move, "back")
        };
        Assert.Equal(expected, routed);
    }

    [Fact]
    public void Dispatcher_TracksHoveredElement()
    {
        using var svg = new SKSvg();
        svg.FromSvg(NestedSvg);

        var dispatcher = new SvgInteractionDispatcher();

        dispatcher.HandlePointerMoved(svg, CreateInput(30, 30, SvgMouseButton.None));
        Assert.Equal("inner", dispatcher.HoveredElement?.ID);

        dispatcher.HandlePointerMoved(svg, CreateInput(10, 10, SvgMouseButton.None));
        Assert.Equal("outer", dispatcher.HoveredElement?.ID);

        dispatcher.HandlePointerExited(CreateInput(0, 0, SvgMouseButton.None));
        Assert.Null(dispatcher.HoveredElement);
    }

    private static SvgPointerInput CreateInput(float x, float y, SvgMouseButton button, int clickCount = 0)
    {
        return new SvgPointerInput(
            new SKPoint(x, y),
            SvgPointerDeviceType.Mouse,
            button,
            clickCount,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false,
            sessionId: "mouse");
    }

    private static string GetAttributeValue(SvgElement element, string attributeName)
    {
        return element.TryGetAttribute(attributeName, out var value) ? value ?? string.Empty : string.Empty;
    }

    private static void AssertRectangleFill(SvgDocument document, string elementId, System.Drawing.Color expected)
    {
        var rectangle = Assert.IsType<SvgRectangle>(document.GetElementById(elementId));
        var fill = Assert.IsType<SvgColourServer>(rectangle.Fill);
        Assert.Equal(expected.ToArgb(), fill.Colour.ToArgb());
    }

    private sealed class TestNavigationHandler : ISKSvgNavigationHandler
    {
        public List<SKSvgNavigationRequest> Requests { get; } = new();

        public bool Navigate(SKSvgNavigationRequest request)
        {
            Requests.Add(request);
            return true;
        }
    }
}
