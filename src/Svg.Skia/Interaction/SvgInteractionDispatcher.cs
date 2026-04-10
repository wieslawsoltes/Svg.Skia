using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Skia;

public enum SvgPointerDeviceType
{
    Unknown,
    Mouse,
    Touch,
    Pen
}

public enum SvgMouseButton
{
    None,
    Left,
    Middle,
    Right,
    XButton1,
    XButton2
}

public enum SvgPointerEventRoutePhase
{
    Tunnel,
    Target,
    Bubble
}

public sealed class SvgPointerInput
{
    public SvgPointerInput(
        SKPoint picturePoint,
        SvgPointerDeviceType pointerDeviceType,
        SvgMouseButton button,
        int clickCount,
        int wheelDelta,
        bool altKey,
        bool shiftKey,
        bool ctrlKey,
        string sessionId)
    {
        PicturePoint = picturePoint;
        PointerDeviceType = pointerDeviceType;
        Button = button;
        ClickCount = clickCount;
        WheelDelta = wheelDelta;
        AltKey = altKey;
        ShiftKey = shiftKey;
        CtrlKey = ctrlKey;
        SessionId = sessionId ?? string.Empty;
    }

    public SKPoint PicturePoint { get; }

    public SvgPointerDeviceType PointerDeviceType { get; }

    public SvgMouseButton Button { get; }

    public int ClickCount { get; }

    public int WheelDelta { get; }

    public bool AltKey { get; }

    public bool ShiftKey { get; }

    public bool CtrlKey { get; }

    public string SessionId { get; }
}

public sealed class SvgPointerEventArgs : EventArgs
{
    internal SvgPointerEventArgs(
        SvgPointerEventType eventType,
        SvgElement? element,
        SvgElement? targetElement,
        SvgElement? relatedElement,
        SvgPointerEventRoutePhase routePhase,
        SvgPointerInput input,
        string? cursor)
    {
        EventType = eventType;
        Element = element;
        TargetElement = targetElement;
        RelatedElement = relatedElement;
        RoutePhase = routePhase;
        Input = input;
        Cursor = cursor;
    }

    public SvgPointerEventType EventType { get; }

    public SvgElement? Element { get; }

    public SvgElement? TargetElement { get; }

    public SvgElement? RelatedElement { get; }

    public SvgPointerEventRoutePhase RoutePhase { get; }

    public SvgPointerInput Input { get; }

    public string? Cursor { get; }

    public bool Handled { get; set; }

    public SKPoint PicturePoint => Input.PicturePoint;
}

public sealed class SvgInteractionDispatchResult
{
    internal SvgInteractionDispatchResult(SvgElement? targetElement, string? cursor, bool handled)
    {
        TargetElement = targetElement;
        Cursor = cursor;
        Handled = handled;
    }

    public SvgElement? TargetElement { get; }

    public string? Cursor { get; }

    public bool Handled { get; }
}

public sealed class SvgInteractionDispatcher
{
    private readonly SvgEventCallerRegistry _eventCallerRegistry = new();
    private SvgElement? _registeredRoot;
    private SvgElement? _hoveredElement;
    private SvgElement? _pressedElement;
    private SvgElement? _capturedElement;

    public bool RaiseSvgElementEvents { get; set; } = true;

    public SvgElement? HoveredElement => _hoveredElement;

    public SvgElement? PressedElement => _pressedElement;

    public SvgElement? CapturedElement => _capturedElement;

    public string? CurrentCursor { get; private set; }

    public event EventHandler<SvgPointerEventArgs>? Dispatched;

    public SvgElement? HitTestTopmostElement(SKSvg? svg, SKPoint picturePoint)
    {
        return svg?.HitTestTopmostElement(picturePoint);
    }

    public void HandlePointerMoved(SKSvg? svg, SvgPointerInput input)
    {
        _ = DispatchPointerMoved(svg, input);
    }

    public void HandlePointerPressed(SKSvg? svg, SvgPointerInput input)
    {
        _ = DispatchPointerPressed(svg, input);
    }

    public void HandlePointerReleased(SKSvg? svg, SvgPointerInput input)
    {
        _ = DispatchPointerReleased(svg, input);
    }

    public void HandlePointerWheelChanged(SKSvg? svg, SvgPointerInput input)
    {
        _ = DispatchPointerWheelChanged(svg, input);
    }

    public void HandlePointerExited(SvgPointerInput input)
    {
        _ = DispatchPointerExited(input);
    }

    public SvgInteractionDispatchResult DispatchPointerMoved(SKSvg? svg, SvgPointerInput input)
    {
        EnsureEventBridge(svg);

        var animationFrameDirty = false;
        var hitTarget = svg?.HitTestTopmostElement(input.PicturePoint);
        var routeTarget = _capturedElement ?? hitTarget;
        var handled = false;

        if (_capturedElement is null)
        {
            handled = UpdateHover(svg, hitTarget, input, ref animationFrameDirty);
        }
        else
        {
            CurrentCursor = ResolveCursor(routeTarget);
        }

        handled |= DispatchRoutedEvent(svg, SvgPointerEventType.Move, routeTarget, null, input, "onmousemove", ref animationFrameDirty);
        RefreshAnimationFrame(svg, animationFrameDirty);

        return CreateResult(_hoveredElement ?? routeTarget, handled);
    }

    public SvgInteractionDispatchResult DispatchPointerPressed(SKSvg? svg, SvgPointerInput input)
    {
        EnsureEventBridge(svg);

        var animationFrameDirty = false;
        var target = svg?.HitTestTopmostElement(input.PicturePoint);
        var handled = UpdateHover(svg, target, input, ref animationFrameDirty);
        _pressedElement = target;
        _capturedElement = target;
        handled |= DispatchRoutedEvent(svg, SvgPointerEventType.Press, target, null, input, "onmousedown", ref animationFrameDirty);
        RefreshAnimationFrame(svg, animationFrameDirty);

        return CreateResult(_hoveredElement ?? target, handled);
    }

    public SvgInteractionDispatchResult DispatchPointerReleased(SKSvg? svg, SvgPointerInput input)
    {
        EnsureEventBridge(svg);

        var animationFrameDirty = false;
        var hitTarget = svg?.HitTestTopmostElement(input.PicturePoint);
        var routeTarget = _capturedElement ?? hitTarget;
        var captureWasActive = _capturedElement is not null;
        var handled = false;

        if (_capturedElement is null)
        {
            handled = UpdateHover(svg, hitTarget, input, ref animationFrameDirty);
        }
        else
        {
            CurrentCursor = ResolveCursor(routeTarget);
        }

        handled |= DispatchRoutedEvent(svg, SvgPointerEventType.Release, routeTarget, null, input, "onmouseup", ref animationFrameDirty);

        if (routeTarget is not null && ReferenceEquals(hitTarget, _pressedElement))
        {
            handled |= DispatchRoutedEvent(svg, SvgPointerEventType.Click, routeTarget, null, input, "onclick", ref animationFrameDirty);
        }

        _pressedElement = null;
        _capturedElement = null;

        if (captureWasActive)
        {
            handled |= UpdateHover(svg, hitTarget, input, ref animationFrameDirty);
        }

        RefreshAnimationFrame(svg, animationFrameDirty);
        return CreateResult(_hoveredElement ?? hitTarget, handled);
    }

    public SvgInteractionDispatchResult DispatchPointerWheelChanged(SKSvg? svg, SvgPointerInput input)
    {
        EnsureEventBridge(svg);

        var animationFrameDirty = false;
        var hitTarget = svg?.HitTestTopmostElement(input.PicturePoint);
        var routeTarget = _capturedElement ?? hitTarget;
        var handled = false;

        if (_capturedElement is null)
        {
            handled = UpdateHover(svg, hitTarget, input, ref animationFrameDirty);
        }
        else
        {
            CurrentCursor = ResolveCursor(routeTarget);
        }

        handled |= DispatchRoutedScroll(svg, routeTarget, input, ref animationFrameDirty);
        RefreshAnimationFrame(svg, animationFrameDirty);

        return CreateResult(_hoveredElement ?? routeTarget, handled);
    }

    public SvgInteractionDispatchResult DispatchPointerExited(SvgPointerInput input)
    {
        return DispatchPointerExited(null, input);
    }

    public SvgInteractionDispatchResult DispatchPointerExited(SKSvg? svg, SvgPointerInput input)
    {
        if (_capturedElement is not null)
        {
            CurrentCursor = null;
            return CreateResult(_capturedElement, handled: false);
        }

        var target = _hoveredElement;
        var handled = false;
        var animationFrameDirty = false;

        if (target is { })
        {
            handled = DispatchRoutedEvent(svg, SvgPointerEventType.Leave, target, null, input, "onmouseout", ref animationFrameDirty);
        }

        _hoveredElement = null;
        CurrentCursor = null;
        RefreshAnimationFrame(svg, animationFrameDirty);

        return CreateResult(null, handled);
    }

    public void Reset()
    {
        _hoveredElement = null;
        _pressedElement = null;
        _capturedElement = null;
        _registeredRoot = null;
        CurrentCursor = null;
        _eventCallerRegistry.Clear();
    }

    private bool UpdateHover(SKSvg? svg, SvgElement? target, SvgPointerInput input, ref bool animationFrameDirty)
    {
        if (ReferenceEquals(target, _hoveredElement))
        {
            CurrentCursor = ResolveCursor(target);
            return false;
        }

        var previous = _hoveredElement;
        var handled = false;

        if (previous is { })
        {
            handled |= DispatchRoutedEvent(svg, SvgPointerEventType.Leave, previous, target, input, "onmouseout", ref animationFrameDirty);
        }

        _hoveredElement = target;
        CurrentCursor = ResolveCursor(target);

        if (target is { })
        {
            handled |= DispatchRoutedEvent(svg, SvgPointerEventType.Enter, target, previous, input, "onmouseover", ref animationFrameDirty);
        }

        return handled;
    }

    private SvgInteractionDispatchResult CreateResult(SvgElement? target, bool handled)
    {
        return new SvgInteractionDispatchResult(target, CurrentCursor, handled);
    }

    private bool DispatchRoutedScroll(SKSvg? svg, SvgElement? target, SvgPointerInput input, ref bool animationFrameDirty)
    {
        var cursor = ResolveCursor(target);
        if (target is null)
        {
            return DispatchShared(
                SvgPointerEventType.Wheel,
                null,
                null,
                null,
                SvgPointerEventRoutePhase.Target,
                input,
                cursor);
        }

        if (DispatchTunnelEvent(
                SvgPointerEventType.Wheel,
                target,
                null,
                input,
                cursor))
        {
            return true;
        }

        foreach (var element in BuildRoute(target))
        {
            animationFrameDirty |= svg?.RecordAnimationPointerEvent(element, SvgPointerEventType.Wheel) == true;
            DispatchSvgMouseScroll(element, input);
            var routePhase = ReferenceEquals(element, target)
                ? SvgPointerEventRoutePhase.Target
                : SvgPointerEventRoutePhase.Bubble;

            if (DispatchShared(SvgPointerEventType.Wheel, element, target, null, routePhase, input, cursor))
            {
                return true;
            }
        }

        return false;
    }

    private bool DispatchRoutedEvent(
        SKSvg? svg,
        SvgPointerEventType eventType,
        SvgElement? target,
        SvgElement? relatedElement,
        SvgPointerInput input,
        string svgEventName,
        ref bool animationFrameDirty)
    {
        var cursor = ResolveCursor(target);
        if (target is null)
        {
            return DispatchShared(
                eventType,
                null,
                null,
                relatedElement,
                SvgPointerEventRoutePhase.Target,
                input,
                cursor);
        }

        if (DispatchTunnelEvent(
                eventType,
                target,
                relatedElement,
                input,
                cursor))
        {
            return true;
        }

        foreach (var element in BuildRoute(target))
        {
            animationFrameDirty |= svg?.RecordAnimationPointerEvent(element, eventType) == true;
            DispatchSvgMouseEvent(element, svgEventName, input);
            var routePhase = ReferenceEquals(element, target)
                ? SvgPointerEventRoutePhase.Target
                : SvgPointerEventRoutePhase.Bubble;

            if (DispatchShared(eventType, element, target, relatedElement, routePhase, input, cursor))
            {
                return true;
            }
        }

        return false;
    }

    private bool DispatchTunnelEvent(
        SvgPointerEventType eventType,
        SvgElement target,
        SvgElement? relatedElement,
        SvgPointerInput input,
        string? cursor)
    {
        var route = BuildRoute(target);
        for (var index = route.Count - 1; index > 0; index--)
        {
            if (DispatchShared(
                    eventType,
                    route[index],
                    target,
                    relatedElement,
                    SvgPointerEventRoutePhase.Tunnel,
                    input,
                    cursor))
            {
                return true;
            }
        }

        return false;
    }

    private static void RefreshAnimationFrame(SKSvg? svg, bool animationFrameDirty)
    {
        if (animationFrameDirty)
        {
            svg?.RefreshCurrentAnimationFrame(bypassThrottle: true);
        }
    }

    private static List<SvgElement> BuildRoute(SvgElement target)
    {
        var route = new List<SvgElement>();
        for (var current = target; current is not null; current = current.Parent)
        {
            route.Add(current);
        }

        return route;
    }

    private bool DispatchShared(
        SvgPointerEventType eventType,
        SvgElement? element,
        SvgElement? targetElement,
        SvgElement? relatedElement,
        SvgPointerEventRoutePhase routePhase,
        SvgPointerInput input,
        string? cursor)
    {
        var dispatched = Dispatched;
        if (dispatched is null)
        {
            return false;
        }

        var args = new SvgPointerEventArgs(
            eventType,
            element,
            targetElement,
            relatedElement,
            routePhase,
            input,
            cursor);

        dispatched(this, args);
        return args.Handled;
    }

    private void DispatchSvgMouseEvent(SvgElement? element, string eventName, SvgPointerInput input)
    {
        var elementId = element?.ID;
        if (!RaiseSvgElementEvents || string.IsNullOrWhiteSpace(elementId))
        {
            return;
        }

        var registeredElementId = elementId!;
        _eventCallerRegistry.Invoke(
            registeredElementId + "/" + eventName,
            input.PicturePoint.X,
            input.PicturePoint.Y,
            ToSvgMouseButtonValue(input.Button),
            input.ClickCount,
            input.AltKey,
            input.ShiftKey,
            input.CtrlKey,
            input.SessionId);
    }

    private void DispatchSvgMouseScroll(SvgElement? element, SvgPointerInput input)
    {
        var elementId = element?.ID;
        if (!RaiseSvgElementEvents || string.IsNullOrWhiteSpace(elementId))
        {
            return;
        }

        var registeredElementId = elementId!;
        _eventCallerRegistry.Invoke(
            registeredElementId + "/onmousescroll",
            input.WheelDelta,
            input.AltKey,
            input.ShiftKey,
            input.CtrlKey,
            input.SessionId);
    }

    private void EnsureEventBridge(SKSvg? svg)
    {
        if (!RaiseSvgElementEvents)
        {
            return;
        }

        var root = GetRootElement(svg);
        if (ReferenceEquals(root, _registeredRoot))
        {
            return;
        }

        _eventCallerRegistry.Clear();
        _registeredRoot = root;

        if (root is { })
        {
            RegisterTree(root);
        }
    }

    private static SvgElement? GetRootElement(SKSvg? svg)
    {
        return svg?.SourceDocument ?? svg?.RetainedSceneGraph?.SourceDocument;
    }

    private void RegisterTree(SvgElement element)
    {
        if (!string.IsNullOrWhiteSpace(element.ID))
        {
            element.RegisterEvents(_eventCallerRegistry);
        }

        foreach (var child in element.Children)
        {
            RegisterTree(child);
        }
    }

    private static string? ResolveCursor(SvgElement? target)
    {
        for (var current = target; current is not null; current = current.Parent)
        {
            if (current.TryGetAttribute("cursor", out var cursor) &&
                !string.IsNullOrWhiteSpace(cursor))
            {
                var normalizedCursor = cursor.Trim();
                if (string.Equals(normalizedCursor, "inherit", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return normalizedCursor;
            }
        }

        return null;
    }

    private static int ToSvgMouseButtonValue(SvgMouseButton button)
    {
        return button switch
        {
            SvgMouseButton.Left => 1,
            SvgMouseButton.Middle => 2,
            SvgMouseButton.Right => 3,
            SvgMouseButton.XButton1 => 4,
            SvgMouseButton.XButton2 => 5,
            _ => 0
        };
    }
}

internal sealed class SvgEventCallerRegistry : ISvgEventCaller
{
    private readonly Dictionary<string, Delegate> _actions = new(StringComparer.Ordinal);

    public void Clear()
    {
        _actions.Clear();
    }

    public void RegisterAction(string rpcID, Action action)
    {
        _actions[rpcID] = action;
    }

    public void RegisterAction<T1>(string rpcID, Action<T1> action)
    {
        _actions[rpcID] = action;
    }

    public void RegisterAction<T1, T2>(string rpcID, Action<T1, T2> action)
    {
        _actions[rpcID] = action;
    }

    public void RegisterAction<T1, T2, T3>(string rpcID, Action<T1, T2, T3> action)
    {
        _actions[rpcID] = action;
    }

    public void RegisterAction<T1, T2, T3, T4>(string rpcID, Action<T1, T2, T3, T4> action)
    {
        _actions[rpcID] = action;
    }

    public void RegisterAction<T1, T2, T3, T4, T5>(string rpcID, Action<T1, T2, T3, T4, T5> action)
    {
        _actions[rpcID] = action;
    }

    public void RegisterAction<T1, T2, T3, T4, T5, T6>(string rpcID, Action<T1, T2, T3, T4, T5, T6> action)
    {
        _actions[rpcID] = action;
    }

    public void RegisterAction<T1, T2, T3, T4, T5, T6, T7>(string rpcID, Action<T1, T2, T3, T4, T5, T6, T7> action)
    {
        _actions[rpcID] = action;
    }

    public void RegisterAction<T1, T2, T3, T4, T5, T6, T7, T8>(string rpcID, Action<T1, T2, T3, T4, T5, T6, T7, T8> action)
    {
        _actions[rpcID] = action;
    }

    public void UnregisterAction(string rpcID)
    {
        _actions.Remove(rpcID);
    }

    public void Invoke<T1, T2, T3, T4, T5>(string rpcID, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        if (_actions.TryGetValue(rpcID, out var action) && action is Action<T1, T2, T3, T4, T5> typedAction)
        {
            typedAction(arg1, arg2, arg3, arg4, arg5);
        }
    }

    public void Invoke<T1, T2, T3, T4, T5, T6, T7, T8>(
        string rpcID,
        T1 arg1,
        T2 arg2,
        T3 arg3,
        T4 arg4,
        T5 arg5,
        T6 arg6,
        T7 arg7,
        T8 arg8)
    {
        if (_actions.TryGetValue(rpcID, out var action) &&
            action is Action<T1, T2, T3, T4, T5, T6, T7, T8> typedAction)
        {
            typedAction(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }
    }
}
