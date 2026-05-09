namespace Svg.JavaScript;

public enum SvgJavaScriptMouseButton
{
    None,
    Left,
    Middle,
    Right,
    XButton1,
    XButton2
}

public sealed class SvgJavaScriptEventInput
{
    public SvgJavaScriptEventInput(
        float x,
        float y,
        SvgJavaScriptMouseButton button,
        int clickCount,
        int wheelDelta,
        bool altKey,
        bool shiftKey,
        bool ctrlKey)
    {
        X = x;
        Y = y;
        Button = button;
        ClickCount = clickCount;
        WheelDelta = wheelDelta;
        AltKey = altKey;
        ShiftKey = shiftKey;
        CtrlKey = ctrlKey;
    }

    public float X { get; }

    public float Y { get; }

    public SvgJavaScriptMouseButton Button { get; }

    public int ClickCount { get; }

    public int WheelDelta { get; }

    public bool AltKey { get; }

    public bool ShiftKey { get; }

    public bool CtrlKey { get; }
}
