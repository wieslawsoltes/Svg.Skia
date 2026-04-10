using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Linq;
using SkiaSharp;
using Svg.Transforms;

namespace Svg.Skia;

public sealed class SvgAnimationFrameChangedEventArgs : EventArgs
{
    internal SvgAnimationFrameChangedEventArgs(TimeSpan time)
    {
        Time = time;
    }

    public TimeSpan Time { get; }
}

[RequiresUnreferencedCode("Uses TypeDescriptor-based converters for animated SVG values.")]
public sealed class SvgAnimationController : IDisposable
{
    private readonly struct TimingSpec
    {
        public TimingSpec(TimeSpan offset)
        {
            IsEvent = false;
            Offset = offset;
            EventInstanceKey = null;
        }

        public TimingSpec(string eventInstanceKey, TimeSpan offset)
        {
            IsEvent = true;
            Offset = offset;
            EventInstanceKey = eventInstanceKey;
        }

        public bool IsEvent { get; }

        public TimeSpan Offset { get; }

        public string? EventInstanceKey { get; }
    }

    private readonly struct MotionSource
    {
        public MotionSource(string? pathData, IReadOnlyList<SKPoint>? points)
        {
            PathData = pathData;
            Points = points;
        }

        public string? PathData { get; }

        public IReadOnlyList<SKPoint>? Points { get; }
    }

    private readonly struct ResolvedTimingInstance
    {
        public ResolvedTimingInstance(TimeSpan time, string? eventInstanceKey, TimeSpan? sourceEventTime)
        {
            Time = time;
            EventInstanceKey = eventInstanceKey;
            SourceEventTime = sourceEventTime;
        }

        public TimeSpan Time { get; }

        public string? EventInstanceKey { get; }

        public TimeSpan? SourceEventTime { get; }
    }

    private sealed class PointerEventDependency
    {
        public PointerEventDependency(AnimationBinding binding, bool isBegin)
        {
            Binding = binding;
            IsBegin = isBegin;
        }

        public AnimationBinding Binding { get; }

        public bool IsBegin { get; }
    }

    private sealed class AnimationBinding
    {
        [RequiresUnreferencedCode("Calls Svg.Skia.SvgAnimationController.GetAttributeValue(SvgElement, String)")]
        public AnimationBinding(SvgAnimationElement animation, SvgElement sourceTarget, SvgElementAddress targetAddress, string attributeName)
        {
            Animation = animation;
            SourceTarget = sourceTarget;
            TargetAddress = targetAddress;
            AttributeName = attributeName;
            HasExplicitBaseAttribute = sourceTarget.ContainsAttribute(attributeName);
            BaseValue = GetAttributeValue(sourceTarget, attributeName);
            BaseValueString = ConvertAttributeValueToString(BaseValue);
            PropertyType = BaseValue?.GetType();
            TargetAttributeKey = string.Concat(targetAddress.Key, "|", attributeName);
            BeginSpecs = ParseTimingSpecifications(animation.Begin, animation.OwnerDocument, targetAddress, includeImplicitDocumentBegin: true);
            EndSpecs = ParseTimingSpecifications(animation.End, animation.OwnerDocument, targetAddress, includeImplicitDocumentBegin: false);
        }

        public SvgAnimationElement Animation { get; }

        public SvgElement SourceTarget { get; }

        public SvgElementAddress TargetAddress { get; }

        public string AttributeName { get; }

        public bool HasExplicitBaseAttribute { get; }

        public object? BaseValue { get; }

        public string? BaseValueString { get; }

        public Type? PropertyType { get; }

        public string TargetAttributeKey { get; }

        public IReadOnlyList<TimingSpec> BeginSpecs { get; }

        public IReadOnlyList<TimingSpec> EndSpecs { get; }

        private List<string>? _resolvedAnimationValues;
        private bool _resolvedAnimationValuesInitialized;
        private MotionSource _resolvedMotionSource;
        private bool _resolvedMotionSourceInitialized;

        public List<string> GetResolvedAnimationValues(Func<List<string>> valueFactory)
        {
            if (!_resolvedAnimationValuesInitialized)
            {
                _resolvedAnimationValues = valueFactory();
                _resolvedAnimationValuesInitialized = true;
            }

            return _resolvedAnimationValues ?? new List<string>();
        }

        public MotionSource GetResolvedMotionSource(Func<MotionSource> motionFactory)
        {
            if (!_resolvedMotionSourceInitialized)
            {
                _resolvedMotionSource = motionFactory();
                _resolvedMotionSourceInitialized = true;
            }

            return _resolvedMotionSource;
        }
    }

    private enum RepeatCountMode
    {
        DefaultOne,
        Finite,
        Indefinite
    }

    private enum RepeatDurationMode
    {
        None,
        Finite,
        Indefinite
    }

    private readonly struct AnimationSample
    {
        public AnimationSample(float progress, int iterationIndex)
        {
            Progress = progress;
            IterationIndex = iterationIndex;
        }

        public float Progress { get; }

        public int IterationIndex { get; }
    }

    private static readonly TypeConverter s_paintServerConverter = TypeDescriptor.GetConverter(typeof(SvgPaintServer));

    private readonly List<AnimationBinding> _bindings;
    private readonly Dictionary<string, AnimationBinding> _bindingsByTargetAttributeKey;
    private readonly Dictionary<string, List<TimeSpan>> _pointerEventInstances = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pointerEventDependencies;
    private readonly Dictionary<string, List<PointerEventDependency>> _pointerEventDependents;
    private readonly int[]? _animatedTopLevelChildIndexes;
    private SvgAnimationFrameState? _cachedFrameState;
    private int _frameStateVersion;
    private bool _disposed;

    public SvgAnimationController(SvgDocument sourceDocument)
    {
        SourceDocument = sourceDocument ?? throw new ArgumentNullException(nameof(sourceDocument));
        Clock = new SvgAnimationClock();
        Clock.TimeChanged += OnClockTimeChanged;
        _bindings = DiscoverBindings(sourceDocument);
        _bindingsByTargetAttributeKey = BuildBindingLookup(_bindings);
        _pointerEventDependencies = BuildPointerEventDependencies(_bindings);
        _pointerEventDependents = BuildPointerEventDependents(_bindings);
        _animatedTopLevelChildIndexes = DiscoverAnimatedTopLevelChildIndexes(sourceDocument, _bindings);
    }

    public SvgDocument SourceDocument { get; }

    public SvgAnimationClock Clock { get; }

    public bool HasAnimations => _bindings.Count > 0;

    public event EventHandler<SvgAnimationFrameChangedEventArgs>? FrameChanged;

    internal bool TryGetAnimatedTopLevelChildIndexes(out IReadOnlyList<int> childIndexes)
    {
        if (_animatedTopLevelChildIndexes is not { Length: > 0 })
        {
            childIndexes = Array.Empty<int>();
            return false;
        }

        childIndexes = _animatedTopLevelChildIndexes;
        return true;
    }

    internal bool HasDocumentRootAnimationTargets()
    {
        for (var i = 0; i < _bindings.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(_bindings[i].TargetAddress.Key))
            {
                return true;
            }
        }

        return false;
    }

    internal IReadOnlyList<string> GetAnimatedTargetAddressKeys()
    {
        if (_bindings.Count == 0)
        {
            return Array.Empty<string>();
        }

        var keys = new List<string>(_bindings.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var binding in _bindings)
        {
            var key = binding.TargetAddress.Key;
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
            {
                continue;
            }

            keys.Add(key);
        }

        return keys;
    }

    public SvgDocument CreateAnimatedDocument()
    {
        return CreateAnimatedDocument(EvaluateFrameState(Clock.CurrentTime));
    }

    public SvgDocument CreateAnimatedDocument(TimeSpan time)
    {
        ThrowIfDisposed();

        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        return CreateAnimatedDocument(EvaluateFrameState(time));
    }

    internal SvgDocument CreateAnimatedDocument(SvgAnimationFrameState frameState)
    {
        ThrowIfDisposed();

        var clone = SourceDocument.DeepCopy() as SvgDocument
            ?? throw new InvalidOperationException("Svg animation runtime requires SvgDocument.DeepCopy() to return SvgDocument.");

        if (_bindings.Count == 0)
        {
            return clone;
        }

        ApplyFrameState(clone, frameState, previousState: null);

        return clone;
    }

    internal SvgAnimationFrameState EvaluateFrameState(TimeSpan time)
    {
        ThrowIfDisposed();

        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        if (_cachedFrameState is { } cachedFrameState &&
            cachedFrameState.Time == time &&
            cachedFrameState.Version == _frameStateVersion)
        {
            return cachedFrameState;
        }

        var attributes = new Dictionary<string, SvgAnimationFrameAttributeState>(StringComparer.Ordinal);
        foreach (var binding in _bindings)
        {
            attributes.TryGetValue(binding.TargetAttributeKey, out var currentAttributeState);
            if (!TryResolveAnimatedAttributeValue(this, binding, time, currentAttributeState?.Value, out var value))
            {
                continue;
            }

            attributes[binding.TargetAttributeKey] = new SvgAnimationFrameAttributeState(
                binding.TargetAttributeKey,
                binding.TargetAddress,
                binding.AttributeName,
                value);
        }

        var frameState = new SvgAnimationFrameState(time, _frameStateVersion, attributes);
        _cachedFrameState = frameState;
        return frameState;
    }

    internal void ApplyFrameState(SvgDocument document, SvgAnimationFrameState frameState, SvgAnimationFrameState? previousState)
    {
        ThrowIfDisposed();

        foreach (var attribute in frameState.EnumerateDirtyAttributes(previousState))
        {
            var target = attribute.TargetAddress.Resolve(document);
            if (target is null)
            {
                continue;
            }

            _ = SetAttributeValue(target, attribute.AttributeName, attribute.Value);
        }

        foreach (var removedKey in frameState.EnumerateRemovedKeys(previousState))
        {
            if (!_bindingsByTargetAttributeKey.TryGetValue(removedKey, out var binding))
            {
                continue;
            }

            var target = binding.TargetAddress.Resolve(document);
            if (target is null)
            {
                continue;
            }

            if (binding.BaseValueString is not null)
            {
                _ = SetAttributeValue(target, binding.AttributeName, binding.BaseValueString);

                if (!binding.HasExplicitBaseAttribute)
                {
                    _ = ClearAttributeValue(target, binding.AttributeName);
                }

                continue;
            }

            _ = ClearAttributeValue(target, binding.AttributeName);
        }
    }

    public bool RecordPointerEvent(SvgElement? element, SvgPointerEventType eventType)
    {
        ThrowIfDisposed();

        if (!HasAnimations || element is null)
        {
            return false;
        }

        var key = CreateEventInstanceKey(SvgElementAddress.Create(element), eventType);
        if (!_pointerEventDependencies.Contains(key))
        {
            return false;
        }

        if (!_pointerEventInstances.TryGetValue(key, out var eventTimes))
        {
            eventTimes = new List<TimeSpan>();
            _pointerEventInstances[key] = eventTimes;
        }

        eventTimes.Add(Clock.CurrentTime);
        PrunePointerEventInstances(key);
        InvalidateFrameStateCache();
        return true;
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _pointerEventInstances.Clear();
        InvalidateFrameStateCache();
        var currentTime = Clock.CurrentTime;
        Clock.Reset();

        if (currentTime == TimeSpan.Zero && HasAnimations)
        {
            FrameChanged?.Invoke(this, new SvgAnimationFrameChangedEventArgs(TimeSpan.Zero));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clock.TimeChanged -= OnClockTimeChanged;
        _disposed = true;
    }

    private void OnClockTimeChanged(object? sender, SvgAnimationClockChangedEventArgs e)
    {
        if (_disposed || !HasAnimations)
        {
            return;
        }

        FrameChanged?.Invoke(this, new SvgAnimationFrameChangedEventArgs(e.Time));
    }

    private static HashSet<string> BuildPointerEventDependencies(IEnumerable<AnimationBinding> bindings)
    {
        var dependencies = new HashSet<string>(StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            AddEventDependencies(binding.BeginSpecs, dependencies);
            AddEventDependencies(binding.EndSpecs, dependencies);
        }

        return dependencies;
    }

    private static Dictionary<string, List<PointerEventDependency>> BuildPointerEventDependents(IEnumerable<AnimationBinding> bindings)
    {
        var dependents = new Dictionary<string, List<PointerEventDependency>>(StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            AddPointerEventDependents(binding, binding.BeginSpecs, isBegin: true, dependents);
            AddPointerEventDependents(binding, binding.EndSpecs, isBegin: false, dependents);
        }

        return dependents;
    }

    private static void AddPointerEventDependents(
        AnimationBinding binding,
        IEnumerable<TimingSpec> specs,
        bool isBegin,
        Dictionary<string, List<PointerEventDependency>> dependents)
    {
        foreach (var spec in specs)
        {
            if (!spec.IsEvent || spec.EventInstanceKey is not { Length: > 0 } eventInstanceKey)
            {
                continue;
            }

            if (!dependents.TryGetValue(eventInstanceKey, out var bindingsForKey))
            {
                bindingsForKey = new List<PointerEventDependency>();
                dependents[eventInstanceKey] = bindingsForKey;
            }

            if (bindingsForKey.Any(existing => ReferenceEquals(existing.Binding, binding) && existing.IsBegin == isBegin))
            {
                continue;
            }

            bindingsForKey.Add(new PointerEventDependency(binding, isBegin));
        }
    }

    private static Dictionary<string, AnimationBinding> BuildBindingLookup(IEnumerable<AnimationBinding> bindings)
    {
        var lookup = new Dictionary<string, AnimationBinding>(StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            if (!lookup.ContainsKey(binding.TargetAttributeKey))
            {
                lookup.Add(binding.TargetAttributeKey, binding);
            }
        }

        return lookup;
    }

    private static int[]? DiscoverAnimatedTopLevelChildIndexes(SvgDocument sourceDocument, IEnumerable<AnimationBinding> bindings)
    {
        var indexes = new SortedSet<int>();

        foreach (var binding in bindings)
        {
            var current = binding.SourceTarget;
            while (current.Parent is SvgElement parent && parent is not SvgDocument)
            {
                current = parent;
            }

            if (current.Parent is not SvgDocument)
            {
                return null;
            }

            var childIndex = sourceDocument.Children.IndexOf(current);
            if (childIndex < 0)
            {
                return null;
            }

            indexes.Add(childIndex);
        }

        return indexes.Count > 0 ? indexes.ToArray() : null;
    }

    private static void AddEventDependencies(IEnumerable<TimingSpec> specs, HashSet<string> dependencies)
    {
        foreach (var spec in specs)
        {
            if (spec.IsEvent && spec.EventInstanceKey is { Length: > 0 } eventInstanceKey)
            {
                dependencies.Add(eventInstanceKey);
            }
        }
    }

    private static List<AnimationBinding> DiscoverBindings(SvgDocument sourceDocument)
    {
        var bindings = new List<AnimationBinding>();

        foreach (var animation in sourceDocument.Descendants().OfType<SvgAnimationElement>())
        {
            var target = animation.TargetElement;
            var attributeName = ResolveAttributeName(animation);

            if (target is null || string.IsNullOrWhiteSpace(attributeName))
            {
                continue;
            }

            bindings.Add(new AnimationBinding(animation, target, SvgElementAddress.Create(target), attributeName!));
        }

        return bindings;
    }

    private void InvalidateFrameStateCache()
    {
        _frameStateVersion++;
        _cachedFrameState = null;
    }

    private static List<TimingSpec> ParseTimingSpecifications(string? value, SvgDocument? document, SvgElementAddress defaultEventAddress, bool includeImplicitDocumentBegin)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return includeImplicitDocumentBegin
                ? new List<TimingSpec> { new(TimeSpan.Zero) }
                : new List<TimingSpec>();
        }

        var specs = new List<TimingSpec>();
        foreach (var token in SvgAnimationParser.SplitSemicolonList(value))
        {
            if (SvgAnimationParser.TryParseClockValue(token, out var clockOffset))
            {
                specs.Add(new TimingSpec(clockOffset));
                continue;
            }

            if (TryParseEventTimingSpec(token, document, defaultEventAddress, out var eventTimingSpec))
            {
                specs.Add(eventTimingSpec);
            }
        }

        return specs;
    }

    private void PrunePointerEventInstances(string key)
    {
        if (!_pointerEventInstances.TryGetValue(key, out var eventTimes) ||
            eventTimes.Count <= 1)
        {
            return;
        }

        if (!_pointerEventDependents.TryGetValue(key, out var dependents) ||
            dependents.Count == 0)
        {
            eventTimes.Clear();
            return;
        }

        eventTimes.Sort();

        var currentTime = Clock.CurrentTime;
        var relevantEventTimes = new HashSet<long>();

        foreach (var dependent in dependents)
        {
            CollectRelevantEventTimesForBinding(key, dependent.Binding, currentTime, relevantEventTimes);
        }

        eventTimes.RemoveAll(eventTime => !relevantEventTimes.Contains(eventTime.Ticks));
    }

    private void CollectRelevantEventTimesForBinding(
        string key,
        AnimationBinding binding,
        TimeSpan currentTime,
        HashSet<long> relevantEventTimes)
    {
        var beginInstances = ResolveTimingInstancesDetailed(binding.BeginSpecs);
        var endInstances = ResolveTimingInstancesDetailed(binding.EndSpecs);

        PreserveFutureEventInstances(key, beginInstances, currentTime, relevantEventTimes);
        PreserveFutureEventInstances(key, endInstances, currentTime, relevantEventTimes);

        var allowIndefiniteDiscrete = binding.Animation is SvgSet;
        if (!TryResolveCurrentIntervalDetailed(binding.Animation, currentTime, allowIndefiniteDiscrete, beginInstances, endInstances, out var interval))
        {
            return;
        }

        if (interval.BeginInstance.EventInstanceKey == key && interval.BeginInstance.SourceEventTime.HasValue)
        {
            relevantEventTimes.Add(interval.BeginInstance.SourceEventTime.Value.Ticks);
        }

        if (interval.EndInstance is { EventInstanceKey: var endKey, SourceEventTime: { } endSourceTime } &&
            endKey == key)
        {
            relevantEventTimes.Add(endSourceTime.Ticks);
        }
    }

    private static void PreserveFutureEventInstances(
        string key,
        IReadOnlyList<ResolvedTimingInstance> instances,
        TimeSpan currentTime,
        HashSet<long> relevantEventTimes)
    {
        for (var index = 0; index < instances.Count; index++)
        {
            var instance = instances[index];
            if (instance.Time <= currentTime ||
                instance.EventInstanceKey != key ||
                !instance.SourceEventTime.HasValue)
            {
                continue;
            }

            relevantEventTimes.Add(instance.SourceEventTime.Value.Ticks);
        }
    }

    private static bool TryParseEventTimingSpec(string value, SvgDocument? document, SvgElementAddress defaultEventAddress, out TimingSpec spec)
    {
        spec = default;

        if (!SvgAnimationParser.TryParseEventTimingSpec(value, document, defaultEventAddress, out var parsedTiming))
        {
            return false;
        }

        spec = new TimingSpec(CreateEventInstanceKey(parsedTiming.EventAddress, parsedTiming.EventType), parsedTiming.Offset);
        return true;
    }

    private static string? ResolveAttributeName(SvgAnimationElement animation)
    {
        if (animation is SvgAnimateMotion)
        {
            return "transform";
        }

        if (animation is SvgAnimateTransform animateTransform)
        {
            return string.IsNullOrWhiteSpace(animateTransform.AnimationAttributeName)
                ? "transform"
                : animateTransform.AnimationAttributeName;
        }

        if (animation is SvgAnimationAttributeElement attributeAnimation)
        {
            return attributeAnimation.AnimationAttributeName;
        }

        return null;
    }

    private static bool TryResolveAnimatedAttributeValue(
        SvgAnimationController controller,
        AnimationBinding binding,
        TimeSpan time,
        string? currentComposedValue,
        out string value)
    {
        value = string.Empty;

        switch (binding.Animation)
        {
            case SvgSet svgSet:
                if (!TryGetSetSample(controller, binding, svgSet, time) ||
                    !SvgAnimationParser.TryGetTrimmedString(svgSet.To, out var setValue))
                {
                    return false;
                }

                value = setValue;
                return true;
            case SvgAnimateMotion animateMotion:
                if (!TryGetAnimationSample(controller, binding, animateMotion, time, allowIndefiniteDiscrete: false, out var motionSample) ||
                    !TryResolveMotionValue(binding, animateMotion, motionSample, out value))
                {
                    return false;
                }

                if (animateMotion.Additive == SvgAnimationAdditive.Sum)
                {
                    value = CombineTransformValue(ResolveCurrentComposedBaseValue(currentComposedValue, binding.BaseValueString), value);
                }

                return true;
            case SvgAnimateTransform animateTransform:
                if (!TryGetAnimationSample(controller, binding, animateTransform, time, allowIndefiniteDiscrete: false, out var transformSample) ||
                    !TryResolveTransformValue(binding, animateTransform, transformSample, out value))
                {
                    return false;
                }

                if (animateTransform.Additive == SvgAnimationAdditive.Sum)
                {
                    value = CombineTransformValue(ResolveCurrentComposedBaseValue(currentComposedValue, binding.BaseValueString), value);
                }

                return true;
            case SvgAnimateColor animateColor:
                if (!TryGetAnimationSample(controller, binding, animateColor, time, allowIndefiniteDiscrete: false, out var colorSample) ||
                    !TryResolveAnimatedValue(binding, animateColor, colorSample, forceColorInterpolation: true, out value))
                {
                    return false;
                }

                if (animateColor.Additive == SvgAnimationAdditive.Sum &&
                    TryResolveAdditiveBaseValue(binding, animateColor, currentComposedValue, out var additiveColorBase) &&
                    TryAddValue(binding, additiveColorBase, value, out var additiveColorValue))
                {
                    value = additiveColorValue;
                }

                return true;
            case SvgAnimate animate:
                if (!TryGetAnimationSample(controller, binding, animate, time, allowIndefiniteDiscrete: false, out var valueSample) ||
                    !TryResolveAnimatedValue(binding, animate, valueSample, forceColorInterpolation: false, out value))
                {
                    return false;
                }

                if (animate.Additive == SvgAnimationAdditive.Sum &&
                    TryResolveAdditiveBaseValue(binding, animate, currentComposedValue, out var additiveValueBase) &&
                    TryAddValue(binding, additiveValueBase, value, out var additiveValue))
                {
                    value = additiveValue;
                }

                return true;
            default:
                return false;
        }
    }

    private static void ApplyAnimation(SvgAnimationController controller, AnimationBinding binding, SvgDocument document, TimeSpan time)
    {
        var target = binding.TargetAddress.Resolve(document);
        if (target is null)
        {
            return;
        }

        switch (binding.Animation)
        {
            case SvgSet svgSet:
                ApplySet(controller, binding, target, svgSet, time);
                break;
            case SvgAnimateMotion animateMotion:
                ApplyAnimateMotion(controller, binding, target, animateMotion, time);
                break;
            case SvgAnimateTransform animateTransform:
                ApplyAnimateTransform(controller, binding, target, animateTransform, time);
                break;
            case SvgAnimateColor animateColor:
                ApplyAnimateValue(controller, binding, target, animateColor, time, forceColorInterpolation: true);
                break;
            case SvgAnimate animate:
                ApplyAnimateValue(controller, binding, target, animate, time, forceColorInterpolation: false);
                break;
        }
    }

    private static void ApplySet(SvgAnimationController controller, AnimationBinding binding, SvgElement target, SvgSet animation, TimeSpan time)
    {
        if (!TryGetSetSample(controller, binding, animation, time))
        {
            return;
        }

        if (!SvgAnimationParser.TryGetTrimmedString(animation.To, out var setValue))
        {
            return;
        }

        _ = SetAttributeValue(target, binding.AttributeName, setValue);
    }

    private static void ApplyAnimateValue(SvgAnimationController controller, AnimationBinding binding, SvgElement target, SvgAnimationValueElement animation, TimeSpan time, bool forceColorInterpolation)
    {
        if (!TryGetAnimationSample(controller, binding, animation, time, allowIndefiniteDiscrete: false, out var sample))
        {
            return;
        }

        if (!TryResolveAnimatedValue(binding, animation, sample, forceColorInterpolation, out var value))
        {
            return;
        }

        if (animation.Additive == SvgAnimationAdditive.Sum &&
            TryResolveAdditiveBaseValue(
                binding,
                animation,
                ConvertAttributeValueToString(GetAttributeValue(target, binding.AttributeName)),
                out var baseValue) &&
            TryAddValue(binding, baseValue, value, out var additiveValue))
        {
            value = additiveValue;
        }

        _ = SetAttributeValue(target, binding.AttributeName, value);
    }

    private static void ApplyAnimateTransform(SvgAnimationController controller, AnimationBinding binding, SvgElement target, SvgAnimateTransform animation, TimeSpan time)
    {
        if (!TryGetAnimationSample(controller, binding, animation, time, allowIndefiniteDiscrete: false, out var sample))
        {
            return;
        }

        if (!TryResolveTransformValue(binding, animation, sample, out var transformValue))
        {
            return;
        }

        if (animation.Additive == SvgAnimationAdditive.Sum)
        {
            transformValue = CombineTransformValue(
                ResolveCurrentComposedBaseValue(
                    ConvertAttributeValueToString(GetAttributeValue(target, binding.AttributeName)),
                    binding.BaseValueString),
                transformValue);
        }

        _ = SetAttributeValue(target, binding.AttributeName, transformValue);
    }

    private static void ApplyAnimateMotion(SvgAnimationController controller, AnimationBinding binding, SvgElement target, SvgAnimateMotion animation, TimeSpan time)
    {
        if (!TryGetAnimationSample(controller, binding, animation, time, allowIndefiniteDiscrete: false, out var sample))
        {
            return;
        }

        if (!TryResolveMotionValue(binding, animation, sample, out var transformValue))
        {
            return;
        }

        if (animation.Additive == SvgAnimationAdditive.Sum)
        {
            transformValue = CombineTransformValue(
                ResolveCurrentComposedBaseValue(
                    ConvertAttributeValueToString(GetAttributeValue(target, binding.AttributeName)),
                    binding.BaseValueString),
                transformValue);
        }

        _ = SetAttributeValue(target, binding.AttributeName, transformValue);
    }

    private static bool TryGetSetSample(SvgAnimationController controller, AnimationBinding binding, SvgAnimationElement animation, TimeSpan time)
    {
        if (!TryResolveCurrentInterval(controller, binding, animation, time, allowIndefiniteDiscrete: true, out var interval))
        {
            return false;
        }

        return interval.IsActive || (interval.IsFrozen && animation.AnimationFill == SvgAnimationFill.Freeze);
    }

    private readonly struct AnimationInterval
    {
        public AnimationInterval(TimeSpan begin, TimeSpan? activeEnd)
        {
            Begin = begin;
            ActiveEnd = activeEnd;
        }

        public TimeSpan Begin { get; }

        public TimeSpan? ActiveEnd { get; }

        public bool IsActive(TimeSpan time) => !ActiveEnd.HasValue || time <= ActiveEnd.Value;
    }

    private readonly struct ResolvedAnimationInterval
    {
        public ResolvedAnimationInterval(ResolvedTimingInstance beginInstance, TimeSpan? activeEnd, ResolvedTimingInstance? endInstance)
        {
            BeginInstance = beginInstance;
            ActiveEnd = activeEnd;
            EndInstance = endInstance;
        }

        public ResolvedTimingInstance BeginInstance { get; }

        public TimeSpan? ActiveEnd { get; }

        public ResolvedTimingInstance? EndInstance { get; }

        public bool IsActive(TimeSpan time) => !ActiveEnd.HasValue || time <= ActiveEnd.Value;
    }

    private static bool TryGetAnimationSample(SvgAnimationController controller, AnimationBinding binding, SvgAnimationElement animation, TimeSpan time, bool allowIndefiniteDiscrete, out AnimationSample sample)
    {
        sample = default;

        if (!TryResolveCurrentInterval(controller, binding, animation, time, allowIndefiniteDiscrete, out var interval))
        {
            return false;
        }

        var hasDuration = TryParseClockValue(animation.Duration, out var duration);
        if (!hasDuration)
        {
            if (!allowIndefiniteDiscrete)
            {
                return false;
            }

            sample = new AnimationSample(1f, 0);
            return true;
        }

        if (duration < TimeSpan.Zero)
        {
            return false;
        }

        if (duration == TimeSpan.Zero)
        {
            sample = new AnimationSample(1f, 0);
            return true;
        }

        var elapsed = interval.IsFrozen && interval.ActiveEnd.HasValue
            ? interval.ActiveEnd.Value - interval.Begin
            : time - interval.Begin;

        sample = CreateSampleAtElapsed(duration, elapsed);
        return true;
    }

    private static RepeatCountMode ParseRepeatCount(string? value, out double repeatCount)
    {
        repeatCount = 1d;

        if (!SvgAnimationParser.TryGetFirstSemicolonSegment(value, out var trimmed))
        {
            return RepeatCountMode.DefaultOne;
        }

        if (SvgAnimationParser.EqualsKeywordIgnoreCase(trimmed.AsSpan(), "indefinite"))
        {
            return RepeatCountMode.Indefinite;
        }

        if (SvgAnimationParser.TryParseInvariantDouble(trimmed.AsSpan(), out repeatCount))
        {
            repeatCount = Math.Max(0d, repeatCount);
            return RepeatCountMode.Finite;
        }

        repeatCount = 1d;
        return RepeatCountMode.DefaultOne;
    }

    private static AnimationSample CreateSampleAtElapsed(TimeSpan simpleDuration, TimeSpan elapsed)
    {
        if (simpleDuration <= TimeSpan.Zero)
        {
            return new AnimationSample(1f, 0);
        }

        if (elapsed <= TimeSpan.Zero)
        {
            return new AnimationSample(0f, 0);
        }

        var elapsedTicks = elapsed.Ticks;
        var durationTicks = simpleDuration.Ticks;
        if (durationTicks <= 0)
        {
            return new AnimationSample(1f, 0);
        }

        var iterationIndex = ClampIterationIndex(elapsedTicks / durationTicks);
        var localTicks = elapsedTicks % durationTicks;
        if (localTicks == 0 && elapsedTicks > 0)
        {
            return new AnimationSample(1f, Math.Max(0, iterationIndex - 1));
        }

        var progress = (float)localTicks / durationTicks;
        return new AnimationSample(Clamp01(progress), iterationIndex);
    }

    private static bool TryResolveCurrentInterval(SvgAnimationController controller, AnimationBinding binding, SvgAnimationElement animation, TimeSpan time, bool allowIndefiniteDiscrete, out (bool IsActive, bool IsFrozen, TimeSpan Begin, TimeSpan? ActiveEnd) interval)
    {
        interval = default;

        var beginInstances = controller.ResolveTimingInstances(binding.BeginSpecs);
        if (beginInstances.Count == 0)
        {
            return false;
        }

        var endInstances = controller.ResolveTimingInstances(binding.EndSpecs);
        var selected = default(AnimationInterval?);

        foreach (var begin in beginInstances)
        {
            if (begin > time)
            {
                break;
            }

            if (selected.HasValue)
            {
                switch (animation.Restart)
                {
                    case SvgAnimationRestart.Never:
                        continue;
                    case SvgAnimationRestart.WhenNotActive:
                        if (!selected.Value.ActiveEnd.HasValue || begin < selected.Value.ActiveEnd.Value)
                        {
                            continue;
                        }
                        break;
                }
            }

            if (!TryResolveIntervalEnd(animation, begin, endInstances, allowIndefiniteDiscrete, out var activeEnd))
            {
                continue;
            }

            selected = new AnimationInterval(begin, activeEnd);
        }

        if (!selected.HasValue)
        {
            return false;
        }

        var resolved = selected.Value;
        if (resolved.IsActive(time))
        {
            interval = (true, false, resolved.Begin, resolved.ActiveEnd);
            return true;
        }

        if (animation.AnimationFill == SvgAnimationFill.Freeze)
        {
            interval = (false, true, resolved.Begin, resolved.ActiveEnd);
            return true;
        }

        return false;
    }

    private List<TimeSpan> ResolveTimingInstances(IReadOnlyList<TimingSpec> specs)
    {
        return ResolveTimingInstancesDetailed(specs)
            .Select(static instance => instance.Time)
            .ToList();
    }

    private List<ResolvedTimingInstance> ResolveTimingInstancesDetailed(IReadOnlyList<TimingSpec> specs)
    {
        var instances = new List<ResolvedTimingInstance>();

        foreach (var spec in specs)
        {
            if (spec.IsEvent)
            {
                if (spec.EventInstanceKey is null ||
                    !_pointerEventInstances.TryGetValue(spec.EventInstanceKey, out var eventTimes))
                {
                    continue;
                }

                instances.AddRange(eventTimes.Select(eventTime => new ResolvedTimingInstance(
                    eventTime + spec.Offset,
                    spec.EventInstanceKey,
                    eventTime)));
                continue;
            }

            instances.Add(new ResolvedTimingInstance(spec.Offset, eventInstanceKey: null, sourceEventTime: null));
        }

        instances.Sort(static (left, right) => left.Time.CompareTo(right.Time));
        return instances;
    }

    private static bool TryResolveCurrentIntervalDetailed(
        SvgAnimationElement animation,
        TimeSpan time,
        bool allowIndefiniteDiscrete,
        IReadOnlyList<ResolvedTimingInstance> beginInstances,
        IReadOnlyList<ResolvedTimingInstance> endInstances,
        out ResolvedAnimationInterval interval)
    {
        interval = default;

        if (beginInstances.Count == 0)
        {
            return false;
        }

        ResolvedAnimationInterval? selected = null;

        for (var index = 0; index < beginInstances.Count; index++)
        {
            var begin = beginInstances[index];
            if (begin.Time > time)
            {
                break;
            }

            if (selected.HasValue)
            {
                switch (animation.Restart)
                {
                    case SvgAnimationRestart.Never:
                        continue;
                    case SvgAnimationRestart.WhenNotActive:
                        if (!selected.Value.ActiveEnd.HasValue || begin.Time < selected.Value.ActiveEnd.Value)
                        {
                            continue;
                        }

                        break;
                }
            }

            if (!TryResolveIntervalEndDetailed(animation, begin.Time, endInstances, allowIndefiniteDiscrete, out var activeEnd, out var endInstance))
            {
                continue;
            }

            selected = new ResolvedAnimationInterval(begin, activeEnd, endInstance);
        }

        if (!selected.HasValue)
        {
            return false;
        }

        var resolved = selected.Value;
        if (resolved.IsActive(time) || animation.AnimationFill == SvgAnimationFill.Freeze)
        {
            interval = resolved;
            return true;
        }

        return false;
    }

    private static bool TryResolveIntervalEnd(SvgAnimationElement animation, TimeSpan begin, IReadOnlyList<TimeSpan> endInstances, bool allowIndefiniteDiscrete, out TimeSpan? activeEnd)
    {
        activeEnd = null;

        TimeSpan? explicitEnd = null;
        foreach (var endInstance in endInstances)
        {
            if (endInstance > begin)
            {
                explicitEnd = endInstance;
                break;
            }
        }

        var hasDuration = TryParseClockValue(animation.Duration, out var duration);
        if (!hasDuration)
        {
            if (!allowIndefiniteDiscrete)
            {
                return false;
            }

            activeEnd = explicitEnd;
            return true;
        }

        if (duration < TimeSpan.Zero)
        {
            return false;
        }

        activeEnd = ComputeActiveEnd(animation, begin, duration, explicitEnd);
        return true;
    }

    private static bool TryResolveIntervalEndDetailed(
        SvgAnimationElement animation,
        TimeSpan begin,
        IReadOnlyList<ResolvedTimingInstance> endInstances,
        bool allowIndefiniteDiscrete,
        out TimeSpan? activeEnd,
        out ResolvedTimingInstance? endInstance)
    {
        activeEnd = null;
        endInstance = null;

        TimeSpan? explicitEnd = null;
        for (var index = 0; index < endInstances.Count; index++)
        {
            var candidate = endInstances[index];
            if (candidate.Time <= begin)
            {
                continue;
            }

            explicitEnd = candidate.Time;
            endInstance = candidate;
            break;
        }

        var hasDuration = TryParseClockValue(animation.Duration, out var duration);
        if (!hasDuration)
        {
            if (!allowIndefiniteDiscrete)
            {
                return false;
            }

            activeEnd = explicitEnd;
            return true;
        }

        if (duration < TimeSpan.Zero)
        {
            return false;
        }

        activeEnd = ComputeActiveEnd(animation, begin, duration, explicitEnd);
        return true;
    }

    private static TimeSpan? ComputeActiveEnd(SvgAnimationElement animation, TimeSpan begin, TimeSpan simpleDuration, TimeSpan? explicitEnd)
    {
        var totalDuration = ComputeTotalDuration(animation, simpleDuration, explicitEnd, begin);
        return totalDuration.HasValue
            ? begin + totalDuration.Value
            : null;
    }

    private static TimeSpan? ComputeTotalDuration(SvgAnimationElement animation, TimeSpan simpleDuration, TimeSpan? explicitEnd, TimeSpan begin)
    {
        TimeSpan? totalDuration;
        var repeatCountMode = ParseRepeatCount(animation.RepeatCount, out var repeatCount);

        switch (repeatCountMode)
        {
            case RepeatCountMode.Indefinite:
                totalDuration = null;
                break;
            case RepeatCountMode.Finite:
                totalDuration = Multiply(simpleDuration, repeatCount);
                break;
            default:
                totalDuration = simpleDuration;
                break;
        }

        switch (ParseRepeatDuration(animation.RepeatDuration, out var repeatDuration))
        {
            case RepeatDurationMode.Indefinite:
                if (repeatCountMode != RepeatCountMode.Finite)
                {
                    totalDuration = null;
                }

                break;
            case RepeatDurationMode.Finite:
                totalDuration = MinDuration(totalDuration, repeatDuration);
                break;
        }

        if (explicitEnd.HasValue && explicitEnd.Value > begin)
        {
            totalDuration = MinDuration(totalDuration, explicitEnd.Value - begin);
        }

        switch (ParseRepeatDuration(animation.Minimum, out var minimumDuration))
        {
            case RepeatDurationMode.Finite:
                totalDuration = MaxDuration(totalDuration, minimumDuration);
                break;
        }

        switch (ParseRepeatDuration(animation.Maximum, out var maximumDuration))
        {
            case RepeatDurationMode.Finite:
                totalDuration = MinDuration(totalDuration, maximumDuration);
                break;
        }

        return totalDuration;
    }

    private static bool TryParseClockValue(string? value, out TimeSpan result)
    {
        return SvgAnimationParser.TryParseClockValue(value, out result);
    }

    private static RepeatDurationMode ParseRepeatDuration(string? value, out TimeSpan repeatDuration)
    {
        repeatDuration = default;

        if (!SvgAnimationParser.TryGetTrimmedString(value, out var trimmed))
        {
            return RepeatDurationMode.None;
        }

        if (SvgAnimationParser.EqualsKeywordIgnoreCase(trimmed.AsSpan(), "indefinite"))
        {
            return RepeatDurationMode.Indefinite;
        }

        return TryParseClockValue(trimmed, out repeatDuration) && repeatDuration >= TimeSpan.Zero
            ? RepeatDurationMode.Finite
            : RepeatDurationMode.None;
    }

    private static bool TryResolveAnimatedValue(AnimationBinding binding, SvgAnimationValueElement animation, AnimationSample sample, bool forceColorInterpolation, out string value)
    {
        value = string.Empty;

        var values = ResolveAnimationValues(binding, animation);
        if (values.Count == 0)
        {
            return false;
        }

        if (values.Count == 1)
        {
            value = values[0];
            return TryApplyAccumulation(binding, animation, values, sample, forceColorInterpolation, ref value);
        }

        if (animation.CalcMode == SvgAnimationCalcMode.Discrete)
        {
            value = ResolveDiscreteValue(values, animation.KeyTimes, sample.Progress);
            return TryApplyAccumulation(binding, animation, values, sample, forceColorInterpolation, ref value);
        }

        ResolveInterpolatedSegment(
            values.Count,
            animation.KeyTimes,
            animation.KeySplines,
            animation.CalcMode,
            sample.Progress,
            animation.CalcMode == SvgAnimationCalcMode.Paced
                ? ResolvePacedSegmentLengths(binding, values, forceColorInterpolation)
                : null,
            out var startIndex,
            out var endIndex,
            out var localProgress);
        var fromValue = values[startIndex];
        var toValue = values[endIndex];

        if (TryInterpolateValue(binding, fromValue, toValue, localProgress, forceColorInterpolation, out value))
        {
            return TryApplyAccumulation(binding, animation, values, sample, forceColorInterpolation, ref value);
        }

        value = localProgress >= 1f ? toValue : fromValue;
        return TryApplyAccumulation(binding, animation, values, sample, forceColorInterpolation, ref value);
    }

    private static bool TryResolveTransformValue(AnimationBinding binding, SvgAnimateTransform animation, AnimationSample sample, out string transformValue)
    {
        transformValue = string.Empty;

        var values = ResolveAnimationValues(binding, animation);
        if (values.Count == 0)
        {
            return false;
        }

        if (values.Count == 1 || animation.CalcMode == SvgAnimationCalcMode.Discrete)
        {
            var discrete = values.Count == 1
                ? values[0]
                : ResolveDiscreteValue(values, animation.KeyTimes, sample.Progress);

            var discreteValues = ParseTransformNumbers(discrete);
            if (!TryApplyTransformAccumulation(binding, animation, values, sample, ref discreteValues))
            {
                return false;
            }

            return TryCreateTransformString(animation.TransformType, discreteValues, out transformValue);
        }

        ResolveInterpolatedSegment(
            values.Count,
            animation.KeyTimes,
            animation.KeySplines,
            animation.CalcMode,
            sample.Progress,
            animation.CalcMode == SvgAnimationCalcMode.Paced
                ? ResolveTransformPacedSegmentLengths(animation.TransformType, values)
                : null,
            out var startIndex,
            out var endIndex,
            out var localProgress);
        var fromValues = ParseTransformNumbers(values[startIndex]);
        var toValues = ParseTransformNumbers(values[endIndex]);
        var interpolated = InterpolateTransformNumbers(animation.TransformType, fromValues, toValues, localProgress);
        if (!TryApplyTransformAccumulation(binding, animation, values, sample, ref interpolated))
        {
            return false;
        }

        return TryCreateTransformString(animation.TransformType, interpolated, out transformValue);
    }

    private static bool TryResolveMotionValue(AnimationBinding binding, SvgAnimateMotion animation, AnimationSample sample, out string transformValue)
    {
        transformValue = string.Empty;

        var motionSource = ResolveMotionSource(binding, animation);
        if (string.IsNullOrWhiteSpace(motionSource.PathData) &&
            motionSource.Points is not { Count: > 0 })
        {
            return false;
        }

        var motionPosition = TryResolveMotionPoint(animation, sample, motionSource, out var position, out var tangent);
        if (!motionPosition)
        {
            return false;
        }

        if (animation.Accumulate == SvgAnimationAccumulate.Sum &&
            sample.IterationIndex > 0 &&
            TryResolveMotionAccumulationOffset(animation, motionSource, out var accumulatedOffset))
        {
            position = new SKPoint(
                position.X + (accumulatedOffset.X * sample.IterationIndex),
                position.Y + (accumulatedOffset.Y * sample.IterationIndex));
        }

        var transforms = new List<SvgTransform>
        {
            new SvgTranslate(position.X, position.Y)
        };

        if (TryResolveMotionRotation(animation.Rotate, tangent, out var angle))
        {
            transforms.Add(new SvgRotate(angle));
        }

        transformValue = string.Join(" ", transforms.Select(static transform => transform.ToString()));
        return true;
    }

    private static MotionSource ResolveMotionSource(AnimationBinding binding, SvgAnimateMotion animation)
    {
        return binding.GetResolvedMotionSource(() =>
        {
            if (animation.Children.OfType<SvgMPath>().FirstOrDefault()?.TargetPath?.PathData is { Count: > 0 } mpathData)
            {
                return new MotionSource(mpathData.ToString(), points: null);
            }

            if (animation.PathData is { Count: > 0 })
            {
                return new MotionSource(animation.PathData.ToString(), points: null);
            }

            var points = ResolveMotionCoordinatePoints(binding, animation);
            if (points.Count == 0)
            {
                return default;
            }

            return new MotionSource(CreateMotionPathData(points), points);
        });
    }

    private static float ResolveMotionProgress(SvgAnimateMotion animation, float progress)
    {
        if (animation.KeyPoints is not { Count: > 0 })
        {
            return progress;
        }

        if (animation.KeyPoints.Count == 1)
        {
            return animation.KeyPoints[0];
        }

        if (animation.CalcMode == SvgAnimationCalcMode.Discrete)
        {
            return ParseMotionDiscreteProgress(animation.KeyPoints, animation.KeyTimes, progress);
        }

        ResolveInterpolatedSegment(
            animation.KeyPoints.Count,
            animation.KeyTimes,
            animation.KeySplines,
            animation.CalcMode,
            progress,
            animation.CalcMode == SvgAnimationCalcMode.Paced
                ? ResolveScalarPacedSegmentLengths(animation.KeyPoints)
                : null,
            out var startIndex,
            out var endIndex,
            out var localProgress);
        return Lerp(animation.KeyPoints[startIndex], animation.KeyPoints[endIndex], localProgress);
    }

    private static bool TryResolveMotionPoint(SvgAnimateMotion animation, AnimationSample sample, MotionSource motionSource, out SKPoint position, out SKPoint tangent)
    {
        position = default;
        tangent = new SKPoint(1f, 0f);

        if (animation.KeyPoints is { Count: > 0 } || animation.CalcMode == SvgAnimationCalcMode.Paced || motionSource.Points is not { Count: > 1 })
        {
            return TryResolveMotionPointOnPath(animation, sample.Progress, motionSource.PathData, out position, out tangent);
        }

        return TryResolveMotionPointFromValues(animation, sample.Progress, motionSource.Points, out position, out tangent);
    }

    private static bool TryResolveMotionPointOnPath(SvgAnimateMotion animation, float progress, string? pathData, out SKPoint position, out SKPoint tangent)
    {
        position = default;
        tangent = new SKPoint(1f, 0f);

        if (string.IsNullOrWhiteSpace(pathData))
        {
            return false;
        }

        using var path = SKPath.ParseSvgPathData(pathData);
        if (path is null)
        {
            return false;
        }

        using var measure = new SKPathMeasure(path, false);
        if (measure.Length <= 0f)
        {
            return false;
        }

        var distanceProgress = ResolveMotionProgress(animation, progress);
        var distance = measure.Length * Clamp01(distanceProgress);
        return measure.GetPositionAndTangent(distance, out position, out tangent);
    }

    private static bool TryResolveMotionPointFromValues(SvgAnimateMotion animation, float progress, IReadOnlyList<SKPoint> points, out SKPoint position, out SKPoint tangent)
    {
        position = default;
        tangent = new SKPoint(1f, 0f);

        if (points.Count == 0)
        {
            return false;
        }

        if (points.Count == 1)
        {
            position = points[0];
            return true;
        }

        if (animation.CalcMode == SvgAnimationCalcMode.Discrete)
        {
            var discretePoint = ResolveDiscreteMotionPoint(points, animation.KeyTimes, progress);
            position = discretePoint;
            tangent = ResolveMotionTangent(points, Math.Min(points.Count - 2, ResolveDiscreteMotionIndex(points.Count, animation.KeyTimes, progress)));
            return true;
        }

        ResolveInterpolatedSegment(
            points.Count,
            animation.KeyTimes,
            animation.KeySplines,
            animation.CalcMode,
            progress,
            animation.CalcMode == SvgAnimationCalcMode.Paced
                ? ResolvePointPacedSegmentLengths(points)
                : null,
            out var startIndex,
            out var endIndex,
            out var localProgress);
        var fromPoint = points[startIndex];
        var toPoint = points[endIndex];
        position = new SKPoint(
            Lerp(fromPoint.X, toPoint.X, localProgress),
            Lerp(fromPoint.Y, toPoint.Y, localProgress));
        tangent = ResolveMotionTangent(points, startIndex);
        return true;
    }

    private static bool TryResolveMotionAccumulationOffset(SvgAnimateMotion animation, MotionSource motionSource, out SKPoint offset)
    {
        offset = default;

        if (!TryResolveMotionPoint(animation, new AnimationSample(0f, 0), motionSource, out var startPoint, out _) ||
            !TryResolveMotionPoint(animation, new AnimationSample(1f, 0), motionSource, out var endPoint, out _))
        {
            return false;
        }

        offset = new SKPoint(endPoint.X - startPoint.X, endPoint.Y - startPoint.Y);
        return true;
    }

    private static SKPoint ResolveDiscreteMotionPoint(IReadOnlyList<SKPoint> points, SvgNumberCollection? keyTimes, float progress)
    {
        var index = ResolveDiscreteMotionIndex(points.Count, keyTimes, progress);
        return points[Math.Max(0, Math.Min(points.Count - 1, index))];
    }

    private static int ResolveDiscreteMotionIndex(int count, SvgNumberCollection? keyTimes, float progress)
    {
        if (count <= 1)
        {
            return 0;
        }

        if (keyTimes is { Count: > 1 } && keyTimes.Count == count)
        {
            for (var index = keyTimes.Count - 1; index >= 0; index--)
            {
                if (progress >= keyTimes[index])
                {
                    return index;
                }
            }

            return 0;
        }

        var scaled = progress * count;
        var discreteIndex = (int)Math.Floor(scaled);
        if (discreteIndex >= count)
        {
            discreteIndex = count - 1;
        }

        return Math.Max(0, discreteIndex);
    }

    private static SKPoint ResolveMotionTangent(IReadOnlyList<SKPoint> points, int startIndex)
    {
        if (points.Count <= 1)
        {
            return new SKPoint(1f, 0f);
        }

        var clampedStart = Math.Max(0, Math.Min(points.Count - 2, startIndex));
        var fromPoint = points[clampedStart];
        var toPoint = points[clampedStart + 1];
        var tangent = new SKPoint(toPoint.X - fromPoint.X, toPoint.Y - fromPoint.Y);
        if (tangent.X == 0f && tangent.Y == 0f)
        {
            return new SKPoint(1f, 0f);
        }

        return tangent;
    }

    private static List<SKPoint> ResolveMotionCoordinatePoints(AnimationBinding binding, SvgAnimateMotion animation)
    {
        var values = SvgAnimationParser.SplitSemicolonList(animation.Values);
        if (values.Count > 0)
        {
            return values
                .Select(value => TryParseMotionCoordinatePair(value, binding.SourceTarget, out var point) ? point : (SKPoint?)null)
                .Where(static point => point.HasValue)
                .Select(static point => point!.Value)
                .ToList();
        }

        var points = new List<SKPoint>();

        if (SvgAnimationParser.TryGetTrimmedString(animation.From, out var fromValue) &&
            TryParseMotionCoordinatePair(fromValue, binding.SourceTarget, out var fromPoint))
        {
            points.Add(fromPoint);
        }
        else
        {
            points.Add(new SKPoint(0f, 0f));
        }

        if (SvgAnimationParser.TryGetTrimmedString(animation.To, out var toValue) &&
            TryParseMotionCoordinatePair(toValue, binding.SourceTarget, out var toPoint))
        {
            points.Add(toPoint);
            return points;
        }

        if (SvgAnimationParser.TryGetTrimmedString(animation.By, out var byValue) &&
            TryParseMotionCoordinatePair(byValue, binding.SourceTarget, out var byPoint))
        {
            var startPoint = points[0];
            points.Add(new SKPoint(startPoint.X + byPoint.X, startPoint.Y + byPoint.Y));
        }

        return points;
    }

    private static bool TryParseMotionCoordinatePair(string value, SvgElement owner, out SKPoint point)
    {
        return SvgAnimationParser.TryParseMotionCoordinatePair(value, owner, out point);
    }

    private static string CreateMotionPathData(IReadOnlyList<SKPoint> points)
    {
        if (points.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            points.Select((point, index) => index == 0
                ? $"M{point.X.ToSvgString()} {point.Y.ToSvgString()}"
                : $"L{point.X.ToSvgString()} {point.Y.ToSvgString()}"));
    }

    private static float ParseMotionDiscreteProgress(SvgNumberCollection keyPoints, SvgNumberCollection? keyTimes, float progress)
    {
        if (keyPoints.Count == 1)
        {
            return keyPoints[0];
        }

        if (keyTimes is { Count: > 1 } && keyTimes.Count == keyPoints.Count)
        {
            for (var index = keyTimes.Count - 1; index >= 0; index--)
            {
                if (progress >= keyTimes[index])
                {
                    return keyPoints[index];
                }
            }

            return keyPoints[0];
        }

        var scaled = progress * keyPoints.Count;
        var discreteIndex = (int)Math.Floor(scaled);
        if (discreteIndex >= keyPoints.Count)
        {
            discreteIndex = keyPoints.Count - 1;
        }

        return keyPoints[Math.Max(0, discreteIndex)];
    }

    private static bool TryResolveMotionRotation(string? rotateValue, SKPoint tangent, out float angle)
    {
        return SvgAnimationParser.TryResolveMotionRotation(rotateValue, tangent, out angle);
    }

    private static bool TryCreateTransformString(SvgAnimateTransformType transformType, float[] values, out string transformValue)
    {
        transformValue = string.Empty;

        if (values.Length == 0)
        {
            return false;
        }

        SvgTransform transform = transformType switch
        {
            SvgAnimateTransformType.Translate => values.Length > 1 ? new SvgTranslate(values[0], values[1]) : new SvgTranslate(values[0]),
            SvgAnimateTransformType.Scale => values.Length > 1 ? new SvgScale(values[0], values[1]) : new SvgScale(values[0]),
            SvgAnimateTransformType.Rotate => values.Length > 2 ? new SvgRotate(values[0], values[1], values[2]) : new SvgRotate(values[0]),
            SvgAnimateTransformType.SkewX => new SvgSkew(values[0], 0f),
            SvgAnimateTransformType.SkewY => new SvgSkew(0f, values[0]),
            _ => new SvgTranslate(values[0])
        };

        transformValue = transform.ToString();
        return !string.IsNullOrWhiteSpace(transformValue);
    }

    private static float[] InterpolateTransformNumbers(SvgAnimateTransformType transformType, float[] fromValues, float[] toValues, float progress)
    {
        var length = GetExpectedTransformValueCount(transformType, fromValues, toValues);
        var result = new float[length];

        for (var index = 0; index < length; index++)
        {
            var fromValue = index < fromValues.Length ? fromValues[index] : GetDefaultTransformValue(transformType, index, fromValues);
            var toValue = index < toValues.Length ? toValues[index] : GetDefaultTransformValue(transformType, index, toValues);
            result[index] = Lerp(fromValue, toValue, progress);
        }

        return result;
    }

    private static int GetExpectedTransformValueCount(SvgAnimateTransformType transformType, float[] fromValues, float[] toValues)
    {
        return transformType switch
        {
            SvgAnimateTransformType.Rotate => Math.Max(1, Math.Max(fromValues.Length, toValues.Length)),
            SvgAnimateTransformType.Translate => Math.Max(1, Math.Max(fromValues.Length, toValues.Length)),
            SvgAnimateTransformType.Scale => Math.Max(1, Math.Max(fromValues.Length, toValues.Length)),
            _ => 1
        };
    }

    private static float GetDefaultTransformValue(SvgAnimateTransformType transformType, int index, float[] source)
    {
        if (transformType == SvgAnimateTransformType.Scale)
        {
            if (source.Length == 1 && index == 1)
            {
                return source[0];
            }

            return 1f;
        }

        if (transformType == SvgAnimateTransformType.Rotate)
        {
            return 0f;
        }

        if (transformType == SvgAnimateTransformType.Translate)
        {
            return 0f;
        }

        return 0f;
    }

    private static float[] ParseTransformNumbers(string value)
    {
        return SvgAnimationParser.ParseNumberList(value);
    }

    private static List<string> ResolveAnimationValues(AnimationBinding binding, SvgAnimationValueElement animation)
    {
        return binding.GetResolvedAnimationValues(() =>
        {
            var values = SvgAnimationParser.SplitSemicolonList(animation.Values);
            if (values.Count > 0)
            {
                return values;
            }

            var resolved = new List<string>();

            if (SvgAnimationParser.TryGetTrimmedString(animation.From, out var fromValue))
            {
                resolved.Add(fromValue);
            }
            else if (!string.IsNullOrWhiteSpace(binding.BaseValueString))
            {
                resolved.Add(binding.BaseValueString!);
            }

            if (SvgAnimationParser.TryGetTrimmedString(animation.To, out var toValue))
            {
                resolved.Add(toValue);
                return resolved;
            }

            if (SvgAnimationParser.TryGetTrimmedString(animation.By, out var byValue))
            {
                var additiveBaseValue = resolved.Count > 0 ? resolved[resolved.Count - 1] : binding.BaseValueString;
                if (additiveBaseValue is { } && TryAddValue(binding, additiveBaseValue, byValue, out var sumValue))
                {
                    resolved.Add(sumValue);
                }
            }

            return resolved;
        });
    }

    private static bool UsesImplicitBaseValue(AnimationBinding binding, SvgAnimationValueElement animation)
    {
        return string.IsNullOrWhiteSpace(animation.Values) &&
               string.IsNullOrWhiteSpace(animation.From) &&
               !string.IsNullOrWhiteSpace(binding.BaseValueString);
    }

    private static bool TryApplyAccumulation(AnimationBinding binding, SvgAnimationValueElement animation, IReadOnlyList<string> values, AnimationSample sample, bool forceColorInterpolation, ref string value)
    {
        if (animation.Accumulate != SvgAnimationAccumulate.Sum || sample.IterationIndex <= 0 || values.Count == 0)
        {
            return true;
        }

        var startValue = values[0];
        var endValue = values[values.Count - 1];

        if ((forceColorInterpolation || IsPaintServerType(binding.PropertyType) || TryGetColor(value, out _)) &&
            TryGetColor(startValue, out var startColor) &&
            TryGetColor(endValue, out var endColor) &&
            TryGetColor(value, out var currentColor))
        {
            value = new SvgColourServer(Color.FromArgb(
                ClampToByte(currentColor.A + ((endColor.A - startColor.A) * sample.IterationIndex)),
                ClampToByte(currentColor.R + ((endColor.R - startColor.R) * sample.IterationIndex)),
                ClampToByte(currentColor.G + ((endColor.G - startColor.G) * sample.IterationIndex)),
                ClampToByte(currentColor.B + ((endColor.B - startColor.B) * sample.IterationIndex)))).ToString();
            return true;
        }

        if (!TrySubtractValue(binding, endValue, startValue, forceColorInterpolation, out var deltaValue) ||
            !TryScaleValue(binding, deltaValue, sample.IterationIndex, forceColorInterpolation, out var accumulatedDelta) ||
            !TryAddValue(binding, value, accumulatedDelta, out var accumulatedValue))
        {
            return false;
        }

        value = accumulatedValue;
        return true;
    }

    private static bool TryApplyTransformAccumulation(AnimationBinding binding, SvgAnimateTransform animation, IReadOnlyList<string> values, AnimationSample sample, ref float[] currentValues)
    {
        if (animation.Accumulate != SvgAnimationAccumulate.Sum || sample.IterationIndex <= 0 || values.Count == 0)
        {
            return true;
        }

        var startValues = ParseTransformNumbers(values[0]);
        var endValues = ParseTransformNumbers(values[values.Count - 1]);
        var deltaValues = InterpolateTransformNumbers(animation.TransformType, startValues, endValues, 1f);
        for (var index = 0; index < deltaValues.Length; index++)
        {
            deltaValues[index] -= index < startValues.Length
                ? startValues[index]
                : GetDefaultTransformValue(animation.TransformType, index, startValues);
        }

        var originalLength = currentValues.Length;
        var length = Math.Max(currentValues.Length, deltaValues.Length);
        if (currentValues.Length != length)
        {
            Array.Resize(ref currentValues, length);
        }

        for (var index = 0; index < length; index++)
        {
            var currentValue = index < originalLength
                ? currentValues[index]
                : GetDefaultTransformValue(animation.TransformType, index, currentValues);
            var deltaValue = index < deltaValues.Length ? deltaValues[index] : 0f;
            currentValues[index] = currentValue + (deltaValue * sample.IterationIndex);
        }

        return true;
    }

    private static string ResolveDiscreteValue(IReadOnlyList<string> values, SvgNumberCollection? keyTimes, float progress)
    {
        if (values.Count == 1)
        {
            return values[0];
        }

        if (keyTimes is { Count: > 1 } && keyTimes.Count == values.Count)
        {
            for (var index = keyTimes.Count - 1; index >= 0; index--)
            {
                if (progress >= keyTimes[index])
                {
                    return values[index];
                }
            }

            return values[0];
        }

        var scaled = progress * values.Count;
        var discreteIndex = (int)Math.Floor(scaled);
        if (discreteIndex >= values.Count)
        {
            discreteIndex = values.Count - 1;
        }

        return values[Math.Max(0, discreteIndex)];
    }

    private static void ResolveInterpolatedSegment(
        int valueCount,
        SvgNumberCollection? keyTimes,
        string? keySplines,
        SvgAnimationCalcMode calcMode,
        float progress,
        IReadOnlyList<float>? pacedSegmentLengths,
        out int startIndex,
        out int endIndex,
        out float localProgress)
    {
        if (valueCount <= 1)
        {
            startIndex = 0;
            endIndex = 0;
            localProgress = 1f;
            return;
        }

        if (calcMode == SvgAnimationCalcMode.Paced &&
            TryResolvePacedSegment(valueCount, pacedSegmentLengths, progress, out startIndex, out endIndex, out localProgress))
        {
            return;
        }

        if (keyTimes is { Count: > 1 } && keyTimes.Count == valueCount)
        {
            for (var index = 0; index < keyTimes.Count - 1; index++)
            {
                var rangeStart = keyTimes[index];
                var rangeEnd = keyTimes[index + 1];

                if (progress <= rangeEnd || index == keyTimes.Count - 2)
                {
                    startIndex = index;
                    endIndex = index + 1;
                    localProgress = rangeEnd > rangeStart
                        ? Clamp01((progress - rangeStart) / (rangeEnd - rangeStart))
                        : 0f;

                    if (calcMode == SvgAnimationCalcMode.Spline)
                    {
                        localProgress = ResolveSplineProgress(keySplines, startIndex, localProgress);
                    }

                    return;
                }
            }
        }

        var scaled = Clamp01(progress) * (valueCount - 1);
        startIndex = (int)Math.Floor(scaled);
        if (startIndex >= valueCount - 1)
        {
            startIndex = valueCount - 2;
            endIndex = valueCount - 1;
            localProgress = 1f;
            return;
        }

        endIndex = startIndex + 1;
        localProgress = Clamp01(scaled - startIndex);

        if (calcMode == SvgAnimationCalcMode.Spline)
        {
            localProgress = ResolveSplineProgress(keySplines, startIndex, localProgress);
        }
    }

    private static IReadOnlyList<float>? ResolvePacedSegmentLengths(AnimationBinding binding, IReadOnlyList<string> values, bool forceColorInterpolation)
    {
        if (values.Count <= 1)
        {
            return null;
        }

        var segmentLengths = new float[values.Count - 1];
        for (var index = 0; index < segmentLengths.Length; index++)
        {
            if (!TryResolvePacedDistance(binding, values[index], values[index + 1], forceColorInterpolation, out var distance))
            {
                return null;
            }

            segmentLengths[index] = distance;
        }

        return segmentLengths;
    }

    private static IReadOnlyList<float>? ResolveTransformPacedSegmentLengths(SvgAnimateTransformType transformType, IReadOnlyList<string> values)
    {
        if (values.Count <= 1)
        {
            return null;
        }

        var segmentLengths = new float[values.Count - 1];
        for (var index = 0; index < segmentLengths.Length; index++)
        {
            segmentLengths[index] = ResolveTransformDistance(
                transformType,
                ParseTransformNumbers(values[index]),
                ParseTransformNumbers(values[index + 1]));
        }

        return segmentLengths;
    }

    private static IReadOnlyList<float>? ResolvePointPacedSegmentLengths(IReadOnlyList<SKPoint> points)
    {
        if (points.Count <= 1)
        {
            return null;
        }

        var segmentLengths = new float[points.Count - 1];
        for (var index = 0; index < segmentLengths.Length; index++)
        {
            segmentLengths[index] = ResolvePointDistance(points[index], points[index + 1]);
        }

        return segmentLengths;
    }

    private static IReadOnlyList<float>? ResolveScalarPacedSegmentLengths(SvgNumberCollection values)
    {
        if (values.Count <= 1)
        {
            return null;
        }

        var segmentLengths = new float[values.Count - 1];
        for (var index = 0; index < segmentLengths.Length; index++)
        {
            segmentLengths[index] = Math.Abs(values[index + 1] - values[index]);
        }

        return segmentLengths;
    }

    private static bool TryResolvePacedSegment(
        int valueCount,
        IReadOnlyList<float>? segmentLengths,
        float progress,
        out int startIndex,
        out int endIndex,
        out float localProgress)
    {
        startIndex = 0;
        endIndex = 0;
        localProgress = 1f;

        if (valueCount <= 1 || segmentLengths is null || segmentLengths.Count != valueCount - 1)
        {
            return false;
        }

        double totalLength = 0d;
        for (var index = 0; index < segmentLengths.Count; index++)
        {
            totalLength += Math.Max(0f, segmentLengths[index]);
        }

        if (totalLength <= 0d)
        {
            return false;
        }

        var targetLength = Clamp01(progress) * (float)totalLength;
        var accumulatedLength = 0f;

        for (var index = 0; index < segmentLengths.Count; index++)
        {
            var segmentLength = Math.Max(0f, segmentLengths[index]);
            var segmentEndLength = accumulatedLength + segmentLength;
            if (targetLength <= segmentEndLength || index == segmentLengths.Count - 1)
            {
                startIndex = index;
                endIndex = index + 1;
                localProgress = segmentLength > 0f
                    ? Clamp01((targetLength - accumulatedLength) / segmentLength)
                    : 0f;
                return true;
            }

            accumulatedLength = segmentEndLength;
        }

        return false;
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SvgAnimationController.TryConvertStringToType(String, Type, out Object)")]
    private static bool TryResolvePacedDistance(AnimationBinding binding, string fromValue, string toValue, bool forceColorInterpolation, out float distance)
    {
        distance = 0f;

        if ((forceColorInterpolation || IsPaintServerType(binding.PropertyType)) &&
            TryGetColor(fromValue, out var fromPaintColor) &&
            TryGetColor(toValue, out var toPaintColor))
        {
            distance = ResolveColorDistance(fromPaintColor, toPaintColor);
            return true;
        }

        if (binding.PropertyType is { } propertyType &&
            TryConvertStringToType(fromValue, propertyType, out var fromObject) &&
            TryConvertStringToType(toValue, propertyType, out var toObject) &&
            TryResolveTypedPacedDistance(fromObject, toObject, out distance))
        {
            return true;
        }

        if (TryResolveNumberListDistance(fromValue, toValue, out distance))
        {
            return true;
        }

        if (TryGetColor(fromValue, out var fromColor) &&
            TryGetColor(toValue, out var toColor))
        {
            distance = ResolveColorDistance(fromColor, toColor);
            return true;
        }

        if (SvgAnimationParser.TryParseSvgUnit(fromValue, out var fromUnit) &&
            SvgAnimationParser.TryParseSvgUnit(toValue, out var toUnit) &&
            fromUnit.Type == toUnit.Type)
        {
            distance = Math.Abs(toUnit.Value - fromUnit.Value);
            return true;
        }

        return false;
    }

    private static bool TryResolveTypedPacedDistance(object? fromObject, object? toObject, out float distance)
    {
        distance = 0f;

        switch (fromObject)
        {
            case float fromFloat when toObject is float toFloat:
                distance = Math.Abs(toFloat - fromFloat);
                return true;
            case double fromDouble when toObject is double toDouble:
                distance = (float)Math.Abs(toDouble - fromDouble);
                return true;
            case int fromInt when toObject is int toInt:
                distance = Math.Abs(toInt - fromInt);
                return true;
            case SvgUnit fromUnit when toObject is SvgUnit toUnit && fromUnit.Type == toUnit.Type:
                distance = Math.Abs(toUnit.Value - fromUnit.Value);
                return true;
            case SvgPaintServer fromPaint when toObject is SvgPaintServer toPaint:
                if (TryGetColor(fromPaint, out var fromPaintColor) &&
                    TryGetColor(toPaint, out var toPaintColor))
                {
                    distance = ResolveColorDistance(fromPaintColor, toPaintColor);
                    return true;
                }

                return false;
            case SvgColourServer fromColour when toObject is SvgColourServer toColour:
                distance = ResolveColorDistance(fromColour.Colour, toColour.Colour);
                return true;
            default:
                return false;
        }
    }

    private static bool TryResolveNumberListDistance(string fromValue, string toValue, out float distance)
    {
        distance = 0f;

        var fromValues = SvgAnimationParser.ParseNumberList(fromValue);
        var toValues = SvgAnimationParser.ParseNumberList(toValue);
        if (fromValues.Length == 0 || fromValues.Length != toValues.Length)
        {
            return false;
        }

        distance = ResolveEuclideanDistance(fromValues, toValues);
        return true;
    }

    private static float ResolveTransformDistance(SvgAnimateTransformType transformType, float[] fromValues, float[] toValues)
    {
        var length = GetExpectedTransformValueCount(transformType, fromValues, toValues);
        var normalizedFromValues = new float[length];
        var normalizedToValues = new float[length];

        for (var index = 0; index < length; index++)
        {
            normalizedFromValues[index] = index < fromValues.Length
                ? fromValues[index]
                : GetDefaultTransformValue(transformType, index, fromValues);
            normalizedToValues[index] = index < toValues.Length
                ? toValues[index]
                : GetDefaultTransformValue(transformType, index, toValues);
        }

        return ResolveEuclideanDistance(normalizedFromValues, normalizedToValues);
    }

    private static float ResolveEuclideanDistance(float[] fromValues, float[] toValues)
    {
        double sum = 0d;

        for (var index = 0; index < fromValues.Length; index++)
        {
            var delta = toValues[index] - fromValues[index];
            sum += delta * delta;
        }

        return (float)Math.Sqrt(sum);
    }

    private static float ResolveColorDistance(Color fromColor, Color toColor)
    {
        var deltaA = toColor.A - fromColor.A;
        var deltaR = toColor.R - fromColor.R;
        var deltaG = toColor.G - fromColor.G;
        var deltaB = toColor.B - fromColor.B;
        return (float)Math.Sqrt(
            (deltaA * deltaA) +
            (deltaR * deltaR) +
            (deltaG * deltaG) +
            (deltaB * deltaB));
    }

    private static float ResolvePointDistance(SKPoint fromPoint, SKPoint toPoint)
    {
        var deltaX = toPoint.X - fromPoint.X;
        var deltaY = toPoint.Y - fromPoint.Y;
        return (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static float ResolveSplineProgress(string? keySplines, int segmentIndex, float progress)
    {
        if (progress <= 0f || progress >= 1f)
        {
            return Clamp01(progress);
        }

        return TryGetSplineSegment(keySplines, segmentIndex, out var spline)
            ? EvaluateSplineProgress(spline, progress)
            : Clamp01(progress);
    }

    private static bool TryGetSplineSegment(string? keySplines, int segmentIndex, out CubicBezierSpline spline)
    {
        return SvgAnimationParser.TryParseSplineSegment(keySplines, segmentIndex, out spline);
    }

    private static float EvaluateSplineProgress(CubicBezierSpline spline, float progress)
    {
        var targetX = Clamp01(progress);
        var t = targetX;

        for (var iteration = 0; iteration < 8; iteration++)
        {
            var currentX = EvaluateCubicBezierComponent(spline.X1, spline.X2, t) - targetX;
            if (Math.Abs(currentX) < 1e-5f)
            {
                return Clamp01(EvaluateCubicBezierComponent(spline.Y1, spline.Y2, t));
            }

            var derivative = EvaluateCubicBezierDerivative(spline.X1, spline.X2, t);
            if (Math.Abs(derivative) < 1e-6f)
            {
                break;
            }

            t -= currentX / derivative;
            if (t <= 0f || t >= 1f)
            {
                break;
            }
        }

        var low = 0f;
        var high = 1f;
        t = targetX;

        for (var iteration = 0; iteration < 12; iteration++)
        {
            var currentX = EvaluateCubicBezierComponent(spline.X1, spline.X2, t);
            if (Math.Abs(currentX - targetX) < 1e-5f)
            {
                break;
            }

            if (currentX < targetX)
            {
                low = t;
            }
            else
            {
                high = t;
            }

            t = (low + high) * 0.5f;
        }

        return Clamp01(EvaluateCubicBezierComponent(spline.Y1, spline.Y2, t));
    }

    private static float EvaluateCubicBezierComponent(float control1, float control2, float t)
    {
        var inverse = 1f - t;
        return (3f * inverse * inverse * t * control1) +
               (3f * inverse * t * t * control2) +
               (t * t * t);
    }

    private static float EvaluateCubicBezierDerivative(float control1, float control2, float t)
    {
        var inverse = 1f - t;
        return (3f * inverse * inverse * control1) +
               (6f * inverse * t * (control2 - control1)) +
               (3f * t * t * (1f - control2));
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SvgAnimationController.TryConvertStringToType(String, Type, out Object)")]
    private static bool TryInterpolateValue(AnimationBinding binding, string fromValue, string toValue, float progress, bool forceColorInterpolation, out string result)
    {
        result = string.Empty;

        if ((forceColorInterpolation || IsPaintServerType(binding.PropertyType)) &&
            TryInterpolateColor(fromValue, toValue, progress, out result))
        {
            return true;
        }

        if (binding.PropertyType is { } propertyType &&
            TryConvertStringToType(fromValue, propertyType, out var fromObject) &&
            TryConvertStringToType(toValue, propertyType, out var toObject) &&
            TryInterpolateTypedValue(fromObject, toObject, progress, out result))
        {
            return true;
        }

        if (TryInterpolateColor(fromValue, toValue, progress, out result))
        {
            return true;
        }

        if (TryInterpolateSvgUnit(fromValue, toValue, progress, out result))
        {
            return true;
        }

        if (TryInterpolateNumeric(fromValue, toValue, progress, out result))
        {
            return true;
        }

        return false;
    }

    private static bool TryInterpolateTypedValue(object? fromObject, object? toObject, float progress, out string result)
    {
        result = string.Empty;

        switch (fromObject)
        {
            case float fromFloat when toObject is float toFloat:
                result = Lerp(fromFloat, toFloat, progress).ToSvgString();
                return true;
            case double fromDouble when toObject is double toDouble:
                result = Lerp((float)fromDouble, (float)toDouble, progress).ToSvgString();
                return true;
            case int fromInt when toObject is int toInt:
                result = Lerp(fromInt, toInt, progress).ToSvgString();
                return true;
            case SvgUnit fromUnit when toObject is SvgUnit toUnit:
                return TryInterpolateSvgUnit(fromUnit, toUnit, progress, out result);
            case SvgPaintServer fromPaint when toObject is SvgPaintServer toPaint:
                return TryInterpolatePaint(fromPaint, toPaint, progress, out result);
            case SvgColourServer fromColour when toObject is SvgColourServer toColour:
                return TryInterpolateColor(fromColour.Colour, toColour.Colour, progress, out result);
            default:
                return false;
        }
    }

    private static bool TryInterpolateNumeric(string fromValue, string toValue, float progress, out string result)
    {
        result = string.Empty;

        if (!SvgAnimationParser.TryParseInvariantFloat(fromValue, out var fromNumber) ||
            !SvgAnimationParser.TryParseInvariantFloat(toValue, out var toNumber))
        {
            return false;
        }

        result = Lerp(fromNumber, toNumber, progress).ToSvgString();
        return true;
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SvgAnimationController.TryConvertStringToType(String, Type, out Object)")]
    private static bool TryInterpolateSvgUnit(string fromValue, string toValue, float progress, out string result)
    {
        result = string.Empty;

        if (!TryConvertStringToType(fromValue, typeof(SvgUnit), out var fromObject) ||
            !TryConvertStringToType(toValue, typeof(SvgUnit), out var toObject) ||
            fromObject is not SvgUnit fromUnit ||
            toObject is not SvgUnit toUnit)
        {
            return false;
        }

        return TryInterpolateSvgUnit(fromUnit, toUnit, progress, out result);
    }

    private static bool TryInterpolateSvgUnit(SvgUnit fromUnit, SvgUnit toUnit, float progress, out string result)
    {
        result = string.Empty;

        if (fromUnit.Type != toUnit.Type)
        {
            return false;
        }

        var unit = new SvgUnit(fromUnit.Type, Lerp(fromUnit.Value, toUnit.Value, progress));
        result = unit.ToString();
        return true;
    }

    private static bool TryInterpolatePaint(SvgPaintServer fromPaint, SvgPaintServer toPaint, float progress, out string result)
    {
        result = string.Empty;
        return TryGetColor(fromPaint, out var fromColor) &&
               TryGetColor(toPaint, out var toColor) &&
               TryInterpolateColor(fromColor, toColor, progress, out result);
    }

    private static bool TryInterpolateColor(string fromValue, string toValue, float progress, out string result)
    {
        result = string.Empty;

        if (!TryGetColor(fromValue, out var fromColor) || !TryGetColor(toValue, out var toColor))
        {
            return false;
        }

        return TryInterpolateColor(fromColor, toColor, progress, out result);
    }

    private static bool TryInterpolateColor(Color fromColor, Color toColor, float progress, out string result)
    {
        var color = Color.FromArgb(
            ClampToByte(Lerp(fromColor.A, toColor.A, progress)),
            ClampToByte(Lerp(fromColor.R, toColor.R, progress)),
            ClampToByte(Lerp(fromColor.G, toColor.G, progress)),
            ClampToByte(Lerp(fromColor.B, toColor.B, progress)));

        result = new SvgColourServer(color).ToString();
        return true;
    }

    private static byte ClampToByte(float value)
    {
        if (value <= byte.MinValue)
        {
            return byte.MinValue;
        }

        if (value >= byte.MaxValue)
        {
            return byte.MaxValue;
        }

        return (byte)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static bool TryGetColor(string value, out Color color)
    {
        color = default;

        if (!SvgAnimationParser.TryGetTrimmedString(value, out var trimmed))
        {
            return false;
        }

        try
        {
            var paint = s_paintServerConverter.ConvertFrom(null, CultureInfo.InvariantCulture, trimmed) as SvgPaintServer;
            return TryGetColor(paint, out color);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetColor(SvgPaintServer? paintServer, out Color color)
    {
        if (paintServer is SvgColourServer colourServer)
        {
            color = colourServer.Colour;
            return true;
        }

        color = default;
        return false;
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SvgAnimationController.TryConvertStringToType(String, Type, out Object)")]
    private static bool TryAddValue(AnimationBinding binding, string baseValue, string byValue, out string result)
    {
        result = string.Empty;

        if (binding.PropertyType is { } propertyType &&
            TryConvertStringToType(baseValue, propertyType, out var baseObject) &&
            TryConvertStringToType(byValue, propertyType, out var byObject) &&
            TryAddTypedValue(baseObject, byObject, out result))
        {
            return true;
        }

        if (TryConvertStringToType(baseValue, typeof(SvgUnit), out var baseUnitObject) &&
            TryConvertStringToType(byValue, typeof(SvgUnit), out var byUnitObject) &&
            baseUnitObject is SvgUnit baseUnit &&
            byUnitObject is SvgUnit byUnit &&
            baseUnit.Type == byUnit.Type)
        {
            result = new SvgUnit(baseUnit.Type, baseUnit.Value + byUnit.Value).ToString();
            return true;
        }

        if (SvgAnimationParser.TryParseInvariantFloat(baseValue, out var baseNumber) &&
            SvgAnimationParser.TryParseInvariantFloat(byValue, out var byNumber))
        {
            result = (baseNumber + byNumber).ToSvgString();
            return true;
        }

        if (TryGetColor(baseValue, out var baseColor) &&
            TryGetColor(byValue, out var byColor))
        {
            result = new SvgColourServer(Color.FromArgb(
                ClampToByte(baseColor.A + byColor.A),
                ClampToByte(baseColor.R + byColor.R),
                ClampToByte(baseColor.G + byColor.G),
                ClampToByte(baseColor.B + byColor.B))).ToString();
            return true;
        }

        return false;
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SvgAnimationController.TryConvertStringToType(String, Type, out Object)")]
    private static bool TrySubtractValue(AnimationBinding binding, string endValue, string startValue, bool forceColorInterpolation, out string result)
    {
        result = string.Empty;

        if (binding.PropertyType is { } propertyType &&
            TryConvertStringToType(endValue, propertyType, out var endObject) &&
            TryConvertStringToType(startValue, propertyType, out var startObject) &&
            TrySubtractTypedValue(endObject, startObject, out result))
        {
            return true;
        }

        if (TryConvertStringToType(endValue, typeof(SvgUnit), out var endUnitObject) &&
            TryConvertStringToType(startValue, typeof(SvgUnit), out var startUnitObject) &&
            endUnitObject is SvgUnit endUnit &&
            startUnitObject is SvgUnit startUnit &&
            endUnit.Type == startUnit.Type)
        {
            result = new SvgUnit(endUnit.Type, endUnit.Value - startUnit.Value).ToString();
            return true;
        }

        if (SvgAnimationParser.TryParseInvariantFloat(endValue, out var endNumber) &&
            SvgAnimationParser.TryParseInvariantFloat(startValue, out var startNumber))
        {
            result = (endNumber - startNumber).ToSvgString();
            return true;
        }

        return false;
    }

    [RequiresUnreferencedCode("Calls Svg.Skia.SvgAnimationController.TryConvertStringToType(String, Type, out Object)")]
    private static bool TryScaleValue(AnimationBinding binding, string value, int factor, bool forceColorInterpolation, out string result)
    {
        result = string.Empty;

        if (factor == 0)
        {
            result = "0";
            return true;
        }

        if (binding.PropertyType is { } propertyType &&
            TryConvertStringToType(value, propertyType, out var valueObject) &&
            TryScaleTypedValue(valueObject, factor, out result))
        {
            return true;
        }

        if (TryConvertStringToType(value, typeof(SvgUnit), out var unitObject) &&
            unitObject is SvgUnit unit)
        {
            result = new SvgUnit(unit.Type, unit.Value * factor).ToString();
            return true;
        }

        if (SvgAnimationParser.TryParseInvariantFloat(value, out var numeric))
        {
            result = (numeric * factor).ToSvgString();
            return true;
        }

        return false;
    }

    private static bool TryAddTypedValue(object? baseObject, object? byObject, out string result)
    {
        result = string.Empty;

        switch (baseObject)
        {
            case float baseFloat when byObject is float byFloat:
                result = (baseFloat + byFloat).ToSvgString();
                return true;
            case double baseDouble when byObject is double byDouble:
                result = ((float)(baseDouble + byDouble)).ToSvgString();
                return true;
            case int baseInt when byObject is int byInt:
                result = ((float)(baseInt + byInt)).ToSvgString();
                return true;
            case SvgUnit baseUnit when byObject is SvgUnit byUnit && baseUnit.Type == byUnit.Type:
                result = new SvgUnit(baseUnit.Type, baseUnit.Value + byUnit.Value).ToString();
                return true;
            case SvgPaintServer basePaint when byObject is SvgPaintServer byPaint:
                if (TryGetColor(basePaint, out var baseColor) && TryGetColor(byPaint, out var byColor))
                {
                    result = new SvgColourServer(Color.FromArgb(
                        ClampToByte(baseColor.A + byColor.A),
                        ClampToByte(baseColor.R + byColor.R),
                        ClampToByte(baseColor.G + byColor.G),
                        ClampToByte(baseColor.B + byColor.B))).ToString();
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    private static bool TrySubtractTypedValue(object? endObject, object? startObject, out string result)
    {
        result = string.Empty;

        switch (endObject)
        {
            case float endFloat when startObject is float startFloat:
                result = (endFloat - startFloat).ToSvgString();
                return true;
            case double endDouble when startObject is double startDouble:
                result = ((float)(endDouble - startDouble)).ToSvgString();
                return true;
            case int endInt when startObject is int startInt:
                result = ((float)(endInt - startInt)).ToSvgString();
                return true;
            case SvgUnit endUnit when startObject is SvgUnit startUnit && endUnit.Type == startUnit.Type:
                result = new SvgUnit(endUnit.Type, endUnit.Value - startUnit.Value).ToString();
                return true;
            default:
                return false;
        }
    }

    private static bool TryScaleTypedValue(object? valueObject, int factor, out string result)
    {
        result = string.Empty;

        switch (valueObject)
        {
            case float floatValue:
                result = (floatValue * factor).ToSvgString();
                return true;
            case double doubleValue:
                result = ((float)doubleValue * factor).ToSvgString();
                return true;
            case int intValue:
                result = ((float)intValue * factor).ToSvgString();
                return true;
            case SvgUnit unitValue:
                result = new SvgUnit(unitValue.Type, unitValue.Value * factor).ToString();
                return true;
            default:
                return false;
        }
    }

    [RequiresUnreferencedCode("Calls System.ComponentModel.TypeDescriptor.GetConverter(Type)")]
    private static bool TryConvertStringToType(string value, Type targetType, out object? result)
    {
        result = null;

        if (targetType == typeof(string))
        {
            result = value;
            return true;
        }

        try
        {
            if (targetType == typeof(SvgUnit))
            {
                if (SvgAnimationParser.TryParseSvgUnit(value, out var unit))
                {
                    result = unit;
                    return true;
                }

                return false;
            }

            var converter = TypeDescriptor.GetConverter(targetType);
            if (converter.CanConvertFrom(typeof(string)))
            {
                result = converter.ConvertFrom(null, CultureInfo.InvariantCulture, value);
                return result is not null;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static string CombineTransformValue(string? baseValue, string transformValue)
    {
        if (!SvgAnimationParser.TryGetTrimmedString(transformValue, out var trimmedTransformValue))
        {
            return string.Empty;
        }

        if (!SvgAnimationParser.TryGetTrimmedString(baseValue, out var trimmedBaseValue))
        {
            return trimmedTransformValue;
        }

        return string.Concat(trimmedBaseValue, " ", trimmedTransformValue);
    }

    private static string? ResolveCurrentComposedBaseValue(string? currentComposedValue, string? fallbackBaseValue)
    {
        if (!string.IsNullOrWhiteSpace(currentComposedValue))
        {
            return currentComposedValue;
        }

        return fallbackBaseValue;
    }

    private static bool TryResolveAdditiveBaseValue(
        AnimationBinding binding,
        SvgAnimationValueElement animation,
        string? currentComposedValue,
        out string baseValue)
    {
        baseValue = ResolveCurrentComposedBaseValue(currentComposedValue, binding.BaseValueString) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(baseValue))
        {
            return true;
        }

        if (UsesImplicitBaseValue(binding, animation))
        {
            baseValue = string.Empty;
            return false;
        }

        return false;
    }

    private static string CreateEventInstanceKey(SvgElementAddress address, SvgPointerEventType eventType)
    {
        return string.Concat(address.Key, "|", ((int)eventType).ToString(CultureInfo.InvariantCulture));
    }

    private static object? GetAttributeValue(SvgElement element, string attributeName)
    {
        return element.GetAnimationValue(attributeName);
    }

    private static bool SetAttributeValue(SvgElement element, string attributeName, string value)
    {
        return element.TrySetAnimationValue(attributeName, value);
    }

    private static bool ClearAttributeValue(SvgElement element, string attributeName)
    {
        return element.ClearAnimationValue(attributeName);
    }

    private static string? ConvertAttributeValueToString(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            string stringValue => stringValue,
            _ => value.ToString()
        };
    }

    private static bool IsPaintServerType(Type? type)
    {
        return type is not null && typeof(SvgPaintServer).IsAssignableFrom(type);
    }

    private static float Lerp(float from, float to, float progress)
    {
        return from + ((to - from) * Clamp01(progress));
    }

    private static TimeSpan Multiply(TimeSpan duration, double factor)
    {
        if (factor <= 0d || duration <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var ticks = duration.Ticks * factor;
        if (ticks >= TimeSpan.MaxValue.Ticks)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromTicks((long)Math.Round(ticks, MidpointRounding.AwayFromZero));
    }

    private static TimeSpan? MinDuration(TimeSpan? left, TimeSpan right)
    {
        if (!left.HasValue)
        {
            return right;
        }

        return left.Value <= right ? left : right;
    }

    private static TimeSpan? MaxDuration(TimeSpan? left, TimeSpan right)
    {
        if (!left.HasValue)
        {
            return left;
        }

        return left.Value >= right ? left : right;
    }

    private static int ClampIterationIndex(long iterationIndex)
    {
        if (iterationIndex <= 0)
        {
            return 0;
        }

        return iterationIndex >= int.MaxValue
            ? int.MaxValue
            : (int)iterationIndex;
    }

    private static float Clamp01(float value)
    {
        if (value < 0f)
        {
            return 0f;
        }

        if (value > 1f)
        {
            return 1f;
        }

        return value;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SvgAnimationController));
        }
    }
}
