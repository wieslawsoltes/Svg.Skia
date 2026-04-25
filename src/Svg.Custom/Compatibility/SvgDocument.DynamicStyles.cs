#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Svg;

public partial class SvgDocument
{
    private List<SvgCssStyleSource>? _compatibilityStyleSources;
    private Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>? _compatibilityStyleState;
    private List<SvgElement>? _compatibilityStyleStateCandidates;
    private List<SvgElement>? _compatibilityStyleRestoreCandidates;
    private bool _compatibilityStyleStateInitialized;
    private bool _compatibilityStyleStateTrackingEnabled;

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

        _compatibilityStyleStateTrackingEnabled = true;
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

    internal void TrackCompatibilityStyleStateCandidate(SvgElement element)
    {
        _compatibilityStyleStateTrackingEnabled = true;
        if (element.MarkCompatibilityStyleStateCandidate())
        {
            (_compatibilityStyleStateCandidates ??= new List<SvgElement>()).Add(element);
        }
    }

    internal void TrackCompatibilityStyleApplication(SvgElement element)
    {
        if (!_compatibilityStyleStateTrackingEnabled)
        {
            return;
        }

        if (element.MarkCompatibilityStyleRestoreCandidate())
        {
            (_compatibilityStyleRestoreCandidates ??= new List<SvgElement>()).Add(element);
        }
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
            if (HasCompatibilityInlineStyle(element))
            {
                TrackCompatibilityStyleStateCandidate(element);
            }

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
            if (HasCompatibilityInlineStyle(element))
            {
                TrackCompatibilityStyleStateCandidate(element);
            }

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
            CopyCompatibilityStyleTrackingTo(target);
            return;
        }

        CopyCompatibilityStyleTrackingTo(target);
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

    internal bool HasCompatibilityStyleSources => _compatibilityStyleSources is { Count: > 0 };

    internal bool UpdateCompatibilityStyleAttribute(SvgElement element, string name, string? value)
    {
        if (!SvgStyleAttributeNames.Contains(name))
        {
            return false;
        }

        var snapshot = GetOrCreateCompatibilityStyleSnapshot(element);
        if (string.IsNullOrWhiteSpace(value))
        {
            snapshot.RemovePresentationAttribute(name);
        }
        else
        {
            snapshot.SetPresentationAttribute(name, value!);
        }

        return true;
    }

    internal void UpdateCompatibilityStyleText(SvgElement element, string? styleText)
    {
        if (!string.IsNullOrWhiteSpace(styleText))
        {
            TrackCompatibilityStyleStateCandidate(element);
        }

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

        if (!_compatibilityStyleStateTrackingEnabled)
        {
            foreach (var element in EnumerateElements())
            {
                var snapshot = _compatibilityStyleState is not null &&
                               _compatibilityStyleState.TryGetValue(element, out var storedSnapshot)
                    ? storedSnapshot
                    : SvgCompatibilityStyleSnapshot.Empty;

                element.RestoreCompatibilityStyleState(snapshot);
            }

            return;
        }

        if (_compatibilityStyleState is not null)
        {
            foreach (var entry in _compatibilityStyleState)
            {
                entry.Key.RestoreCompatibilityStyleState(entry.Value);
            }
        }

        if (_compatibilityStyleRestoreCandidates is null)
        {
            return;
        }

        for (var i = 0; i < _compatibilityStyleRestoreCandidates.Count; i++)
        {
            var element = _compatibilityStyleRestoreCandidates[i];
            if (_compatibilityStyleState?.ContainsKey(element) == true)
            {
                continue;
            }

            element.RestoreCompatibilityStyleState(SvgCompatibilityStyleSnapshot.Empty);
        }
    }

    private SvgCompatibilityStyleSnapshot GetOrCreateCompatibilityStyleSnapshot(SvgElement element)
    {
        EnsureCompatibilityStyleStateInitialized();
        if (_compatibilityStyleState is null ||
            !_compatibilityStyleState.TryGetValue(element, out var snapshot))
        {
            snapshot = element.CreateCompatibilityStyleSnapshot();
            if (ReferenceEquals(snapshot, SvgCompatibilityStyleSnapshot.Empty))
            {
                snapshot = SvgCompatibilityStyleSnapshot.CreateEmpty();
            }

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
        if (_compatibilityStyleStateTrackingEnabled && _compatibilityStyleStateCandidates is null)
        {
            return null;
        }

        Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>? state = null;
        var elements = _compatibilityStyleStateTrackingEnabled && _compatibilityStyleStateCandidates is not null
            ? _compatibilityStyleStateCandidates
            : EnumerateElements();
        foreach (var element in elements)
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

    private void CopyCompatibilityStyleTrackingTo(SvgDocument target)
    {
        target._compatibilityStyleStateTrackingEnabled = _compatibilityStyleStateTrackingEnabled;
        target._compatibilityStyleStateCandidates = null;
        target._compatibilityStyleRestoreCandidates = null;

        if (!_compatibilityStyleStateTrackingEnabled)
        {
            return;
        }

        var sourceElements = EnumerateElements().ToArray();
        var targetElements = target.EnumerateElements().ToArray();
        if (sourceElements.Length != targetElements.Length)
        {
            target._compatibilityStyleStateTrackingEnabled = false;
            return;
        }

        var targetBySource = new Dictionary<SvgElement, SvgElement>(sourceElements.Length, SvgElementReferenceComparer.Instance);
        for (var i = 0; i < sourceElements.Length; i++)
        {
            targetBySource[sourceElements[i]] = targetElements[i];
        }

        CopyTrackedElements(_compatibilityStyleStateCandidates, target._compatibilityStyleStateCandidates = new List<SvgElement>());
        CopyTrackedElements(_compatibilityStyleRestoreCandidates, target._compatibilityStyleRestoreCandidates = new List<SvgElement>());

        if (target._compatibilityStyleStateCandidates.Count == 0)
        {
            target._compatibilityStyleStateCandidates = null;
        }

        if (target._compatibilityStyleRestoreCandidates.Count == 0)
        {
            target._compatibilityStyleRestoreCandidates = null;
        }

        void CopyTrackedElements(List<SvgElement>? sourceList, List<SvgElement> targetList)
        {
            if (sourceList is null)
            {
                return;
            }

            for (var i = 0; i < sourceList.Count; i++)
            {
                if (!targetBySource.TryGetValue(sourceList[i], out var targetElement))
                {
                    continue;
                }

                if (ReferenceEquals(sourceList, _compatibilityStyleStateCandidates))
                {
                    sourceList[i].CopyCompatibilityRawStyleStateTo(targetElement);
                    if (targetElement.MarkCompatibilityStyleStateCandidate())
                    {
                        targetList.Add(targetElement);
                    }
                }
                else if (targetElement.MarkCompatibilityStyleRestoreCandidate())
                {
                    targetList.Add(targetElement);
                }
            }
        }
    }

    private void ApplyInlineStyles()
    {
        var parser = new SvgInlineStyleAttributeParser();
        var elements = _compatibilityStyleStateTrackingEnabled && _compatibilityStyleStateCandidates is not null
            ? _compatibilityStyleStateCandidates
            : EnumerateElements();
        foreach (var element in elements)
        {
            if (HasCompatibilityInlineStyle(element))
            {
                var styleText = element.CustomAttributes["style"];
                parser.ApplyStyles(element, styleText);
            }
        }
    }

    private static bool HasCompatibilityInlineStyle(SvgElement element)
    {
        return element.CustomAttributes.TryGetValue("style", out var styleText) &&
               !string.IsNullOrWhiteSpace(styleText);
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
    public static readonly SvgCompatibilityStyleSnapshot Empty = new(string.Empty);

    private string? _presentationAttributeName;
    private string? _presentationAttributeValue;
    private Dictionary<string, string>? _presentationAttributes;

    public SvgCompatibilityStyleSnapshot(string inlineStyleText)
    {
        InlineStyleText = inlineStyleText;
    }

    public string InlineStyleText { get; set; }

    public bool HasState =>
        InlineStyleText.Length > 0 ||
        _presentationAttributeName is not null ||
        _presentationAttributes?.Count > 0;

    public static SvgCompatibilityStyleSnapshot CreateEmpty()
    {
        return new SvgCompatibilityStyleSnapshot(string.Empty);
    }

    public void AddPresentationAttributeIfAbsent(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (_presentationAttributes is not null)
        {
            if (!_presentationAttributes.ContainsKey(name))
            {
                _presentationAttributes[name] = value!;
            }

            return;
        }

        if (_presentationAttributeName is null)
        {
            _presentationAttributeName = name;
            _presentationAttributeValue = value!;
            return;
        }

        if (string.Equals(_presentationAttributeName, name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _presentationAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [_presentationAttributeName] = _presentationAttributeValue!
        };
        _presentationAttributeName = null;
        _presentationAttributeValue = null;
        if (!_presentationAttributes.ContainsKey(name))
        {
            _presentationAttributes[name] = value!;
        }
    }

    public void SetPresentationAttribute(string name, string value)
    {
        if (_presentationAttributes is not null)
        {
            _presentationAttributes[name] = value;
            return;
        }

        if (_presentationAttributeName is null ||
            string.Equals(_presentationAttributeName, name, StringComparison.OrdinalIgnoreCase))
        {
            _presentationAttributeName = name;
            _presentationAttributeValue = value;
            return;
        }

        _presentationAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [_presentationAttributeName] = _presentationAttributeValue!,
            [name] = value
        };
        _presentationAttributeName = null;
        _presentationAttributeValue = null;
    }

    public void RemovePresentationAttribute(string name)
    {
        if (_presentationAttributes is not null)
        {
            _presentationAttributes.Remove(name);
            return;
        }

        if (string.Equals(_presentationAttributeName, name, StringComparison.OrdinalIgnoreCase))
        {
            _presentationAttributeName = null;
            _presentationAttributeValue = null;
        }
    }

    public void ApplyPresentationAttributesTo(SvgElement element)
    {
        if (_presentationAttributeName is not null)
        {
            element.AddStyle(_presentationAttributeName, _presentationAttributeValue!, SvgElement.StyleSpecificity_PresAttribute);
            return;
        }

        if (_presentationAttributes is null)
        {
            return;
        }

        foreach (var attribute in _presentationAttributes)
        {
            element.AddStyle(attribute.Key, attribute.Value, SvgElement.StyleSpecificity_PresAttribute);
        }
    }

    public SvgCompatibilityStyleSnapshot Clone()
    {
        var clone = new SvgCompatibilityStyleSnapshot(InlineStyleText);
        clone._presentationAttributeName = _presentationAttributeName;
        clone._presentationAttributeValue = _presentationAttributeValue;
        clone._presentationAttributes = _presentationAttributes is null
            ? null
            : new Dictionary<string, string>(_presentationAttributes, StringComparer.OrdinalIgnoreCase);
        return clone;
    }
}
