#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Svg;

public partial class SvgDocument
{
    private List<SvgCssStyleSource>? _compatibilityStyleSources;
    private Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>? _compatibilityStyleState;
    private Dictionary<SvgElement, SvgCompatibilityElementStyleState>? _compatibilityRawStyleState;
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
        if (GetOrCreateCompatibilityRawStyleState(element).MarkStateCandidate())
        {
            (_compatibilityStyleStateCandidates ??= new List<SvgElement>()).Add(element);
        }
    }

    internal void PreserveCompatibilityPresentationAttribute(SvgElement element, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _compatibilityStyleStateTrackingEnabled = true;
        var rawState = GetOrCreateCompatibilityRawStyleState(element);
        if (rawState.PreservePresentationAttribute(name, value!) &&
            rawState.MarkStateCandidate())
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

        if (GetOrCreateCompatibilityRawStyleState(element).MarkRestoreCandidate())
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

            var snapshot = CreateCompatibilityStyleSnapshot(element);
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

            var snapshot = CreateCompatibilityStyleSnapshot(element);
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

        var state = new Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>(_compatibilityStyleState.Count, SvgElementReferenceComparer.Instance);
        using var sourceEnumerator = EnumerateElements().GetEnumerator();
        using var targetEnumerator = target.EnumerateElements().GetEnumerator();
        while (true)
        {
            var hasSource = sourceEnumerator.MoveNext();
            var hasTarget = targetEnumerator.MoveNext();
            if (hasSource != hasTarget)
            {
                target._compatibilityStyleState = null;
                target._compatibilityStyleStateInitialized = false;
                return;
            }

            if (!hasSource)
            {
                break;
            }

            var sourceElement = sourceEnumerator.Current;
            var targetElement = targetEnumerator.Current;
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
        InvalidateComputedStyleCache();
        RestoreCompatibilityStyleState();
        ApplyCompatibilityStyles();
    }

    internal void ReapplyCompatibilityStylesAfterSelectorMutation()
    {
        EnsureCompatibilityStyleStateInitialized();
        InvalidateComputedStyleCache();
        RestoreCompatibilityStyleStateAfterSelectorMutation();
        ApplyCompatibilityStyles();
    }

    internal void ApplyCompatibilityStyles()
    {
        if (_compatibilityStyleSources is { Count: > 0 })
        {
            SvgCssCompatibilityProcessor.Apply(this, _compatibilityStyleSources, new SvgElementFactory(), LoadOptions);
        }

        ApplyInlineStyles();
        FlushStyles(children: true);
        InvalidateComputedStyleCache();
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

    private void RestoreCompatibilityStyleStateAfterSelectorMutation()
    {
        foreach (var element in EnumerateElements())
        {
            SvgCompatibilityStyleSnapshot snapshot;
            if (_compatibilityRawStyleState is not null &&
                _compatibilityRawStyleState.TryGetValue(element, out var rawState) &&
                rawState.HasPresentationAttributes)
            {
                snapshot = element.CreateCompatibilityStyleSnapshot(rawState);
            }
            else if (_compatibilityStyleState is not null &&
                     _compatibilityStyleState.TryGetValue(element, out var storedSnapshot))
            {
                snapshot = storedSnapshot;
            }
            else if (HasCompatibilityInlineStyle(element))
            {
                snapshot = new SvgCompatibilityStyleSnapshot(element.CustomAttributes["style"]);
            }
            else
            {
                snapshot = SvgCompatibilityStyleSnapshot.Empty;
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
            snapshot = CreateCompatibilityStyleSnapshot(element);
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

    private SvgCompatibilityStyleSnapshot CreateCompatibilityStyleSnapshot(SvgElement element)
    {
        return element.CreateCompatibilityStyleSnapshot(
            _compatibilityRawStyleState is not null &&
            _compatibilityRawStyleState.TryGetValue(element, out var rawState)
                ? rawState
                : null);
    }

    private Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>? CreateCompatibilityStyleStateMap()
    {
        Dictionary<SvgElement, SvgCompatibilityStyleSnapshot>? state = null;
        var elements = _compatibilityStyleStateTrackingEnabled && _compatibilityStyleStateCandidates is { Count: > 0 }
            ? _compatibilityStyleStateCandidates
            : EnumerateElements();
        foreach (var element in elements)
        {
            var snapshot = CreateCompatibilityStyleSnapshot(element);
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

        var trackedElementCount =
            (_compatibilityStyleStateCandidates?.Count ?? 0) +
            (_compatibilityStyleRestoreCandidates?.Count ?? 0);
        if (trackedElementCount == 0)
        {
            return;
        }

        var trackedElements = new HashSet<SvgElement>(SvgElementReferenceComparer.Instance);
        AddTrackedElements(_compatibilityStyleStateCandidates);
        AddTrackedElements(_compatibilityStyleRestoreCandidates);

        var targetBySource = new Dictionary<SvgElement, SvgElement>(trackedElements.Count, SvgElementReferenceComparer.Instance);
        using (var sourceEnumerator = EnumerateElements().GetEnumerator())
        using (var targetEnumerator = target.EnumerateElements().GetEnumerator())
        {
            while (true)
            {
                var hasSource = sourceEnumerator.MoveNext();
                var hasTarget = targetEnumerator.MoveNext();
                if (hasSource != hasTarget)
                {
                    target._compatibilityStyleStateTrackingEnabled = false;
                    return;
                }

                if (!hasSource)
                {
                    break;
                }

                if (trackedElements.Contains(sourceEnumerator.Current))
                {
                    targetBySource[sourceEnumerator.Current] = targetEnumerator.Current;
                    if (targetBySource.Count == trackedElements.Count)
                    {
                        break;
                    }
                }
            }
        }

        if (targetBySource.Count != trackedElements.Count)
        {
            target._compatibilityStyleStateTrackingEnabled = false;
            return;
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
                    CopyCompatibilityRawStyleStateTo(sourceList[i], target, targetElement);
                    if (target.GetOrCreateCompatibilityRawStyleState(targetElement).MarkStateCandidate())
                    {
                        targetList.Add(targetElement);
                    }
                }
                else if (target.GetOrCreateCompatibilityRawStyleState(targetElement).MarkRestoreCandidate())
                {
                    targetList.Add(targetElement);
                }
            }
        }

        void AddTrackedElements(List<SvgElement>? sourceList)
        {
            if (sourceList is null)
            {
                return;
            }

            for (var i = 0; i < sourceList.Count; i++)
            {
                trackedElements.Add(sourceList[i]);
            }
        }
    }

    private SvgCompatibilityElementStyleState GetOrCreateCompatibilityRawStyleState(SvgElement element)
    {
        _compatibilityRawStyleState ??= new Dictionary<SvgElement, SvgCompatibilityElementStyleState>(SvgElementReferenceComparer.Instance);
        if (!_compatibilityRawStyleState.TryGetValue(element, out var state))
        {
            state = new SvgCompatibilityElementStyleState();
            _compatibilityRawStyleState[element] = state;
        }

        return state;
    }

    private void CopyCompatibilityRawStyleStateTo(SvgElement sourceElement, SvgDocument target, SvgElement targetElement)
    {
        if (_compatibilityRawStyleState is null ||
            !_compatibilityRawStyleState.TryGetValue(sourceElement, out var sourceState))
        {
            target._compatibilityRawStyleState?.Remove(targetElement);
            return;
        }

        target._compatibilityRawStyleState ??= new Dictionary<SvgElement, SvgCompatibilityElementStyleState>(SvgElementReferenceComparer.Instance);
        target._compatibilityRawStyleState[targetElement] = sourceState.CloneRawPresentationState();
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
    private List<KeyValuePair<string, string>>? _presentationAttributes;

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
            if (IndexOfPresentationAttribute(name) < 0)
            {
                _presentationAttributes.Add(new KeyValuePair<string, string>(name, value!));
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

        _presentationAttributes = new List<KeyValuePair<string, string>>(2)
        {
            new(_presentationAttributeName, _presentationAttributeValue!)
        };
        _presentationAttributeName = null;
        _presentationAttributeValue = null;
        _presentationAttributes.Add(new KeyValuePair<string, string>(name, value!));
    }

    public void SetPresentationAttribute(string name, string value)
    {
        if (_presentationAttributes is not null)
        {
            var index = IndexOfPresentationAttribute(name);
            if (index >= 0)
            {
                _presentationAttributes[index] = new KeyValuePair<string, string>(_presentationAttributes[index].Key, value);
            }
            else
            {
                _presentationAttributes.Add(new KeyValuePair<string, string>(name, value));
            }

            return;
        }

        if (_presentationAttributeName is null ||
            string.Equals(_presentationAttributeName, name, StringComparison.OrdinalIgnoreCase))
        {
            _presentationAttributeName = name;
            _presentationAttributeValue = value;
            return;
        }

        _presentationAttributes = new List<KeyValuePair<string, string>>(2)
        {
            new(_presentationAttributeName, _presentationAttributeValue!),
            new(name, value)
        };
        _presentationAttributeName = null;
        _presentationAttributeValue = null;
    }

    public void RemovePresentationAttribute(string name)
    {
        if (_presentationAttributes is not null)
        {
            var index = IndexOfPresentationAttribute(name);
            if (index >= 0)
            {
                _presentationAttributes.RemoveAt(index);
            }

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
            : new List<KeyValuePair<string, string>>(_presentationAttributes);
        return clone;
    }

    private int IndexOfPresentationAttribute(string name)
    {
        if (_presentationAttributes is null)
        {
            return -1;
        }

        for (var i = 0; i < _presentationAttributes.Count; i++)
        {
            if (string.Equals(_presentationAttributes[i].Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
