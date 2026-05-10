using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Svg;
using Svg.Model;
using Svg.Skia;

namespace Svg.JavaScript;

public sealed partial class SvgJavaScriptRuntime
{
    private static readonly HashSet<string> s_supportedScriptTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/ecmascript",
        "application/javascript",
        "application/x-ecmascript",
        "application/x-javascript",
        "text/ecmascript",
        "text/javascript",
        "text/jscript",
        "text/x-ecmascript",
        "text/x-javascript"
    };

    private readonly SvgDocument _document;
    private readonly SvgJavaScriptSettings _settings;
    private readonly Engine _engine;
    private readonly SvgJavaScriptDocument _documentFacade;
    private readonly SvgJavaScriptWindow _windowFacade;
    private readonly SvgJavaScriptAssetLoader _assetLoader;
    private readonly Dictionary<SvgUse, SvgJavaScriptElementInstance?> _useInstanceRoots = new();
    private readonly List<SvgJavaScriptTimeoutEntry> _pendingTimeouts = new();
    private int _mutationVersion;
    private SvgSceneDocument? _sceneDocument;
    private int _sceneDocumentMutationVersion = -1;
    private int _nextTimeoutId;
    private long _nextTimeoutSequence;
    private ISvgJavaScriptAnimationHost? _animationHost;
    private ISvgJavaScriptTextContentHost? _textContentHost;
    private TimeSpan? _pendingAnimationTime;
    private double _virtualTimerMilliseconds;
    private bool _drainingTimeouts;

    public SvgJavaScriptRuntime(SvgDocument document, SvgJavaScriptSettings settings)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _assetLoader = new SvgJavaScriptAssetLoader();
        _documentFacade = new SvgJavaScriptDocument(this, document);
        _windowFacade = _documentFacade.defaultView;
        _engine = CreateEngine(settings);
        _engine.SetValue("document", _documentFacade);
        _engine.SetValue("window", _windowFacade);
        _engine.SetValue("self", _windowFacade);
        _engine.SetValue("console", new SvgJavaScriptConsole());
        _engine.SetValue("evt", JsValue.Null);
        _engine.SetValue("event", JsValue.Null);
        _engine.SetValue("Node", SvgJavaScriptDomConstants.CreateNodeObject(this));
        _engine.SetValue("DOMException", SvgJavaScriptDomConstants.CreateDomExceptionObject(this));
        _engine.SetValue("SVGTransform", SvgJavaScriptDomConstants.CreateSvgTransformObject(this));
        _engine.SetValue("SVGLength", SvgJavaScriptDomConstants.CreateSvgLengthObject(this));
        _engine.SetValue("SVGAngle", SvgJavaScriptDomConstants.CreateSvgAngleObject(this));
        _engine.SetValue("SVGPreserveAspectRatio", SvgJavaScriptDomConstants.CreateSvgPreserveAspectRatioObject(this));
        _engine.SetValue("alert", new Action<object?>(_windowFacade.alert));
        _engine.SetValue("setTimeout", new Func<object?, object?, int>(_windowFacade.setTimeout));
        _engine.SetValue("clearTimeout", new Action<int>(_windowFacade.clearTimeout));
    }

    public int MutationVersion => _mutationVersion;

    internal SvgJavaScriptDocument DocumentFacade => _documentFacade;

    public ISvgJavaScriptAnimationHost? AnimationHost
    {
        get => _animationHost;
        set
        {
            _animationHost = value;
            if (_animationHost is not null && _pendingAnimationTime is { } pendingTime)
            {
                _animationHost.Seek(pendingTime);
                _pendingAnimationTime = null;
            }
        }
    }

    public ISvgJavaScriptTextContentHost? TextContentHost
    {
        get => _textContentHost;
        set => _textContentHost = value;
    }

    public SvgJavaScriptElement GetElement(SvgElement element)
    {
        return _documentFacade.GetOrCreateElement(element);
    }

    internal void MarkMutation()
    {
        _mutationVersion++;
        _sceneDocument = null;
        _sceneDocumentMutationVersion = -1;
        _useInstanceRoots.Clear();
    }

    public void ExecuteDocumentScripts(bool dispatchLoadEvent = true)
    {
        foreach (var script in _document.Descendants().OfType<SvgScript>().ToArray())
        {
            ExecuteScriptElement(script);
        }

        if (dispatchLoadEvent)
        {
            ExecuteEventHandlerAndListeners(
                _document,
                GetElement(_document),
                relatedTargetNode: null,
                "load",
                "onload",
                input: null);
        }

        DrainPendingTimeouts();
    }

    public SvgJavaScriptEventResult ExecuteEventHandler(
        SvgElement element,
        SvgElement targetElement,
        SvgElement? relatedElement,
        string eventType,
        string attributeName,
        SvgJavaScriptEventInput? input)
    {
        return ExecuteEventHandler(
            element,
            GetElement(targetElement),
            relatedElement is null ? null : GetElement(relatedElement),
            eventType,
            attributeName,
            input);
    }

    internal SvgJavaScriptEventResult ExecuteEventHandler(
        SvgElement element,
        object targetNode,
        object? relatedTargetNode,
        string eventType,
        string attributeName,
        SvgJavaScriptEventInput? input)
    {
        var eventFacade = CreateEvent(eventType, targetNode, relatedTargetNode, input);
        eventFacade.SetCurrentTarget(GetElement(element));
        return ExecuteEventHandlerCore(element, attributeName, eventFacade);
    }

    internal SvgJavaScriptEvent CreateEvent(
        string eventType,
        object targetNode,
        object? relatedTargetNode,
        SvgJavaScriptEventInput? input)
    {
        return new SvgJavaScriptEvent(
            eventType,
            targetNode,
            targetNode,
            relatedTargetNode,
            input);
    }

    internal SvgJavaScriptEventResult ExecuteEventHandlerAndListeners(
        SvgElement element,
        object targetNode,
        object? relatedTargetNode,
        string eventType,
        string attributeName,
        SvgJavaScriptEventInput? input)
    {
        var eventFacade = CreateEvent(eventType, targetNode, relatedTargetNode, input);
        return ExecuteEventHandlerAndListeners(element, eventFacade, eventType, attributeName);
    }

    internal SvgJavaScriptEventResult ExecuteEventHandlerAndListeners(
        SvgElement element,
        SvgJavaScriptEvent eventFacade,
        string eventType,
        string attributeName)
    {
        eventFacade.SetCurrentTarget(GetElement(element));
        var handlerResult = ExecuteEventHandlerCore(element, attributeName, eventFacade);
        var listenerResult = GetElement(element).DispatchRegisteredEventListeners(eventType, eventFacade, useCapture: false);
        return handlerResult.Executed || listenerResult.Executed
            ? new SvgJavaScriptEventResult(
                executed: true,
                mutated: handlerResult.Mutated || listenerResult.Mutated,
                cancelBubble: handlerResult.CancelBubble || listenerResult.CancelBubble,
                defaultPrevented: eventFacade.defaultPrevented)
            : SvgJavaScriptEventResult.NotExecuted;
    }

    internal SvgJavaScriptElementInstance? GetUseInstanceRoot(SvgUse use)
    {
        if (!_useInstanceRoots.TryGetValue(use, out var root))
        {
            root = SvgJavaScriptElementInstance.CreateTree(this, _documentFacade, use);
            _useInstanceRoots[use] = root;
        }

        return root;
    }

    internal SvgJavaScriptElementInstance? FindUseInstance(SvgUse use, SvgElement correspondingElement)
    {
        return GetUseInstanceRoot(use)?.FindFirst(correspondingElement);
    }

    internal bool DispatchEvent(SvgJavaScriptElement target, SvgJavaScriptEvent evt)
    {
        if (evt is null)
        {
            throw new ArgumentNullException(nameof(evt));
        }

        return DispatchEventCore(
            evt,
            target,
            static current => current.parentElement,
            static current => current.Element,
            static current => current);
    }

    internal bool DispatchEvent(SvgJavaScriptElementInstance target, SvgJavaScriptEvent evt)
    {
        if (evt is null)
        {
            throw new ArgumentNullException(nameof(evt));
        }

        return DispatchEventCore(
            evt,
            target,
            static current => current.parentNode,
            static current => current.CorrespondingEventElementRaw,
            static current => (object)current);
    }

    private bool DispatchEventCore<TTarget>(
        SvgJavaScriptEvent evt,
        TTarget target,
        Func<TTarget, TTarget?> getParent,
        Func<TTarget, SvgElement> getHandlerElement,
        Func<TTarget, object> getFacade)
        where TTarget : class
    {
        var eventType = NormalizeEventType(evt.type);
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return true;
        }

        var eventPath = new List<TTarget>();
        for (var current = target; current is not null; current = getParent(current))
        {
            eventPath.Add(current);
        }

        evt.BeginDispatch(eventType, getFacade(target), evt.relatedTarget);
        for (var index = eventPath.Count - 1; index > 0; index--)
        {
            if (DispatchRegisteredEventListeners(eventPath[index], useCapture: true).CancelBubble ||
                evt.cancelBubble)
            {
                return !evt.defaultPrevented;
            }
        }

        _ = DispatchRegisteredEventListeners(eventPath[0], useCapture: true);

        for (var index = 0; index < eventPath.Count; index++)
        {
            var current = eventPath[index];
            var listenerResult = DispatchRegisteredEventListeners(current, useCapture: false);
            var currentFacade = getFacade(current);
            evt.SetCurrentTarget(currentFacade);
            var handlerResult = ExecuteEventHandlerCore(getHandlerElement(current), "on" + eventType, evt);
            if (listenerResult.CancelBubble ||
                handlerResult.CancelBubble ||
                !evt.bubbles)
            {
                break;
            }
        }

        return !evt.defaultPrevented;

        SvgJavaScriptEventResult DispatchRegisteredEventListeners(TTarget current, bool useCapture)
        {
            var currentFacade = getFacade(current);
            evt.SetCurrentTarget(currentFacade);
            return currentFacade is SvgJavaScriptElement elementFacade
                ? elementFacade.DispatchRegisteredEventListeners(eventType, evt, useCapture)
                : SvgJavaScriptEventResult.NotExecuted;
        }
    }

    private SvgJavaScriptEventResult ExecuteEventHandlerCore(
        SvgElement element,
        string attributeName,
        SvgJavaScriptEvent eventFacade)
    {
        if (!IsSupportedDefaultScriptType())
        {
            return SvgJavaScriptEventResult.NotExecuted;
        }

        if (!element.TryGetAttribute(attributeName, out var script) || string.IsNullOrWhiteSpace(script))
        {
            return SvgJavaScriptEventResult.NotExecuted;
        }

        var before = _mutationVersion;
        BindEventValues(eventFacade);

        try
        {
            var returnValue = ExecuteEventHandlerScript(NormalizeEventHandlerScript(script), $"event attribute {attributeName}");
            if (returnValue.ToObject() is bool handled && !handled)
            {
                eventFacade.preventDefault();
            }

            DrainPendingTimeouts();
        }
        finally
        {
            ClearEventValues();
        }

        return new SvgJavaScriptEventResult(
            executed: true,
            mutated: _mutationVersion != before,
            cancelBubble: eventFacade.cancelBubble,
            defaultPrevented: eventFacade.defaultPrevented);
    }

    private void BindEventValues(SvgJavaScriptEvent eventFacade)
    {
        _engine.SetValue("evt", eventFacade);
        _engine.SetValue("event", eventFacade);
        _engine.SetValue("__svgSkiaCurrentTarget", eventFacade.currentTarget ?? JsValue.Null);
        _engine.SetValue("__svgSkiaEvent", eventFacade);
    }

    private void ClearEventValues()
    {
        _engine.SetValue("evt", JsValue.Null);
        _engine.SetValue("event", JsValue.Null);
        _engine.SetValue("__svgSkiaCurrentTarget", JsValue.Null);
        _engine.SetValue("__svgSkiaEvent", JsValue.Null);
    }

    private static Engine CreateEngine(SvgJavaScriptSettings settings)
    {
        return new Engine(options =>
        {
            if (settings.TimeoutMilliseconds > 0)
            {
                options.TimeoutInterval(TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds));
            }

            if (settings.MaxStatements > 0)
            {
                options.MaxStatements(settings.MaxStatements);
            }
        });
    }

    internal ObjectInstance CreatePlainObject()
    {
        return _engine.Intrinsics.Object.Construct(Array.Empty<JsValue>());
    }

    internal void ThrowDomException(int code, string message)
    {
        var domException = CreatePlainObject();
        domException.FastSetDataProperty("name", JsValue.FromObject(_engine, "DOMException"));
        domException.FastSetDataProperty("message", JsValue.FromObject(_engine, message));
        domException.FastSetDataProperty("code", JsNumber.Create(code));
        throw new JavaScriptException(domException);
    }

    internal int SetTimeout(object? handler)
    {
        var id = ++_nextTimeoutId;

        if (handler is null)
        {
            return id;
        }

        _pendingTimeouts.Add(new SvgJavaScriptTimeoutEntry(id, _virtualTimerMilliseconds, ++_nextTimeoutSequence, handler));
        return id;
    }

    internal void ClearTimeout(int id)
    {
        for (var i = _pendingTimeouts.Count - 1; i >= 0; i--)
        {
            if (_pendingTimeouts[i].Id == id)
            {
                _pendingTimeouts.RemoveAt(i);
            }
        }
    }

    internal int SetTimeout(object? handler, object? delay)
    {
        var id = ++_nextTimeoutId;

        if (handler is null)
        {
            return id;
        }

        _pendingTimeouts.Add(new SvgJavaScriptTimeoutEntry(
            id,
            _virtualTimerMilliseconds + ParseTimeoutDelay(delay),
            ++_nextTimeoutSequence,
            handler));
        return id;
    }

    internal bool TryGetSceneNode(SvgElement element, out SvgSceneNode? node)
    {
        var sceneDocument = GetSceneDocument();
        if (sceneDocument is null)
        {
            node = null;
            return false;
        }

        if (sceneDocument.TryGetNode(element, out node) && node is not null)
        {
            return true;
        }

        SvgSceneNode? fallback = null;
        foreach (var candidate in sceneDocument.Traverse())
        {
            if (!ReferenceEquals(candidate.Element, element) &&
                !ReferenceEquals(candidate.HitTestTargetElement, element))
            {
                continue;
            }

            fallback ??= candidate;
            if (candidate.IsRenderable && !candidate.TransformedBounds.IsEmpty)
            {
                node = candidate;
                return true;
            }
        }

        if (fallback is not null)
        {
            node = fallback;
            return true;
        }

        node = null;
        return false;
    }

    internal SvgSceneDocument? GetSceneDocument()
    {
        if (_sceneDocument is not null && _sceneDocumentMutationVersion == _mutationVersion)
        {
            return _sceneDocument;
        }

        _sceneDocument = SvgSceneRuntime.TryCompile(_document, _assetLoader, DrawAttributes.None, out var sceneDocument)
            ? sceneDocument
            : null;
        _sceneDocumentMutationVersion = _mutationVersion;
        return _sceneDocument;
    }

    internal double GetCurrentTimeSeconds()
    {
        return (_animationHost?.CurrentTime ?? TimeSpan.Zero).TotalSeconds;
    }

    internal void SetCurrentTimeSeconds(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        if (_animationHost is not null)
        {
            _animationHost.Seek(time);
            return;
        }

        _pendingAnimationTime = time;
    }

    internal void BeginElement(SvgAnimationElement animation, TimeSpan offset)
    {
        _ = _animationHost?.BeginElement(animation, offset);
    }

    internal void EndElement(SvgAnimationElement animation, TimeSpan offset)
    {
        _ = _animationHost?.EndElement(animation, offset);
    }

    internal bool TryGetBaseAttributeValue(SvgElement element, string attributeName, out string value)
    {
        value = string.Empty;
        return _animationHost is not null && _animationHost.TryGetBaseAttributeValue(element, attributeName, out value);
    }

    internal double GetAnimationStartTimeSeconds(SvgAnimationElement animation)
    {
        if (_animationHost is not null &&
            _animationHost.TryGetStartTime(animation, out var startTime))
        {
            return startTime.TotalSeconds;
        }

        ThrowDomException(11, "The animation element has no current or future interval.");
        return 0d;
    }

    internal double GetComputedTextLength(SvgTextBase textContentElement)
    {
        return RequireTextContentHost().GetComputedTextLength(textContentElement);
    }

    internal int GetNumberOfChars(SvgTextBase textContentElement)
    {
        return RequireTextContentHost().GetNumberOfChars(textContentElement);
    }

    internal double GetSubStringLength(SvgTextBase textContentElement, int charnum, int nchars)
    {
        try
        {
            return RequireTextContentHost().GetSubStringLength(textContentElement, charnum, nchars);
        }
        catch (ArgumentOutOfRangeException)
        {
            ThrowDomException(1, "The character index is out of range.");
            return 0d;
        }
    }

    internal SvgJavaScriptPoint GetStartPositionOfChar(SvgTextBase textContentElement, int charnum)
    {
        try
        {
            return RequireTextContentHost().GetStartPositionOfChar(textContentElement, charnum);
        }
        catch (ArgumentOutOfRangeException)
        {
            ThrowDomException(1, "The character index is out of range.");
            return new SvgJavaScriptPoint();
        }
    }

    internal SvgJavaScriptPoint GetEndPositionOfChar(SvgTextBase textContentElement, int charnum)
    {
        try
        {
            return RequireTextContentHost().GetEndPositionOfChar(textContentElement, charnum);
        }
        catch (ArgumentOutOfRangeException)
        {
            ThrowDomException(1, "The character index is out of range.");
            return new SvgJavaScriptPoint();
        }
    }

    internal SvgJavaScriptRect GetExtentOfChar(SvgTextBase textContentElement, int charnum)
    {
        try
        {
            return RequireTextContentHost().GetExtentOfChar(textContentElement, charnum);
        }
        catch (ArgumentOutOfRangeException)
        {
            ThrowDomException(1, "The character index is out of range.");
            return new SvgJavaScriptRect(0f, 0f, 0f, 0f);
        }
    }

    internal double GetRotationOfChar(SvgTextBase textContentElement, int charnum)
    {
        try
        {
            return RequireTextContentHost().GetRotationOfChar(textContentElement, charnum);
        }
        catch (ArgumentOutOfRangeException)
        {
            ThrowDomException(1, "The character index is out of range.");
            return 0d;
        }
    }

    internal int GetCharNumAtPosition(SvgTextBase textContentElement, SvgJavaScriptPoint point)
    {
        return RequireTextContentHost().GetCharNumAtPosition(textContentElement, point);
    }

    internal void SelectSubString(SvgTextBase textContentElement, int charnum, int nchars)
    {
        try
        {
            RequireTextContentHost().SelectSubString(textContentElement, charnum, nchars);
        }
        catch (ArgumentOutOfRangeException)
        {
            ThrowDomException(1, "The character index is out of range.");
        }
    }

    private ISvgJavaScriptTextContentHost RequireTextContentHost()
    {
        if (_textContentHost is not null)
        {
            return _textContentHost;
        }

        ThrowDomException(11, "The text content element is not available in the current host.");
        return null!;
    }

    private void ExecuteScriptElement(SvgScript script)
    {
        if (!IsSupportedScriptType(script.ScriptType, useDocumentDefault: true))
        {
            return;
        }

        var source = GetScriptElementSource(script);
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        ExecuteScript(source!, GetScriptElementDescription(script));
    }

    private string? GetScriptElementSource(SvgScript script)
    {
        var href = GetScriptHref(script);
        if (string.IsNullOrWhiteSpace(href))
        {
            return script.Script;
        }

        if (!_settings.EnableExternalJavaScript)
        {
            return null;
        }

        return TryLoadExternalScript(script, href!);
    }

    private string? GetScriptHref(SvgScript script)
    {
        if (!string.IsNullOrWhiteSpace(script.Href))
        {
            return script.Href;
        }

        if (script.TryGetAttribute("href", out var href) && !string.IsNullOrWhiteSpace(href))
        {
            return href;
        }

        if (script.CustomAttributes.TryGetValue("xlink:href", out href) && !string.IsNullOrWhiteSpace(href))
        {
            return href;
        }

        var xlinkHrefKey = string.Concat(SvgNamespaces.XLinkNamespace, ":href");
        return script.CustomAttributes.TryGetValue(xlinkHrefKey, out href) && !string.IsNullOrWhiteSpace(href)
            ? href
            : null;
    }

    private string? TryLoadExternalScript(SvgElement owner, string href)
    {
        try
        {
            if (TryReadDataUri(href, out var dataScript))
            {
                return dataScript;
            }

            var uri = ResolveUri(owner, href);
            if (uri is null)
            {
                return null;
            }

            if (uri.IsFile)
            {
                return File.ReadAllText(uri.LocalPath);
            }

            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
#pragma warning disable SYSLIB0014
                var request = WebRequest.Create(uri);
                if (_settings.TimeoutMilliseconds > 0)
                {
                    request.Timeout = _settings.TimeoutMilliseconds;
                    if (request is HttpWebRequest httpRequest)
                    {
                        httpRequest.ReadWriteTimeout = _settings.TimeoutMilliseconds;
                    }
                }

                using var response = request.GetResponse();
                using var stream = response.GetResponseStream();
#pragma warning restore SYSLIB0014
                if (stream is null)
                {
                    return null;
                }

                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            HandleScriptError(ex, $"external script {href}");
        }

        return null;
    }

    private static Uri? ResolveUri(SvgElement owner, string href)
    {
        if (!Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri))
        {
            return null;
        }

        if (uri.IsAbsoluteUri)
        {
            return uri;
        }

        var baseUri = owner.OwnerDocument?.BaseUri;
        return baseUri is null ? null : new Uri(baseUri, uri);
    }

    private static bool TryReadDataUri(string href, out string script)
    {
        script = string.Empty;

        if (!href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var commaIndex = href.IndexOf(',');
        if (commaIndex < 0)
        {
            return false;
        }

        var metadata = href.Substring(5, commaIndex - 5);
        var data = href.Substring(commaIndex + 1);
        if (metadata.IndexOf(";base64", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            script = Encoding.UTF8.GetString(Convert.FromBase64String(data));
        }
        else
        {
            script = Uri.UnescapeDataString(data);
        }

        return true;
    }

    private void DrainPendingTimeouts()
    {
        if (_drainingTimeouts || _pendingTimeouts.Count == 0)
        {
            return;
        }

        _drainingTimeouts = true;
        try
        {
            while (true)
            {
                var nextIndex = -1;
                for (var i = 0; i < _pendingTimeouts.Count; i++)
                {
                    if (nextIndex < 0 ||
                        _pendingTimeouts[i].DueMilliseconds < _pendingTimeouts[nextIndex].DueMilliseconds ||
                        (_pendingTimeouts[i].DueMilliseconds == _pendingTimeouts[nextIndex].DueMilliseconds &&
                         _pendingTimeouts[i].Sequence < _pendingTimeouts[nextIndex].Sequence))
                    {
                        nextIndex = i;
                    }
                }

                if (nextIndex < 0)
                {
                    break;
                }

                var entry = _pendingTimeouts[nextIndex];
                _pendingTimeouts.RemoveAt(nextIndex);
                if (entry.DueMilliseconds > _virtualTimerMilliseconds)
                {
                    _virtualTimerMilliseconds = entry.DueMilliseconds;
                }

                ExecuteTimeoutHandler(entry.Handler);
            }
        }
        finally
        {
            _drainingTimeouts = false;
        }
    }

    private void ExecuteTimeoutHandler(object handler)
    {
        switch (handler)
        {
            case string code when !string.IsNullOrWhiteSpace(code):
                ExecuteScript(code, "window.setTimeout");
                break;
            case JsValue callback:
                _engine.Call(callback, JsValue.FromObject(_engine, _windowFacade), Array.Empty<JsValue>());
                break;
            case Func<JsValue, JsValue[], JsValue> callback:
                callback(JsValue.FromObject(_engine, _windowFacade), Array.Empty<JsValue>());
                break;
            default:
                ExecuteScript(handler.ToString() ?? string.Empty, "window.setTimeout");
                break;
        }
    }

    private static double ParseTimeoutDelay(object? delay)
    {
        try
        {
            var value = Convert.ToDouble(delay, System.Globalization.CultureInfo.InvariantCulture);
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0d ? 0d : value;
        }
        catch
        {
            return 0d;
        }
    }

    private void ExecuteScript(string script, string sourceDescription)
    {
        try
        {
            _engine.Execute(script, sourceDescription);
        }
        catch (Exception ex)
        {
            HandleScriptError(ex, sourceDescription);
        }
    }

    private JsValue ExecuteEventHandlerScript(string script, string sourceDescription)
    {
        try
        {
            return _engine.Evaluate(
                "(function(evt, event) {\n" + script + "\n}).call(__svgSkiaCurrentTarget, __svgSkiaEvent, __svgSkiaEvent);",
                sourceDescription);
        }
        catch (Exception ex)
        {
            HandleScriptError(ex, sourceDescription);
            return JsValue.Undefined;
        }
    }

    private void HandleScriptError(Exception exception, string sourceDescription)
    {
        if (_settings.ThrowOnError)
        {
            throw new SvgJavaScriptException($"Failed to execute SVG JavaScript from {sourceDescription}.", exception);
        }

        Trace.TraceError($"Failed to execute SVG JavaScript from {sourceDescription}: {exception.Message}");
    }

    private string GetScriptElementDescription(SvgScript script)
    {
        return string.IsNullOrWhiteSpace(script.ID)
            ? "script element"
            : "script element #" + script.ID;
    }

    private bool IsSupportedDefaultScriptType()
    {
        return IsSupportedScriptType(null, useDocumentDefault: true);
    }

    private bool IsSupportedScriptType(string? scriptType, bool useDocumentDefault)
    {
        if (string.IsNullOrWhiteSpace(scriptType) && useDocumentDefault)
        {
            _document.TryGetAttribute("contentScriptType", out scriptType);
        }

        if (string.IsNullOrWhiteSpace(scriptType))
        {
            return true;
        }

        var normalizedType = scriptType!.Split(';')[0].Trim();
        return s_supportedScriptTypes.Contains(normalizedType);
    }

    private static string NormalizeEventHandlerScript(string script)
    {
        var trimmed = script.Trim();
        return trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            ? trimmed.Substring("javascript:".Length)
            : script;
    }

    private static string NormalizeEventType(string? eventType)
    {
        return eventType?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private sealed class SvgJavaScriptTimeoutEntry
    {
        public SvgJavaScriptTimeoutEntry(int id, double dueMilliseconds, long sequence, object handler)
        {
            Id = id;
            DueMilliseconds = dueMilliseconds;
            Sequence = sequence;
            Handler = handler;
        }

        public int Id { get; }

        public double DueMilliseconds { get; }

        public long Sequence { get; }

        public object Handler { get; }
    }
}

public sealed class SvgJavaScriptException : Exception
{
    public SvgJavaScriptException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
