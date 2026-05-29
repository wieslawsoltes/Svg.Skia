using System;
using ShimSkiaSharp;
using Svg;

namespace Svg.Skia;

public sealed class SKSvgJavaScriptRuntimeSettings
{
    public bool EnableExternalJavaScript { get; set; }

    public int TimeoutMilliseconds { get; set; }

    public int MaxStatements { get; set; }

    public bool ThrowOnError { get; set; }
}

public interface ISKSvgJavaScriptRuntimeFactory
{
    ISKSvgJavaScriptRuntime Create(SvgDocument document, SKSvgJavaScriptRuntimeSettings settings);
}

public interface ISKSvgJavaScriptRuntime
{
    int MutationVersion { get; }

    object Runtime { get; }

    void SetAnimationHost(ISKSvgJavaScriptAnimationHost? animationHost);

    void SetTextContentHost(ISKSvgJavaScriptTextContentHost? textContentHost);

    void ExecuteDocumentScripts(bool dispatchLoadEvent);

    object GetElement(SvgElement element);

    object? FindUseInstance(SvgUse use, SvgElement correspondingElement);

    object CreateEvent(
        string eventType,
        object targetNode,
        object? relatedTargetNode,
        SKSvgJavaScriptEventInput? input);

    SKSvgJavaScriptEventResult ExecuteEventHandlerAndListeners(
        SvgElement element,
        object eventFacade,
        string eventType,
        string attributeName);

    SKSvgJavaScriptEventResult ExecuteEventHandlerAndListeners(
        SvgElement element,
        object targetNode,
        object? relatedTargetNode,
        string eventType,
        string attributeName,
        SKSvgJavaScriptEventInput? input);
}

public interface ISKSvgJavaScriptViewerRuntime
{
    void SetViewerHost(ISKSvgJavaScriptViewerHost? viewerHost);
}

public interface ISKSvgJavaScriptAnimationHost
{
    TimeSpan CurrentTime { get; }

    void Seek(TimeSpan time);

    bool BeginElement(SvgAnimationElement animation, TimeSpan offset);

    bool EndElement(SvgAnimationElement animation, TimeSpan offset);

    bool TryGetStartTime(SvgAnimationElement animation, out TimeSpan startTime);

    bool TryGetBaseAttributeValue(SvgElement element, string attributeName, out string value);
}

public interface ISKSvgJavaScriptTextContentHost
{
    double GetComputedTextLength(SvgTextBase textContentElement);

    int GetNumberOfChars(SvgTextBase textContentElement);

    double GetSubStringLength(SvgTextBase textContentElement, int charnum, int nchars);

    SKPoint GetStartPositionOfChar(SvgTextBase textContentElement, int charnum);

    SKPoint GetEndPositionOfChar(SvgTextBase textContentElement, int charnum);

    SKRect GetExtentOfChar(SvgTextBase textContentElement, int charnum);

    double GetRotationOfChar(SvgTextBase textContentElement, int charnum);

    int GetCharNumAtPosition(SvgTextBase textContentElement, SKPoint point);

    void SelectSubString(SvgTextBase textContentElement, int charnum, int nchars);
}

public interface ISKSvgJavaScriptTextSelectionHost
{
    bool TryBeginTextSelection(SvgTextBase textContentElement, int anchorCharnum);

    bool TryExtendTextSelection(SvgTextBase textContentElement, int focusCharnum);

    bool TrySelectTextRange(SvgTextBase textContentElement, int anchorCharnum, int focusCharnum);

    void ClearTextSelection();

    bool TryGetTextSelection(SvgTextBase? textContentElement, out SKSvg.SvgTextSelectionRange selection);
}

public interface ISKSvgJavaScriptViewerHost
{
    double CurrentScale { get; set; }

    float CurrentTranslateX { get; set; }

    float CurrentTranslateY { get; set; }
}

public enum SKSvgJavaScriptMouseButton
{
    None,
    Left,
    Middle,
    Right,
    XButton1,
    XButton2
}

public sealed class SKSvgJavaScriptEventInput
{
    public SKSvgJavaScriptEventInput(
        float x,
        float y,
        SKSvgJavaScriptMouseButton button,
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

    public SKSvgJavaScriptMouseButton Button { get; }

    public int ClickCount { get; }

    public int WheelDelta { get; }

    public bool AltKey { get; }

    public bool ShiftKey { get; }

    public bool CtrlKey { get; }
}

public sealed class SKSvgJavaScriptEventResult
{
    public static readonly SKSvgJavaScriptEventResult NotExecuted = new(
        executed: false,
        mutated: false,
        cancelBubble: false,
        defaultPrevented: false);

    public SKSvgJavaScriptEventResult(bool executed, bool mutated, bool cancelBubble, bool defaultPrevented)
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
