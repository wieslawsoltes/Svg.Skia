namespace Svg.JavaScript;

public sealed class SvgJavaScriptEvent
{
    internal SvgJavaScriptEvent(
        string type,
        SvgJavaScriptElement target,
        SvgJavaScriptElement currentTarget,
        SvgJavaScriptElement? relatedTarget,
        SvgJavaScriptEventInput? input)
    {
        this.type = type;
        this.target = target;
        this.currentTarget = currentTarget;
        this.relatedTarget = relatedTarget;
        clientX = input?.X ?? 0f;
        clientY = input?.Y ?? 0f;
        x = clientX;
        y = clientY;
        button = input is null ? 0 : ToJavaScriptButton(input.Button);
        detail = input?.ClickCount ?? 0;
        wheelDelta = input?.WheelDelta ?? 0;
        altKey = input?.AltKey ?? false;
        shiftKey = input?.ShiftKey ?? false;
        ctrlKey = input?.CtrlKey ?? false;
    }

    public string type { get; }

    public SvgJavaScriptElement target { get; }

    public SvgJavaScriptElement currentTarget { get; }

    public SvgJavaScriptElement? relatedTarget { get; }

    public float clientX { get; }

    public float clientY { get; }

    public float x { get; }

    public float y { get; }

    public int button { get; }

    public int detail { get; }

    public int wheelDelta { get; }

    public bool altKey { get; }

    public bool shiftKey { get; }

    public bool ctrlKey { get; }

    public bool cancelBubble { get; set; }

    public bool defaultPrevented { get; private set; }

    public void stopPropagation()
    {
        cancelBubble = true;
    }

    public void preventDefault()
    {
        defaultPrevented = true;
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
