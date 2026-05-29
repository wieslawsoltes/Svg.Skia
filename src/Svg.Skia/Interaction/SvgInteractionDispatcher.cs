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

public sealed class SvgCursorChangedEventArgs : EventArgs
{
    internal SvgCursorChangedEventArgs(string? oldCursor, string? newCursor, SvgElement? targetElement)
    {
        OldCursor = oldCursor;
        NewCursor = newCursor;
        TargetElement = targetElement;
    }

    public string? OldCursor { get; }

    public string? NewCursor { get; }

    public SvgElement? TargetElement { get; }
}

public sealed class SvgFocusChangedEventArgs : EventArgs
{
    internal SvgFocusChangedEventArgs(SvgElement? oldElement, SvgElement? newElement, SvgPointerInput input)
    {
        OldElement = oldElement;
        NewElement = newElement;
        Input = input;
    }

    public SvgElement? OldElement { get; }

    public SvgElement? NewElement { get; }

    public SvgPointerInput Input { get; }
}

public sealed class SvgInteractionDispatchResult
{
    internal SvgInteractionDispatchResult(
        SvgElement? targetElement,
        SvgElement? focusedElement,
        string? cursor,
        bool handled,
        bool defaultPrevented,
        bool defaultActionActivated,
        bool hyperlinkActivated)
    {
        TargetElement = targetElement;
        FocusedElement = focusedElement;
        Cursor = cursor;
        Handled = handled;
        DefaultPrevented = defaultPrevented;
        DefaultActionActivated = defaultActionActivated;
        HyperlinkActivated = hyperlinkActivated;
    }

    public SvgElement? TargetElement { get; }

    public SvgElement? FocusedElement { get; }

    public string? Cursor { get; }

    public bool Handled { get; }

    public bool DefaultPrevented { get; }

    public bool DefaultActionActivated { get; }

    public bool HyperlinkActivated { get; }
}

public sealed class SvgInteractionDispatcher
{
    private const int MaxMutationRetestPasses = 8;

    private readonly SvgEventCallerRegistry _eventCallerRegistry = new();
    private SvgElement? _registeredRoot;
    private SvgElement? _hoveredElement;
    private SvgElement? _pressedElement;
    private SvgElement? _capturedElement;
    private SvgElement? _focusedElement;
    private SvgSceneNode? _hoveredNode;
    private SvgSceneNode? _pressedNode;
    private SvgSceneNode? _capturedNode;

    public bool RaiseSvgElementEvents { get; set; } = true;

    public bool RetestHoverAfterMutation { get; set; } = true;

    public SvgElement? HoveredElement => _hoveredElement;

    public SvgElement? PressedElement => _pressedElement;

    public SvgElement? CapturedElement => _capturedElement;

    public SvgElement? FocusedElement => _focusedElement;

    public string? CurrentCursor { get; private set; }

    public event EventHandler<SvgPointerEventArgs>? Dispatched;

    public event EventHandler<SvgCursorChangedEventArgs>? CursorChanged;

    public event EventHandler<SvgFocusChangedEventArgs>? FocusChanged;

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

    public void HandlePointerClick(SKSvg? svg, SvgPointerInput input)
    {
        _ = DispatchPointerClick(svg, input);
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
        var pointerRetestDirty = false;
        var hitNode = svg?.HitTestTopmostSceneNode(input.PicturePoint);
        var hitTarget = hitNode?.HitTestTargetElement;
        var routeTarget = _capturedElement ?? hitTarget;
        var routeNode = _capturedNode ?? hitNode;
        var handled = false;
        var defaultPrevented = false;

        if (_capturedElement is null)
        {
            handled = UpdateHover(svg, hitNode, input, ref animationFrameDirty, ref pointerRetestDirty);
            handled |= RetestHoverAfterPointerMutation(svg, input, ref animationFrameDirty, ref pointerRetestDirty);
            routeTarget = _hoveredElement;
            routeNode = _hoveredNode;
        }
        else
        {
            SetCurrentCursor(ResolveCursor(routeTarget), routeTarget);
        }

        var moveResult = DispatchRoutedEventCore(svg, SvgPointerEventType.Move, routeTarget, routeNode, null, input, "onmousemove", ref animationFrameDirty, ref pointerRetestDirty);
        handled |= moveResult.Handled;
        defaultPrevented |= moveResult.DefaultPrevented;
        handled |= RetestHoverAfterPointerMutation(svg, input, ref animationFrameDirty, ref pointerRetestDirty);
        RefreshAnimationFrame(svg, animationFrameDirty);

        return CreateResult(_hoveredElement ?? routeTarget, handled, defaultPrevented);
    }

    public SvgInteractionDispatchResult DispatchPointerPressed(SKSvg? svg, SvgPointerInput input)
    {
        EnsureEventBridge(svg);

        var animationFrameDirty = false;
        var pointerRetestDirty = false;
        var targetNode = svg?.HitTestTopmostSceneNode(input.PicturePoint);
        var target = targetNode?.HitTestTargetElement;
        var handled = UpdateHover(svg, targetNode, input, ref animationFrameDirty, ref pointerRetestDirty);
        handled |= RetestHoverAfterPointerMutation(svg, input, ref animationFrameDirty, ref pointerRetestDirty);
        target = _hoveredElement;
        targetNode = _hoveredNode;
        _pressedElement = target;
        _capturedElement = target;
        _pressedNode = targetNode;
        _capturedNode = targetNode;
        var pressResult = DispatchRoutedEventCore(svg, SvgPointerEventType.Press, target, targetNode, null, input, "onmousedown", ref animationFrameDirty, ref pointerRetestDirty);
        handled |= pressResult.Handled;
        var defaultPrevented = pressResult.DefaultPrevented;
        var defaultActionActivated = false;
        if (!pressResult.DefaultPrevented)
        {
            defaultActionActivated = ApplyFocusDefaultAction(svg, target, input, ref animationFrameDirty, ref pointerRetestDirty);
            handled |= defaultActionActivated;
        }

        RefreshAnimationFrame(svg, animationFrameDirty);

        return CreateResult(_hoveredElement ?? target, handled, defaultPrevented, defaultActionActivated);
    }

    public SvgInteractionDispatchResult DispatchPointerClick(SKSvg? svg, SvgPointerInput input)
    {
        _ = DispatchPointerPressed(svg, input);
        return DispatchPointerReleased(svg, input);
    }

    public SvgInteractionDispatchResult DispatchPointerReleased(SKSvg? svg, SvgPointerInput input)
    {
        EnsureEventBridge(svg);

        var animationFrameDirty = false;
        var pointerRetestDirty = false;
        var hitNode = svg?.HitTestTopmostSceneNode(input.PicturePoint);
        var hitTarget = hitNode?.HitTestTargetElement;
        var routeTarget = _capturedElement ?? hitTarget;
        var routeNode = _capturedNode ?? hitNode;
        var captureWasActive = _capturedElement is not null;
        var handled = false;
        var defaultPrevented = false;
        var defaultActionActivated = false;
        var hyperlinkActivated = false;

        if (_capturedElement is null)
        {
            handled = UpdateHover(svg, hitNode, input, ref animationFrameDirty, ref pointerRetestDirty);
            handled |= RetestHoverAfterPointerMutation(svg, input, ref animationFrameDirty, ref pointerRetestDirty);
            hitTarget = _hoveredElement;
            hitNode = _hoveredNode;
            routeTarget = hitTarget;
            routeNode = hitNode;
        }
        else
        {
            SetCurrentCursor(ResolveCursor(routeTarget), routeTarget);
        }

        var releaseResult = DispatchRoutedEventCore(svg, SvgPointerEventType.Release, routeTarget, routeNode, null, input, "onmouseup", ref animationFrameDirty, ref pointerRetestDirty);
        handled |= releaseResult.Handled;
        defaultPrevented |= releaseResult.DefaultPrevented;

        if (routeTarget is not null && ReferenceEquals(hitTarget, _pressedElement))
        {
            var clickResult = DispatchRoutedEventCore(svg, SvgPointerEventType.Click, routeTarget, routeNode, null, input, "onclick", ref animationFrameDirty, ref pointerRetestDirty);
            handled |= clickResult.Handled;
            defaultPrevented |= clickResult.DefaultPrevented;
            if (!clickResult.DefaultPrevented)
            {
                hyperlinkActivated = svg?.ActivateHyperlink(routeTarget, input) == true;
                defaultActionActivated |= hyperlinkActivated;
                handled |= hyperlinkActivated;
            }
        }

        _pressedElement = null;
        _capturedElement = null;
        _pressedNode = null;
        _capturedNode = null;

        if (captureWasActive)
        {
            RefreshAnimationFrame(svg, animationFrameDirty);
            animationFrameDirty = false;
            hitNode = svg?.HitTestTopmostSceneNode(input.PicturePoint);
            hitTarget = hitNode?.HitTestTargetElement;
            handled |= UpdateHover(svg, hitNode, input, ref animationFrameDirty, ref pointerRetestDirty);
        }

        handled |= RetestHoverAfterPointerMutation(svg, input, ref animationFrameDirty, ref pointerRetestDirty);
        RefreshAnimationFrame(svg, animationFrameDirty);
        return CreateResult(_hoveredElement ?? hitTarget, handled, defaultPrevented, defaultActionActivated, hyperlinkActivated);
    }

    public SvgInteractionDispatchResult DispatchPointerWheelChanged(SKSvg? svg, SvgPointerInput input)
    {
        EnsureEventBridge(svg);

        var animationFrameDirty = false;
        var pointerRetestDirty = false;
        var hitNode = svg?.HitTestTopmostSceneNode(input.PicturePoint);
        var hitTarget = hitNode?.HitTestTargetElement;
        var routeTarget = _capturedElement ?? hitTarget;
        var routeNode = _capturedNode ?? hitNode;
        var handled = false;
        var defaultPrevented = false;

        if (_capturedElement is null)
        {
            handled = UpdateHover(svg, hitNode, input, ref animationFrameDirty, ref pointerRetestDirty);
            handled |= RetestHoverAfterPointerMutation(svg, input, ref animationFrameDirty, ref pointerRetestDirty);
            routeTarget = _hoveredElement;
            routeNode = _hoveredNode;
        }
        else
        {
            SetCurrentCursor(ResolveCursor(routeTarget), routeTarget);
        }

        var scrollResult = DispatchRoutedScroll(svg, routeTarget, routeNode, input, ref animationFrameDirty, ref pointerRetestDirty);
        handled |= scrollResult.Handled;
        defaultPrevented |= scrollResult.DefaultPrevented;
        handled |= RetestHoverAfterPointerMutation(svg, input, ref animationFrameDirty, ref pointerRetestDirty);
        RefreshAnimationFrame(svg, animationFrameDirty);

        return CreateResult(_hoveredElement ?? routeTarget, handled, defaultPrevented);
    }

    public SvgInteractionDispatchResult DispatchPointerExited(SvgPointerInput input)
    {
        return DispatchPointerExited(null, input);
    }

    public SvgInteractionDispatchResult DispatchPointerExited(SKSvg? svg, SvgPointerInput input)
    {
        if (_capturedElement is not null)
        {
            SetCurrentCursor(null, null);
            return CreateResult(_capturedElement, handled: false);
        }

        var target = _hoveredElement;
        var targetNode = _hoveredNode;
        var handled = false;
        var animationFrameDirty = false;
        var pointerRetestDirty = false;

        if (target is { })
        {
            handled = DispatchRoutedEvent(svg, SvgPointerEventType.Leave, target, targetNode, null, input, "onmouseout", ref animationFrameDirty, ref pointerRetestDirty);
        }

        _hoveredElement = null;
        _hoveredNode = null;
        SetCurrentCursor(null, null);
        RefreshAnimationFrame(svg, animationFrameDirty);

        return CreateResult(null, handled);
    }

    public void Reset()
    {
        _hoveredElement = null;
        _pressedElement = null;
        _capturedElement = null;
        _focusedElement = null;
        _hoveredNode = null;
        _pressedNode = null;
        _capturedNode = null;
        _registeredRoot = null;
        SetCurrentCursor(null, null);
        _eventCallerRegistry.Clear();
    }

    private bool UpdateHover(
        SKSvg? svg,
        SvgSceneNode? targetNode,
        SvgPointerInput input,
        ref bool animationFrameDirty,
        ref bool pointerRetestDirty)
    {
        var target = targetNode?.HitTestTargetElement;
        if (ReferenceEquals(target, _hoveredElement))
        {
            SetCurrentCursor(ResolveCursor(target), target);
            return false;
        }

        var previous = _hoveredElement;
        var previousNode = _hoveredNode;
        var handled = false;

        if (previous is { })
        {
            handled |= DispatchRoutedEvent(svg, SvgPointerEventType.Leave, previous, previousNode, target, input, "onmouseout", ref animationFrameDirty, ref pointerRetestDirty);
        }

        _hoveredElement = target;
        _hoveredNode = targetNode;
        SetCurrentCursor(ResolveCursor(target), target);

        if (target is { })
        {
            handled |= DispatchRoutedEvent(svg, SvgPointerEventType.Enter, target, targetNode, previous, input, "onmouseover", ref animationFrameDirty, ref pointerRetestDirty);
        }

        return handled;
    }

    private bool RetestHoverAfterPointerMutation(
        SKSvg? svg,
        SvgPointerInput input,
        ref bool animationFrameDirty,
        ref bool pointerRetestDirty)
    {
        if (!RetestHoverAfterMutation ||
            !pointerRetestDirty ||
            _capturedElement is not null)
        {
            return false;
        }

        var handled = false;
        for (var i = 0; i < MaxMutationRetestPasses && pointerRetestDirty; i++)
        {
            pointerRetestDirty = false;
            RefreshAnimationFrame(svg, animationFrameDirty);
            animationFrameDirty = false;

            var targetNode = svg?.HitTestTopmostSceneNode(input.PicturePoint);
            handled |= UpdateHover(svg, targetNode, input, ref animationFrameDirty, ref pointerRetestDirty);
        }

        return handled;
    }

    public SvgInteractionDispatchResult FocusElement(SKSvg? svg, SvgElement? element, SvgPointerInput input)
    {
        EnsureEventBridge(svg);

        var animationFrameDirty = false;
        var pointerRetestDirty = false;
        var focusTarget = element is not null && IsFocusableElement(element) ? element : null;
        var changed = SetFocusedElement(svg, focusTarget, input, ref animationFrameDirty, ref pointerRetestDirty);
        RefreshAnimationFrame(svg, animationFrameDirty);
        return CreateResult(focusTarget, changed, defaultActionActivated: changed);
    }

    public SvgInteractionDispatchResult BlurFocusedElement(SKSvg? svg, SvgPointerInput input)
    {
        EnsureEventBridge(svg);

        var animationFrameDirty = false;
        var pointerRetestDirty = false;
        var previous = _focusedElement;
        var changed = SetFocusedElement(svg, null, input, ref animationFrameDirty, ref pointerRetestDirty);
        RefreshAnimationFrame(svg, animationFrameDirty);
        return CreateResult(previous, changed, defaultActionActivated: changed);
    }

    private bool ApplyFocusDefaultAction(
        SKSvg? svg,
        SvgElement? target,
        SvgPointerInput input,
        ref bool animationFrameDirty,
        ref bool pointerRetestDirty)
    {
        return SetFocusedElement(svg, ResolveFocusableElement(target), input, ref animationFrameDirty, ref pointerRetestDirty);
    }

    private bool SetFocusedElement(
        SKSvg? svg,
        SvgElement? element,
        SvgPointerInput input,
        ref bool animationFrameDirty,
        ref bool pointerRetestDirty)
    {
        if (ReferenceEquals(_focusedElement, element))
        {
            return false;
        }

        var previous = _focusedElement;
        if (previous is not null)
        {
            _ = DispatchJavaScriptFocusEvent(svg, previous, element, "blur", "onblur", bubbles: false, input, ref pointerRetestDirty);
            _ = DispatchJavaScriptFocusEvent(svg, previous, element, "focusout", "onfocusout", bubbles: true, input, ref pointerRetestDirty);
        }

        _focusedElement = element;
        FocusChanged?.Invoke(this, new SvgFocusChangedEventArgs(previous, element, input));

        if (element is not null)
        {
            _ = DispatchJavaScriptFocusEvent(svg, element, previous, "focus", "onfocus", bubbles: false, input, ref pointerRetestDirty);
            _ = DispatchJavaScriptFocusEvent(svg, element, previous, "focusin", "onfocusin", bubbles: true, input, ref pointerRetestDirty);
        }

        animationFrameDirty |= pointerRetestDirty;
        return true;
    }

    private static bool DispatchJavaScriptFocusEvent(
        SKSvg? svg,
        SvgElement target,
        SvgElement? relatedElement,
        string eventType,
        string attributeName,
        bool bubbles,
        SvgPointerInput input,
        ref bool pointerRetestDirty)
    {
        var handled = false;
        object? javaScriptEvent = null;
        if (bubbles)
        {
            foreach (var element in BuildRoute(target))
            {
                var javaScriptResult = svg?.DispatchJavaScriptEvent(element, target, relatedElement, eventType, attributeName, input, ref javaScriptEvent);
                pointerRetestDirty |= javaScriptResult?.Mutated == true;
                handled |= javaScriptResult?.DefaultPrevented == true;
                if (javaScriptResult?.CancelBubble == true)
                {
                    return true;
                }
            }

            return handled;
        }

        var result = svg?.DispatchJavaScriptEvent(target, target, relatedElement, eventType, attributeName, input, ref javaScriptEvent);
        pointerRetestDirty |= result?.Mutated == true;
        return result?.DefaultPrevented == true;
    }

    private SvgInteractionDispatchResult CreateResult(
        SvgElement? target,
        bool handled,
        bool defaultPrevented = false,
        bool defaultActionActivated = false,
        bool hyperlinkActivated = false)
    {
        return new SvgInteractionDispatchResult(
            target,
            _focusedElement,
            CurrentCursor,
            handled,
            defaultPrevented,
            defaultActionActivated,
            hyperlinkActivated);
    }

    private void SetCurrentCursor(string? cursor, SvgElement? target)
    {
        if (string.Equals(CurrentCursor, cursor, StringComparison.Ordinal))
        {
            return;
        }

        var previous = CurrentCursor;
        CurrentCursor = cursor;
        CursorChanged?.Invoke(this, new SvgCursorChangedEventArgs(previous, cursor, target));
    }

    private readonly struct SvgRoutedEventDispatchResult
    {
        public SvgRoutedEventDispatchResult(bool handled, bool defaultPrevented)
        {
            Handled = handled;
            DefaultPrevented = defaultPrevented;
        }

        public bool Handled { get; }

        public bool DefaultPrevented { get; }
    }

    private SvgRoutedEventDispatchResult DispatchRoutedScroll(
        SKSvg? svg,
        SvgElement? target,
        SvgSceneNode? targetNode,
        SvgPointerInput input,
        ref bool animationFrameDirty,
        ref bool pointerRetestDirty)
    {
        var cursor = ResolveCursor(target);
        if (target is null)
        {
            return new SvgRoutedEventDispatchResult(
                DispatchShared(
                    SvgPointerEventType.Wheel,
                    null,
                    null,
                    null,
                    SvgPointerEventRoutePhase.Target,
                    input,
                    cursor),
                defaultPrevented: false);
        }

        if (DispatchTunnelEvent(
                SvgPointerEventType.Wheel,
                target,
                null,
                input,
                cursor))
        {
            return new SvgRoutedEventDispatchResult(handled: true, defaultPrevented: false);
        }

        var handled = false;
        var defaultPrevented = false;
        object? javaScriptEvent = null;
        foreach (var element in BuildRoute(target))
        {
            var animationTriggered = ReferenceEquals(element, target)
                ? svg?.RecordAnimationPointerEvent(element, targetNode, SvgPointerEventType.Wheel) == true
                : svg?.RecordAnimationPointerEvent(element, SvgPointerEventType.Wheel) == true;
            animationFrameDirty |= animationTriggered;
            pointerRetestDirty |= animationTriggered;
            var javaScriptResult = svg?.DispatchJavaScriptEvent(element, target, null, "mousescroll", "onmousescroll", input, ref javaScriptEvent);
            pointerRetestDirty |= javaScriptResult?.Mutated == true;
            var javaScriptDefaultPrevented = javaScriptResult?.DefaultPrevented == true;
            defaultPrevented |= javaScriptDefaultPrevented;
            handled |= javaScriptDefaultPrevented;
            DispatchSvgMouseScroll(element, input);
            var routePhase = ReferenceEquals(element, target)
                ? SvgPointerEventRoutePhase.Target
                : SvgPointerEventRoutePhase.Bubble;

            if (DispatchShared(SvgPointerEventType.Wheel, element, target, null, routePhase, input, cursor) ||
                javaScriptResult?.CancelBubble == true)
            {
                return new SvgRoutedEventDispatchResult(handled: true, defaultPrevented: defaultPrevented);
            }
        }

        return new SvgRoutedEventDispatchResult(handled, defaultPrevented);
    }

    private bool DispatchRoutedEvent(
        SKSvg? svg,
        SvgPointerEventType eventType,
        SvgElement? target,
        SvgSceneNode? targetNode,
        SvgElement? relatedElement,
        SvgPointerInput input,
        string svgEventName,
        ref bool animationFrameDirty,
        ref bool pointerRetestDirty)
    {
        return DispatchRoutedEventCore(svg, eventType, target, targetNode, relatedElement, input, svgEventName, ref animationFrameDirty, ref pointerRetestDirty).Handled;
    }

    private SvgRoutedEventDispatchResult DispatchRoutedEventCore(
        SKSvg? svg,
        SvgPointerEventType eventType,
        SvgElement? target,
        SvgSceneNode? targetNode,
        SvgElement? relatedElement,
        SvgPointerInput input,
        string svgEventName,
        ref bool animationFrameDirty,
        ref bool pointerRetestDirty)
    {
        var cursor = ResolveCursor(target);
        if (target is null)
        {
            return new SvgRoutedEventDispatchResult(
                DispatchShared(
                    eventType,
                    null,
                    null,
                    relatedElement,
                    SvgPointerEventRoutePhase.Target,
                    input,
                    cursor),
                defaultPrevented: false);
        }

        if (DispatchTunnelEvent(
                eventType,
                target,
                relatedElement,
                input,
                cursor))
        {
            return new SvgRoutedEventDispatchResult(handled: true, defaultPrevented: false);
        }

        var handled = false;
        var defaultPrevented = false;
        object? javaScriptEvent = null;
        foreach (var element in BuildRoute(target))
        {
            var animationTriggered = ReferenceEquals(element, target)
                ? svg?.RecordAnimationPointerEvent(element, targetNode, eventType) == true
                : svg?.RecordAnimationPointerEvent(element, eventType) == true;
            animationFrameDirty |= animationTriggered;
            pointerRetestDirty |= animationTriggered;
            var javaScriptResult = svg?.DispatchJavaScriptEvent(element, target, relatedElement, ToJavaScriptEventType(svgEventName), svgEventName, input, ref javaScriptEvent);
            pointerRetestDirty |= javaScriptResult?.Mutated == true;
            var javaScriptDefaultPrevented = javaScriptResult?.DefaultPrevented == true;
            defaultPrevented |= javaScriptDefaultPrevented;
            handled |= javaScriptDefaultPrevented;
            DispatchSvgMouseEvent(element, svgEventName, input);
            var routePhase = ReferenceEquals(element, target)
                ? SvgPointerEventRoutePhase.Target
                : SvgPointerEventRoutePhase.Bubble;

            if (DispatchShared(eventType, element, target, relatedElement, routePhase, input, cursor) ||
                javaScriptResult?.CancelBubble == true)
            {
                return new SvgRoutedEventDispatchResult(handled: true, defaultPrevented: defaultPrevented);
            }
        }

        return new SvgRoutedEventDispatchResult(handled, defaultPrevented);
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

    private static SvgElement? ResolveFocusableElement(SvgElement? target)
    {
        for (var current = target; current is not null; current = current.Parent)
        {
            if (IsFocusableElement(current))
            {
                return current;
            }
        }

        return null;
    }

    private static bool IsFocusableElement(SvgElement element)
    {
        if (TryGetBooleanAttribute(element, "focusable", out var focusable))
        {
            return focusable;
        }

        if (TryGetTabIndex(element, out var tabIndex))
        {
            return tabIndex >= 0;
        }

        return element is SvgAnchor anchor &&
               anchor.TryGetEffectiveHrefString(out var href) &&
               !string.IsNullOrWhiteSpace(href);
    }

    private static bool TryGetBooleanAttribute(SvgElement element, string attributeName, out bool value)
    {
        value = false;
        if (!element.TryGetAttribute(attributeName, out var rawValue) ||
            string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = rawValue.Trim();
        if (string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        return false;
    }

    private static bool TryGetTabIndex(SvgElement element, out int tabIndex)
    {
        tabIndex = 0;
        return element.TryGetAttribute("tabindex", out var rawValue) &&
               int.TryParse(rawValue?.Trim(), out tabIndex);
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

    private static string ToJavaScriptEventType(string svgEventName)
    {
        return svgEventName.StartsWith("on", StringComparison.Ordinal)
            ? svgEventName.Substring(2)
            : svgEventName;
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
