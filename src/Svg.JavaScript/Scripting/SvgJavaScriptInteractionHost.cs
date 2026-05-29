using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg;
using Svg.Skia;

namespace Svg.JavaScript;

public sealed class SvgJavaScriptInteractionEventResult
{
    internal SvgJavaScriptInteractionEventResult(
        string eventType,
        object? targetNode,
        object? relatedTargetNode,
        bool dispatched,
        bool allowedDefault,
        bool mutated,
        bool cancelBubble,
        bool defaultPrevented)
    {
        EventType = eventType;
        TargetNode = targetNode;
        RelatedTargetNode = relatedTargetNode;
        Dispatched = dispatched;
        AllowedDefault = allowedDefault;
        Mutated = mutated;
        CancelBubble = cancelBubble;
        DefaultPrevented = defaultPrevented;
    }

    public string EventType { get; }

    public object? TargetNode { get; }

    public object? RelatedTargetNode { get; }

    public bool Dispatched { get; }

    public bool AllowedDefault { get; }

    public bool Mutated { get; }

    public bool CancelBubble { get; }

    public bool DefaultPrevented { get; }
}

public sealed class SvgJavaScriptInteractionDispatchResult
{
    internal SvgJavaScriptInteractionDispatchResult(
        SvgJavaScriptElement? targetElement,
        SvgJavaScriptElement? focusedElement,
        IReadOnlyList<SvgJavaScriptInteractionEventResult> events,
        bool defaultActionActivated)
    {
        TargetElement = targetElement;
        FocusedElement = focusedElement;
        Events = events;
        DefaultActionActivated = defaultActionActivated;
    }

    public SvgJavaScriptElement? TargetElement { get; }

    public SvgJavaScriptElement? FocusedElement { get; }

    public IReadOnlyList<SvgJavaScriptInteractionEventResult> Events { get; }

    public bool DefaultActionActivated { get; }

    public bool Dispatched
    {
        get
        {
            for (var i = 0; i < Events.Count; i++)
            {
                if (Events[i].Dispatched)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool Mutated
    {
        get
        {
            for (var i = 0; i < Events.Count; i++)
            {
                if (Events[i].Mutated)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool DefaultPrevented
    {
        get
        {
            for (var i = 0; i < Events.Count; i++)
            {
                if (Events[i].DefaultPrevented)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool CancelBubble
    {
        get
        {
            for (var i = 0; i < Events.Count; i++)
            {
                if (Events[i].CancelBubble)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

public sealed class SvgJavaScriptInteractionHost
{
    private readonly SvgJavaScriptRuntime _runtime;
    private SvgElement? _hoveredElement;
    private SvgElement? _pressedElement;
    private SvgElement? _capturedElement;
    private SvgElement? _focusedElement;

    public SvgJavaScriptInteractionHost(SvgJavaScriptRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public SvgJavaScriptElement? HoveredElement => WrapElement(_hoveredElement);

    public SvgJavaScriptElement? PressedElement => WrapElement(_pressedElement);

    public SvgJavaScriptElement? CapturedElement => WrapElement(_capturedElement);

    public SvgJavaScriptElement? FocusedElement => WrapElement(_focusedElement);

    public SvgJavaScriptElement? HitTest(float x, float y)
    {
        return WrapElement(HitTestElement(new SKPoint(x, y)));
    }

    public SvgJavaScriptInteractionDispatchResult DispatchPointerMoved(SvgJavaScriptEventInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var events = new List<SvgJavaScriptInteractionEventResult>();
        var hitTarget = HitTestElement(input);
        var routeTarget = _capturedElement ?? hitTarget;

        if (_capturedElement is null)
        {
            DispatchHoverTransition(hitTarget, input, events);
        }

        DispatchPointerAndMouseEvents(routeTarget, null, input, events, "pointermove", "mousemove");
        return CreateResult(_hoveredElement ?? routeTarget, events);
    }

    public SvgJavaScriptInteractionDispatchResult DispatchPointerPressed(SvgJavaScriptEventInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var events = new List<SvgJavaScriptInteractionEventResult>();
        var target = HitTestElement(input);

        DispatchHoverTransition(target, input, events);
        _pressedElement = target;
        _capturedElement = target;
        DispatchPointerAndMouseEvents(target, null, input, events, "pointerdown", "mousedown");

        var defaultActionActivated = false;
        if (!HasDefaultPrevented(events))
        {
            defaultActionActivated = ApplyFocusDefaultAction(target, input, events);
        }

        return CreateResult(_hoveredElement ?? target, events, defaultActionActivated);
    }

    public SvgJavaScriptInteractionDispatchResult DispatchPointerReleased(SvgJavaScriptEventInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var events = new List<SvgJavaScriptInteractionEventResult>();
        var hitTarget = HitTestElement(input);
        var routeTarget = _capturedElement ?? hitTarget;
        var captureWasActive = _capturedElement is not null;

        if (_capturedElement is null)
        {
            DispatchHoverTransition(hitTarget, input, events);
        }

        DispatchPointerAndMouseEvents(routeTarget, null, input, events, "pointerup", "mouseup");

        if (routeTarget is not null && ReferenceEquals(hitTarget, _pressedElement))
        {
            events.Add(DispatchEvent("click", routeTarget, null, input));
        }

        _pressedElement = null;
        _capturedElement = null;

        if (captureWasActive)
        {
            DispatchHoverTransition(hitTarget, input, events);
        }

        return CreateResult(_hoveredElement ?? hitTarget, events);
    }

    public SvgJavaScriptInteractionDispatchResult DispatchPointerExited(SvgJavaScriptEventInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var events = new List<SvgJavaScriptInteractionEventResult>();
        if (_capturedElement is null && _hoveredElement is { } previous)
        {
            DispatchPointerAndMouseEvents(previous, null, input, events, "pointerout", "mouseout");
            _hoveredElement = null;
        }

        return CreateResult(null, events);
    }

    public SvgJavaScriptInteractionDispatchResult DispatchMouseEventAt(string eventType, SvgJavaScriptEventInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var target = HitTestElement(input);
        var events = new List<SvgJavaScriptInteractionEventResult>
        {
            DispatchEvent(eventType, target, null, input)
        };
        return CreateResult(target, events);
    }

    public SvgJavaScriptInteractionDispatchResult DispatchMouseEvent(
        string eventType,
        SvgJavaScriptElement targetElement,
        SvgJavaScriptElement? relatedTargetElement,
        SvgJavaScriptEventInput input)
    {
        if (targetElement is null)
        {
            throw new ArgumentNullException(nameof(targetElement));
        }

        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var events = new List<SvgJavaScriptInteractionEventResult>
        {
            DispatchEvent(eventType, targetElement.Element, relatedTargetElement?.Element, input)
        };
        return CreateResult(targetElement.Element, events);
    }

    public void Reset()
    {
        _hoveredElement = null;
        _pressedElement = null;
        _capturedElement = null;
        _focusedElement = null;
    }

    public bool Focus(SvgJavaScriptElement? element)
    {
        return Focus(element, null);
    }

    public bool Focus(SvgJavaScriptElement? element, SvgJavaScriptEventInput? input)
    {
        return SetFocusedElement(element?.Element, input, null);
    }

    public bool Blur(SvgJavaScriptElement? element)
    {
        if (element is null || !ReferenceEquals(_focusedElement, element.Element))
        {
            return false;
        }

        return SetFocusedElement(null, null, null);
    }

    private void DispatchHoverTransition(
        SvgElement? target,
        SvgJavaScriptEventInput input,
        List<SvgJavaScriptInteractionEventResult> events)
    {
        if (ReferenceEquals(target, _hoveredElement))
        {
            return;
        }

        var previous = _hoveredElement;
        if (previous is not null)
        {
            DispatchPointerAndMouseEvents(previous, target, input, events, "pointerout", "mouseout");
        }

        _hoveredElement = target;

        if (target is not null)
        {
            DispatchPointerAndMouseEvents(target, previous, input, events, "pointerover", "mouseover");
        }
    }

    private void DispatchPointerAndMouseEvents(
        SvgElement? target,
        SvgElement? relatedTarget,
        SvgJavaScriptEventInput input,
        List<SvgJavaScriptInteractionEventResult> events,
        string pointerEventType,
        string mouseEventType)
    {
        events.Add(DispatchEvent(pointerEventType, target, relatedTarget, input));
        events.Add(DispatchEvent(mouseEventType, target, relatedTarget, input));
    }

    private bool ApplyFocusDefaultAction(
        SvgElement? target,
        SvgJavaScriptEventInput input,
        List<SvgJavaScriptInteractionEventResult> events)
    {
        return SetFocusedElement(ResolveFocusableElement(target), input, events);
    }

    private bool SetFocusedElement(
        SvgElement? element,
        SvgJavaScriptEventInput? input,
        List<SvgJavaScriptInteractionEventResult>? events)
    {
        if (ReferenceEquals(_focusedElement, element))
        {
            return false;
        }

        if (events is null)
        {
            var changed = element is null
                ? _focusedElement is not null && _runtime.BlurElement(_focusedElement)
                : _runtime.FocusElement(element);
            if (changed)
            {
                _focusedElement = element;
            }

            return changed;
        }

        var previous = _focusedElement;
        if (previous is not null)
        {
            DispatchFocusEvent(previous, element, input, events, "blur");
            DispatchFocusEvent(previous, element, input, events, "focusout");
        }

        _focusedElement = element;
        _runtime.SetFocusedElementState(element);

        if (element is not null)
        {
            DispatchFocusEvent(element, previous, input, events, "focus");
            DispatchFocusEvent(element, previous, input, events, "focusin");
        }

        return true;
    }

    private void DispatchFocusEvent(
        SvgElement target,
        SvgElement? relatedTarget,
        SvgJavaScriptEventInput? input,
        List<SvgJavaScriptInteractionEventResult>? events,
        string eventType)
    {
        if (events is null)
        {
            return;
        }

        events.Add(DispatchEvent(eventType, target, relatedTarget, input ?? CreateDefaultFocusInput()));
    }

    private SvgJavaScriptInteractionEventResult DispatchEvent(
        string eventType,
        SvgElement? targetElement,
        SvgElement? relatedTargetElement,
        SvgJavaScriptEventInput input)
    {
        var normalizedEventType = NormalizeEventType(eventType);
        if (normalizedEventType.Length == 0 || targetElement is null)
        {
            return new SvgJavaScriptInteractionEventResult(
                normalizedEventType,
                targetNode: null,
                relatedTargetNode: null,
                dispatched: false,
                allowedDefault: true,
                mutated: false,
                cancelBubble: false,
                defaultPrevented: false);
        }

        var hasUseInstanceTarget = TryResolveUseInstanceEventTarget(
            targetElement,
            input,
            out var targetNode,
            out var correspondingElement);
        var relatedTargetNode = relatedTargetElement is null ? null : _runtime.GetElement(relatedTargetElement);
        var eventFacade = _runtime.CreateEvent(normalizedEventType, targetNode, relatedTargetNode, input);
        var before = _runtime.MutationVersion;
        var eventPath = BuildRoute(targetElement);
        if (hasUseInstanceTarget && correspondingElement is not null)
        {
            eventPath.Insert(0, correspondingElement);
        }

        for (var index = eventPath.Count - 1; index > 0; index--)
        {
            var current = _runtime.GetElement(eventPath[index]);
            eventFacade.SetCurrentTarget(current);
            var listenerResult = current.DispatchRegisteredEventListeners(normalizedEventType, eventFacade, useCapture: true);
            if (listenerResult.CancelBubble || eventFacade.cancelBubble)
            {
                return CreateEventResult(normalizedEventType, targetNode, relatedTargetNode, eventFacade, before);
            }
        }

        if (eventPath.Count > 0)
        {
            var targetFacade = _runtime.GetElement(eventPath[0]);
            eventFacade.SetCurrentTarget(targetFacade);
            _ = targetFacade.DispatchRegisteredEventListeners(normalizedEventType, eventFacade, useCapture: true);
        }

        for (var index = 0; index < eventPath.Count; index++)
        {
            var current = eventPath[index];
            var result = _runtime.ExecuteEventHandlerAndListeners(
                current,
                eventFacade,
                normalizedEventType,
                "on" + normalizedEventType);

            if (result.CancelBubble || eventFacade.cancelBubble || !eventFacade.bubbles)
            {
                break;
            }
        }

        return CreateEventResult(normalizedEventType, targetNode, relatedTargetNode, eventFacade, before);
    }

    private SvgJavaScriptInteractionEventResult CreateEventResult(
        string eventType,
        object targetNode,
        object? relatedTargetNode,
        SvgJavaScriptEvent eventFacade,
        int beforeMutationVersion)
    {
        return new SvgJavaScriptInteractionEventResult(
            eventType,
            targetNode,
            relatedTargetNode,
            dispatched: true,
            allowedDefault: !eventFacade.defaultPrevented,
            mutated: _runtime.MutationVersion != beforeMutationVersion,
            cancelBubble: eventFacade.cancelBubble,
            defaultPrevented: eventFacade.defaultPrevented);
    }

    private bool TryResolveUseInstanceEventTarget(
        SvgElement targetElement,
        SvgJavaScriptEventInput input,
        out object targetNode,
        out SvgElement? correspondingElement)
    {
        if (targetElement is SvgUse use &&
            _runtime.GetSceneDocument()?.HitTestTopmostNode(new SKPoint(input.X, input.Y)) is { } hitNode)
        {
            var hitTargetElement = hitNode.HitTestTargetElement;
            var hitElement = hitNode.Element as SvgElement;

            if (ReferenceEquals(hitTargetElement, targetElement) &&
                hitElement is not null &&
                !ReferenceEquals(hitElement, targetElement))
            {
                var instance = _runtime.FindUseInstance(use, hitElement);
                if (instance is not null)
                {
                    correspondingElement = hitElement;
                    targetNode = instance;
                    return true;
                }
            }
        }

        correspondingElement = null;
        targetNode = _runtime.GetElement(targetElement);
        return false;
    }

    private SvgElement? HitTestElement(SvgJavaScriptEventInput input)
    {
        return HitTestElement(new SKPoint(input.X, input.Y));
    }

    private SvgElement? HitTestElement(SKPoint point)
    {
        return _runtime.GetSceneDocument()?.HitTestTopmostNode(point)?.HitTestTargetElement;
    }

    private SvgJavaScriptInteractionDispatchResult CreateResult(
        SvgElement? targetElement,
        List<SvgJavaScriptInteractionEventResult> events,
        bool defaultActionActivated = false)
    {
        return new SvgJavaScriptInteractionDispatchResult(
            WrapElement(targetElement),
            WrapElement(_focusedElement),
            events.AsReadOnly(),
            defaultActionActivated);
    }

    private SvgJavaScriptElement? WrapElement(SvgElement? element)
    {
        return element is null ? null : _runtime.GetElement(element);
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

    private static string NormalizeEventType(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return string.Empty;
        }

        var normalized = eventType!.Trim();
        if (normalized.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(2);
        }

        return normalized.ToLowerInvariant();
    }

    private static bool HasDefaultPrevented(List<SvgJavaScriptInteractionEventResult> events)
    {
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].DefaultPrevented)
            {
                return true;
            }
        }

        return false;
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

    private static SvgJavaScriptEventInput CreateDefaultFocusInput()
    {
        return new SvgJavaScriptEventInput(
            0f,
            0f,
            SvgJavaScriptMouseButton.None,
            0,
            0,
            altKey: false,
            shiftKey: false,
            ctrlKey: false);
    }
}
