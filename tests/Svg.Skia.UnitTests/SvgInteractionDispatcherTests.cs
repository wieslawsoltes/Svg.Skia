using System;
using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
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
        var dispatcher = new SvgInteractionDispatcher();
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
}
