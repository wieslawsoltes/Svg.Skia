namespace Svg.JavaScript;

public sealed class SvgJavaScriptEvent
{
    public SvgJavaScriptEvent()
    {
    }

    internal SvgJavaScriptEvent(
        string type,
        object target,
        object currentTarget,
        object? relatedTarget,
        SvgJavaScriptEventInput? input)
    {
        BeginDispatch(type, target, relatedTarget);
        SetCurrentTarget(currentTarget);
        clientX = input?.X ?? 0f;
        clientY = input?.Y ?? 0f;
        x = clientX;
        y = clientY;
        screenX = clientX;
        screenY = clientY;
        button = input is null ? 0 : ToJavaScriptButton(input.Button);
        detail = input?.ClickCount ?? 0;
        wheelDelta = input?.WheelDelta ?? 0;
        altKey = input?.AltKey ?? false;
        shiftKey = input?.ShiftKey ?? false;
        ctrlKey = input?.CtrlKey ?? false;
        bubbles = true;
        cancelable = true;
    }

    public string type { get; private set; } = string.Empty;

    public object? target { get; private set; }

    public object? currentTarget { get; private set; }

    public object? relatedTarget { get; private set; }

    public object? view { get; private set; }

    public bool bubbles { get; private set; }

    public bool cancelable { get; private set; }

    public float screenX { get; private set; }

    public float screenY { get; private set; }

    public float clientX { get; private set; }

    public float clientY { get; private set; }

    public float x { get; private set; }

    public float y { get; private set; }

    public int button { get; private set; }

    public int detail { get; private set; }

    public int wheelDelta { get; private set; }

    public bool altKey { get; private set; }

    public bool shiftKey { get; private set; }

    public bool ctrlKey { get; private set; }

    public bool metaKey { get; private set; }

    public bool cancelBubble { get; set; }

    public bool defaultPrevented { get; private set; }

    internal void BeginDispatch(string eventType, object targetNode, object? relatedTargetNode)
    {
        type = eventType ?? string.Empty;
        target = targetNode;
        currentTarget = targetNode;
        relatedTarget = relatedTargetNode;
        cancelBubble = false;
        defaultPrevented = false;
    }

    internal void SetCurrentTarget(object? currentTargetNode)
    {
        currentTarget = currentTargetNode;
    }

    public void initEvent(string eventType, bool canBubble, bool isCancelable)
    {
        type = eventType ?? string.Empty;
        bubbles = canBubble;
        cancelable = isCancelable;
        target = null;
        currentTarget = null;
        relatedTarget = null;
        cancelBubble = false;
        defaultPrevented = false;
    }

    public void initMouseEvent(
        string eventType,
        bool canBubble,
        bool isCancelable,
        object? abstractView,
        int detailValue,
        float screenXValue,
        float screenYValue,
        float clientXValue,
        float clientYValue,
        bool ctrlKeyValue,
        bool altKeyValue,
        bool shiftKeyValue,
        bool metaKeyValue,
        int buttonValue,
        object? relatedTargetValue)
    {
        initEvent(eventType, canBubble, isCancelable);
        view = abstractView;
        detail = detailValue;
        screenX = screenXValue;
        screenY = screenYValue;
        clientX = clientXValue;
        clientY = clientYValue;
        x = clientXValue;
        y = clientYValue;
        ctrlKey = ctrlKeyValue;
        altKey = altKeyValue;
        shiftKey = shiftKeyValue;
        metaKey = metaKeyValue;
        button = buttonValue;
        relatedTarget = relatedTargetValue;
    }

    public void stopPropagation()
    {
        cancelBubble = true;
    }

    public void preventDefault()
    {
        if (cancelable)
        {
            defaultPrevented = true;
        }
    }

    private static int ToJavaScriptButton(SvgJavaScriptMouseButton button)
    {
        return button switch
        {
            SvgJavaScriptMouseButton.Left => 0,
            SvgJavaScriptMouseButton.Middle => 1,
            SvgJavaScriptMouseButton.Right => 2,
            SvgJavaScriptMouseButton.XButton1 => 3,
            SvgJavaScriptMouseButton.XButton2 => 4,
            _ => 0
        };
    }
}
