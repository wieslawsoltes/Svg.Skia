#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Svg;

public partial class SvgDocument
{
    private List<SvgCssStyleSource>? _compatibilityStyleSources;
    private Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>? _compatibilityStyleState;

    internal void SetCompatibilityStyleSources(IEnumerable<SvgCssStyleSource> styles)
    {
        _compatibilityStyleSources = styles is null
            ? null
            : styles.Select(style => new SvgCssStyleSource(style.Content, style.BaseUri)).ToList();
    }

    internal void CopyCompatibilityStyleSourcesTo(SvgDocument target)
    {
        target.SetCompatibilityStyleSources(_compatibilityStyleSources ?? Enumerable.Empty<SvgCssStyleSource>());
    }

    internal void CaptureCompatibilityStyleState()
    {
        _compatibilityStyleState = CreateCompatibilityStyleStateMap();
    }

    internal void CaptureCompatibilityStyleState(SvgElement root)
    {
        _compatibilityStyleState ??= CreateCompatibilityStyleStateMap();
        foreach (var element in EnumerateElements(root))
        {
            _compatibilityStyleState[element] = SvgCompatibilityStyleSnapshot.Create(element);
        }
    }

    internal void EnsureCompatibilityStyleState(SvgElement root)
    {
        _compatibilityStyleState ??= CreateCompatibilityStyleStateMap();
        foreach (var element in EnumerateElements(root))
        {
            if (!_compatibilityStyleState.ContainsKey(element))
            {
                _compatibilityStyleState[element] = SvgCompatibilityStyleSnapshot.Create(element);
            }
        }
    }

    internal void CopyCompatibilityStyleStateTo(SvgDocument target)
    {
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
            return;
        }

        var state = new Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>(targetElements.Length, SvgElementReferenceComparer.Instance);
        for (var i = 0; i < sourceElements.Length; i++)
        {
            var sourceElement = sourceElements[i];
            var targetElement = targetElements[i];
            state[targetElement] = _compatibilityStyleState.TryGetValue(sourceElement, out var snapshot)
                ? snapshot.Clone()
                : SvgCompatibilityStyleSnapshot.Create(targetElement);
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
        if (_compatibilityStyleState is null)
        {
            return;
        }

        foreach (var element in EnumerateElements())
        {
            if (!_compatibilityStyleState.TryGetValue(element, out var snapshot))
            {
                snapshot = SvgCompatibilityStyleSnapshot.Create(element);
                _compatibilityStyleState[element] = snapshot;
            }

            element.RestoreCompatibilityStyleState(snapshot);
        }
    }

    private SvgCompatibilityStyleSnapshot GetOrCreateCompatibilityStyleSnapshot(SvgElement element)
    {
        _compatibilityStyleState ??= CreateCompatibilityStyleStateMap();
        if (!_compatibilityStyleState.TryGetValue(element, out var snapshot))
        {
            snapshot = SvgCompatibilityStyleSnapshot.Create(element);
            _compatibilityStyleState[element] = snapshot;
        }

        return snapshot;
    }

    private Dictionary<SvgElement, SvgCompatibilityStyleSnapshot> CreateCompatibilityStyleStateMap()
    {
        var state = new Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>(SvgElementReferenceComparer.Instance);
        foreach (var element in EnumerateElements())
        {
            state[element] = SvgCompatibilityStyleSnapshot.Create(element);
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
    public SvgCompatibilityStyleSnapshot(string inlineStyleText, Dictionary<string, string> presentationAttributes)
    {
        InlineStyleText = inlineStyleText;
        PresentationAttributes = presentationAttributes;
    }

    public string InlineStyleText { get; set; }

    public Dictionary<string, string> PresentationAttributes { get; }

    public SvgCompatibilityStyleSnapshot Clone()
    {
        return new SvgCompatibilityStyleSnapshot(InlineStyleText, new Dictionary<string, string>(PresentationAttributes, StringComparer.OrdinalIgnoreCase));
    }

    public static SvgCompatibilityStyleSnapshot Create(SvgElement element)
    {
        var presentationAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in SvgStyleAttributeNames.All)
        {
            if (element.TryGetAttribute(name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                presentationAttributes[name] = value;
            }
        }

        var inlineStyleText = element.CustomAttributes.TryGetValue("style", out var styleText)
            ? styleText ?? string.Empty
            : string.Empty;

        return new SvgCompatibilityStyleSnapshot(inlineStyleText, presentationAttributes);
    }
}
