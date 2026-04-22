using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Jint;
using Jint.Native;
using Svg;

namespace Svg.JavaScript;

public sealed class SvgJavaScriptRuntime
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
    private int _mutationVersion;

    public SvgJavaScriptRuntime(SvgDocument document, SvgJavaScriptSettings settings)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _documentFacade = new SvgJavaScriptDocument(this, document);
        _windowFacade = _documentFacade.defaultView;
        _engine = CreateEngine(settings);
        _engine.SetValue("document", _documentFacade);
        _engine.SetValue("window", _windowFacade);
        _engine.SetValue("self", _windowFacade);
        _engine.SetValue("console", new SvgJavaScriptConsole());
        _engine.SetValue("evt", JsValue.Null);
        _engine.SetValue("event", JsValue.Null);
    }

    public int MutationVersion => _mutationVersion;

    public SvgJavaScriptElement GetElement(SvgElement element)
    {
        return _documentFacade.GetOrCreateElement(element);
    }

    internal void MarkMutation()
    {
        _mutationVersion++;
    }

    public void ExecuteDocumentScripts()
    {
        foreach (var script in _document.Descendants().OfType<SvgScript>().ToArray())
        {
            ExecuteScriptElement(script);
        }

        ExecuteEventHandler(_document, _document, null, "load", "onload", null);
    }

    public SvgJavaScriptEventResult ExecuteEventHandler(
        SvgElement element,
        SvgElement targetElement,
        SvgElement? relatedElement,
        string eventType,
        string attributeName,
        SvgJavaScriptEventInput? input)
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
        var currentTarget = GetElement(element);
        var eventFacade = new SvgJavaScriptEvent(
            eventType,
            GetElement(targetElement),
            currentTarget,
            relatedElement is null ? null : GetElement(relatedElement),
            input);

        _engine.SetValue("evt", eventFacade);
        _engine.SetValue("event", eventFacade);
        _engine.SetValue("__svgSkiaCurrentTarget", currentTarget);
        _engine.SetValue("__svgSkiaEvent", eventFacade);

        try
        {
            var returnValue = ExecuteEventHandlerScript(NormalizeEventHandlerScript(script), $"event attribute {attributeName}");
            if (returnValue.ToObject() is bool handled && !handled)
            {
                eventFacade.preventDefault();
            }
        }
        finally
        {
            _engine.SetValue("evt", JsValue.Null);
            _engine.SetValue("event", JsValue.Null);
            _engine.SetValue("__svgSkiaCurrentTarget", JsValue.Null);
            _engine.SetValue("__svgSkiaEvent", JsValue.Null);
        }

        return new SvgJavaScriptEventResult(
            executed: true,
            mutated: _mutationVersion != before,
            cancelBubble: eventFacade.cancelBubble,
            defaultPrevented: eventFacade.defaultPrevented);
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

    private void ExecuteScript(string script, string sourceDescription)
    {
        try
        {
            _engine.Execute(script);
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
}

public sealed class SvgJavaScriptException : Exception
{
    public SvgJavaScriptException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
