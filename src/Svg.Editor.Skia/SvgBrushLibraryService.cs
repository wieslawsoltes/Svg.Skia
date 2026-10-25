using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Editor.Svg.Models;
using Svg.Pathing;

namespace Svg.Editor.Skia;

public enum SvgBrushKind { WidthProfile, Pattern, Scatter }

/// <summary>Portable vector brush parameters, stored in the document and exportable as a library.</summary>
public sealed class SvgBrushDefinition
{
    public string Id { get; set; } = "brush";
    public string Name { get; set; } = "New brush";
    public SvgBrushKind Kind { get; set; }
    public double Width { get; set; } = 24;
    public double Spacing { get; set; } = 1.25;
    public double Rotation { get; set; }
    public double Scatter { get; set; } = 0.5;
    public int Seed { get; set; } = 1;
    public bool FollowTangent { get; set; } = true;
    public SKStrokeCap Cap { get; set; } = SKStrokeCap.Round;
    public SKColor Color { get; set; } = new(112, 97, 220);
    public StrokeProfile Profile { get; set; } = new();
    public string StampPath { get; set; } = "M0 -5 L1.5 -1.5 L5 0 L1.5 1.5 L0 5 L-1.5 1.5 L-5 0 L-1.5 -1.5 Z";
    public override string ToString() => Name;
}

/// <summary>Creates width, repeating-pattern and deterministic scatter brushes as native SVG paths.</summary>
public static class SvgBrushLibraryService
{
    public const string DefinitionAttribute = "data-unodraw-brush";
    public const string BrushIdAttribute = "data-unodraw-brush-id";
    public const string SettingsAttribute = "data-unodraw-brush-settings";
    public const string BaseOpacityAttribute = "data-unodraw-brush-base-opacity";
    private const int MaximumStamps = 4096;

    public static string? GetBrushId(SvgElement element)
    {
        if (!element.CustomAttributes.TryGetValue(BrushIdAttribute, out var value))
            return null;
        return value.StartsWith("url(#", StringComparison.Ordinal) && value.EndsWith(")", StringComparison.Ordinal)
            ? value.Substring(5, value.Length - 6) : value;
    }

    public static SvgBrushDefinition? GetAppliedBrush(SvgPath path)
    {
        if (!path.CustomAttributes.TryGetValue(SettingsAttribute, out var data))
            return null;
        if (data.Length > 1400000)
            throw new FormatException("The brush metadata exceeds its supported size.");
        var definition = ParseBrush(ReadXml(Encoding.UTF8.GetString(Convert.FromBase64String(data))));
        definition.Id = GetBrushId(path) ?? definition.Id;
        return definition;
    }

    public static IReadOnlyList<SvgBrushDefinition> Load(SvgDocument document)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        var result = new List<SvgBrushDefinition>();
        foreach (var element in document.Descendants().Where(element => element.CustomAttributes.ContainsKey(DefinitionAttribute)))
        {
            var data = element.CustomAttributes[DefinitionAttribute];
            if (data.Length > 1400000)
                throw new FormatException("A brush definition exceeds its size limit.");
            var definition = ParseBrush(ReadXml(Encoding.UTF8.GetString(Convert.FromBase64String(data))));
            definition.Id = element.ID ?? throw new FormatException("A document brush needs a resource ID.");
            result.Add(definition);
        }
        return result;
    }

    /// <summary>Stages all linked geometry before updating a definition or its linked artwork.</summary>
    public static void Save(SvgDocument document, SvgBrushDefinition definition, bool updateLinkedArtwork = true)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        Validate(definition);
        var existing = document.GetElementById(definition.Id);
        if (existing is not null && !existing.CustomAttributes.ContainsKey(DefinitionAttribute))
            throw new InvalidOperationException("That brush ID already belongs to other artwork.");
        if (existing is not null && SvgEditorController.IsEditLocked(existing))
            throw new InvalidOperationException("Unlock the brush resource before editing it.");
        var targets = updateLinkedArtwork ? document.Descendants().OfType<SvgPath>()
            .Where(path => GetBrushId(path) == definition.Id).ToArray() : Array.Empty<SvgPath>();
        var staged = new List<(SvgPath Target, SvgPath Result)>();
        foreach (var target in targets)
        {
            if (SvgEditorController.IsEditLocked(target))
                throw new InvalidOperationException("A linked brush object is locked. Unlock it or disable updating linked artwork.");
            var clone = (SvgPath)target.DeepCopy();
            Apply(clone, definition);
            staged.Add((target, clone));
        }
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(BrushXml(definition).ToString(SaveOptions.DisableFormatting)));
        var definitions = document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (definitions is null)
        {
            definitions = new SvgDefinitionList();
            document.Children.Insert(0, definitions);
        }
        if (existing is null)
        {
            existing = new SvgGroup { ID = definition.Id };
            definitions.Children.Add(existing);
        }
        existing.CustomAttributes[DefinitionAttribute] = encoded;
        foreach (var (target, result) in staged)
            CopyGeneratedState(result, target);
    }

    public static void Remove(SvgDocument document, string id)
    {
        var resource = document.GetElementById(id);
        if (resource is null || !resource.CustomAttributes.ContainsKey(DefinitionAttribute))
            return;
        if (SvgEditorController.IsEditLocked(resource))
            throw new InvalidOperationException("Unlock the brush before removing it.");
        resource.Parent.Children.Remove(resource);
        // Artwork retains its independent geometry and full brush settings after detachment.
        foreach (var path in document.Descendants().OfType<SvgPath>())
            if (GetBrushId(path) == id)
                path.CustomAttributes.Remove(BrushIdAttribute);
    }

    public static SKPath CreateOutline(SKPath centerline, SvgBrushDefinition definition)
    {
        if (centerline is null)
            throw new ArgumentNullException(nameof(centerline));
        Validate(definition);
        if (definition.Kind == SvgBrushKind.WidthProfile)
        {
            var profile = new StrokeProfile();
            profile.Points.Clear();
            foreach (var point in definition.Profile.Points)
                profile.Points.Add(new StrokePointInfo { Offset = point.Offset, Width = point.Width * definition.Width });
            return SvgVariableStrokeService.CreateOutline(centerline, profile, definition.Cap);
        }
        using var motif = SKPath.ParseSvgPathData(definition.StampPath)
            ?? throw new FormatException("The brush stamp must be a valid SVG path.");
        var bounds = motif.Bounds;
        var extent = Math.Max(bounds.Width, bounds.Height);
        if (extent <= 0)
            throw new FormatException("The stamp needs nonzero geometry.");
        using var builder = new SKPathBuilder();
        using var measure = new SKPathMeasure(centerline, false, 2);
        var random = new Random(definition.Seed);
        var count = 0;
        var contours = 0;
        do
        {
            if (++contours > 256)
                throw new ArgumentException("A brush stroke may contain at most 256 contours.");
            var length = measure.Length;
            if (!Finite(length))
                throw new ArgumentException("The centerline has invalid coordinates.", nameof(centerline));
            if (length <= 0.00001f)
                continue;
            var step = definition.Width * definition.Spacing;
            var requested = measure.IsClosed ? Math.Ceiling(length / step) : Math.Floor(length / step) + 1;
            var stamps = Math.Max(1, (int)Math.Min(MaximumStamps + 1d, requested));
            if (count + stamps > MaximumStamps)
                throw new InvalidOperationException("The brush would create more than 4,096 stamps. Increase spacing or width.");
            for (var index = 0; index < stamps; index++)
            {
                // Closed contours fit a seamless repeat; open contours retain exact spacing.
                var distance = measure.IsClosed ? length * index / stamps : (float)(index * step);
                if (!measure.GetPositionAndTangent(distance, out var point, out var tangent))
                    throw new InvalidOperationException("The centerline could not be sampled.");
                var angle = definition.Rotation * Math.PI / 180;
                if (definition.FollowTangent)
                    angle += Math.Atan2(tangent.Y, tangent.X);
                var size = definition.Width / extent;
                if (definition.Kind == SvgBrushKind.Scatter)
                {
                    var displacement = (random.NextDouble() * 2 - 1) * definition.Scatter * definition.Width;
                    point.X -= (float)(tangent.Y * displacement);
                    point.Y += (float)(tangent.X * displacement);
                    angle += (random.NextDouble() * 2 - 1) * Math.PI * definition.Scatter;
                    size *= 0.65 + random.NextDouble() * 0.7;
                }
                var cos = (float)(Math.Cos(angle) * size);
                var sin = (float)(Math.Sin(angle) * size);
                var transform = new SKMatrix(cos, -sin, point.X - cos * bounds.MidX + sin * bounds.MidY,
                    sin, cos, point.Y - sin * bounds.MidX - cos * bounds.MidY, 0, 0, 1);
                builder.AddPath(motif, transform);
                count++;
            }
        } while (measure.NextContour());
        if (count == 0)
            throw new ArgumentException("Choose a centerline with a nonzero length.", nameof(centerline));
        return builder.Detach();
    }

    public static void Apply(SvgPath path, SvgBrushDefinition definition)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (SvgEditorController.IsEditLocked(path))
            throw new InvalidOperationException("Unlock this path before applying a brush.");
        using var centerline = SvgVariableStrokeService.GetCenterline(path);
        using var outline = CreateOutline(centerline, definition);
        var geometry = SvgPathBuilder.Parse(PathService.ToSvgPathData(outline).AsSpan());
        var settings = Convert.ToBase64String(Encoding.UTF8.GetBytes(BrushXml(definition).ToString(SaveOptions.DisableFormatting)));
        var source = path.CustomAttributes.TryGetValue(SvgVariableStrokeService.CenterlineAttribute, out var original)
            ? original : path.PathData?.ToString() ?? string.Empty;
        var opacity = path.CustomAttributes.TryGetValue(BaseOpacityAttribute, out var savedOpacity)
            && float.TryParse(savedOpacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var baseOpacity)
            && Finite(baseOpacity) && baseOpacity is >= 0 and <= 1 ? baseOpacity
            : path.CustomAttributes.ContainsKey(SvgVariableStrokeService.CenterlineAttribute) ? path.FillOpacity : path.StrokeOpacity;
        path.PathData = geometry;
        path.Fill = new SvgColourServer(System.Drawing.Color.FromArgb(definition.Color.Red, definition.Color.Green, definition.Color.Blue));
        path.FillOpacity = opacity * definition.Color.Alpha / 255f;
        path.Stroke = SvgPaintServer.None;
        path.FillRule = SvgFillRule.NonZero;
        path.MarkerStart = path.MarkerMid = path.MarkerEnd = new Uri("none", UriKind.Relative);
        path.CustomAttributes[SvgVariableStrokeService.CenterlineAttribute] = source;
        path.CustomAttributes[SvgVariableStrokeService.OutlineAttribute] = Fingerprint(path.PathData.ToString());
        // Standard fragment-url spelling participates in the controller's resource ID remapping.
        path.CustomAttributes[BrushIdAttribute] = "url(#" + definition.Id + ")";
        path.CustomAttributes[SettingsAttribute] = settings;
        path.CustomAttributes[BaseOpacityAttribute] = opacity.ToString("R", CultureInfo.InvariantCulture);
        if (definition.Kind == SvgBrushKind.WidthProfile)
        {
            var profile = new StrokeProfile();
            profile.Points.Clear();
            foreach (var point in definition.Profile.Points)
                profile.Points.Add(new StrokePointInfo { Offset = point.Offset, Width = point.Width * definition.Width });
            path.CustomAttributes[SvgVariableStrokeService.ProfileAttribute] = profile.ToString();
            path.CustomAttributes[SvgVariableStrokeService.CapAttribute] = definition.Cap == SKStrokeCap.Round ? "round" : definition.Cap == SKStrokeCap.Square ? "square" : "butt";
        }
        else
        {
            path.CustomAttributes.Remove(SvgVariableStrokeService.ProfileAttribute);
            path.CustomAttributes.Remove(SvgVariableStrokeService.CapAttribute);
        }
        path.CustomAttributes.Remove("stroke-profile");
        path.OnPathUpdated();
    }

    public static string Export(IEnumerable<SvgBrushDefinition> brushes)
    {
        if (brushes is null)
            throw new ArgumentNullException(nameof(brushes));
        var entries = brushes.ToArray();
        if (entries.Length > 256)
            throw new ArgumentException("A library can contain at most 256 brushes.", nameof(brushes));
        if (entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new ArgumentException("Brush IDs must be unique within a library.", nameof(brushes));
        foreach (var entry in entries)
            Validate(entry);
        return new XElement("unodraw-brush-library", new XAttribute("version", 1), entries.Select(BrushXml)).ToString();
    }

    public static IReadOnlyList<SvgBrushDefinition> Import(string xml)
    {
        var root = ReadXml(xml);
        if (root.Name != "unodraw-brush-library" || (int?)root.Attribute("version") != 1)
            throw new FormatException("This brush library format or version is not supported.");
        var entries = root.Elements("brush").Take(257).Select(ParseBrush).ToArray();
        if (entries.Length > 256 || entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new FormatException("A library needs at most 256 brushes with unique IDs.");
        return entries;
    }

    private static XElement BrushXml(SvgBrushDefinition definition) => new("brush",
        new XAttribute("id", definition.Id), new XAttribute("name", definition.Name), new XAttribute("kind", definition.Kind),
        new XAttribute("width", definition.Width), new XAttribute("spacing", definition.Spacing),
        new XAttribute("rotation", definition.Rotation), new XAttribute("scatter", definition.Scatter),
        new XAttribute("seed", definition.Seed), new XAttribute("follow", definition.FollowTangent), new XAttribute("cap", definition.Cap),
        new XAttribute("color", $"#{definition.Color.Red:X2}{definition.Color.Green:X2}{definition.Color.Blue:X2}{definition.Color.Alpha:X2}"),
        new XElement("profile", definition.Profile.ToString()), new XElement("stamp", definition.StampPath));

    private static SvgBrushDefinition ParseBrush(XElement element)
    {
        if (element.Name != "brush")
            throw new FormatException("Expected a brush definition.");
        var definition = new SvgBrushDefinition
        {
            Id = (string?)element.Attribute("id") ?? string.Empty,
            Name = (string?)element.Attribute("name") ?? string.Empty,
            Kind = Enum.TryParse<SvgBrushKind>((string?)element.Attribute("kind"), out var kind) ? kind : (SvgBrushKind)(-1),
            Width = (double?)element.Attribute("width") ?? double.NaN,
            Spacing = (double?)element.Attribute("spacing") ?? double.NaN,
            Rotation = (double?)element.Attribute("rotation") ?? double.NaN,
            Scatter = (double?)element.Attribute("scatter") ?? double.NaN,
            Seed = (int?)element.Attribute("seed") ?? 1,
            FollowTangent = (bool?)element.Attribute("follow") ?? true,
            Cap = Enum.TryParse<SKStrokeCap>((string?)element.Attribute("cap"), out var cap) ? cap : (SKStrokeCap)(-1),
            StampPath = (string?)element.Element("stamp") ?? string.Empty,
            Profile = ParseProfile((string?)element.Element("profile") ?? string.Empty)
        };
        var color = (string?)element.Attribute("color") ?? string.Empty;
        if (color.Length != 9 || color[0] != '#' || !uint.TryParse(color.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgba))
            throw new FormatException("Brush colors must use #RRGGBBAA.");
        definition.Color = new SKColor((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
        Validate(definition);
        return definition;
    }

    public static StrokeProfile ParseProfile(string text)
    {
        var profile = new StrokeProfile();
        profile.Points.Clear();
        foreach (var pair in text.Split(';'))
        {
            var values = pair.Split(',');
            if (values.Length != 2 || !double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var offset)
                || !double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
                throw new FormatException("Write profile points as position,width;position,width, with positions between 0 and 1.");
            profile.Points.Add(new StrokePointInfo { Offset = offset, Width = width });
        }
        return profile;
    }

    private static void Validate(SvgBrushDefinition definition)
    {
        if (definition is null)
            throw new ArgumentNullException(nameof(definition));
        XmlConvert.VerifyNCName(definition.Id);
        if (definition.Name.Length is < 1 or > 128 || definition.Id.Length > 128)
            throw new ArgumentException("Brush names and IDs need 1–128 characters.", nameof(definition));
        if (!Enum.IsDefined(typeof(SvgBrushKind), definition.Kind) || !Enum.IsDefined(typeof(SKStrokeCap), definition.Cap))
            throw new ArgumentException("Unsupported brush type or end cap.", nameof(definition));
        if (!Finite(definition.Width) || definition.Width is < 0.0001 or > 1000000
            || !Finite(definition.Spacing) || definition.Spacing is < 0.02 or > 100
            || !Finite(definition.Rotation) || Math.Abs(definition.Rotation) > 360000
            || !Finite(definition.Scatter) || definition.Scatter is < 0 or > 10)
            throw new ArgumentException("Brush dimensions, spacing, rotation and scatter must be finite and within their supported ranges.", nameof(definition));
        if (definition.Profile is null || definition.Profile.Points.Count is < 2 or > 256
            || definition.Profile.Points.Any(point => point is null || !Finite(point.Offset) || point.Offset is < 0 or > 1 || !Finite(point.Width) || point.Width is < 0 or > 100)
            || definition.Profile.Points.Select(point => point.Offset).Distinct().Count() != definition.Profile.Points.Count
            || !definition.Profile.Points.Any(point => point.Width > 0))
            throw new ArgumentException("The profile needs 2–256 distinct positions and nonnegative width multipliers.", nameof(definition));
        if (definition.StampPath.Length > 65536)
            throw new ArgumentException("The stamp path exceeds 65,536 characters.", nameof(definition));
        if (definition.Kind != SvgBrushKind.WidthProfile)
        {
            using var stamp = SKPath.ParseSvgPathData(definition.StampPath);
            if (stamp is null || stamp.IsEmpty || stamp.PointCount > 4096 || !Finite(stamp.Bounds.Width) || !Finite(stamp.Bounds.Height)
                || Math.Max(stamp.Bounds.Width, stamp.Bounds.Height) <= 0 || Math.Abs(stamp.Bounds.Left) > 10000000
                || Math.Abs(stamp.Bounds.Right) > 10000000 || Math.Abs(stamp.Bounds.Top) > 10000000 || Math.Abs(stamp.Bounds.Bottom) > 10000000)
                throw new ArgumentException("The stamp must be a finite SVG path with at most 4,096 points.", nameof(definition));
        }
    }

    private static XElement ReadXml(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml ?? throw new ArgumentNullException(nameof(xml))),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1000000 });
        return XElement.Load(reader);
    }
    private static string Fingerprint(string text)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty);
    }
    private static void CopyGeneratedState(SvgPath source, SvgPath target)
    {
        target.PathData = source.PathData;
        target.Fill = source.Fill;
        target.FillOpacity = source.FillOpacity;
        target.Stroke = source.Stroke;
        target.FillRule = source.FillRule;
        target.MarkerStart = source.MarkerStart;
        target.MarkerMid = source.MarkerMid;
        target.MarkerEnd = source.MarkerEnd;
        foreach (var key in new[] { SvgVariableStrokeService.CenterlineAttribute, SvgVariableStrokeService.OutlineAttribute, BrushIdAttribute, SettingsAttribute, BaseOpacityAttribute })
            target.CustomAttributes[key] = source.CustomAttributes[key];
        foreach (var key in new[] { SvgVariableStrokeService.ProfileAttribute, SvgVariableStrokeService.CapAttribute })
            if (source.CustomAttributes.TryGetValue(key, out var value))
                target.CustomAttributes[key] = value;
            else
                target.CustomAttributes.Remove(key);
        target.CustomAttributes.Remove("stroke-profile");
        target.OnPathUpdated();
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
