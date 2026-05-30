using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
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

internal readonly struct SvgAnimationTimelineCallback
{
    public SvgAnimationTimelineCallback(SvgElementAddress animationAddress, string eventType, string attributeName, TimeSpan time)
    {
        AnimationAddress = animationAddress;
        EventType = eventType;
        AttributeName = attributeName;
        Time = time;
    }

    public SvgElementAddress AnimationAddress { get; }

    public string EventType { get; }

    public string AttributeName { get; }

    public TimeSpan Time { get; }
}

public sealed class SvgAnimationController : IDisposable
{
    private static readonly ResolvedTimingInstance[] s_emptyResolvedTimingInstances = Array.Empty<ResolvedTimingInstance>();

    private readonly struct TimingSpec
    {
        public TimingSpec(TimeSpan offset)
        {
            IsEvent = false;
            IsAccessKey = false;
            Offset = offset;
            EventAddress = null;
            EventType = default;
            RepeatIteration = null;
            AccessKey = null;
        }

        public TimingSpec(SvgElementAddress eventAddress, SvgAnimationTimingEventType eventType, TimeSpan offset, int? repeatIteration = null)
        {
            IsEvent = true;
            IsAccessKey = false;
            Offset = offset;
            EventAddress = eventAddress;
            EventType = eventType;
            RepeatIteration = repeatIteration;
            AccessKey = null;
        }

        public TimingSpec(string accessKey, TimeSpan offset)
        {
            IsEvent = false;
            IsAccessKey = true;
            Offset = offset;
            EventAddress = null;
            EventType = SvgAnimationTimingEventType.AccessKey;
            RepeatIteration = null;
            AccessKey = accessKey;
        }

        public bool IsEvent { get; }

        public bool IsAccessKey { get; }

        public TimeSpan Offset { get; }

        public SvgElementAddress? EventAddress { get; }

        public SvgAnimationTimingEventType EventType { get; }

        public int? RepeatIteration { get; }

        public string? AccessKey { get; }
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
        public PointerEventDependency(AnimationBinding binding)
        {
            Binding = binding;
        }

        public AnimationBinding Binding { get; }
    }

    private sealed class AnimationBinding
    {
        public AnimationBinding(SvgAnimationElement animation, SvgElement sourceTarget, SvgElementAddress targetAddress, string attributeName, DateTimeOffset wallclockTimeOrigin)
        {
            Animation = animation;
            AnimationAddress = SvgElementAddress.Create(animation);
            SourceTarget = sourceTarget;
            TargetAddress = targetAddress;
            AttributeName = attributeName;
            var propertyDescriptor = GetAttributePropertyDescriptor(sourceTarget, attributeName);
            ValueConverter = propertyDescriptor?.Converter;
            ValueContext = sourceTarget.OwnerDocument;
            BaseValue = GetAttributeValue(sourceTarget, attributeName);
            BaseValueString = ConvertAttributeValueToString(BaseValue);
            HasExplicitBaseAttribute = HasExplicitAnimationBaseAttribute(sourceTarget, attributeName, BaseValue);
            PropertyType = propertyDescriptor?.Type ?? BaseValue?.GetType();
            TargetAttributeKey = string.Concat(targetAddress.Key, "|", attributeName);
            BeginSpecs = ParseTimingSpecifications(animation.Begin, animation.OwnerDocument, targetAddress, wallclockTimeOrigin, includeImplicitDocumentBegin: true);
            EndSpecs = ParseTimingSpecifications(animation.End, animation.OwnerDocument, targetAddress, wallclockTimeOrigin, includeImplicitDocumentBegin: false);
            HasDynamicBeginTiming = ContainsDynamicTiming(BeginSpecs);
            HasDynamicEndTiming = ContainsDynamicTiming(EndSpecs);
            StaticBeginInstances = HasDynamicBeginTiming ? s_emptyResolvedTimingInstances : CreateStaticTimingInstances(BeginSpecs);
            StaticEndInstances = HasDynamicEndTiming ? s_emptyResolvedTimingInstances : CreateStaticTimingInstances(EndSpecs);
        }

        public SvgAnimationElement Animation { get; }

        public SvgElementAddress AnimationAddress { get; }

        public SvgElement SourceTarget { get; }

        public SvgElementAddress TargetAddress { get; }

        public string AttributeName { get; }

        public bool HasExplicitBaseAttribute { get; }

        public object? BaseValue { get; }

        public string? BaseValueString { get; }

        public Type? PropertyType { get; }

        public TypeConverter? ValueConverter { get; }

        public ITypeDescriptorContext? ValueContext { get; }

        public string TargetAttributeKey { get; }

        public IReadOnlyList<TimingSpec> BeginSpecs { get; }

        public IReadOnlyList<TimingSpec> EndSpecs { get; }

        public bool HasDynamicBeginTiming { get; }

        public bool HasDynamicEndTiming { get; }

        public IReadOnlyList<ResolvedTimingInstance> StaticBeginInstances { get; }

        public IReadOnlyList<ResolvedTimingInstance> StaticEndInstances { get; }

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

    private readonly struct PathDataToken
    {
        public PathDataToken(char command)
        {
            IsCommand = true;
            Command = command;
            Number = 0f;
        }

        public PathDataToken(float number)
        {
            IsCommand = false;
            Command = '\0';
            Number = number;
        }

        public bool IsCommand { get; }

        public char Command { get; }

        public float Number { get; }
    }

    private static readonly TypeConverter s_paintServerConverter = new SvgPaintServerFactory();

    private readonly List<AnimationBinding> _bindings;
    private readonly List<AnimationBinding> _frameEvaluationBindings;
    private readonly Dictionary<string, AnimationBinding> _bindingsByTargetAttributeKey;
    private readonly Dictionary<string, AnimationBinding> _bindingsByAnimationAddressKey;
    private readonly Dictionary<string, List<TimeSpan>> _pointerEventInstances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<TimeSpan>> _accessKeyEventInstances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<TimeSpan>> _scheduledBeginInstances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<TimeSpan>> _scheduledEndInstances = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pointerEventDependencies;
    private readonly HashSet<string> _accessKeyEventDependencies;
    private readonly Dictionary<string, List<PointerEventDependency>> _pointerEventDependents;
    private readonly Dictionary<string, List<PointerEventDependency>> _accessKeyEventDependents;
    private readonly List<AnimationBinding> _timelineTrackedBindings;
    private readonly int[]? _animatedTopLevelChildIndexes;
    private SvgAnimationFrameState? _cachedFrameState;
    private int _frameStateVersion;
    private bool _disposed;

    public SvgAnimationController(SvgDocument sourceDocument, DateTimeOffset? wallclockTimeOrigin = null)
    {
        SourceDocument = sourceDocument ?? throw new ArgumentNullException(nameof(sourceDocument));
        WallclockTimeOrigin = (wallclockTimeOrigin ?? DateTimeOffset.UtcNow).ToUniversalTime();
        Clock = new SvgAnimationClock();
        Clock.TimeChanged += OnClockTimeChanged;
        _bindings = DiscoverBindings(sourceDocument, WallclockTimeOrigin);
        _frameEvaluationBindings = CreateFrameEvaluationBindings(_bindings);
        _bindingsByTargetAttributeKey = BuildBindingLookup(_bindings);
        _bindingsByAnimationAddressKey = BuildAnimationBindingLookup(_bindings);
        _pointerEventDependencies = BuildPointerEventDependencies(_bindings);
        _accessKeyEventDependencies = BuildAccessKeyEventDependencies(_bindings);
        _pointerEventDependents = BuildPointerEventDependents(_bindings);
        _accessKeyEventDependents = BuildAccessKeyEventDependents(_bindings);
        _timelineTrackedBindings = DiscoverTimelineTrackedBindings(_bindings);
        _animatedTopLevelChildIndexes = DiscoverAnimatedTopLevelChildIndexes(sourceDocument, _bindings);
    }

    public SvgDocument SourceDocument { get; }

    public DateTimeOffset WallclockTimeOrigin { get; }

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

    internal bool BeginElement(SvgAnimationElement animation, TimeSpan offset)
    {
        ThrowIfDisposed();

        return TryScheduleElementInstance(animation, offset, _scheduledBeginInstances);
    }

    internal bool EndElement(SvgAnimationElement animation, TimeSpan offset)
    {
        ThrowIfDisposed();

        return TryScheduleElementInstance(animation, offset, _scheduledEndInstances);
    }

    internal bool TryGetStartTime(SvgAnimationElement animation, out TimeSpan startTime)
    {
        return TryGetStartTime(animation, Clock.CurrentTime, out startTime);
    }

    internal bool TryGetBaseAttributeValue(SvgElement element, string attributeName, out string value)
    {
        value = string.Empty;
        if (element is null || string.IsNullOrWhiteSpace(attributeName))
        {
            return false;
        }

        var targetAddress = SvgElementAddress.Create(element);
        if (!TryGetBindingByAttributeName(targetAddress, attributeName, out var binding))
        {
            return false;
        }

        value = binding.BaseValueString ?? string.Empty;
        return true;
    }

    private bool TryGetBindingByAttributeName(SvgElementAddress targetAddress, string attributeName, out AnimationBinding binding)
    {
        var key = string.Concat(targetAddress.Key, "|", attributeName);
        if (_bindingsByTargetAttributeKey.TryGetValue(key, out binding!))
        {
            return true;
        }

        var colonIndex = attributeName.IndexOf(':');
        if (colonIndex >= 0)
        {
            var localName = attributeName.Substring(colonIndex + 1);
            key = string.Concat(targetAddress.Key, "|", localName);
            return _bindingsByTargetAttributeKey.TryGetValue(key, out binding!);
        }

        key = string.Concat(targetAddress.Key, "|xlink:", attributeName);
        return _bindingsByTargetAttributeKey.TryGetValue(key, out binding!);
    }

    internal bool TryGetStartTime(SvgAnimationElement animation, TimeSpan currentTime, out TimeSpan startTime)
    {
        ThrowIfDisposed();

        startTime = default;
        if (!TryGetBinding(animation, out var binding))
        {
            return false;
        }

        var intervals = ResolveAnimationIntervals(binding, recursionGuard: null);
        if (intervals.Count == 0)
        {
            return false;
        }

        if (TrySelectCurrentInterval(binding.Animation, currentTime, intervals, requireActive: true, out var activeInterval))
        {
            startTime = activeInterval.BeginInstance.Time;
            return true;
        }

        for (var index = 0; index < intervals.Count; index++)
        {
            var candidate = intervals[index];
            if (candidate.BeginInstance.Time <= currentTime)
            {
                continue;
            }

            startTime = candidate.BeginInstance.Time;
            return true;
        }

        return false;
    }

    internal IReadOnlyList<SvgAnimationTimelineCallback> GetTimelineCallbacks(TimeSpan currentTime, TimeSpan? previousTime)
    {
        ThrowIfDisposed();

        if (_timelineTrackedBindings.Count == 0)
        {
            return Array.Empty<SvgAnimationTimelineCallback>();
        }

        if (previousTime.HasValue && currentTime < previousTime.Value)
        {
            return Array.Empty<SvgAnimationTimelineCallback>();
        }

        var callbacks = new List<SvgAnimationTimelineCallback>();
        for (var i = 0; i < _timelineTrackedBindings.Count; i++)
        {
            CollectTimelineCallbacks(_timelineTrackedBindings[i], previousTime, currentTime, callbacks);
        }

        callbacks.Sort(static (left, right) =>
        {
            var comparison = left.Time.CompareTo(right.Time);
            if (comparison != 0)
            {
                return comparison;
            }

            return string.CompareOrdinal(left.AttributeName, right.AttributeName);
        });

        return callbacks;
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
        clone.RebindSameDocumentDeferredPaintServers();

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
        var motionTransformPrefixes = new Dictionary<string, string>(StringComparer.Ordinal);
        var transformAnimationValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var binding in _frameEvaluationBindings)
        {
            attributes.TryGetValue(binding.TargetAttributeKey, out var currentAttributeState);
            if (!TryResolveAnimatedAttributeValue(this, binding, time, currentAttributeState?.Value, attributes, out var value))
            {
                continue;
            }

            if (binding.Animation is SvgAnimateMotion { Additive: not SvgAnimationAdditive.Sum })
            {
                motionTransformPrefixes[binding.TargetAttributeKey] = value;
                if (transformAnimationValues.TryGetValue(binding.TargetAttributeKey, out var transformAnimationValue))
                {
                    value = CombineTransformValue(value, transformAnimationValue);
                }
            }
            else if (binding.Animation is SvgAnimateTransform animateTransform)
            {
                transformAnimationValues[binding.TargetAttributeKey] = value;
                if (animateTransform.Additive != SvgAnimationAdditive.Sum &&
                    motionTransformPrefixes.TryGetValue(binding.TargetAttributeKey, out var motionTransformPrefix))
                {
                    value = CombineTransformValue(motionTransformPrefix, value);
                }
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

        List<AnimationBinding>? deferredSelectorRemovals = null;
        foreach (var removedKey in frameState.EnumerateRemovedKeys(previousState))
        {
            if (!_bindingsByTargetAttributeKey.TryGetValue(removedKey, out var binding))
            {
                continue;
            }

            if (RequiresSelectorStyleReapplication(binding.AttributeName))
            {
                (deferredSelectorRemovals ??= new List<AnimationBinding>()).Add(binding);
                continue;
            }

            ApplyRemovedFrameAttribute(document, binding);
        }

        List<SvgAnimationFrameAttributeState>? deferredAttributes = null;
        foreach (var attribute in frameState.EnumerateDirtyAttributes(previousState))
        {
            if (!RequiresSelectorStyleReapplication(attribute.AttributeName))
            {
                (deferredAttributes ??= new List<SvgAnimationFrameAttributeState>()).Add(attribute);
                continue;
            }

            ApplyDirtyFrameAttribute(document, attribute);
        }

        if (deferredSelectorRemovals is not null)
        {
            foreach (var binding in deferredSelectorRemovals)
            {
                ApplyRemovedFrameAttribute(document, binding);
            }
        }

        if (deferredAttributes is not null)
        {
            foreach (var attribute in deferredAttributes)
            {
                ApplyDirtyFrameAttribute(document, attribute);
            }
        }
    }

    private static void ApplyDirtyFrameAttribute(SvgDocument document, SvgAnimationFrameAttributeState attribute)
    {
        var target = attribute.TargetAddress.Resolve(document);
        if (target is null)
        {
            return;
        }

        _ = SetAttributeValue(target, attribute.AttributeName, attribute.Value);
    }

    private static void ApplyRemovedFrameAttribute(SvgDocument document, AnimationBinding binding)
    {
        var target = binding.TargetAddress.Resolve(document);
        if (target is null)
        {
            return;
        }

        if (binding.BaseValueString is not null)
        {
            _ = SetAttributeValue(target, binding.AttributeName, binding.BaseValueString);

            if (!binding.HasExplicitBaseAttribute)
            {
                _ = ClearAttributeValue(target, binding.AttributeName);
            }

            return;
        }

        _ = ClearAttributeValue(target, binding.AttributeName);
    }

    private static bool RequiresSelectorStyleReapplication(string attributeName)
    {
        return string.Equals(attributeName, "class", StringComparison.Ordinal) ||
               string.Equals(attributeName, "style", StringComparison.Ordinal);
    }

    public bool RecordPointerEvent(SvgElement? element, SvgPointerEventType eventType)
    {
        return RecordPointerEvent(element, eventType, Clock.CurrentTime);
    }

    public bool RecordPointerEvent(SvgElement? element, SvgPointerEventType eventType, TimeSpan eventTime)
    {
        ThrowIfDisposed();

        if (!HasAnimations || element is null)
        {
            return false;
        }

        var key = CreateEventInstanceKey(SvgElementAddress.Create(element), ToTimingEventType(eventType));
        if (!_pointerEventDependencies.Contains(key))
        {
            return false;
        }

        RecordUserEventInstance(_pointerEventInstances, key, eventTime);
        PrunePointerEventInstances(key);
        InvalidateFrameStateCache();
        return true;
    }

    public bool RecordAccessKey(string? accessKey)
    {
        return RecordAccessKey(accessKey, Clock.CurrentTime);
    }

    public bool RecordAccessKey(string? accessKey, TimeSpan eventTime)
    {
        ThrowIfDisposed();

        if (!HasAnimations || !TryNormalizeAccessKey(accessKey, out var normalizedAccessKey))
        {
            return false;
        }

        var key = CreateAccessKeyEventInstanceKey(normalizedAccessKey);
        if (!_accessKeyEventDependencies.Contains(key))
        {
            return false;
        }

        RecordUserEventInstance(_accessKeyEventInstances, key, eventTime);
        PruneAccessKeyEventInstances(key);
        InvalidateFrameStateCache();
        return true;
    }

    private static void RecordUserEventInstance(Dictionary<string, List<TimeSpan>> eventInstances, string key, TimeSpan eventTime)
    {
        if (eventTime < TimeSpan.Zero)
        {
            eventTime = TimeSpan.Zero;
        }

        if (!eventInstances.TryGetValue(key, out var eventTimes))
        {
            eventTimes = new List<TimeSpan>();
            eventInstances[key] = eventTimes;
        }

        eventTimes.Add(eventTime);
        eventTimes.Sort();
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _pointerEventInstances.Clear();
        _accessKeyEventInstances.Clear();
        _scheduledBeginInstances.Clear();
        _scheduledEndInstances.Clear();
        InvalidateFrameStateCache();
        var currentTime = Clock.CurrentTime;
        Clock.Reset();

        if (currentTime == TimeSpan.Zero && HasAnimations)
        {
            FrameChanged?.Invoke(this, new SvgAnimationFrameChangedEventArgs(TimeSpan.Zero));
        }
    }

    private bool TryScheduleElementInstance(
        SvgAnimationElement animation,
        TimeSpan offset,
        Dictionary<string, List<TimeSpan>> scheduledInstances)
    {
        if (!TryGetBinding(animation, out var binding))
        {
            return false;
        }

        var scheduledTime = Clock.CurrentTime + offset;
        if (scheduledTime < TimeSpan.Zero)
        {
            scheduledTime = TimeSpan.Zero;
        }

        if (!scheduledInstances.TryGetValue(binding.AnimationAddress.Key, out var times))
        {
            times = new List<TimeSpan>();
            scheduledInstances[binding.AnimationAddress.Key] = times;
        }

        times.Add(scheduledTime);
        times.Sort();
        InvalidateFrameStateCache();
        return true;
    }

    private bool TryGetBinding(SvgAnimationElement animation, out AnimationBinding binding)
    {
        return _bindingsByAnimationAddressKey.TryGetValue(SvgElementAddress.Create(animation).Key, out binding!);
    }

    private IReadOnlyList<ResolvedTimingInstance> ResolveBeginInstances(AnimationBinding binding, HashSet<string>? recursionGuard, TimeSpan? horizon = null)
    {
        if (!binding.HasDynamicBeginTiming &&
            (!_scheduledBeginInstances.TryGetValue(binding.AnimationAddress.Key, out var scheduledTimes) || scheduledTimes.Count == 0))
        {
            return binding.StaticBeginInstances;
        }

        recursionGuard ??= new HashSet<string>(StringComparer.Ordinal);
        if (!recursionGuard.Add(binding.AnimationAddress.Key))
        {
            return s_emptyResolvedTimingInstances;
        }

        try
        {
            var instances = ResolveTimingInstancesDetailed(binding.BeginSpecs, recursionGuard, horizon);
            AppendScheduledInstances(binding.AnimationAddress, SvgAnimationTimingEventType.Begin, _scheduledBeginInstances, instances);
            SortAndDeduplicateTimingInstances(instances);
            return instances.Count == 0 ? s_emptyResolvedTimingInstances : instances;
        }
        finally
        {
            recursionGuard.Remove(binding.AnimationAddress.Key);
        }
    }

    private IReadOnlyList<ResolvedTimingInstance> ResolveEndTimingInstances(AnimationBinding binding, HashSet<string>? recursionGuard, TimeSpan? horizon = null)
    {
        if (!binding.HasDynamicEndTiming &&
            (!_scheduledEndInstances.TryGetValue(binding.AnimationAddress.Key, out var scheduledTimes) || scheduledTimes.Count == 0))
        {
            return binding.StaticEndInstances;
        }

        recursionGuard ??= new HashSet<string>(StringComparer.Ordinal);
        if (!recursionGuard.Add(binding.AnimationAddress.Key))
        {
            return s_emptyResolvedTimingInstances;
        }

        try
        {
            var instances = ResolveTimingInstancesDetailed(binding.EndSpecs, recursionGuard, horizon);
            AppendScheduledInstances(binding.AnimationAddress, SvgAnimationTimingEventType.End, _scheduledEndInstances, instances);
            SortAndDeduplicateTimingInstances(instances);
            return instances.Count == 0 ? s_emptyResolvedTimingInstances : instances;
        }
        finally
        {
            recursionGuard.Remove(binding.AnimationAddress.Key);
        }
    }

    private static void AppendScheduledInstances(
        SvgElementAddress animationAddress,
        SvgAnimationTimingEventType eventType,
        IReadOnlyDictionary<string, List<TimeSpan>> scheduledInstances,
        List<ResolvedTimingInstance> target)
    {
        if (!scheduledInstances.TryGetValue(animationAddress.Key, out var times) || times.Count == 0)
        {
            return;
        }

        var eventInstanceKey = CreateEventInstanceKey(animationAddress, eventType);
        for (var index = 0; index < times.Count; index++)
        {
            var time = times[index];
            target.Add(new ResolvedTimingInstance(time, eventInstanceKey, time));
        }
    }

    private static void SortAndDeduplicateTimingInstances(List<ResolvedTimingInstance> instances)
    {
        if (instances.Count <= 1)
        {
            return;
        }

        instances.Sort(static (left, right) =>
        {
            var comparison = left.Time.CompareTo(right.Time);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.CompareOrdinal(left.EventInstanceKey, right.EventInstanceKey);
            if (comparison != 0)
            {
                return comparison;
            }

            return Nullable.Compare(left.SourceEventTime, right.SourceEventTime);
        });

        var writeIndex = 1;
        for (var readIndex = 1; readIndex < instances.Count; readIndex++)
        {
            var previous = instances[writeIndex - 1];
            var current = instances[readIndex];
            if (previous.Time == current.Time &&
                string.Equals(previous.EventInstanceKey, current.EventInstanceKey, StringComparison.Ordinal) &&
                previous.SourceEventTime == current.SourceEventTime)
            {
                continue;
            }

            instances[writeIndex++] = current;
        }

        if (writeIndex < instances.Count)
        {
            instances.RemoveRange(writeIndex, instances.Count - writeIndex);
        }
    }

    private static bool TryResolveCurrentActiveIntervalDetailed(
        SvgAnimationElement animation,
        TimeSpan time,
        bool allowIndefiniteDiscrete,
        IReadOnlyList<ResolvedTimingInstance> beginInstances,
        IReadOnlyList<ResolvedTimingInstance> endInstances,
        out ResolvedAnimationInterval interval)
    {
        if (!TryResolveCurrentIntervalDetailed(animation, time, allowIndefiniteDiscrete, beginInstances, endInstances, out interval))
        {
            return false;
        }

        return interval.IsActive(time);
    }

    private void CollectTimelineCallbacks(
        AnimationBinding binding,
        TimeSpan? previousTime,
        TimeSpan currentTime,
        List<SvgAnimationTimelineCallback> callbacks)
    {
        var intervals = ResolveAnimationIntervals(binding, recursionGuard: null);
        if (intervals.Count == 0)
        {
            return;
        }

        for (var index = 0; index < intervals.Count; index++)
        {
            var beginTime = intervals[index].BeginInstance.Time;
            if (ShouldDispatchTimelineEvent(beginTime, previousTime, currentTime))
            {
                callbacks.Add(new SvgAnimationTimelineCallback(
                    binding.AnimationAddress,
                    "beginEvent",
                    "onbegin",
                    beginTime));
            }
        }

        for (var index = 0; index < intervals.Count; index++)
        {
            if (intervals[index].ActiveEnd is not { } endTime ||
                !ShouldDispatchTimelineEvent(endTime, previousTime, currentTime))
            {
                continue;
            }

            callbacks.Add(new SvgAnimationTimelineCallback(
                binding.AnimationAddress,
                "endEvent",
                "onend",
                endTime));
        }

        for (var index = 0; index < intervals.Count; index++)
        {
            var repeatTimes = new List<TimeSpan>();
            AddRepeatEventInstances(binding.Animation, intervals[index], requestedIteration: null, repeatTimes);
            for (var repeatIndex = 0; repeatIndex < repeatTimes.Count; repeatIndex++)
            {
                var repeatTime = repeatTimes[repeatIndex];
                if (!ShouldDispatchTimelineEvent(repeatTime, previousTime, currentTime))
                {
                    continue;
                }

                callbacks.Add(new SvgAnimationTimelineCallback(
                    binding.AnimationAddress,
                    "repeatEvent",
                    "onrepeat",
                    repeatTime));
            }
        }
    }

    private static bool ShouldDispatchTimelineEvent(TimeSpan eventTime, TimeSpan? previousTime, TimeSpan currentTime)
    {
        if (eventTime > currentTime)
        {
            return false;
        }

        return previousTime.HasValue
            ? eventTime > previousTime.Value
            : eventTime == currentTime;
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

    private static HashSet<string> BuildAccessKeyEventDependencies(IEnumerable<AnimationBinding> bindings)
    {
        var dependencies = new HashSet<string>(StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            AddAccessKeyEventDependencies(binding.BeginSpecs, dependencies);
            AddAccessKeyEventDependencies(binding.EndSpecs, dependencies);
        }

        return dependencies;
    }

    private static Dictionary<string, List<PointerEventDependency>> BuildPointerEventDependents(IEnumerable<AnimationBinding> bindings)
    {
        var dependents = new Dictionary<string, List<PointerEventDependency>>(StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            AddPointerEventDependents(binding, binding.BeginSpecs, dependents);
            AddPointerEventDependents(binding, binding.EndSpecs, dependents);
        }

        return dependents;
    }

    private static Dictionary<string, List<PointerEventDependency>> BuildAccessKeyEventDependents(IEnumerable<AnimationBinding> bindings)
    {
        var dependents = new Dictionary<string, List<PointerEventDependency>>(StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            AddAccessKeyEventDependents(binding, binding.BeginSpecs, dependents);
            AddAccessKeyEventDependents(binding, binding.EndSpecs, dependents);
        }

        return dependents;
    }

    private static void AddPointerEventDependents(
        AnimationBinding binding,
        IEnumerable<TimingSpec> specs,
        Dictionary<string, List<PointerEventDependency>> dependents)
    {
        foreach (var spec in specs)
        {
            if (!TryGetPointerEventInstanceKey(spec, out var eventInstanceKey))
            {
                continue;
            }

            if (!dependents.TryGetValue(eventInstanceKey, out var bindingsForKey))
            {
                bindingsForKey = new List<PointerEventDependency>();
                dependents[eventInstanceKey] = bindingsForKey;
            }

            if (bindingsForKey.Any(existing => ReferenceEquals(existing.Binding, binding)))
            {
                continue;
            }

            bindingsForKey.Add(new PointerEventDependency(binding));
        }
    }

    private static void AddAccessKeyEventDependents(
        AnimationBinding binding,
        IEnumerable<TimingSpec> specs,
        Dictionary<string, List<PointerEventDependency>> dependents)
    {
        foreach (var spec in specs)
        {
            if (!TryGetAccessKeyEventInstanceKey(spec, out var eventInstanceKey))
            {
                continue;
            }

            if (!dependents.TryGetValue(eventInstanceKey, out var bindingsForKey))
            {
                bindingsForKey = new List<PointerEventDependency>();
                dependents[eventInstanceKey] = bindingsForKey;
            }

            if (bindingsForKey.Any(existing => ReferenceEquals(existing.Binding, binding)))
            {
                continue;
            }

            bindingsForKey.Add(new PointerEventDependency(binding));
        }
    }

    private static List<AnimationBinding> CreateFrameEvaluationBindings(List<AnimationBinding> bindings)
    {
        return bindings
            .Select(static (binding, index) => (binding, index))
            .OrderBy(static entry => IsCurrentColorSourceAttribute(entry.binding.AttributeName) ? 0 : 1)
            .ThenBy(static entry => entry.index)
            .Select(static entry => entry.binding)
            .ToList();
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

    private static Dictionary<string, AnimationBinding> BuildAnimationBindingLookup(IEnumerable<AnimationBinding> bindings)
    {
        var lookup = new Dictionary<string, AnimationBinding>(StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
            if (!lookup.ContainsKey(binding.AnimationAddress.Key))
            {
                lookup.Add(binding.AnimationAddress.Key, binding);
            }
        }

        return lookup;
    }

    private static List<AnimationBinding> DiscoverTimelineTrackedBindings(IEnumerable<AnimationBinding> bindings)
    {
        var trackedBindings = new List<AnimationBinding>();

        foreach (var binding in bindings)
        {
            trackedBindings.Add(binding);
        }

        return trackedBindings;
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
            if (TryGetPointerEventInstanceKey(spec, out var eventInstanceKey))
            {
                dependencies.Add(eventInstanceKey);
            }
        }
    }

    private static void AddAccessKeyEventDependencies(IEnumerable<TimingSpec> specs, HashSet<string> dependencies)
    {
        foreach (var spec in specs)
        {
            if (TryGetAccessKeyEventInstanceKey(spec, out var eventInstanceKey))
            {
                dependencies.Add(eventInstanceKey);
            }
        }
    }

    private static bool TryGetPointerEventInstanceKey(TimingSpec spec, out string key)
    {
        if (spec.IsEvent && IsPointerTimingEventType(spec.EventType))
        {
            key = CreateEventInstanceKey(spec.EventAddress!, spec.EventType);
            return true;
        }

        key = string.Empty;
        return false;
    }

    private static bool TryGetAccessKeyEventInstanceKey(TimingSpec spec, out string key)
    {
        if (spec.IsAccessKey && spec.AccessKey is { } accessKey)
        {
            key = CreateAccessKeyEventInstanceKey(accessKey);
            return true;
        }

        key = string.Empty;
        return false;
    }

    private static bool HasExplicitAnimationBaseAttribute(SvgElement sourceTarget, string attributeName, object? baseValue)
    {
        if (sourceTarget.ContainsAttribute(attributeName))
        {
            return true;
        }

        return IsHrefAnimationAttribute(attributeName) && baseValue is not null;
    }

    private static bool IsHrefAnimationAttribute(string attributeName)
    {
        return string.Equals(attributeName, "href", StringComparison.Ordinal) ||
               string.Equals(attributeName, "xlink:href", StringComparison.Ordinal) ||
               attributeName.StartsWith(SvgNamespaces.XLinkNamespace + ":href", StringComparison.Ordinal);
    }

    private static List<AnimationBinding> DiscoverBindings(SvgDocument sourceDocument, DateTimeOffset wallclockTimeOrigin)
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

            if (ShouldIgnorePaintServerDefinitionAnimateColorBinding(animation, target, attributeName!))
            {
                continue;
            }

            bindings.Add(new AnimationBinding(animation, target, SvgElementAddress.Create(target), attributeName!, wallclockTimeOrigin));
        }

        return bindings;
    }

    private static bool ShouldIgnorePaintServerDefinitionAnimateColorBinding(
        SvgAnimationElement animation,
        SvgElement target,
        string attributeName)
    {
        // Direct SVG 1.1 animateColor interpolation is supported. This guard is
        // limited to inherited paint-server color state in defs, where current
        // browser snapshots keep referenced gradients stable while regular
        // numeric animation on the same subtree still applies.
        if (animation is not SvgAnimateColor ||
            !IsInheritedPaintServerColorAttribute(attributeName) ||
            !IsInsideDefinitions(target))
        {
            return false;
        }

        return SubtreeContainsPaintServer(target);
    }

    private static bool IsInheritedPaintServerColorAttribute(string attributeName)
    {
        return string.Equals(attributeName, "color", StringComparison.Ordinal) ||
               string.Equals(attributeName, "stop-color", StringComparison.Ordinal);
    }

    private static bool IsCurrentColorSourceAttribute(string attributeName)
    {
        return string.Equals(attributeName, "color", StringComparison.Ordinal);
    }

    private static bool IsInsideDefinitions(SvgElement element)
    {
        for (var current = element.Parent as SvgElement; current is not null; current = current.Parent as SvgElement)
        {
            if (current is SvgDefinitionList)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SubtreeContainsPaintServer(SvgElement element)
    {
        if (element is SvgPaintServer)
        {
            return true;
        }

        for (var i = 0; i < element.Children.Count; i++)
        {
            if (SubtreeContainsPaintServer(element.Children[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void InvalidateFrameStateCache()
    {
        _frameStateVersion++;
        _cachedFrameState = null;
    }

    private static List<TimingSpec> ParseTimingSpecifications(string? value, SvgDocument? document, SvgElementAddress defaultEventAddress, DateTimeOffset wallclockTimeOrigin, bool includeImplicitDocumentBegin)
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

            if (SvgAnimationParser.TryParseWallclockTimingSpec(token, out var wallclockTime))
            {
                specs.Add(new TimingSpec(wallclockTime.ToUniversalTime() - wallclockTimeOrigin));
                continue;
            }

            if (SvgAnimationParser.TryParseAccessKeyTimingSpec(token, out var accessKey, out var accessKeyOffset))
            {
                specs.Add(new TimingSpec(NormalizeAccessKey(accessKey), accessKeyOffset));
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

        PruneUserEventInstances(key, eventTimes, _pointerEventDependents);
    }

    private void PruneAccessKeyEventInstances(string key)
    {
        if (!_accessKeyEventInstances.TryGetValue(key, out var eventTimes) ||
            eventTimes.Count <= 1)
        {
            return;
        }

        PruneUserEventInstances(key, eventTimes, _accessKeyEventDependents);
    }

    private void PruneUserEventInstances(
        string key,
        List<TimeSpan> eventTimes,
        IReadOnlyDictionary<string, List<PointerEventDependency>> dependentsByKey)
    {
        if (!dependentsByKey.TryGetValue(key, out var dependents) ||
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
        var beginInstances = ResolveBeginInstances(binding, recursionGuard: null);
        var endInstances = ResolveEndTimingInstances(binding, recursionGuard: null);

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

        spec = new TimingSpec(parsedTiming.EventAddress, parsedTiming.EventType, parsedTiming.Offset, parsedTiming.RepeatIteration);
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
                : ResolveNamespacedAnimationAttributeName(animation, animateTransform.AnimationAttributeName);
        }

        if (animation is SvgAnimationAttributeElement attributeAnimation)
        {
            return ResolveNamespacedAnimationAttributeName(animation, attributeAnimation.AnimationAttributeName);
        }

        return null;
    }

    private static string? ResolveNamespacedAnimationAttributeName(SvgAnimationElement animation, string? attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return attributeName;
        }

        var resolvedAttributeName = attributeName!;
        var colonIndex = resolvedAttributeName.IndexOf(':');
        if (colonIndex <= 0 || colonIndex == resolvedAttributeName.Length - 1)
        {
            return resolvedAttributeName;
        }

        var prefix = resolvedAttributeName.Substring(0, colonIndex);
        var localName = resolvedAttributeName.Substring(colonIndex + 1);
        if (TryResolveNamespace(animation, prefix, out var namespaceName))
        {
            if (string.Equals(namespaceName, SvgNamespaces.XLinkNamespace, StringComparison.Ordinal))
            {
                return string.Concat("xlink:", localName);
            }

            if (string.Equals(namespaceName, SvgNamespaces.XmlNamespace, StringComparison.Ordinal))
            {
                return string.Concat("xml:", localName);
            }

            return resolvedAttributeName;
        }

        return resolvedAttributeName;
    }

    private static bool TryResolveNamespace(SvgElement element, string prefix, out string namespaceName)
    {
        for (SvgElement? current = element; current is not null; current = current.Parent as SvgElement)
        {
            if (current.Namespaces.TryGetValue(prefix, out var resolvedNamespace))
            {
                namespaceName = resolvedNamespace;
                return true;
            }
        }

        var document = element as SvgDocument ?? element.OwnerDocument;
        if (document is not null && document.Namespaces.TryGetValue(prefix, out var documentNamespace))
        {
            namespaceName = documentNamespace;
            return true;
        }

        namespaceName = string.Empty;
        return false;
    }

    private static bool TryResolveAnimatedAttributeValue(
        SvgAnimationController controller,
        AnimationBinding binding,
        TimeSpan time,
        string? currentComposedValue,
        IReadOnlyDictionary<string, SvgAnimationFrameAttributeState>? frameAttributes,
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
                    !TryResolveAnimatedValue(binding, animateColor, colorSample, forceColorInterpolation: true, frameAttributes, out value))
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
                    !TryResolveAnimatedValue(binding, animate, valueSample, forceColorInterpolation: false, frameAttributes, out value))
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

        if (!TryResolveAnimatedValue(binding, animation, sample, forceColorInterpolation, frameAttributes: null, out var value))
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

        public bool IsActive(TimeSpan time) => IsTimeInActiveInterval(Begin, ActiveEnd, time);
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

        public bool IsActive(TimeSpan time) => IsTimeInActiveInterval(BeginInstance.Time, ActiveEnd, time);
    }

    private static bool IsTimeInActiveInterval(TimeSpan begin, TimeSpan? activeEnd, TimeSpan time)
    {
        if (!activeEnd.HasValue)
        {
            return true;
        }

        return activeEnd.Value == begin
            ? time == begin
            : time < activeEnd.Value;
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

        var intervals = controller.ResolveAnimationIntervals(binding, recursionGuard: null, horizon: time);
        if (!TrySelectCurrentInterval(animation, time, intervals, requireActive: false, out var resolved))
        {
            return false;
        }

        if (resolved.IsActive(time))
        {
            interval = (true, false, resolved.BeginInstance.Time, resolved.ActiveEnd);
            return true;
        }

        if (animation.AnimationFill == SvgAnimationFill.Freeze)
        {
            interval = (false, true, resolved.BeginInstance.Time, resolved.ActiveEnd);
            return true;
        }

        return false;
    }

    private List<ResolvedTimingInstance> ResolveTimingInstancesDetailed(IReadOnlyList<TimingSpec> specs, HashSet<string>? recursionGuard, TimeSpan? horizon = null)
    {
        var instances = new List<ResolvedTimingInstance>();

        foreach (var spec in specs)
        {
            if (TryGetAccessKeyEventInstanceKey(spec, out var accessKeyEventKey))
            {
                if (!_accessKeyEventInstances.TryGetValue(accessKeyEventKey, out var eventTimes))
                {
                    continue;
                }

                for (var index = 0; index < eventTimes.Count; index++)
                {
                    var eventTime = eventTimes[index];
                    instances.Add(new ResolvedTimingInstance(
                        eventTime + spec.Offset,
                        accessKeyEventKey,
                        eventTime));
                }

                continue;
            }

            if (!spec.IsEvent)
            {
                instances.Add(new ResolvedTimingInstance(spec.Offset, eventInstanceKey: null, sourceEventTime: null));
                continue;
            }

            if (TryGetPointerEventInstanceKey(spec, out var pointerEventKey))
            {
                if (!_pointerEventInstances.TryGetValue(pointerEventKey, out var eventTimes))
                {
                    continue;
                }

                for (var index = 0; index < eventTimes.Count; index++)
                {
                    var eventTime = eventTimes[index];
                    instances.Add(new ResolvedTimingInstance(
                        eventTime + spec.Offset,
                        pointerEventKey,
                        eventTime));
                }

                continue;
            }

            var dependencyInstances = ResolveAnimationEventInstances(spec, recursionGuard, horizon);
            if (dependencyInstances.Count == 0)
            {
                continue;
            }

            var eventInstanceKey = CreateEventInstanceKey(spec.EventAddress!, spec.EventType);
            for (var index = 0; index < dependencyInstances.Count; index++)
            {
                var dependencyTime = dependencyInstances[index];
                instances.Add(new ResolvedTimingInstance(
                    dependencyTime + spec.Offset,
                    eventInstanceKey,
                    dependencyTime));
            }
        }

        SortAndDeduplicateTimingInstances(instances);
        return instances;
    }

    private List<TimeSpan> ResolveAnimationEventInstances(TimingSpec spec, HashSet<string>? recursionGuard, TimeSpan? horizon = null)
    {
        if (spec.EventAddress is null ||
            !_bindingsByAnimationAddressKey.TryGetValue(spec.EventAddress.Key, out var dependencyBinding))
        {
            return new List<TimeSpan>();
        }

        var dependencyIntervals = ResolveAnimationIntervals(dependencyBinding, recursionGuard, horizon);
        if (dependencyIntervals.Count == 0)
        {
            return new List<TimeSpan>();
        }

        var instances = new List<TimeSpan>(dependencyIntervals.Count);
        for (var index = 0; index < dependencyIntervals.Count; index++)
        {
            var interval = dependencyIntervals[index];
            switch (spec.EventType)
            {
                case SvgAnimationTimingEventType.Begin:
                    instances.Add(interval.BeginInstance.Time);
                    break;
                case SvgAnimationTimingEventType.End:
                    if (interval.ActiveEnd.HasValue)
                    {
                        instances.Add(interval.ActiveEnd.Value);
                    }

                    break;
                case SvgAnimationTimingEventType.Repeat:
                    AddRepeatEventInstances(dependencyBinding.Animation, interval, spec.RepeatIteration, instances);
                    break;
            }
        }

        if (instances.Count > 1)
        {
            instances.Sort();
        }

        return instances;
    }

    private static void AddRepeatEventInstances(
        SvgAnimationElement animation,
        ResolvedAnimationInterval interval,
        int? requestedIteration,
        List<TimeSpan> instances)
    {
        if (!TryParseClockValue(animation.Duration, out var simpleDuration) || simpleDuration <= TimeSpan.Zero)
        {
            return;
        }

        var activeEnd = interval.ActiveEnd;
        if (requestedIteration.HasValue)
        {
            AddRepeatEventInstance(interval.BeginInstance.Time, simpleDuration, activeEnd, requestedIteration.Value, instances);
            return;
        }

        var repeatLimit = ResolveRepeatEventLimit(animation, simpleDuration, interval);
        for (var iteration = 1; iteration <= repeatLimit; iteration++)
        {
            AddRepeatEventInstance(interval.BeginInstance.Time, simpleDuration, activeEnd, iteration, instances);
        }
    }

    private static int ResolveRepeatEventLimit(SvgAnimationElement animation, TimeSpan simpleDuration, ResolvedAnimationInterval interval)
    {
        const int maxUnboundedRepeatEvents = 4096;
        var repeatCountMode = ParseRepeatCount(animation.RepeatCount, out var repeatCount);
        if (repeatCountMode == RepeatCountMode.Finite)
        {
            return Math.Max(0, (int)Math.Ceiling(repeatCount) - 1);
        }

        if (interval.ActiveEnd.HasValue)
        {
            var elapsed = interval.ActiveEnd.Value - interval.BeginInstance.Time;
            if (elapsed <= TimeSpan.Zero)
            {
                return 0;
            }

            return Math.Max(0, (int)(elapsed.Ticks / simpleDuration.Ticks));
        }

        return maxUnboundedRepeatEvents;
    }

    private static void AddRepeatEventInstance(
        TimeSpan begin,
        TimeSpan simpleDuration,
        TimeSpan? activeEnd,
        int iteration,
        List<TimeSpan> instances)
    {
        if (iteration <= 0)
        {
            return;
        }

        var time = begin + Multiply(simpleDuration, iteration);
        if (time <= begin)
        {
            return;
        }

        if (activeEnd.HasValue && time >= activeEnd.Value)
        {
            return;
        }

        instances.Add(time);
    }

    private List<ResolvedAnimationInterval> ResolveAnimationIntervals(AnimationBinding binding, HashSet<string>? recursionGuard, TimeSpan? horizon = null)
    {
        var beginInstances = ResolveBeginInstances(binding, recursionGuard, horizon);
        if (beginInstances.Count == 0)
        {
            return new List<ResolvedAnimationInterval>();
        }

        var endInstances = ResolveEndTimingInstances(binding, recursionGuard, horizon);
        var allowIndefiniteDiscrete = binding.Animation is SvgSet;
        if (HasSelfEventTiming(binding.BeginSpecs, binding.AnimationAddress) ||
            HasSelfEventTiming(binding.EndSpecs, binding.AnimationAddress))
        {
            return ResolveAnimationIntervalsWithSelfSync(binding, beginInstances, endInstances, allowIndefiniteDiscrete, horizon);
        }

        return ResolveAnimationIntervalsCore(binding, beginInstances, endInstances, allowIndefiniteDiscrete, horizon);
    }

    private static List<ResolvedAnimationInterval> ResolveAnimationIntervalsCore(
        AnimationBinding binding,
        IReadOnlyList<ResolvedTimingInstance> beginInstances,
        IReadOnlyList<ResolvedTimingInstance> endInstances,
        bool allowIndefiniteDiscrete,
        TimeSpan? horizon)
    {
        var intervals = new List<ResolvedAnimationInterval>();
        ResolvedAnimationInterval? selected = null;

        for (var index = 0; index < beginInstances.Count; index++)
        {
            var begin = beginInstances[index];
            if (horizon.HasValue && begin.Time > horizon.Value)
            {
                break;
            }

            if (selected.HasValue)
            {
                switch (binding.Animation.Restart)
                {
                    case SvgAnimationRestart.Never:
                        continue;
                    case SvgAnimationRestart.WhenNotActive:
                        if (!selected.Value.ActiveEnd.HasValue || begin.Time < selected.Value.ActiveEnd.Value)
                        {
                            continue;
                        }

                        break;
                    case SvgAnimationRestart.Always:
                        TruncateRestartedInterval(intervals, selected.Value, begin.Time);
                        break;
                }
            }

            var effectiveEndInstances = CreateEffectiveEndInstances(binding, endInstances, begin.Time, horizon);
            if (!TryResolveIntervalEndDetailed(binding.Animation, begin.Time, effectiveEndInstances, allowIndefiniteDiscrete, out var activeEnd, out var endInstance))
            {
                continue;
            }

            selected = new ResolvedAnimationInterval(begin, activeEnd, endInstance);
            intervals.Add(selected.Value);
        }

        return intervals;
    }

    private static List<ResolvedAnimationInterval> ResolveAnimationIntervalsWithSelfSync(
        AnimationBinding binding,
        IReadOnlyList<ResolvedTimingInstance> seedBeginInstances,
        IReadOnlyList<ResolvedTimingInstance> endInstances,
        bool allowIndefiniteDiscrete,
        TimeSpan? horizon)
    {
        const int maxSelfSyncIntervals = 4096;

        var pendingBeginInstances = new List<ResolvedTimingInstance>(seedBeginInstances);
        SortAndDeduplicateTimingInstances(pendingBeginInstances);

        var knownBeginTimes = new HashSet<long>();
        for (var index = 0; index < pendingBeginInstances.Count; index++)
        {
            knownBeginTimes.Add(pendingBeginInstances[index].Time.Ticks);
        }

        var intervals = new List<ResolvedAnimationInterval>();
        ResolvedAnimationInterval? selected = null;
        var cursor = 0;

        while (cursor < pendingBeginInstances.Count && intervals.Count < maxSelfSyncIntervals)
        {
            var begin = pendingBeginInstances[cursor++];
            if (horizon.HasValue && begin.Time > horizon.Value)
            {
                break;
            }

            if (selected.HasValue)
            {
                switch (binding.Animation.Restart)
                {
                    case SvgAnimationRestart.Never:
                        continue;
                    case SvgAnimationRestart.WhenNotActive:
                        if (!selected.Value.ActiveEnd.HasValue || begin.Time < selected.Value.ActiveEnd.Value)
                        {
                            continue;
                        }

                        break;
                    case SvgAnimationRestart.Always:
                        TruncateRestartedInterval(intervals, selected.Value, begin.Time);
                        break;
                }
            }

            var effectiveEndInstances = CreateEffectiveEndInstances(binding, endInstances, begin.Time, horizon);
            if (!TryResolveIntervalEndDetailed(binding.Animation, begin.Time, effectiveEndInstances, allowIndefiniteDiscrete, out var activeEnd, out var endInstance))
            {
                continue;
            }

            selected = new ResolvedAnimationInterval(begin, activeEnd, endInstance);
            intervals.Add(selected.Value);

            AddSelfSyncBeginInstances(
                binding,
                selected.Value,
                pendingBeginInstances,
                knownBeginTimes,
                horizon);

            if (pendingBeginInstances.Count > cursor)
            {
                SortAndDeduplicateTimingInstances(pendingBeginInstances);
            }
        }

        return intervals;
    }

    private static void TruncateRestartedInterval(
        List<ResolvedAnimationInterval> intervals,
        ResolvedAnimationInterval selected,
        TimeSpan restartTime)
    {
        if (intervals.Count == 0 ||
            (selected.ActiveEnd.HasValue && selected.ActiveEnd.Value <= restartTime))
        {
            return;
        }

        intervals[intervals.Count - 1] = new ResolvedAnimationInterval(
            selected.BeginInstance,
            restartTime,
            selected.EndInstance);
    }

    private static IReadOnlyList<ResolvedTimingInstance> CreateEffectiveEndInstances(
        AnimationBinding binding,
        IReadOnlyList<ResolvedTimingInstance> endInstances,
        TimeSpan beginTime,
        TimeSpan? horizon)
    {
        if (!HasSelfEventTiming(binding.EndSpecs, binding.AnimationAddress))
        {
            return endInstances;
        }

        var effective = new List<ResolvedTimingInstance>(endInstances);
        for (var index = 0; index < binding.EndSpecs.Count; index++)
        {
            var spec = binding.EndSpecs[index];
            if (!IsSelfEventTiming(spec, binding.AnimationAddress))
            {
                continue;
            }

            var eventInstanceKey = CreateEventInstanceKey(binding.AnimationAddress, spec.EventType);
            switch (spec.EventType)
            {
                case SvgAnimationTimingEventType.Begin:
                    AddSelfEndInstance(beginTime, beginTime, spec.Offset, eventInstanceKey, effective, horizon);
                    break;
                case SvgAnimationTimingEventType.Repeat:
                    var provisionalInterval = new ResolvedAnimationInterval(
                        new ResolvedTimingInstance(beginTime, eventInstanceKey: null, sourceEventTime: null),
                        activeEnd: null,
                        endInstance: null);
                    var repeatTimes = new List<TimeSpan>();
                    AddRepeatEventInstances(binding.Animation, provisionalInterval, spec.RepeatIteration, repeatTimes);
                    for (var repeatIndex = 0; repeatIndex < repeatTimes.Count; repeatIndex++)
                    {
                        AddSelfEndInstance(repeatTimes[repeatIndex], beginTime, spec.Offset, eventInstanceKey, effective, horizon);
                    }

                    break;
            }
        }

        SortAndDeduplicateTimingInstances(effective);
        return effective.Count == 0 ? s_emptyResolvedTimingInstances : effective;
    }

    private static void AddSelfEndInstance(
        TimeSpan eventTime,
        TimeSpan sourceBegin,
        TimeSpan offset,
        string eventInstanceKey,
        List<ResolvedTimingInstance> endInstances,
        TimeSpan? horizon)
    {
        var candidateTime = eventTime + offset;
        if (candidateTime <= sourceBegin ||
            (horizon.HasValue && candidateTime > horizon.Value))
        {
            return;
        }

        endInstances.Add(new ResolvedTimingInstance(candidateTime, eventInstanceKey, eventTime));
    }

    private static bool HasSelfEventTiming(IReadOnlyList<TimingSpec> specs, SvgElementAddress animationAddress)
    {
        for (var index = 0; index < specs.Count; index++)
        {
            if (IsSelfEventTiming(specs[index], animationAddress))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSelfEventTiming(TimingSpec spec, SvgElementAddress animationAddress)
    {
        return spec.IsEvent &&
               spec.EventAddress is { } eventAddress &&
               string.Equals(eventAddress.Key, animationAddress.Key, StringComparison.Ordinal);
    }

    private static void AddSelfSyncBeginInstances(
        AnimationBinding binding,
        ResolvedAnimationInterval interval,
        List<ResolvedTimingInstance> pendingBeginInstances,
        HashSet<long> knownBeginTimes,
        TimeSpan? horizon)
    {
        for (var index = 0; index < binding.BeginSpecs.Count; index++)
        {
            var spec = binding.BeginSpecs[index];
            if (!IsSelfEventTiming(spec, binding.AnimationAddress))
            {
                continue;
            }

            var eventInstanceKey = CreateEventInstanceKey(binding.AnimationAddress, spec.EventType);
            switch (spec.EventType)
            {
                case SvgAnimationTimingEventType.Begin:
                    AddSelfSyncBeginInstance(
                        interval.BeginInstance.Time,
                        interval.BeginInstance.Time,
                        spec.Offset,
                        eventInstanceKey,
                        pendingBeginInstances,
                        knownBeginTimes,
                        horizon);
                    break;
                case SvgAnimationTimingEventType.End:
                    if (interval.ActiveEnd.HasValue)
                    {
                        AddSelfSyncBeginInstance(
                            interval.ActiveEnd.Value,
                            interval.BeginInstance.Time,
                            spec.Offset,
                            eventInstanceKey,
                            pendingBeginInstances,
                            knownBeginTimes,
                            horizon);
                    }

                    break;
                case SvgAnimationTimingEventType.Repeat:
                    var repeatEventTimes = new List<TimeSpan>();
                    AddRepeatEventInstances(binding.Animation, interval, spec.RepeatIteration, repeatEventTimes);
                    for (var repeatIndex = 0; repeatIndex < repeatEventTimes.Count; repeatIndex++)
                    {
                        AddSelfSyncBeginInstance(
                            repeatEventTimes[repeatIndex],
                            interval.BeginInstance.Time,
                            spec.Offset,
                            eventInstanceKey,
                            pendingBeginInstances,
                            knownBeginTimes,
                            horizon);
                    }

                    break;
            }
        }
    }

    private static void AddSelfSyncBeginInstance(
        TimeSpan eventTime,
        TimeSpan sourceBegin,
        TimeSpan offset,
        string eventInstanceKey,
        List<ResolvedTimingInstance> pendingBeginInstances,
        HashSet<long> knownBeginTimes,
        TimeSpan? horizon)
    {
        var candidateTime = eventTime + offset;
        if (candidateTime <= sourceBegin)
        {
            return;
        }

        if (horizon.HasValue && candidateTime > horizon.Value)
        {
            return;
        }

        if (!knownBeginTimes.Add(candidateTime.Ticks))
        {
            return;
        }

        pendingBeginInstances.Add(new ResolvedTimingInstance(candidateTime, eventInstanceKey, eventTime));
    }

    private static bool TrySelectCurrentInterval(
        SvgAnimationElement animation,
        TimeSpan time,
        IReadOnlyList<ResolvedAnimationInterval> intervals,
        bool requireActive,
        out ResolvedAnimationInterval interval)
    {
        interval = default;
        ResolvedAnimationInterval? selected = null;

        for (var index = 0; index < intervals.Count; index++)
        {
            var candidate = intervals[index];
            if (candidate.BeginInstance.Time > time)
            {
                break;
            }

            selected = candidate;
        }

        if (!selected.HasValue)
        {
            return false;
        }

        var resolved = selected.Value;
        if (resolved.IsActive(time) || (!requireActive && animation.AnimationFill == SvgAnimationFill.Freeze))
        {
            interval = resolved;
            return true;
        }

        return false;
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

    private static bool ContainsDynamicTiming(IReadOnlyList<TimingSpec> specs)
    {
        for (var index = 0; index < specs.Count; index++)
        {
            if (specs[index].IsEvent || specs[index].IsAccessKey)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<ResolvedTimingInstance> CreateStaticTimingInstances(IReadOnlyList<TimingSpec> specs)
    {
        if (specs.Count == 0)
        {
            return s_emptyResolvedTimingInstances;
        }

        var instances = new ResolvedTimingInstance[specs.Count];
        for (var index = 0; index < specs.Count; index++)
        {
            instances[index] = new ResolvedTimingInstance(specs[index].Offset, eventInstanceKey: null, sourceEventTime: null);
        }

        if (instances.Length > 1)
        {
            Array.Sort(instances, static (left, right) => left.Time.CompareTo(right.Time));
        }

        return instances;
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

            activeEnd = ComputeIndefiniteActiveEnd(animation, begin, explicitEnd);
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

            activeEnd = ComputeIndefiniteActiveEnd(animation, begin, explicitEnd);
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

    private static TimeSpan? ComputeIndefiniteActiveEnd(SvgAnimationElement animation, TimeSpan begin, TimeSpan? explicitEnd)
    {
        var totalDuration = ComputeConstrainedTotalDuration(animation, totalDuration: null, explicitEnd, begin);
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

        return ComputeConstrainedTotalDuration(animation, totalDuration, explicitEnd, begin);
    }

    private static TimeSpan? ComputeConstrainedTotalDuration(
        SvgAnimationElement animation,
        TimeSpan? totalDuration,
        TimeSpan? explicitEnd,
        TimeSpan begin)
    {
        if (explicitEnd.HasValue && explicitEnd.Value > begin)
        {
            totalDuration = MinDuration(totalDuration, explicitEnd.Value - begin);
        }

        var minimumMode = ParseRepeatDuration(animation.Minimum, out var minimumDuration);
        var maximumMode = ParseRepeatDuration(animation.Maximum, out var maximumDuration);
        if (minimumMode == RepeatDurationMode.Finite &&
            maximumMode == RepeatDurationMode.Finite &&
            minimumDuration > maximumDuration)
        {
            return totalDuration;
        }

        switch (minimumMode)
        {
            case RepeatDurationMode.Finite:
                totalDuration = MaxDuration(totalDuration, minimumDuration);
                break;
        }

        switch (maximumMode)
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

    private static bool TryResolveAnimatedValue(
        AnimationBinding binding,
        SvgAnimationValueElement animation,
        AnimationSample sample,
        bool forceColorInterpolation,
        IReadOnlyDictionary<string, SvgAnimationFrameAttributeState>? frameAttributes,
        out string value)
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

        if (TryInterpolateValue(binding, fromValue, toValue, localProgress, forceColorInterpolation, frameAttributes, out value))
        {
            return TryApplyAccumulation(binding, animation, values, sample, forceColorInterpolation, ref value);
        }

        value = IsToOnlyAnimation(animation)
            ? toValue
            : ResolveNonInterpolableFallbackValue(fromValue, toValue, localProgress);
        return TryApplyAccumulation(binding, animation, values, sample, forceColorInterpolation, ref value);
    }

    private static bool IsToOnlyAnimation(SvgAnimationValueElement animation)
    {
        return string.IsNullOrWhiteSpace(animation.Values) &&
               string.IsNullOrWhiteSpace(animation.From) &&
               string.IsNullOrWhiteSpace(animation.By) &&
               !string.IsNullOrWhiteSpace(animation.To);
    }

    private static string ResolveNonInterpolableFallbackValue(string fromValue, string toValue, float localProgress)
    {
        return localProgress >= 0.5f ? toValue : fromValue;
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

            var discreteValues = ParseTransformNumbers(animation.TransformType, discrete);
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
        var fromValues = ParseTransformNumbers(animation.TransformType, values[startIndex]);
        var toValues = ParseTransformNumbers(animation.TransformType, values[endIndex]);
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
        return ParseTransformNumbers(transformType: null, value);
    }

    private static float[] ParseTransformNumbers(SvgAnimateTransformType transformType, string value)
    {
        return ParseTransformNumbers((SvgAnimateTransformType?)transformType, value);
    }

    private static float[] ParseTransformNumbers(SvgAnimateTransformType? transformType, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<float>();
        }

        if (value.IndexOf('(') >= 0)
        {
            if (transformType is { } concreteTransformType &&
                TryParseTransformFunctionNumbers(value, GetTransformFunctionName(concreteTransformType), out var matchingValues))
            {
                return matchingValues;
            }

            if (TryParseFirstTransformFunctionNumbers(value, out var functionValues))
            {
                return functionValues;
            }
        }

        return SvgAnimationParser.ParseNumberList(value);
    }

    private static string GetTransformFunctionName(SvgAnimateTransformType transformType)
    {
        return transformType switch
        {
            SvgAnimateTransformType.Translate => "translate",
            SvgAnimateTransformType.Scale => "scale",
            SvgAnimateTransformType.Rotate => "rotate",
            SvgAnimateTransformType.SkewX => "skewX",
            SvgAnimateTransformType.SkewY => "skewY",
            _ => string.Empty
        };
    }

    private static bool TryParseTransformFunctionNumbers(string value, string functionName, out float[] values)
    {
        values = Array.Empty<float>();
        if (string.IsNullOrWhiteSpace(functionName))
        {
            return false;
        }

        var searchIndex = 0;
        while (searchIndex < value.Length)
        {
            var functionIndex = value.IndexOf(functionName, searchIndex, StringComparison.Ordinal);
            if (functionIndex < 0)
            {
                return false;
            }

            searchIndex = functionIndex + functionName.Length;
            if (functionIndex > 0 && char.IsLetterOrDigit(value[functionIndex - 1]))
            {
                continue;
            }

            var openIndex = value.IndexOf('(', searchIndex);
            if (openIndex < 0 || !ContainsOnlyWhitespace(value, searchIndex, openIndex))
            {
                continue;
            }

            var closeIndex = value.IndexOf(')', openIndex + 1);
            if (closeIndex < 0)
            {
                return false;
            }

            var numberText = value.Substring(openIndex + 1, closeIndex - openIndex - 1);
            values = SvgAnimationParser.ParseNumberList(numberText);
            return values.Length > 0;
        }

        return false;
    }

    private static bool TryParseFirstTransformFunctionNumbers(string value, out float[] values)
    {
        values = Array.Empty<float>();

        var openIndex = value.IndexOf('(');
        if (openIndex < 0)
        {
            return false;
        }

        var closeIndex = value.IndexOf(')', openIndex + 1);
        if (closeIndex < 0)
        {
            return false;
        }

        var numberText = value.Substring(openIndex + 1, closeIndex - openIndex - 1);
        values = SvgAnimationParser.ParseNumberList(numberText);
        return values.Length > 0;
    }

    private static bool ContainsOnlyWhitespace(string value, int startIndex, int endIndex)
    {
        for (var index = startIndex; index < endIndex; index++)
        {
            if (!char.IsWhiteSpace(value[index]))
            {
                return false;
            }
        }

        return true;
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
            else if (animation is SvgAnimateTransform animateTransform &&
                     string.IsNullOrWhiteSpace(animation.To) &&
                     SvgAnimationParser.TryGetTrimmedString(animation.By, out var transformByValue))
            {
                resolved.Add(CreateZeroTransformValue(animateTransform.TransformType, transformByValue));
            }

            if (SvgAnimationParser.TryGetTrimmedString(animation.To, out var toValue))
            {
                resolved.Add(toValue);
                return resolved;
            }

            if (SvgAnimationParser.TryGetTrimmedString(animation.By, out var byValue))
            {
                var additiveBaseValue = resolved.Count > 0 ? resolved[resolved.Count - 1] : binding.BaseValueString;
                if (animation is SvgAnimateTransform byTransform &&
                    additiveBaseValue is { } &&
                    TryAddTransformValue(byTransform.TransformType, additiveBaseValue, byValue, out var transformSumValue))
                {
                    resolved.Add(transformSumValue);
                }
                else if (additiveBaseValue is { } && TryAddValue(binding, additiveBaseValue, byValue, out var sumValue))
                {
                    resolved.Add(sumValue);
                }
            }

            return resolved;
        });
    }

    private static bool TryAddTransformValue(SvgAnimateTransformType transformType, string baseValue, string byValue, out string result)
    {
        result = string.Empty;

        var baseValues = ParseTransformNumbers(transformType, baseValue);
        var byValues = ParseTransformNumbers(transformType, byValue);
        if (baseValues.Length == 0 || byValues.Length == 0)
        {
            return false;
        }

        var length = GetExpectedTransformValueCount(transformType, baseValues, byValues);
        var values = new float[length];
        for (var index = 0; index < values.Length; index++)
        {
            var currentBaseValue = index < baseValues.Length
                ? baseValues[index]
                : GetDefaultTransformValue(transformType, index, baseValues);
            var currentByValue = index < byValues.Length
                ? byValues[index]
                : GetImplicitTransformByValue(transformType, index, byValues);
            values[index] = currentBaseValue + currentByValue;
        }

        return TryCreateTransformString(transformType, values, out result);
    }

    private static float GetImplicitTransformByValue(SvgAnimateTransformType transformType, int index, float[] byValues)
    {
        if (transformType == SvgAnimateTransformType.Scale && byValues.Length == 1 && index == 1)
        {
            return byValues[0];
        }

        return 0f;
    }

    private static string CreateZeroTransformValue(SvgAnimateTransformType transformType, string byValue)
    {
        var byValues = ParseTransformNumbers(byValue);
        var length = Math.Max(1, byValues.Length);
        var values = new string[length];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = GetZeroTransformValue(transformType, index).ToSvgString();
        }

        return string.Join(" ", values);
    }

    private static float GetZeroTransformValue(SvgAnimateTransformType transformType, int index)
    {
        return transformType switch
        {
            SvgAnimateTransformType.Scale => 0f,
            _ => 0f
        };
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

        if (animation.CalcMode == SvgAnimationCalcMode.Discrete)
        {
            if (!TryScaleValue(binding, endValue, sample.IterationIndex, forceColorInterpolation, out var discreteAccumulationValue) ||
                !TryAddValue(binding, value, discreteAccumulationValue, out var discreteAccumulatedValue))
            {
                return false;
            }

            value = discreteAccumulatedValue;
            return true;
        }

        if ((forceColorInterpolation || IsPaintServerType(binding.PropertyType) || TryGetColor(value, out _)) &&
            TryGetColor(startValue, out var startColor) &&
            TryGetColor(endValue, out var endColor) &&
            TryGetColor(value, out var currentColor))
        {
            value = FormatColor(Color.FromArgb(
                ClampToByte(currentColor.A + ((endColor.A - startColor.A) * sample.IterationIndex)),
                ClampToByte(currentColor.R + ((endColor.R - startColor.R) * sample.IterationIndex)),
                ClampToByte(currentColor.G + ((endColor.G - startColor.G) * sample.IterationIndex)),
                ClampToByte(currentColor.B + ((endColor.B - startColor.B) * sample.IterationIndex))));
            return true;
        }

        if (!TrySubtractValue(binding, endValue, startValue, forceColorInterpolation, out var deltaValue) ||
            !TryScaleValue(binding, deltaValue, sample.IterationIndex, forceColorInterpolation, out var accumulatedDelta) ||
            !TryAddValue(binding, value, accumulatedDelta, out var summedValue))
        {
            return false;
        }

        value = summedValue;
        return true;
    }

    private static bool TryApplyTransformAccumulation(AnimationBinding binding, SvgAnimateTransform animation, IReadOnlyList<string> values, AnimationSample sample, ref float[] currentValues)
    {
        if (animation.Accumulate != SvgAnimationAccumulate.Sum || sample.IterationIndex <= 0 || values.Count == 0)
        {
            return true;
        }

        var startValues = ParseTransformNumbers(animation.TransformType, values[0]);
        var endValues = ParseTransformNumbers(animation.TransformType, values[values.Count - 1]);
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
                ParseTransformNumbers(transformType, values[index]),
                ParseTransformNumbers(transformType, values[index + 1]));
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
            TryConvertStringToType(fromValue, propertyType, binding.ValueConverter, binding.ValueContext, out var fromObject) &&
            TryConvertStringToType(toValue, propertyType, binding.ValueConverter, binding.ValueContext, out var toObject) &&
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

    private static bool TryInterpolateValue(
        AnimationBinding binding,
        string fromValue,
        string toValue,
        float progress,
        bool forceColorInterpolation,
        IReadOnlyDictionary<string, SvgAnimationFrameAttributeState>? frameAttributes,
        out string result)
    {
        result = string.Empty;

        if (forceColorInterpolation || IsPaintServerType(binding.PropertyType) || IsColorKeyword(fromValue) || IsColorKeyword(toValue))
        {
            fromValue = ResolveColorKeywordValue(binding, fromValue, frameAttributes);
            toValue = ResolveColorKeywordValue(binding, toValue, frameAttributes);
        }

        if ((forceColorInterpolation || IsPaintServerType(binding.PropertyType)) &&
            TryInterpolateColor(fromValue, toValue, progress, out result))
        {
            return true;
        }

        if (binding.PropertyType is { } propertyType &&
            TryConvertStringToType(fromValue, propertyType, binding.ValueConverter, binding.ValueContext, out var fromObject) &&
            TryConvertStringToType(toValue, propertyType, binding.ValueConverter, binding.ValueContext, out var toObject) &&
            TryInterpolateTypedValue(fromObject, toObject, progress, out result))
        {
            return true;
        }

        if (TryInterpolateColor(fromValue, toValue, progress, out result))
        {
            return true;
        }

        if (binding.AttributeName == "d" &&
            TryInterpolatePathData(fromValue, toValue, progress, out result))
        {
            return true;
        }

        if (TryInterpolateNumberList(fromValue, toValue, progress, out result))
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

    private static string ResolveColorKeywordValue(
        AnimationBinding binding,
        string value,
        IReadOnlyDictionary<string, SvgAnimationFrameAttributeState>? frameAttributes)
    {
        if (!SvgAnimationParser.TryGetTrimmedString(value, out var trimmed))
        {
            return value;
        }

        if (SvgAnimationParser.EqualsKeywordIgnoreCase(trimmed.AsSpan(), "currentColor") &&
            (TryGetAnimatedFrameColor(binding.SourceTarget, "color", frameAttributes, out var currentColor) ||
             TryGetColor(binding.SourceTarget.Color, binding.SourceTarget, out currentColor)))
        {
            return FormatColor(currentColor);
        }

        if (SvgAnimationParser.EqualsKeywordIgnoreCase(trimmed.AsSpan(), "inherit") &&
            TryGetInheritedColor(binding.SourceTarget, binding.AttributeName, frameAttributes, out var inheritedColor))
        {
            return FormatColor(inheritedColor);
        }

        return value;
    }

    private static bool IsColorKeyword(string value)
    {
        return SvgAnimationParser.TryGetTrimmedString(value, out var trimmed) &&
               (SvgAnimationParser.EqualsKeywordIgnoreCase(trimmed.AsSpan(), "currentColor") ||
                SvgAnimationParser.EqualsKeywordIgnoreCase(trimmed.AsSpan(), "inherit"));
    }

    private static bool TryGetInheritedColor(
        SvgElement element,
        string attributeName,
        IReadOnlyDictionary<string, SvgAnimationFrameAttributeState>? frameAttributes,
        out Color color)
    {
        color = default;
        for (var parent = element.Parent as SvgElement; parent is not null; parent = parent.Parent as SvgElement)
        {
            if (TryGetAnimatedFrameColor(parent, attributeName, frameAttributes, out color))
            {
                return true;
            }

            if (TryGetPaintAttribute(parent, attributeName, out var paintServer) &&
                TryGetColor(paintServer, parent, out color))
            {
                return true;
            }

            if (parent.TryGetAttribute(attributeName, out var rawValue) &&
                TryGetColor(rawValue, out color))
            {
                return true;
            }

            var value = GetAttributeValue(parent, attributeName);
            if (TryGetColor(value, parent, out color))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetAnimatedFrameColor(
        SvgElement element,
        string attributeName,
        IReadOnlyDictionary<string, SvgAnimationFrameAttributeState>? frameAttributes,
        out Color color)
    {
        color = default;
        if (frameAttributes is null)
        {
            return false;
        }

        var key = string.Concat(SvgElementAddress.Create(element).Key, "|", attributeName);
        return frameAttributes.TryGetValue(key, out var attribute) &&
               TryGetColor(attribute.Value, out color);
    }

    private static bool TryGetPaintAttribute(SvgElement element, string attributeName, out SvgPaintServer? paintServer)
    {
        switch (attributeName)
        {
            case "color":
                paintServer = element.Color;
                return true;
            case "fill" when element is SvgVisualElement visualElement:
                paintServer = visualElement.Fill;
                return true;
            case "stroke" when element is SvgVisualElement visualElement:
                paintServer = visualElement.Stroke;
                return true;
            case "stop-color" when element is SvgGradientStop gradientStop:
                paintServer = gradientStop.StopColor;
                return true;
            case "stop-color" when element is SvgGradientServer gradientServer:
                paintServer = gradientServer.StopColor;
                return true;
            case "flood-color" when element is Svg.FilterEffects.SvgDropShadow dropShadow:
                paintServer = dropShadow.FloodColor;
                return true;
            default:
                paintServer = null;
                return false;
        }
    }

    private static bool TryInterpolateNumberList(string fromValue, string toValue, float progress, out string result)
    {
        result = string.Empty;

        var fromNumbers = SvgAnimationParser.ParseNumberList(fromValue);
        var toNumbers = SvgAnimationParser.ParseNumberList(toValue);
        if (fromNumbers.Length == 0 || fromNumbers.Length != toNumbers.Length)
        {
            return false;
        }

        var values = new string[fromNumbers.Length];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = Lerp(fromNumbers[index], toNumbers[index], progress).ToSvgString();
        }

        result = string.Join(" ", values);
        return true;
    }

    private static bool TryInterpolatePathData(string fromValue, string toValue, float progress, out string result)
    {
        result = string.Empty;

        if (!TryTokenizePathData(fromValue, out var fromTokens) ||
            !TryTokenizePathData(toValue, out var toTokens) ||
            fromTokens.Count == 0 ||
            fromTokens.Count != toTokens.Count)
        {
            return false;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < fromTokens.Count; index++)
        {
            var fromToken = fromTokens[index];
            var toToken = toTokens[index];
            if (fromToken.IsCommand != toToken.IsCommand)
            {
                return false;
            }

            if (fromToken.IsCommand)
            {
                if (fromToken.Command != toToken.Command)
                {
                    return false;
                }

                AppendPathToken(builder, fromToken.Command.ToString());
                continue;
            }

            AppendPathToken(builder, Lerp(fromToken.Number, toToken.Number, progress).ToSvgString());
        }

        result = builder.ToString();
        return result.Length > 0;
    }

    private static bool TryTokenizePathData(string value, out List<PathDataToken> tokens)
    {
        tokens = new List<PathDataToken>();
        if (!SvgAnimationParser.TryGetTrimmedString(value, out var trimmed))
        {
            return false;
        }

        var span = trimmed.AsSpan();
        var index = 0;
        while (index < span.Length)
        {
            var ch = span[index];
            if (char.IsWhiteSpace(ch) || ch == ',')
            {
                index++;
                continue;
            }

            if (IsSvgPathCommand(ch))
            {
                tokens.Add(new PathDataToken(ch));
                index++;
                continue;
            }

            var start = index;
            index++;
            while (index < span.Length && IsPathNumberContinuation(span, index))
            {
                index++;
            }

            var token = span.Slice(start, index - start);
            if (!SvgAnimationParser.TryParseInvariantFloat(token, out var number))
            {
                return false;
            }

            tokens.Add(new PathDataToken(number));
        }

        return true;
    }

    private static bool IsPathNumberContinuation(ReadOnlySpan<char> span, int index)
    {
        var ch = span[index];
        if (char.IsWhiteSpace(ch) || ch == ',' || IsSvgPathCommand(ch))
        {
            return false;
        }

        if ((ch == '-' || ch == '+') && index > 0)
        {
            var previous = span[index - 1];
            return previous == 'e' || previous == 'E';
        }

        return true;
    }

    private static bool IsSvgPathCommand(char ch)
    {
        return ch is 'M' or 'm' or
            'Z' or 'z' or
            'L' or 'l' or
            'H' or 'h' or
            'V' or 'v' or
            'C' or 'c' or
            'S' or 's' or
            'Q' or 'q' or
            'T' or 't' or
            'A' or 'a';
    }

    private static void AppendPathToken(StringBuilder builder, string token)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(token);
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

    private static bool TryInterpolateSvgUnit(string fromValue, string toValue, float progress, out string result)
    {
        result = string.Empty;

        if (!TryConvertStringToType(fromValue, typeof(SvgUnit), converter: null, context: null, out var fromObject) ||
            !TryConvertStringToType(toValue, typeof(SvgUnit), converter: null, context: null, out var toObject) ||
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

        result = FormatColor(color);
        return true;
    }

    private static string FormatColor(Color color)
    {
        if (color.A == byte.MaxValue)
        {
            return new SvgColourServer(color).ToString();
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "#{0:x2}{1:x2}{2:x2}{3:x2}",
            color.R,
            color.G,
            color.B,
            color.A);
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
        return TryGetColor(paintServer, styleOwner: null, out color);
    }

    private static bool TryGetColor(object? value, SvgElement? styleOwner, out Color color)
    {
        switch (value)
        {
            case SvgPaintServer paintServer:
                return TryGetColor(paintServer, styleOwner, out color);
            case string stringValue:
                return TryGetColor(stringValue, out color);
            default:
                color = default;
                return false;
        }
    }

    private static bool TryGetColor(SvgPaintServer? paintServer, SvgElement? styleOwner, out Color color)
    {
        if (paintServer == SvgPaintServer.None ||
            paintServer == SvgPaintServer.Inherit ||
            paintServer == SvgPaintServer.NotSet)
        {
            color = default;
            return false;
        }

        if (paintServer is SvgDeferredPaintServer && styleOwner is not null)
        {
            var deferredColourServer = SvgDeferredPaintServer.TryGet<SvgColourServer>(paintServer, styleOwner);
            if (deferredColourServer is not null)
            {
                color = deferredColourServer.Colour;
                return true;
            }
        }

        if (paintServer is SvgColourServer colourServer)
        {
            color = colourServer.Colour;
            return true;
        }

        color = default;
        return false;
    }

    private static bool TryAddValue(AnimationBinding binding, string baseValue, string byValue, out string result)
    {
        result = string.Empty;

        if (binding.PropertyType is { } propertyType &&
            TryConvertStringToType(baseValue, propertyType, binding.ValueConverter, binding.ValueContext, out var baseObject) &&
            TryConvertStringToType(byValue, propertyType, binding.ValueConverter, binding.ValueContext, out var byObject) &&
            TryAddTypedValue(baseObject, byObject, out result))
        {
            return true;
        }

        if (TryConvertStringToType(baseValue, typeof(SvgUnit), converter: null, context: null, out var baseUnitObject) &&
            TryConvertStringToType(byValue, typeof(SvgUnit), converter: null, context: null, out var byUnitObject) &&
            baseUnitObject is SvgUnit baseUnit &&
            byUnitObject is SvgUnit byUnit &&
            baseUnit.Type == byUnit.Type)
        {
            result = new SvgUnit(baseUnit.Type, baseUnit.Value + byUnit.Value).ToString();
            return true;
        }

        if (TryAddNumberLists(baseValue, byValue, out result))
        {
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
            result = FormatColor(Color.FromArgb(
                ClampToByte(baseColor.A + byColor.A),
                ClampToByte(baseColor.R + byColor.R),
                ClampToByte(baseColor.G + byColor.G),
                ClampToByte(baseColor.B + byColor.B)));
            return true;
        }

        return false;
    }

    private static bool TrySubtractValue(AnimationBinding binding, string endValue, string startValue, bool forceColorInterpolation, out string result)
    {
        result = string.Empty;

        if (binding.PropertyType is { } propertyType &&
            TryConvertStringToType(endValue, propertyType, binding.ValueConverter, binding.ValueContext, out var endObject) &&
            TryConvertStringToType(startValue, propertyType, binding.ValueConverter, binding.ValueContext, out var startObject) &&
            TrySubtractTypedValue(endObject, startObject, out result))
        {
            return true;
        }

        if (TryConvertStringToType(endValue, typeof(SvgUnit), converter: null, context: null, out var endUnitObject) &&
            TryConvertStringToType(startValue, typeof(SvgUnit), converter: null, context: null, out var startUnitObject) &&
            endUnitObject is SvgUnit endUnit &&
            startUnitObject is SvgUnit startUnit &&
            endUnit.Type == startUnit.Type)
        {
            result = new SvgUnit(endUnit.Type, endUnit.Value - startUnit.Value).ToString();
            return true;
        }

        if (TrySubtractNumberLists(endValue, startValue, out result))
        {
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

    private static bool TryScaleValue(AnimationBinding binding, string value, int factor, bool forceColorInterpolation, out string result)
    {
        result = string.Empty;

        if (factor == 0)
        {
            result = "0";
            return true;
        }

        if (binding.PropertyType is { } propertyType &&
            TryConvertStringToType(value, propertyType, binding.ValueConverter, binding.ValueContext, out var valueObject) &&
            TryScaleTypedValue(valueObject, factor, out result))
        {
            return true;
        }

        if (TryConvertStringToType(value, typeof(SvgUnit), converter: null, context: null, out var unitObject) &&
            unitObject is SvgUnit unit)
        {
            result = new SvgUnit(unit.Type, unit.Value * factor).ToString();
            return true;
        }

        if (TryScaleNumberList(value, factor, out result))
        {
            return true;
        }

        if (SvgAnimationParser.TryParseInvariantFloat(value, out var numeric))
        {
            result = (numeric * factor).ToSvgString();
            return true;
        }

        return false;
    }

    private static bool TryAddNumberLists(string leftValue, string rightValue, out string result)
    {
        return TryCombineNumberLists(leftValue, rightValue, static (left, right) => left + right, out result);
    }

    private static bool TrySubtractNumberLists(string leftValue, string rightValue, out string result)
    {
        return TryCombineNumberLists(leftValue, rightValue, static (left, right) => left - right, out result);
    }

    private static bool TryCombineNumberLists(string leftValue, string rightValue, Func<float, float, float> combine, out string result)
    {
        result = string.Empty;

        var leftNumbers = SvgAnimationParser.ParseNumberList(leftValue);
        var rightNumbers = SvgAnimationParser.ParseNumberList(rightValue);
        if (leftNumbers.Length == 0 || leftNumbers.Length != rightNumbers.Length)
        {
            return false;
        }

        var values = new string[leftNumbers.Length];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = combine(leftNumbers[index], rightNumbers[index]).ToSvgString();
        }

        result = string.Join(" ", values);
        return true;
    }

    private static bool TryScaleNumberList(string value, int factor, out string result)
    {
        result = string.Empty;

        var numbers = SvgAnimationParser.ParseNumberList(value);
        if (numbers.Length == 0)
        {
            return false;
        }

        var values = new string[numbers.Length];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = (numbers[index] * factor).ToSvgString();
        }

        result = string.Join(" ", values);
        return true;
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
                    result = FormatColor(Color.FromArgb(
                        ClampToByte(baseColor.A + byColor.A),
                        ClampToByte(baseColor.R + byColor.R),
                        ClampToByte(baseColor.G + byColor.G),
                        ClampToByte(baseColor.B + byColor.B)));
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

    private static bool TryConvertStringToType(
        string value,
        Type targetType,
        TypeConverter? converter,
        ITypeDescriptorContext? context,
        out object? result)
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

            if (targetType == typeof(float))
            {
                if (SvgAnimationParser.TryParseInvariantFloat(value, out var floatValue))
                {
                    result = floatValue;
                    return true;
                }

                return false;
            }

            if (targetType == typeof(double))
            {
                if (SvgAnimationParser.TryParseInvariantDouble(value.AsSpan(), out var doubleValue))
                {
                    result = doubleValue;
                    return true;
                }

                return false;
            }

            if (targetType == typeof(int))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    result = intValue;
                    return true;
                }

                return false;
            }

            if (converter is { } && converter.CanConvertFrom(typeof(string)))
            {
                result = converter.ConvertFrom(context, CultureInfo.InvariantCulture, value);
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

    private static bool IsPointerTimingEventType(SvgAnimationTimingEventType eventType)
    {
        return eventType is SvgAnimationTimingEventType.Move or
               SvgAnimationTimingEventType.Press or
               SvgAnimationTimingEventType.Release or
               SvgAnimationTimingEventType.Enter or
               SvgAnimationTimingEventType.Leave or
               SvgAnimationTimingEventType.Wheel or
               SvgAnimationTimingEventType.Click;
    }

    private static SvgAnimationTimingEventType ToTimingEventType(SvgPointerEventType eventType)
    {
        return eventType switch
        {
            SvgPointerEventType.Move => SvgAnimationTimingEventType.Move,
            SvgPointerEventType.Press => SvgAnimationTimingEventType.Press,
            SvgPointerEventType.Release => SvgAnimationTimingEventType.Release,
            SvgPointerEventType.Enter => SvgAnimationTimingEventType.Enter,
            SvgPointerEventType.Leave => SvgAnimationTimingEventType.Leave,
            SvgPointerEventType.Wheel => SvgAnimationTimingEventType.Wheel,
            SvgPointerEventType.Click => SvgAnimationTimingEventType.Click,
            _ => throw new ArgumentOutOfRangeException(nameof(eventType))
        };
    }

    private static string CreateEventInstanceKey(SvgElementAddress address, SvgAnimationTimingEventType eventType)
    {
        return string.Concat(address.Key, "|", ((int)eventType).ToString(CultureInfo.InvariantCulture));
    }

    private static string CreateAccessKeyEventInstanceKey(string accessKey)
    {
        return string.Concat("accessKey|", NormalizeAccessKey(accessKey));
    }

    private static bool TryNormalizeAccessKey(string? accessKey, out string normalizedAccessKey)
    {
        normalizedAccessKey = string.Empty;
        if (string.IsNullOrWhiteSpace(accessKey))
        {
            return false;
        }

        normalizedAccessKey = NormalizeAccessKey(accessKey!);
        return normalizedAccessKey.Length > 0;
    }

    private static string NormalizeAccessKey(string accessKey)
    {
        return accessKey.Trim().ToUpperInvariant();
    }

    private static object? GetAttributeValue(SvgElement element, string attributeName)
    {
        return element.GetAnimationValue(attributeName);
    }

    private static ISvgPropertyDescriptor? GetAttributePropertyDescriptor(SvgElement element, string attributeName)
    {
        return element.Properties.TryGetValue(attributeName, out var propertyDescriptor)
            ? propertyDescriptor
            : null;
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
