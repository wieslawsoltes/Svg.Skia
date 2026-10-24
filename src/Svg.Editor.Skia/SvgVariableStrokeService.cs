using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;
using Svg.Editor.Svg.Models;
using Svg.Pathing;

namespace Svg.Editor.Skia;

/// <summary>
/// Turns a width profile into ordinary filled SVG geometry. Widths are measured in the
/// centerline's local coordinate system, so the existing SVG transform remains applicable.
/// </summary>
public static class SvgVariableStrokeService
{
    public const string CenterlineAttribute = "data-unodraw-stroke-centerline";
    public const string ProfileAttribute = "data-unodraw-stroke-profile";
    public const string CapAttribute = "data-unodraw-stroke-cap";
    public const string OutlineAttribute = "data-unodraw-stroke-outline";

    private const int MaximumSamples = 16384;
    private const int MaximumDepth = 18;
    private const int MaximumContours = 256;
    private const float QuarterCircleControl = 0.5522847498307936f;

    /// <summary>
    /// Creates an adaptively sampled outline of each contour. Width is interpolated by arc
    /// length, with flat, round or square caps on open contours. Closed contours use opposed
    /// winding boundaries and require matching widths at the seam.
    /// The caller owns the returned path; the centerline and profile are left unchanged.
    /// </summary>
    public static SKPath CreateOutline(SKPath centerline, StrokeProfile profile,
        SKStrokeCap cap = SKStrokeCap.Round, float tolerance = 0.75f)
    {
        if (centerline is null)
            throw new ArgumentNullException(nameof(centerline));
        var points = ValidateProfile(profile);
        if (!IsFinite(tolerance) || tolerance <= 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Outline tolerance must be positive and finite.");
        if (cap is not (SKStrokeCap.Butt or SKStrokeCap.Round or SKStrokeCap.Square))
            throw new ArgumentOutOfRangeException(nameof(cap), "Choose a flat, round or square cap.");

        using var measure = new SKPathMeasure(centerline, false, 2);
        using var builder = new SKPathBuilder();
        var contourCount = 0;
        var sampleCount = 0;
        var hasGeometry = false;
        do
        {
            if (++contourCount > MaximumContours)
                throw new InvalidOperationException("The path has too many contours for a width profile.");
            if (measure.IsClosed && Math.Abs(points[0].Width - points[points.Length - 1].Width) > 0.00001)
                throw new ArgumentException("A closed contour needs the same width at the start and end of its profile.", nameof(profile));
            var length = measure.Length;
            if (!IsFinite(length))
                throw new ArgumentException("The path contains invalid coordinates.", nameof(centerline));
            if (length <= 0.00001f)
                continue;

            var samples = new List<Sample>();
            var first = SampleAt(measure, 0, length, points);
            samples.Add(first);
            sampleCount++;
            var distances = points.Select(point => (float)point.Offset * length)
                .Where(distance => distance > 0 && distance < length).Concat(new[] { length })
                .Distinct().OrderBy(distance => distance);
            foreach (var distance in distances)
            {
                var last = SampleAt(measure, distance, length, points);
                Subdivide(first, last, 0);
                first = last;
            }

            if (measure.IsClosed)
                AppendClosedContour(builder, samples);
            else
                AppendContour(builder, samples, cap);
            hasGeometry = true;

            void Subdivide(Sample start, Sample end, int depth)
            {
                var distance = start.Distance + (end.Distance - start.Distance) * 0.5f;
                var middle = SampleAt(measure, distance, length, points);
                var flat = SKPoint.Distance(middle.Left, Midpoint(start.Left, end.Left)) <= tolerance
                    && SKPoint.Distance(middle.Right, Midpoint(start.Right, end.Right)) <= tolerance
                    && end.Distance - start.Distance - SKPoint.Distance(start.Position, end.Position) <= tolerance;
                if (flat)
                {
                    // An S curve can cross its chord at the midpoint. Its extra arc length
                    // can also be tiny on a long, shallow curve, so inspect both halves.
                    var firstQuarter = SampleAt(measure, start.Distance + (end.Distance - start.Distance) * 0.25f, length, points);
                    var lastQuarter = SampleAt(measure, start.Distance + (end.Distance - start.Distance) * 0.75f, length, points);
                    flat = SKPoint.Distance(firstQuarter.Left, Midpoint(start.Left, Midpoint(start.Left, end.Left))) <= tolerance
                        && SKPoint.Distance(firstQuarter.Right, Midpoint(start.Right, Midpoint(start.Right, end.Right))) <= tolerance
                        && SKPoint.Distance(lastQuarter.Left, Midpoint(end.Left, Midpoint(start.Left, end.Left))) <= tolerance
                        && SKPoint.Distance(lastQuarter.Right, Midpoint(end.Right, Midpoint(start.Right, end.Right))) <= tolerance;
                }
                if (flat || depth == MaximumDepth || distance == start.Distance || distance == end.Distance)
                {
                    if (++sampleCount > MaximumSamples)
                        throw new InvalidOperationException("The path is too complex for this width profile. Simplify the centerline first.");
                    samples.Add(end);
                    return;
                }

                Subdivide(start, middle, depth + 1);
                Subdivide(middle, end, depth + 1);
            }
        }
        while (measure.NextContour());

        if (!hasGeometry)
            throw new ArgumentException("Choose a path with a nonzero length.", nameof(centerline));
        var result = builder.Detach();
        result.FillType = SKPathFillType.Winding;
        return result;
    }

    /// <summary>
    /// Returns the original centerline of an untouched generated outline, or the geometry of
    /// an ordinary SVG path. Reopening is rejected if the generated outline's nodes changed.
    /// </summary>
    public static SKPath GetCenterline(SvgPath path)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (path.CustomAttributes.TryGetValue(CenterlineAttribute, out var data))
        {
            if (!path.CustomAttributes.TryGetValue(OutlineAttribute, out var fingerprint)
                || !string.Equals(fingerprint, Fingerprint(path.PathData?.ToString() ?? string.Empty), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("This outline's nodes have changed. Undo the node edits or choose a new centerline to edit its width profile.");
            }

            var source = new SvgPath { PathData = SvgPathBuilder.Parse(data.AsSpan()) };
            return PathService.ElementToPath(source)
                ?? throw new InvalidOperationException("The stored centerline could not be read.");
        }

        return PathService.ElementToPath(path)
            ?? throw new InvalidOperationException("The selected path has no editable geometry.");
    }

    /// <summary>
    /// Converts the existing element in place. Wrap this method in a controller Execute call
    /// for undo. IDs, transforms, clipping, effects, child metadata and unrelated attributes
    /// are preserved; the source stroke becomes a filled outline without repeated markers.
    /// </summary>
    public static void Apply(SvgPath path, StrokeProfile profile, SvgPaintServer paint,
        SKStrokeCap cap = SKStrokeCap.Round, float tolerance = 0.75f)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (paint is null)
            throw new ArgumentNullException(nameof(paint));
        if (SvgEditorController.IsEditLocked(path))
            throw new InvalidOperationException("Unlock the path or its locked layer before editing its width profile.");

        var points = ValidateProfile(profile);
        using var centerline = GetCenterline(path);
        using var outline = CreateOutline(centerline, profile, cap, tolerance);
        var data = SvgPathBuilder.Parse(PathService.ToSvgPathData(outline).AsSpan());
        var source = path.CustomAttributes.TryGetValue(CenterlineAttribute, out var original)
            ? original : path.PathData?.ToString() ?? string.Empty;
        var canonicalProfile = new StrokeProfile();
        canonicalProfile.Points.Clear();
        foreach (var point in points)
            canonicalProfile.Points.Add(new StrokePointInfo { Offset = point.Offset, Width = point.Width });
        var fillOpacity = path.CustomAttributes.ContainsKey(CenterlineAttribute) ? path.FillOpacity : path.StrokeOpacity;

        // Every operation above is read-only or operates on new data. Invalid input never leaves
        // a partially converted element, even when the caller does not use a transaction.
        path.PathData = data;
        path.Fill = paint;
        path.FillOpacity = fillOpacity;
        path.FillRule = SvgFillRule.NonZero;
        path.Stroke = SvgPaintServer.None;
        path.MarkerStart = new Uri("none", UriKind.Relative);
        path.MarkerMid = new Uri("none", UriKind.Relative);
        path.MarkerEnd = new Uri("none", UriKind.Relative);
        path.CustomAttributes[CenterlineAttribute] = source;
        path.CustomAttributes[ProfileAttribute] = canonicalProfile.ToString();
        path.CustomAttributes[CapAttribute] = cap == SKStrokeCap.Round ? "round" : cap == SKStrokeCap.Square ? "square" : "butt";
        path.CustomAttributes[OutlineAttribute] = Fingerprint(path.PathData.ToString());
        path.CustomAttributes.Remove("stroke-profile");
        path.CustomAttributes.Remove(SvgBrushLibraryService.BrushIdAttribute);
        path.CustomAttributes.Remove(SvgBrushLibraryService.SettingsAttribute);
        path.CustomAttributes.Remove(SvgBrushLibraryService.BaseOpacityAttribute);
        path.OnPathUpdated();
    }

    private static StrokePointInfo[] ValidateProfile(StrokeProfile profile)
    {
        if (profile is null)
            throw new ArgumentNullException(nameof(profile));
        if (profile.Points.Count is < 2 or > 256 || profile.Points.Any(point => point is null))
            throw new ArgumentException("A width profile needs between 2 and 256 points.", nameof(profile));
        var points = profile.Points.OrderBy(point => point.Offset).ToArray();
        var hasWidth = false;
        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            if (!IsFinite(point.Offset) || point.Offset is < 0 or > 1
                || !IsFinite(point.Width) || point.Width < 0 || point.Width > float.MaxValue)
                throw new ArgumentException("Profile positions must be between 0 and 1 and widths must be nonnegative finite numbers.", nameof(profile));
            if (i > 0 && point.Offset <= points[i - 1].Offset)
                throw new ArgumentException("Every width profile point needs a different position.", nameof(profile));
            hasWidth |= point.Width > 0;
        }

        if (!hasWidth)
            throw new ArgumentException("At least one width must be greater than zero.", nameof(profile));
        return points;
    }

    private static Sample SampleAt(SKPathMeasure measure, float distance, float length, StrokePointInfo[] points)
    {
        if (!measure.GetPositionAndTangent(distance, out var position, out var tangent))
            throw new InvalidOperationException("The centerline could not be sampled.");
        var magnitude = Math.Sqrt((double)tangent.X * tangent.X + (double)tangent.Y * tangent.Y);
        if (!IsFinite(magnitude) || magnitude <= 0)
            throw new InvalidOperationException("The centerline has a point with no usable direction. Simplify the path first.");
        tangent = new SKPoint((float)(tangent.X / magnitude), (float)(tangent.Y / magnitude));
        var offset = distance / length;
        var width = points[0].Width;
        if (offset >= points[points.Length - 1].Offset)
            width = points[points.Length - 1].Width;
        else
            for (var i = 1; i < points.Length; i++)
                if (offset >= points[i - 1].Offset && offset <= points[i].Offset)
                {
                    var amount = (offset - points[i - 1].Offset) / (points[i].Offset - points[i - 1].Offset);
                    width = points[i - 1].Width + (points[i].Width - points[i - 1].Width) * amount;
                    break;
                }
        var radius = (float)(width * 0.5);
        var normal = new SKPoint(-tangent.Y, tangent.X);
        var left = Add(position, Multiply(normal, radius));
        var right = Add(position, Multiply(normal, -radius));
        if (!IsFinite(left.X) || !IsFinite(left.Y) || !IsFinite(right.X) || !IsFinite(right.Y))
            throw new ArgumentException("The profile produces coordinates outside the supported range.", nameof(points));
        return new Sample(distance, position, tangent, left, right, radius);
    }

    private static void AppendContour(SKPathBuilder builder, List<Sample> samples, SKStrokeCap cap)
    {
        var first = samples[0];
        var last = samples[samples.Count - 1];
        var startExtension = cap == SKStrokeCap.Square ? Multiply(first.Tangent, -first.Radius) : new SKPoint();
        var endExtension = cap == SKStrokeCap.Square ? Multiply(last.Tangent, last.Radius) : new SKPoint();
        builder.MoveTo(Add(first.Left, startExtension));
        builder.LineTo(first.Left);
        for (var i = 1; i < samples.Count; i++)
            builder.LineTo(samples[i].Left);
        if (cap == SKStrokeCap.Square)
        {
            builder.LineTo(Add(last.Left, endExtension));
            builder.LineTo(Add(last.Right, endExtension));
        }
        if (cap == SKStrokeCap.Round && last.Radius > 0)
            AppendRoundCap(builder, last.Position, new SKPoint(-last.Tangent.Y, last.Tangent.X), last.Tangent, last.Radius);
        else
            builder.LineTo(last.Right);
        for (var i = samples.Count - 2; i >= 0; i--)
            builder.LineTo(samples[i].Right);
        if (cap == SKStrokeCap.Square)
            builder.LineTo(Add(first.Right, startExtension));
        if (cap == SKStrokeCap.Round && first.Radius > 0)
            AppendRoundCap(builder, first.Position, new SKPoint(first.Tangent.Y, -first.Tangent.X), Multiply(first.Tangent, -1), first.Radius);
        builder.Close();
    }

    private static void AppendClosedContour(SKPathBuilder builder, List<Sample> samples)
    {
        // Opposite contour directions preserve the centre hole with the nonzero fill rule,
        // including mirrored source contours and subsequent SVG transformations.
        builder.MoveTo(samples[0].Left);
        for (var i = 1; i < samples.Count; i++)
            builder.LineTo(samples[i].Left);
        builder.Close();
        builder.MoveTo(samples[samples.Count - 1].Right);
        for (var i = samples.Count - 2; i >= 0; i--)
            builder.LineTo(samples[i].Right);
        builder.Close();
    }

    private static void AppendRoundCap(SKPathBuilder builder, SKPoint center, SKPoint normal, SKPoint outward, float radius)
    {
        var from = Add(center, Multiply(normal, radius));
        var middle = Add(center, Multiply(outward, radius));
        var to = Add(center, Multiply(normal, -radius));
        var control = radius * QuarterCircleControl;
        builder.CubicTo(Add(from, Multiply(outward, control)), Add(middle, Multiply(normal, control)), middle);
        builder.CubicTo(Add(middle, Multiply(normal, -control)), Add(to, Multiply(outward, control)), to);
    }

    private static string Fingerprint(string data)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-", string.Empty);
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static SKPoint Add(SKPoint first, SKPoint second) => new(first.X + second.X, first.Y + second.Y);
    private static SKPoint Multiply(SKPoint point, float value) => new(point.X * value, point.Y * value);
    private static SKPoint Midpoint(SKPoint first, SKPoint second) => new(
        (float)(((double)first.X + second.X) * 0.5), (float)(((double)first.Y + second.Y) * 0.5));

    private readonly struct Sample
    {
        public Sample(float distance, SKPoint position, SKPoint tangent, SKPoint left, SKPoint right, float radius)
        {
            Distance = distance;
            Position = position;
            Tangent = tangent;
            Left = left;
            Right = right;
            Radius = radius;
        }

        public float Distance { get; }
        public SKPoint Position { get; }
        public SKPoint Tangent { get; }
        public SKPoint Left { get; }
        public SKPoint Right { get; }
        public float Radius { get; }
    }
}
