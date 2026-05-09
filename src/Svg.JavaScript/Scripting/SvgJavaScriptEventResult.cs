namespace Svg.JavaScript;

public sealed class SvgJavaScriptEventResult
{
    public static readonly SvgJavaScriptEventResult NotExecuted = new(
        executed: false,
        mutated: false,
        cancelBubble: false,
        defaultPrevented: false);

    public SvgJavaScriptEventResult(bool executed, bool mutated, bool cancelBubble, bool defaultPrevented)
    {
        Executed = executed;
        Mutated = mutated;
        CancelBubble = cancelBubble;
        DefaultPrevented = defaultPrevented;
    }

    public bool Executed { get; }

    public bool Mutated { get; }

    public bool CancelBubble { get; }

    public bool DefaultPrevented { get; }
}
