using System;
using ShimSkiaSharp;
using Svg;
using Svg.JavaScript;

namespace Svg.Skia;

public static class SKSvgJavaScriptRuntime
{
    public static ISKSvgJavaScriptRuntimeFactory Factory { get; } = SvgJavaScriptRuntimeFactory.Instance;

    public static void Register()
    {
        SKSvgSettings.DefaultJavaScriptRuntimeFactory = Factory;
    }

    private sealed class SvgJavaScriptRuntimeFactory : ISKSvgJavaScriptRuntimeFactory
    {
        public static readonly SvgJavaScriptRuntimeFactory Instance = new();

        public ISKSvgJavaScriptRuntime Create(SvgDocument document, SKSvgJavaScriptRuntimeSettings settings)
        {
            var runtimeSettings = new SvgJavaScriptSettings
            {
                EnableExternalJavaScript = settings.EnableExternalJavaScript,
                TimeoutMilliseconds = settings.TimeoutMilliseconds,
                MaxStatements = settings.MaxStatements,
                ThrowOnError = settings.ThrowOnError
            };

            return new SvgJavaScriptRuntimeAdapter(new SvgJavaScriptRuntime(document, runtimeSettings));
        }
    }

    private sealed class SvgJavaScriptRuntimeAdapter : ISKSvgJavaScriptRuntime
    {
        private readonly SvgJavaScriptRuntime _runtime;

        public SvgJavaScriptRuntimeAdapter(SvgJavaScriptRuntime runtime)
        {
            _runtime = runtime;
        }

        public int MutationVersion => _runtime.MutationVersion;

        public object Runtime => _runtime;

        public void SetAnimationHost(ISKSvgJavaScriptAnimationHost? animationHost)
        {
            _runtime.AnimationHost = animationHost is null ? null : new SvgJavaScriptAnimationHostAdapter(animationHost);
        }

        public void SetTextContentHost(ISKSvgJavaScriptTextContentHost? textContentHost)
        {
            _runtime.TextContentHost = textContentHost is null ? null : new SvgJavaScriptTextContentHostAdapter(textContentHost);
        }

        public void ExecuteDocumentScripts(bool dispatchLoadEvent)
        {
            _runtime.ExecuteDocumentScripts(dispatchLoadEvent);
        }

        public object GetElement(SvgElement element)
        {
            return _runtime.GetElement(element);
        }

        public object? FindUseInstance(SvgUse use, SvgElement correspondingElement)
        {
            return _runtime.FindUseInstance(use, correspondingElement);
        }

        public object CreateEvent(
            string eventType,
            object targetNode,
            object? relatedTargetNode,
            SKSvgJavaScriptEventInput? input)
        {
            return _runtime.CreateEvent(eventType, targetNode, relatedTargetNode, ConvertInput(input));
        }

        public SKSvgJavaScriptEventResult ExecuteEventHandlerAndListeners(
            SvgElement element,
            object eventFacade,
            string eventType,
            string attributeName)
        {
            if (eventFacade is not SvgJavaScriptEvent javaScriptEvent)
            {
                throw new ArgumentException("The JavaScript event facade was not created by the registered Svg.JavaScript runtime.", nameof(eventFacade));
            }

            return ConvertResult(_runtime.ExecuteEventHandlerAndListeners(element, javaScriptEvent, eventType, attributeName));
        }

        public SKSvgJavaScriptEventResult ExecuteEventHandlerAndListeners(
            SvgElement element,
            object targetNode,
            object? relatedTargetNode,
            string eventType,
            string attributeName,
            SKSvgJavaScriptEventInput? input)
        {
            return ConvertResult(_runtime.ExecuteEventHandlerAndListeners(
                element,
                targetNode,
                relatedTargetNode,
                eventType,
                attributeName,
                ConvertInput(input)));
        }

        private static SvgJavaScriptEventInput? ConvertInput(SKSvgJavaScriptEventInput? input)
        {
            return input is null
                ? null
                : new SvgJavaScriptEventInput(
                    input.X,
                    input.Y,
                    ConvertButton(input.Button),
                    input.ClickCount,
                    input.WheelDelta,
                    input.AltKey,
                    input.ShiftKey,
                    input.CtrlKey);
        }

        private static SvgJavaScriptMouseButton ConvertButton(SKSvgJavaScriptMouseButton button)
        {
            return button switch
            {
                SKSvgJavaScriptMouseButton.Left => SvgJavaScriptMouseButton.Left,
                SKSvgJavaScriptMouseButton.Middle => SvgJavaScriptMouseButton.Middle,
                SKSvgJavaScriptMouseButton.Right => SvgJavaScriptMouseButton.Right,
                SKSvgJavaScriptMouseButton.XButton1 => SvgJavaScriptMouseButton.XButton1,
                SKSvgJavaScriptMouseButton.XButton2 => SvgJavaScriptMouseButton.XButton2,
                _ => SvgJavaScriptMouseButton.None
            };
        }

        private static SKSvgJavaScriptEventResult ConvertResult(SvgJavaScriptEventResult result)
        {
            return new SKSvgJavaScriptEventResult(
                result.Executed,
                result.Mutated,
                result.CancelBubble,
                result.DefaultPrevented);
        }
    }

    private sealed class SvgJavaScriptAnimationHostAdapter : ISvgJavaScriptAnimationHost
    {
        private readonly ISKSvgJavaScriptAnimationHost _host;

        public SvgJavaScriptAnimationHostAdapter(ISKSvgJavaScriptAnimationHost host)
        {
            _host = host;
        }

        public TimeSpan CurrentTime => _host.CurrentTime;

        public void Seek(TimeSpan time)
        {
            _host.Seek(time);
        }

        public bool BeginElement(SvgAnimationElement animation, TimeSpan offset)
        {
            return _host.BeginElement(animation, offset);
        }

        public bool EndElement(SvgAnimationElement animation, TimeSpan offset)
        {
            return _host.EndElement(animation, offset);
        }

        public bool TryGetStartTime(SvgAnimationElement animation, out TimeSpan startTime)
        {
            return _host.TryGetStartTime(animation, out startTime);
        }

        public bool TryGetBaseAttributeValue(SvgElement element, string attributeName, out string value)
        {
            return _host.TryGetBaseAttributeValue(element, attributeName, out value);
        }
    }

    private sealed class SvgJavaScriptTextContentHostAdapter : ISvgJavaScriptTextContentHost
    {
        private readonly ISKSvgJavaScriptTextContentHost _host;

        public SvgJavaScriptTextContentHostAdapter(ISKSvgJavaScriptTextContentHost host)
        {
            _host = host;
        }

        public double GetComputedTextLength(SvgTextBase textContentElement)
        {
            return _host.GetComputedTextLength(textContentElement);
        }

        public int GetNumberOfChars(SvgTextBase textContentElement)
        {
            return _host.GetNumberOfChars(textContentElement);
        }

        public double GetSubStringLength(SvgTextBase textContentElement, int charnum, int nchars)
        {
            return _host.GetSubStringLength(textContentElement, charnum, nchars);
        }

        public SvgJavaScriptPoint GetStartPositionOfChar(SvgTextBase textContentElement, int charnum)
        {
            return ConvertPoint(_host.GetStartPositionOfChar(textContentElement, charnum));
        }

        public SvgJavaScriptPoint GetEndPositionOfChar(SvgTextBase textContentElement, int charnum)
        {
            return ConvertPoint(_host.GetEndPositionOfChar(textContentElement, charnum));
        }

        public SvgJavaScriptRect GetExtentOfChar(SvgTextBase textContentElement, int charnum)
        {
            var rect = _host.GetExtentOfChar(textContentElement, charnum);
            return new SvgJavaScriptRect(rect.Left, rect.Top, rect.Width, rect.Height);
        }

        public double GetRotationOfChar(SvgTextBase textContentElement, int charnum)
        {
            return _host.GetRotationOfChar(textContentElement, charnum);
        }

        public int GetCharNumAtPosition(SvgTextBase textContentElement, SvgJavaScriptPoint point)
        {
            return _host.GetCharNumAtPosition(textContentElement, new SKPoint(point.x, point.y));
        }

        public void SelectSubString(SvgTextBase textContentElement, int charnum, int nchars)
        {
            _host.SelectSubString(textContentElement, charnum, nchars);
        }

        private static SvgJavaScriptPoint ConvertPoint(SKPoint point)
        {
            return new SvgJavaScriptPoint(point.X, point.Y);
        }
    }
}
