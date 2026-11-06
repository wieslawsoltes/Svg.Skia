using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using Svg.Pathing;
using Svg.Skia;

namespace Svg.Editor.Skia;

public enum SvgTimelineTrackKind { Attribute, Transform, Motion, Set }
public enum SvgTimelineValueMode { KeyFrames, FromTo, FromBy, To, By, MotionPath }

/// <summary>A key in normalized simple-duration coordinates; easing belongs to the following interval.</summary>
public sealed class SvgTimelineKeyFrame
{
    public double Offset { get; set; }
    public string Value { get; set; } = "0";
    public string Spline { get; set; } = "0 0 1 1";
}

/// <summary>An editable animation description. Reading it never modifies the SVG document.</summary>
public sealed class SvgTimelineTrack
{
    internal SvgTimelineTrack(SvgElement target, SvgAnimationElement? source = null)
    {
        Target = target;
        Source = source;
    }

    internal SvgAnimationElement? Template { get; set; }
    public SvgElement Target { get; }
    public SvgAnimationElement? Source { get; internal set; }
    public SvgTimelineTrackKind Kind { get; set; }
    public SvgTimelineValueMode ValueMode { get; set; }
    public string AttributeName { get; set; } = "opacity";
    public string Begin { get; set; } = "0s";
    public string Duration { get; set; } = "2s";
    public string RepeatCount { get; set; } = "1";
    public bool Freeze { get; set; } = true;
    public bool Additive { get; set; }
    public bool Accumulate { get; set; }
    public SvgAnimationCalcMode Interpolation { get; set; } = SvgAnimationCalcMode.Linear;
    public SvgAnimateTransformType TransformType { get; set; }
    public string From { get; set; } = "0";
    public string To { get; set; } = "1";
    public string By { get; set; } = "1";
    public string MotionPath { get; set; } = string.Empty;
    public string MotionRotation { get; set; } = "auto";
    public string MotionPathReference { get; set; } = string.Empty;
    public List<SvgTimelineKeyFrame> Keys { get; } = new();
    public double DurationSeconds => SvgTimelineService.TryParseClock(Duration, out var seconds) ? seconds : 2;
    public double BeginSeconds => SvgTimelineService.TryParseClock(Begin, out var seconds) ? seconds : 0;
    public string Label => $"{(string.IsNullOrEmpty(Target.ID) ? global::Svg.Editor.Core.SvgElementInfo.GetElementName(Target.GetType()) : Target.ID)} · {(Kind == SvgTimelineTrackKind.Motion ? "motion" : Kind == SvgTimelineTrackKind.Transform ? TransformType.ToString() : AttributeName)}";
    public override string ToString() => Label;
}

/// <summary>
/// SVG 1.1 animation authoring on the native DOM. Evaluation remains owned by Svg.Animation.
/// See https://www.w3.org/TR/SVG11/animate.html#AnimationAttributes.
/// </summary>
public static class SvgTimelineService
{
    public const int MaximumKeys = 4096;

    public static IReadOnlyList<SvgTimelineTrack> ReadTracks(SvgDocument document)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        return document.Descendants().OfType<SvgAnimationElement>()
            .Where(animation => animation.TargetElement is not null)
            .Select(ReadTrack).ToArray();
    }

    public static SvgTimelineTrack CreateTrack(SvgElement target, string attributeName = "opacity", string from = "0", string to = "1")
    {
        if (target is null)
            throw new ArgumentNullException(nameof(target));
        var track = new SvgTimelineTrack(target) { AttributeName = attributeName };
        track.Keys.Add(new SvgTimelineKeyFrame { Offset = 0, Value = from });
        track.Keys.Add(new SvgTimelineKeyFrame { Offset = 1, Value = to });
        return track;
    }

    public static SvgTimelineTrack ReadTrack(SvgAnimationElement animation)
    {
        if (animation is null)
            throw new ArgumentNullException(nameof(animation));
        var target = animation.TargetElement ?? throw new InvalidOperationException("The animation target could not be resolved.");
        var track = new SvgTimelineTrack(target, animation)
        {
            Kind = KindOf(animation),
            Begin = string.IsNullOrWhiteSpace(animation.Begin) ? "0s" : animation.Begin,
            Duration = string.IsNullOrWhiteSpace(animation.Duration) ? "indefinite" : animation.Duration,
            RepeatCount = string.IsNullOrWhiteSpace(animation.RepeatCount) ? "1" : animation.RepeatCount,
            Freeze = animation.AnimationFill == SvgAnimationFill.Freeze
        };
        if (animation is SvgAnimationAttributeElement attribute)
            track.AttributeName = attribute.AnimationAttributeName ?? string.Empty;
        if (animation is SvgAnimateTransform transform)
            track.TransformType = transform.TransformType;
        string? values = null, splines = null, from = null, to = null, by = null;
        SvgNumberCollection? times = null;
        if (animation is SvgAnimationValueElement value)
        {
            values = value.Values;
            times = value.KeyTimes;
            splines = value.KeySplines;
            from = value.From;
            to = value.To;
            by = value.By;
            track.Interpolation = value.CalcMode;
            track.Additive = value.Additive == SvgAnimationAdditive.Sum;
            track.Accumulate = value.Accumulate == SvgAnimationAccumulate.Sum;
        }
        else if (animation is SvgAnimateMotion motion)
        {
            values = motion.Values;
            times = motion.KeyTimes;
            splines = motion.KeySplines;
            from = motion.From;
            to = motion.To;
            by = motion.By;
            track.MotionPath = motion.PathData?.ToString() ?? string.Empty;
            var reference = motion.Children.OfType<SvgMPath>().FirstOrDefault();
            if (reference is not null && string.IsNullOrWhiteSpace(track.MotionPath))
                track.MotionPathReference = reference.ReferencedPath?.ToString() ?? string.Empty;
            track.MotionRotation = motion.Rotate;
            track.Interpolation = motion.CalcMode;
            track.Additive = motion.Additive == SvgAnimationAdditive.Sum;
            track.Accumulate = motion.Accumulate == SvgAnimationAccumulate.Sum;
            if (!string.IsNullOrWhiteSpace(track.MotionPath) || !string.IsNullOrWhiteSpace(track.MotionPathReference))
            {
                track.ValueMode = SvgTimelineValueMode.MotionPath;
                values = motion.KeyPoints is { Count: > 0 }
                    ? string.Join(";", motion.KeyPoints.Select(number => Format(number))) : "0;1";
            }
        }
        else if (animation is SvgSet set)
        {
            track.To = set.To ?? string.Empty;
            track.ValueMode = SvgTimelineValueMode.To;
            track.Interpolation = SvgAnimationCalcMode.Discrete;
            return track;
        }
        track.From = from ?? string.Empty;
        track.To = to ?? string.Empty;
        track.By = by ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(values))
        {
            if (track.ValueMode != SvgTimelineValueMode.MotionPath)
                track.ValueMode = SvgTimelineValueMode.KeyFrames;
            var tokens = Split(values!);
            var curves = Split(splines ?? string.Empty);
            for (var i = 0; i < tokens.Length; i++)
            {
                // Discrete values without explicit keyTimes occupy equal intervals, including
                // the last interval. Linear/spline endpoints span the full simple duration.
                var offset = times is not null && i < times.Count ? times[i]
                    : tokens.Length <= 1 ? 0d
                    : track.Interpolation == SvgAnimationCalcMode.Discrete ? (double)i / tokens.Length
                    : (double)i / (tokens.Length - 1);
                track.Keys.Add(new SvgTimelineKeyFrame
                {
                    Offset = offset,
                    Value = tokens[i],
                    Spline = i < curves.Length ? curves[i] : "0 0 1 1"
                });
            }
        }
        else
        {
            track.ValueMode = !string.IsNullOrWhiteSpace(from)
                ? !string.IsNullOrWhiteSpace(to) ? SvgTimelineValueMode.FromTo : SvgTimelineValueMode.FromBy
                : !string.IsNullOrWhiteSpace(to) ? SvgTimelineValueMode.To : SvgTimelineValueMode.By;
        }
        return track;
    }

    /// <summary>Validates before changing the document; the host supplies its undo transaction.</summary>
    public static SvgAnimationElement Apply(SvgDocument document, SvgTimelineTrack track)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        Validate(track);
        EnsureEditable(document, track);
        var animation = track.Source ?? track.Template?.DeepCopy() as SvgAnimationElement ?? CreateAnimation(track.Kind);
        if (KindOf(animation) != track.Kind)
            throw new InvalidOperationException("Create a new track to change its animation kind.");
        // Preserve IDs, href, event handlers, end/min/max/restart/repeatDur, custom attributes,
        // children and any unexposed fields by updating the existing element in place.
        animation.Begin = track.Begin.Trim();
        animation.Duration = track.Duration.Trim();
        animation.RepeatCount = track.RepeatCount.Trim();
        animation.AnimationFill = track.Freeze ? SvgAnimationFill.Freeze : SvgAnimationFill.Remove;
        if (animation is SvgAnimationAttributeElement attribute)
            attribute.AnimationAttributeName = track.Kind == SvgTimelineTrackKind.Transform ? "transform" : track.AttributeName.Trim();
        if (animation is SvgAnimateTransform transform)
            transform.TransformType = track.TransformType;
        if (animation is SvgSet set)
            set.To = track.To.Trim();
        else
        {
            var keys = track.Keys.OrderBy(key => key.Offset).ToArray();
            var useKeys = track.ValueMode is SvgTimelineValueMode.KeyFrames or SvgTimelineValueMode.MotionPath;
            var values = useKeys ? string.Join(";", keys.Select(key => key.Value.Trim())) : null;
            var times = useKeys ? Numbers(keys.Select(key => (float)key.Offset)) : null;
            var splines = useKeys && track.Interpolation == SvgAnimationCalcMode.Spline
                ? string.Join(";", keys.Take(Math.Max(0, keys.Length - 1)).Select(key => key.Spline.Trim())) : null;
            var from = track.ValueMode is SvgTimelineValueMode.FromTo or SvgTimelineValueMode.FromBy ? track.From.Trim() : null;
            var to = track.ValueMode is SvgTimelineValueMode.FromTo or SvgTimelineValueMode.To ? track.To.Trim() : null;
            var by = track.ValueMode is SvgTimelineValueMode.FromBy or SvgTimelineValueMode.By ? track.By.Trim() : null;
            if (animation is SvgAnimationValueElement value)
            {
                value.Values = values!;
                value.KeyTimes = times!;
                value.KeySplines = splines!;
                value.From = from!;
                value.To = to!;
                value.By = by!;
                value.CalcMode = track.Interpolation;
                value.Additive = track.Additive ? SvgAnimationAdditive.Sum : SvgAnimationAdditive.Replace;
                value.Accumulate = track.Accumulate ? SvgAnimationAccumulate.Sum : SvgAnimationAccumulate.None;
            }
            else if (animation is SvgAnimateMotion motion)
            {
                motion.Values = track.ValueMode == SvgTimelineValueMode.MotionPath ? null! : values!;
                motion.KeyTimes = times!;
                motion.KeySplines = splines!;
                motion.From = from!;
                motion.To = to!;
                motion.By = by!;
                motion.CalcMode = track.Interpolation;
                motion.Additive = track.Additive ? SvgAnimationAdditive.Sum : SvgAnimationAdditive.Replace;
                motion.Accumulate = track.Accumulate ? SvgAnimationAccumulate.Sum : SvgAnimationAccumulate.None;
                motion.Rotate = track.MotionRotation.Trim();
                if (track.ValueMode == SvgTimelineValueMode.MotionPath)
                {
                    motion.PathData = string.IsNullOrWhiteSpace(track.MotionPathReference) ? SvgPathBuilder.Parse(track.MotionPath) : null!;
                    motion.KeyPoints = Numbers(keys.Select(key => (float)ParseNumber(key.Value)));
                    if (string.IsNullOrWhiteSpace(track.MotionPathReference))
                    {
                        // Switching to an inline path explicitly removes the old source link.
                        foreach (var child in motion.Children.OfType<SvgMPath>().ToArray())
                            motion.Children.Remove(child);
                    }
                    else
                    {
                        var reference = motion.Children.OfType<SvgMPath>().FirstOrDefault();
                        if (reference is null)
                        { reference = new SvgMPath(); motion.Children.Add(reference); }
                        reference.ReferencedPath = new Uri(track.MotionPathReference, UriKind.RelativeOrAbsolute);
                    }
                }
                else
                {
                    motion.PathData = null!;
                    motion.KeyPoints = null!;
                    foreach (var child in motion.Children.OfType<SvgMPath>().ToArray())
                        motion.Children.Remove(child);
                }
            }
        }
        if (track.Source is null)
        {
            var seed = string.IsNullOrEmpty(track.Target.ID) ? "animation" : track.Target.ID + "-animation";
            var id = seed;
            var index = 2;
            while (document.GetElementById(id) is not null)
                id = seed + "-" + (index++).ToString(CultureInfo.InvariantCulture);
            animation.ID = id;
            RemapDuplicateChildIds(document, animation);
            track.Target.Children.Add(animation);
            track.Source = animation;
        }
        return animation;
    }

    public static SvgTimelineTrack DuplicateTrack(SvgTimelineTrack track)
    {
        if (track?.Source is not { } source)
            throw new InvalidOperationException("Apply the track before duplicating it.");
        var copy = ReadTrack(source);
        copy.Template = (SvgAnimationElement)source.DeepCopy();
        copy.Source = null;
        return copy;
    }

    public static void Delete(SvgDocument document, SvgTimelineTrack track)
    {
        EnsureEditable(document, track);
        if (track.Source is not { Parent: { } parent } animation)
            throw new InvalidOperationException("The track is not part of the document.");
        parent.Children.Remove(animation);
        track.Source = null;
    }

    /// <summary>Produces a static SVG snapshot through the same evaluator used for preview.</summary>
    public static SvgDocument BakeFrame(SvgDocument document, TimeSpan time)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        if (time < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(time));
        using var animator = new SvgAnimationController(document);
        var snapshot = animator.CreateAnimatedDocument(time);
        foreach (var animation in snapshot.Descendants().OfType<SvgAnimationElement>().ToArray())
            animation.Parent?.Children.Remove(animation);
        snapshot.BaseUri = document.BaseUri;
        return snapshot;
    }

    public static void SetKey(SvgTimelineTrack track, double offset, string value, string spline = "0 0 1 1")
    {
        if (track is null)
            throw new ArgumentNullException(nameof(track));
        if (!Finite(offset) || offset < 0 || offset > 1)
            throw new ArgumentOutOfRangeException(nameof(offset));
        ValidateValue(value);
        ValidateSpline(spline);
        var existing = track.Keys.FirstOrDefault(key => Math.Abs(key.Offset - offset) < 0.0000001);
        if (existing is not null)
        {
            existing.Value = value;
            existing.Spline = spline;
        }
        else
        {
            if (track.Keys.Count >= MaximumKeys)
                throw new InvalidOperationException("The track has reached the keyframe limit.");
            track.Keys.Add(new SvgTimelineKeyFrame { Offset = offset, Value = value, Spline = spline });
            track.Keys.Sort((left, right) => left.Offset.CompareTo(right.Offset));
        }
    }

    public static SvgElement MapElement(SvgElement element, SvgDocument destination)
    {
        if (element is null || destination is null)
            throw new ArgumentNullException(element is null ? nameof(element) : nameof(destination));
        var path = new Stack<int>();
        for (var current = element; current.Parent is { } parent; current = parent)
            path.Push(parent.Children.IndexOf(current));
        SvgElement mapped = destination;
        foreach (var index in path)
        {
            if (index < 0 || index >= mapped.Children.Count)
                throw new InvalidOperationException("The document structure changed while the timeline was open.");
            mapped = mapped.Children[index];
        }
        return mapped;
    }

    public static void Validate(SvgTimelineTrack track)
    {
        if (track is null)
            throw new ArgumentNullException(nameof(track));
        if (!Enum.IsDefined(typeof(SvgTimelineTrackKind), track.Kind)
            || !Enum.IsDefined(typeof(SvgTimelineValueMode), track.ValueMode)
            || !Enum.IsDefined(typeof(SvgAnimationCalcMode), track.Interpolation))
            throw new ArgumentException("Choose a supported track kind, value source and interpolation.");
        if (track.Begin is null || track.Begin.Length > 4096 || track.Begin.IndexOfAny(new[] { '<', '>', '\0' }) >= 0)
            throw new ArgumentException("Enter a clock offset or SVG timing expression for the track start.");
        if (!TryParseClock(track.Duration, out var duration) || duration <= 0 || duration > 604800)
        {
            if (!(track.Kind == SvgTimelineTrackKind.Set && track.Duration == "indefinite"))
                throw new ArgumentException("Duration must be greater than zero and at most seven days.");
        }
        if (track.RepeatCount != "indefinite" && (!TryNumber(track.RepeatCount, out var repeats) || repeats <= 0 || repeats > 1000000))
            throw new ArgumentException("Repeat count must be positive or indefinite.");
        if (track.Kind is SvgTimelineTrackKind.Attribute or SvgTimelineTrackKind.Set)
        {
            try
            { XmlConvert.VerifyName(track.AttributeName); }
            catch (Exception exception) when (exception is XmlException or ArgumentException)
            { throw new ArgumentException("Enter an SVG attribute name."); }
        }
        if (track.Kind == SvgTimelineTrackKind.Transform && !Enum.IsDefined(typeof(SvgAnimateTransformType), track.TransformType))
            throw new ArgumentException("Choose a supported transform type.");
        if (track.Kind == SvgTimelineTrackKind.Set)
        { ValidateValue(track.To); return; }
        if (track.ValueMode == SvgTimelineValueMode.MotionPath)
        {
            if (track.Kind != SvgTimelineTrackKind.Motion || (string.IsNullOrWhiteSpace(track.MotionPath) && string.IsNullOrWhiteSpace(track.MotionPathReference)) || track.MotionPath.Length > 1000000)
                throw new ArgumentException("A motion path track needs SVG path data.");
            if (string.IsNullOrWhiteSpace(track.MotionPathReference))
            {
                using var path = SkiaSharp.SKPath.ParseSvgPathData(track.MotionPath);
                if (path is null || path.IsEmpty || path.Points.Any(point => !Finite(point.X) || !Finite(point.Y)))
                    throw new ArgumentException("The motion path is invalid or empty.");
            }
            else if (!track.MotionPathReference.StartsWith("#", StringComparison.Ordinal)
                || track.Target.OwnerDocument?.GetElementById(track.MotionPathReference.Substring(1)) is not SvgPath)
                throw new ArgumentException("The motion path reference must identify a path in this document.");
        }
        if (track.Kind == SvgTimelineTrackKind.Motion && track.MotionRotation != "auto" && track.MotionRotation != "auto-reverse"
            && !TryNumber(track.MotionRotation, out _))
            throw new ArgumentException("Motion rotation must be an angle, auto, or auto-reverse.");
        if (track.ValueMode is SvgTimelineValueMode.KeyFrames or SvgTimelineValueMode.MotionPath)
        {
            var minimum = track.Interpolation == SvgAnimationCalcMode.Discrete ? 1 : 2;
            if (track.Keys.Count < minimum || track.Keys.Count > MaximumKeys)
                throw new ArgumentException($"This track needs between {minimum} and {MaximumKeys} keys.");
            var keys = track.Keys.OrderBy(key => key.Offset).ToArray();
            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                if (!Finite(key.Offset) || key.Offset < 0 || key.Offset > 1 || i > 0 && key.Offset <= keys[i - 1].Offset)
                    throw new ArgumentException("Key positions must be unique, increasing values from 0 to 100%.");
                ValidateValue(key.Value);
                ValidateTransformValue(track, key.Value);
                if (track.ValueMode == SvgTimelineValueMode.MotionPath && (!TryNumber(key.Value, out var point) || point < 0 || point > 1))
                    throw new ArgumentException("Motion path key values are distances from 0 to 1 along the path.");
                if (track.Interpolation == SvgAnimationCalcMode.Spline && i < keys.Length - 1)
                    ValidateSpline(key.Spline);
            }
            if (Math.Abs(keys[0].Offset) > 0.0000001 || track.Interpolation != SvgAnimationCalcMode.Discrete && Math.Abs(keys[keys.Length - 1].Offset - 1) > 0.0000001)
                throw new ArgumentException("The first key must be at 0%; linear, paced and spline tracks also need a key at 100%.");
        }
        else
        {
            if (track.ValueMode is SvgTimelineValueMode.FromTo or SvgTimelineValueMode.FromBy)
            { ValidateValue(track.From); ValidateTransformValue(track, track.From); }
            if (track.ValueMode is SvgTimelineValueMode.FromTo or SvgTimelineValueMode.To)
            { ValidateValue(track.To); ValidateTransformValue(track, track.To); }
            if (track.ValueMode is SvgTimelineValueMode.FromBy or SvgTimelineValueMode.By)
            { ValidateValue(track.By); ValidateTransformValue(track, track.By); }
        }
    }

    public static bool TryParseClock(string? value, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var text = value!.Trim();
        var multiplier = 1d;
        if (text.EndsWith("ms", StringComparison.Ordinal))
        { multiplier = .001; text = text.Substring(0, text.Length - 2); }
        else if (text.EndsWith("min", StringComparison.Ordinal))
        { multiplier = 60; text = text.Substring(0, text.Length - 3); }
        else if (text.EndsWith("h", StringComparison.Ordinal))
        { multiplier = 3600; text = text.Substring(0, text.Length - 1); }
        else if (text.EndsWith("s", StringComparison.Ordinal))
            text = text.Substring(0, text.Length - 1);
        if (text.IndexOf(':') >= 0)
        {
            var parts = text.Split(':');
            if (parts.Length < 2 || parts.Length > 3)
                return false;
            var total = 0d;
            for (var i = 0; i < parts.Length; i++)
            {
                if (!TryNumber(parts[i], out var part) || part < 0 || i > 0 && part >= 60)
                    return false;
                total = total * 60 + part;
            }
            seconds = total * multiplier;
            return Finite(seconds);
        }
        if (!TryNumber(text, out var number))
            return false;
        seconds = number * multiplier;
        return Finite(seconds);
    }

    private static void EnsureEditable(SvgDocument document, SvgTimelineTrack track)
    {
        if (track is null)
            throw new ArgumentNullException(nameof(track));
        if (track.Target != document && track.Target.OwnerDocument != document)
            throw new InvalidOperationException("The animation target is no longer part of this document.");
        if (SvgEditorController.IsEditLocked(track.Target) || track.Source is { } source && SvgEditorController.IsLocked(source))
            throw new InvalidOperationException("Unlock the animation target and its layer before editing its timeline.");
        if (track.Source is { } existing && (existing.OwnerDocument != document || existing.TargetElement != track.Target))
            throw new InvalidOperationException("The animation or its target changed. Reopen the timeline.");
    }

    private static SvgTimelineTrackKind KindOf(SvgAnimationElement animation) => animation switch
    {
        SvgAnimateTransform => SvgTimelineTrackKind.Transform,
        SvgAnimateMotion => SvgTimelineTrackKind.Motion,
        SvgSet => SvgTimelineTrackKind.Set,
        SvgAnimationValueElement => SvgTimelineTrackKind.Attribute,
        _ => throw new ArgumentException("This SVG animation element has no editable track representation.")
    };

    private static SvgAnimationElement CreateAnimation(SvgTimelineTrackKind kind) => kind switch
    {
        SvgTimelineTrackKind.Transform => new SvgAnimateTransform(),
        SvgTimelineTrackKind.Motion => new SvgAnimateMotion(),
        SvgTimelineTrackKind.Set => new SvgSet(),
        _ => new SvgAnimate()
    };

    private static void RemapDuplicateChildIds(SvgDocument document, SvgAnimationElement animation)
    {
        var used = new HashSet<string>(document.Descendants().Select(element => element.ID)
            .Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal) { document.ID ?? string.Empty, animation.ID };
        var remapped = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in animation.Descendants())
        {
            if (string.IsNullOrEmpty(child.ID))
                continue;
            var old = child.ID;
            var id = old;
            var suffix = 2;
            while (!used.Add(id))
                id = old + "-" + (suffix++).ToString(CultureInfo.InvariantCulture);
            if (id == old)
                continue;
            remapped[old] = id;
            child.ID = id;
        }
        foreach (var child in animation.Descendants().Concat(new[] { animation }))
        {
            foreach (var attribute in child.CustomAttributes.Keys.ToArray())
            {
                var value = child.CustomAttributes[attribute];
                foreach (var pair in remapped)
                    value = value.Replace("url(#" + pair.Key + ")", "url(#" + pair.Value + ")");
                if (value.Length > 1 && value[0] == '#' && remapped.TryGetValue(value.Substring(1), out var target))
                    value = "#" + target;
                child.CustomAttributes[attribute] = value;
            }
            if (child is SvgMPath path && path.ReferencedPath is { } reference
                && reference.OriginalString.StartsWith("#", StringComparison.Ordinal)
                && remapped.TryGetValue(reference.OriginalString.Substring(1), out var pathId))
                path.ReferencedPath = new Uri("#" + pathId, UriKind.Relative);
        }
    }

    private static void ValidateTransformValue(SvgTimelineTrack track, string value)
    {
        if (track.Kind != SvgTimelineTrackKind.Transform)
            return;
        var numbers = value.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var count = numbers.Length;
        var valid = track.TransformType switch
        {
            SvgAnimateTransformType.Translate or SvgAnimateTransformType.Scale => count is 1 or 2,
            SvgAnimateTransformType.Rotate => count is 1 or 3,
            _ => count == 1
        };
        if (!valid || numbers.Any(number => !TryNumber(number, out _)))
            throw new ArgumentException("Transform keys need finite SVG numbers: translate/scale use 1–2, rotate uses 1 or 3, and skew uses 1.");
    }

    private static void ValidateValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1000000 || value.IndexOf(';') >= 0 || value.IndexOf('\0') >= 0)
            throw new ArgumentException("Each key needs one nonempty SVG value without a semicolon.");
    }

    private static void ValidateSpline(string spline)
    {
        var parts = (spline ?? string.Empty).Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts.Any(part => !TryNumber(part, out var value) || value < 0 || value > 1))
            throw new ArgumentException("Easing needs four Bézier control values between 0 and 1.");
    }

    private static SvgNumberCollection Numbers(IEnumerable<float> values)
    {
        var result = new SvgNumberCollection();
        result.AddRange(values);
        return result;
    }

    private static string[] Split(string text) => text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()).ToArray();
    private static double ParseNumber(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static bool TryNumber(string? value, out double number) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && Finite(number);
    private static bool Finite(double number) => !double.IsNaN(number) && !double.IsInfinity(number);
    private static string Format(double value) => value.ToString("G9", CultureInfo.InvariantCulture);
}
