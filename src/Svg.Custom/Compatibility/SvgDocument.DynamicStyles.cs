#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Svg;

public partial class SvgDocument
{
    private List<SvgCssStyleSource>? _compatibilityStyleSources;
    private Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>? _compatibilityStyleState;
    private bool _compatibilityStyleStateInitialized;

    internal void SetCompatibilityStyleSources(IEnumerable<SvgCssStyleSource> styles)
    {
        if (styles is null)
        {
            _compatibilityStyleSources = null;
            return;
        }

        var styleSources = styles as IReadOnlyCollection<SvgCssStyleSource> ?? styles.ToArray();
        if (styleSources.Count == 0)
        {
            _compatibilityStyleSources = null;
            return;
        }

        _compatibilityStyleSources = new List<SvgCssStyleSource>(styleSources.Count);
        foreach (var style in styleSources)
        {
            _compatibilityStyleSources.Add(new SvgCssStyleSource(style.Content, style.BaseUri));
        }
    }

    internal void CopyCompatibilityStyleSourcesTo(SvgDocument target)
    {
        if (_compatibilityStyleSources is null)
        {
            target._compatibilityStyleSources = null;
            return;
        }

        target.SetCompatibilityStyleSources(_compatibilityStyleSources);
    }

    internal void CaptureCompatibilityStyleState()
    {
        _compatibilityStyleState = CreateCompatibilityStyleStateMap();
        _compatibilityStyleStateInitialized = true;
    }

    internal void CaptureCompatibilityStyleState(SvgElement root)
    {
        EnsureCompatibilityStyleStateInitialized();
        foreach (var element in EnumerateElements(root))
        {
            var snapshot = element.CreateCompatibilityStyleSnapshot();
            if (snapshot.HasState)
            {
                _compatibilityStyleState![element] = snapshot;
            }
            else
            {
                _compatibilityStyleState?.Remove(element);
            }
        }
    }

    internal void EnsureCompatibilityStyleState(SvgElement root)
    {
        EnsureCompatibilityStyleStateInitialized();
        foreach (var element in EnumerateElements(root))
        {
            if (_compatibilityStyleState?.ContainsKey(element) == true)
            {
                continue;
            }

            var snapshot = element.CreateCompatibilityStyleSnapshot();
            if (snapshot.HasState)
            {
                _compatibilityStyleState ??= new Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>(SvgElementReferenceComparer.Instance);
                _compatibilityStyleState[element] = snapshot;
            }
        }
    }

    internal void CopyCompatibilityStyleStateTo(SvgDocument target)
    {
        if (!_compatibilityStyleStateInitialized)
        {
            target._compatibilityStyleState = null;
            target._compatibilityStyleStateInitialized = false;
            return;
        }

        target._compatibilityStyleStateInitialized = true;
        if (_compatibilityStyleState is null || _compatibilityStyleState.Count == 0)
        {
            target._compatibilityStyleState = null;
            return;
        }

        var sourceElements = EnumerateElements().ToArray();
        var targetElements = target.EnumerateElements().ToArray();
        if (sourceElements.Length != targetElements.Length)
        {
            target._compatibilityStyleState = null;
            target._compatibilityStyleStateInitialized = false;
            return;
        }

        var state = new Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>(_compatibilityStyleState.Count, SvgElementReferenceComparer.Instance);
        for (var i = 0; i < sourceElements.Length; i++)
        {
            var sourceElement = sourceElements[i];
            var targetElement = targetElements[i];
            if (_compatibilityStyleState.TryGetValue(sourceElement, out var snapshot))
            {
                state[targetElement] = snapshot.Clone();
            }
        }

        target._compatibilityStyleState = state;
    }

    internal void UpdateCompatibilityStyleAttribute(SvgElement element, string name, string? value)
    {
        if (!SvgStyleAttributeNames.Contains(name))
        {
            return;
        }

        var snapshot = GetOrCreateCompatibilityStyleSnapshot(element);
        if (string.IsNullOrWhiteSpace(value))
        {
            snapshot.PresentationAttributes.Remove(name);
        }
        else
        {
            snapshot.PresentationAttributes[name] = value!;
        }
    }

    internal void UpdateCompatibilityStyleText(SvgElement element, string? styleText)
    {
        GetOrCreateCompatibilityStyleSnapshot(element).InlineStyleText = styleText ?? string.Empty;
    }

    internal void ReapplyCompatibilityStyles()
    {
        EnsureCompatibilityStyleStateInitialized();
        RestoreCompatibilityStyleState();
        ApplyCompatibilityStyles();
    }

    internal void ApplyCompatibilityStyles()
    {
        if (_compatibilityStyleSources is { Count: > 0 })
        {
            SvgCssCompatibilityProcessor.Apply(this, _compatibilityStyleSources, new SvgElementFactory());
        }

        ApplyInlineStyles();
        FlushStyles(children: true);
    }

    private void RestoreCompatibilityStyleState()
    {
        if (!_compatibilityStyleStateInitialized)
        {
            return;
        }

        foreach (var element in EnumerateElements())
        {
            var snapshot = _compatibilityStyleState is not null &&
                           _compatibilityStyleState.TryGetValue(element, out var storedSnapshot)
                ? storedSnapshot
                : SvgCompatibilityStyleSnapshot.Empty;

            if (ReferenceEquals(snapshot, SvgCompatibilityStyleSnapshot.Empty))
            {
                element.RestoreCompatibilityStyleState(snapshot);
                continue;
            }

            element.RestoreCompatibilityStyleState(snapshot);
        }
    }

    private SvgCompatibilityStyleSnapshot GetOrCreateCompatibilityStyleSnapshot(SvgElement element)
    {
        EnsureCompatibilityStyleStateInitialized();
        if (_compatibilityStyleState is null ||
            !_compatibilityStyleState.TryGetValue(element, out var snapshot))
        {
            snapshot = element.CreateCompatibilityStyleSnapshot();
            _compatibilityStyleState ??= new Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>(SvgElementReferenceComparer.Instance);
            _compatibilityStyleState[element] = snapshot;
        }

        return snapshot;
    }

    private void EnsureCompatibilityStyleStateInitialized()
    {
        if (_compatibilityStyleStateInitialized)
        {
            _compatibilityStyleState ??= new Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>(SvgElementReferenceComparer.Instance);
            return;
        }

        _compatibilityStyleState = CreateCompatibilityStyleStateMap();
        _compatibilityStyleStateInitialized = true;
    }

    private Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>? CreateCompatibilityStyleStateMap()
    {
        Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>? state = null;
        foreach (var element in EnumerateElements())
        {
            var snapshot = element.CreateCompatibilityStyleSnapshot();
            if (!snapshot.HasState)
            {
                continue;
            }

            state ??= new Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>(SvgElementReferenceComparer.Instance);
            state[element] = snapshot;
        }

        return state;
    }

    private void ApplyInlineStyles()
    {
        var parser = new SvgInlineStyleAttributeParser();
        foreach (var element in EnumerateElements())
        {
            if (element.CustomAttributes.TryGetValue("style", out var styleText) &&
                !string.IsNullOrWhiteSpace(styleText))
            {
                parser.ApplyStyles(element, styleText);
            }
        }
    }

    private IEnumerable<SvgElement> EnumerateElements(SvgElement? scopeRoot = null)
    {
        if (scopeRoot is null)
        {
            yield return this;
            foreach (var descendant in Descendants())
            {
                yield return descendant;
            }

            yield break;
        }

        yield return scopeRoot;
        foreach (var descendant in scopeRoot.Descendants())
        {
            yield return descendant;
        }
    }

    private sealed class SvgElementReferenceComparer : IEqualityComparer<SvgElement>
    {
        public static readonly SvgElementReferenceComparer Instance = new();

        public bool Equals(SvgElement? x, SvgElement? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(SvgElement obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}

internal sealed class SvgCompatibilityStyleSnapshot
{
    public static readonly SvgCompatibilityStyleSnapshot Empty = new(string.Empty, new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase));

    public SvgCompatibilityStyleSnapshot(string inlineStyleText, Dictionary<string, string> presentationAttributes)
    {
        InlineStyleText = inlineStyleText;
        PresentationAttributes = presentationAttributes;
    }

    public string InlineStyleText { get; set; }

    public Dictionary<string, string> PresentationAttributes { get; }

    public bool HasState => InlineStyleText.Length > 0 || PresentationAttributes.Count > 0;

    public SvgCompatibilityStyleSnapshot Clone()
    {
        return new SvgCompatibilityStyleSnapshot(InlineStyleText, new Dictionary<string, string>(PresentationAttributes, StringComparer.OrdinalIgnoreCase));
    }
}
