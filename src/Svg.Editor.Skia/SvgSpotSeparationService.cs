using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Model.Services;

namespace Svg.Editor.Skia;

/// <summary>An immutable named ink snapshot; resource IDs may share the same physical ink.</summary>
public sealed class SvgSpotInk
{
    internal SvgSpotInk(string name, IEnumerable<string> ids, SKColor alternateColor)
    { Name = name; ResourceIds = Array.AsReadOnly(ids.ToArray()); AlternateColor = alternateColor; }
    public string Name { get; }
    public IReadOnlyList<string> ResourceIds { get; }
    public SKColor AlternateColor { get; }
}

public sealed class SvgSpotSeparationAnalysis
{
    internal SvgSpotSeparationAnalysis(IEnumerable<SvgSpotInk> inks, IEnumerable<SvgPreflightIssue> issues)
    { Inks = Array.AsReadOnly(inks.ToArray()); Issues = Array.AsReadOnly(issues.ToArray()); }
    public IReadOnlyList<SvgSpotInk> Inks { get; }
    public IReadOnlyList<SvgPreflightIssue> Issues { get; }
    public bool CanSeparate => !Issues.Any(issue => issue.Severity == SvgPreflightSeverity.Error);
}

/// <summary>
/// Native SVG spot plates for ordinary source-over artwork. Black means full ink and white
/// means no ink. Non-spot paints knock out an underlying plate using their original alpha.
/// Unsupported color-changing compositing is rejected before producing a misleading plate.
/// Plates must be rendered against white to obtain coverage. Alpha and group handling follow
/// https://www.w3.org/TR/compositing-1/#simplealphacompositing.
/// </summary>
public static class SvgSpotSeparationService
{
    public const string SpotNameAttribute = "data-unodraw-spot-name";

    public static SvgSpotSeparationAnalysis Analyze(SvgDocument document)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        var xml = document.GetXML();
        return AnalyzeCore(Snapshot(document), xml);
    }

    public static SvgDocument CreateProcessDocument(SvgDocument document)
        => CreateDocument(document, null);

    public static SvgDocument CreatePlateDocument(SvgDocument document, string inkName)
    {
        if (string.IsNullOrWhiteSpace(inkName))
            throw new ArgumentException("Choose a named ink.", nameof(inkName));
        return CreateDocument(document, inkName);
    }

    private static SvgSpotSeparationAnalysis AnalyzeCore(SvgDocument document, string sourceXml)
    {
        var issues = new List<SvgPreflightIssue>();
        var inks = new Dictionary<string, (SKColor Color, List<string> Ids)>(StringComparer.Ordinal);
        var elements = new[] { (SvgElement)document }.Concat(document.Descendants()).ToArray();
        void Report(string code, string text, SvgElement? element = null)
            => issues.Add(new SvgPreflightIssue(code, SvgPreflightSeverity.Error, text, element?.ID));
        foreach (var resource in elements.Where(element => element.CustomAttributes.ContainsKey(SpotNameAttribute)))
        {
            var name = resource.CustomAttributes[SpotNameAttribute].Trim();
            if (name.Length is < 1 or > 128 || name.Any(char.IsControl) || string.IsNullOrEmpty(resource.ID))
            { Report("spot-invalid-name", "A spot swatch needs a resource ID and a 1–128 character ink name without control characters.", resource); continue; }
            if (resource is not SvgGradientServer gradient)
            { Report("spot-invalid-resource", "Spot ink names must be attached to solid-color gradient swatches.", resource); continue; }
            try
            {
                var stops = GetStops(gradient);
                if (stops.Count == 0)
                    throw new InvalidOperationException("The spot swatch has no stops.");
                var colors = stops.Select(stop => stop.GetColor(gradient)).ToArray();
                if (colors.Any(color => color.R != colors[0].R || color.G != colors[0].G || color.B != colors[0].B))
                    throw new InvalidOperationException("All stops in a spot swatch must use the same alternate RGB color. Stop opacity may vary to produce tints.");
                var alternate = new SKColor(colors[0].R, colors[0].G, colors[0].B);
                if (inks.TryGetValue(name, out var ink))
                {
                    if (ink.Color != alternate)
                        Report("spot-conflicting-alternate", "Swatches with the same ink name must use the same alternate display color.", resource);
                    ink.Ids.Add(resource.ID);
                }
                else
                    inks.Add(name, (alternate, new List<string> { resource.ID }));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            { Report("spot-invalid-swatch", exception.Message, resource); }
        }
        if (inks.Count > 64)
            Report("spot-ink-limit", "A separation job supports at most 64 named inks.");

        // Without named inks, the process document is an unchanged SVG clone. Ordinary
        // process-only export remains available for documents that need no spot analysis.
        if (inks.Count == 0 && issues.Count == 0)
            return new SvgSpotSeparationAnalysis(Array.Empty<SvgSpotInk>(), issues);
        // Inspect the original serialization as well as resolved paints. Some authored
        // CSS properties are represented as custom attributes and normalized away when
        // reparsing, so the clone alone cannot reliably report their source semantics.
        var tree = XDocument.Parse(sourceXml);
        foreach (var xml in tree.Root!.DescendantsAndSelf())
        {
            if (xml.Name.LocalName is "animate" or "animateTransform" or "animateMotion" or "set" or "script" or "foreignObject")
                Report("spot-dynamic-content", "Bake animation or dynamic content to static SVG before separating named inks.");
            var style = (string?)xml.Attribute("style") ?? (xml.Name.LocalName == "style" ? xml.Value : string.Empty);
            if (Regex.IsMatch(style, @"(?:^|[;{])\s*mix-blend-mode\s*:\s*(?!normal\b)[^;}]+", RegexOptions.IgnoreCase)
                || xml.Attribute("mix-blend-mode") is { } blend && blend.Value != "normal")
                Report("spot-blend-mode", "Named ink separation requires normal source-over blending. Remove or flatten this blend mode.");
        }
        foreach (var element in elements.Where(IsArtwork))
        {
            if (element is SvgImage)
                Report("spot-image", "Raster and embedded images need a process/spot-aware image separation before named ink export.", element);
            if (element is SvgUse)
                Report("spot-instance", "Expand symbol/use instances before named ink separation.", element);
            if (element is SvgVisualElement visual && visual.Filter is { } filter && filter.ToString() != "none")
                Report("spot-filter", "Color-changing filters are not supported by named ink separation. Bake or remove the filter first.", element);
            if (element is SvgMarkerElement marker && new[] { marker.MarkerStart, marker.MarkerMid, marker.MarkerEnd }.Any(uri => uri is not null && uri.ToString() != "none"))
                Report("spot-marker", "Expand path markers to artwork before named ink separation.", element);
            foreach (var paint in new[] { ResolvePaint(element, element.Fill, true), ResolvePaint(element, element.Stroke, false) })
            {
                if (paint is null || paint == SvgPaintServer.None)
                    continue;
                if (paint is SvgColourServer)
                    continue;
                if (paint is not SvgLinearGradientServer && paint is not SvgRadialGradientServer)
                { Report("spot-paint-server", "Only solid colors and linear/radial gradients can be separated into named inks. Expand pattern or custom paint artwork first.", element); continue; }
                try
                {
                    var gradient = (SvgGradientServer)paint;
                    var name = GetSpotName(gradient);
                    var stops = GetStops(gradient);
                    if (stops.Count == 0)
                        throw new InvalidOperationException("A gradient has no usable stops.");
                    if (name is not null && inks.TryGetValue(name, out var ink)
                        && stops.Select(stop => stop.GetColor(gradient)).Any(color => color.R != ink.Color.Red || color.G != ink.Color.Green || color.B != ink.Color.Blue))
                        Report("spot-derived-color", "A derived spot gradient changes the ink's alternate RGB color. Use opacity for tints or give it a different ink name.", element);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                { Report("spot-gradient", exception.Message, element); }
            }
        }
        return new SvgSpotSeparationAnalysis(inks.Select(pair => new SvgSpotInk(pair.Key, pair.Value.Ids, pair.Value.Color)), issues);
    }

    private static SvgDocument CreateDocument(SvgDocument source, string? inkName)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        var sourceXml = source.GetXML();
        var document = Snapshot(source);
        var analysis = AnalyzeCore(document, sourceXml);
        if (!analysis.CanSeparate)
            throw new InvalidOperationException("Named ink separation cannot continue: " + string.Join(" ", analysis.Issues.Where(issue => issue.Severity == SvgPreflightSeverity.Error).Select(issue => issue.Message).Distinct()));
        if (inkName is not null && !analysis.Inks.Any(ink => ink.Name == inkName))
            throw new ArgumentException("That ink name does not exist in this document.", nameof(inkName));
        if (analysis.Inks.Count == 0)
            return document;

        // Resolve every inherited paint before changing parents. Mask and clip resources
        // remain untouched, including gradients shared between a mask and the artwork.
        var targets = new[] { (SvgElement)document }.Concat(document.Descendants()).Where(IsArtwork)
            .Select(element => (Element: element, Fill: ResolvePaint(element, element.Fill, true), Stroke: ResolvePaint(element, element.Stroke, false))).ToArray();
        var definitions = document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (definitions is null)
        { definitions = new SvgDefinitionList(); document.Children.Insert(0, definitions); }
        var gradients = new Dictionary<SvgGradientServer, SvgPaintServer>();
        var markerName = "data-unodraw-separation-" + Guid.NewGuid().ToString("N");
        var styles = new Dictionary<string, string>();
        var nextId = 0;
        SvgPaintServer ConvertPaint(SvgPaintServer? paint)
        {
            if (paint is null || paint == SvgPaintServer.None)
                return SvgPaintServer.None;
            var named = paint is SvgGradientServer gradient ? GetSpotName(gradient) : null;
            if (inkName is null && named is null)
                return paint;
            var shade = inkName is not null && named == inkName ? (byte)0 : (byte)255;
            if (paint is SvgColourServer solid)
                return new SvgColourServer(Color.FromArgb(solid.Colour.A, shade, shade, shade));
            if (paint is not SvgGradientServer original)
                throw new InvalidOperationException("The paint server has not passed separation preflight.");
            if (gradients.TryGetValue(original, out var existing))
                return existing;
            var copy = (SvgGradientServer)original.DeepCopy();
            do
            { copy.ID = "unodraw-plate-gradient-" + ++nextId; } while (document.GetElementById(copy.ID) is not null || document.ID == copy.ID);
            copy.CustomAttributes.Remove(SpotNameAttribute);
            copy.Children.Clear();
            copy.Nodes.Clear();
            foreach (var stop in GetStops(original))
            {
                var color = stop.GetColor(original);
                var converted = new SvgGradientStop
                {
                    Offset = stop.Offset,
                    StopOpacity = stop.StopOpacity,
                    StopColor = new SvgColourServer(Color.FromArgb(color.A, shade, shade, shade))
                };
                // Author stylesheets still apply when the derivative is reopened. Give
                // generated stops explicit inline priority so global stop rules cannot
                // recolor a monochrome plate or change its resolved source tint.
                var marker = "stop-" + styles.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                converted.CustomAttributes[markerName] = marker;
                styles.Add(marker, ";stop-color:" + converted.StopColor
                    + "!important;stop-opacity:" + converted.StopOpacity.ToString(System.Globalization.CultureInfo.InvariantCulture) + "!important;");
                copy.Children.Add(converted);
            }
            definitions.Children.Add(copy);
            var reference = new SvgDeferredPaintServer("url(#" + copy.ID + ")");
            gradients.Add(original, reference);
            return reference;
        }
        for (var index = 0; index < targets.Length; index++)
        {
            var target = targets[index];
            var fill = ConvertPaint(target.Fill);
            var stroke = ConvertPaint(target.Stroke);
            var marker = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            target.Element.CustomAttributes[markerName] = marker;
            // Explicit important paint declarations also override author stylesheets while
            // preserving all unrelated typography, opacity, clipping and mask declarations.
            styles.Add(marker, ";fill:" + fill + "!important;stroke:" + stroke + "!important;");
        }
        var tree = XDocument.Parse(document.GetXML(), LoadOptions.PreserveWhitespace);
        foreach (var element in tree.Root!.DescendantsAndSelf())
            if (element.Attribute(markerName) is { } marker)
            {
                var style = ((string?)element.Attribute("style") ?? string.Empty) + styles[marker.Value];
                element.SetAttributeValue("style", style);
                styles[marker.Value] = style;
            }
        var result = Parse(tree.ToString(SaveOptions.DisableFormatting), source.BaseUri);
        foreach (var element in new[] { (SvgElement)result }.Concat(result.Descendants()))
            if (element.CustomAttributes.TryGetValue(markerName, out var marker))
            {
                // The parser resolves known declarations into presentation attributes.
                // Retain their authored inline priority too, so saving the derivative
                // cannot let the preserved document stylesheet recolor a plate.
                element.CustomAttributes["style"] = styles[marker];
                element.CustomAttributes.Remove(markerName);
            }
        return result;
    }

    private static bool IsArtwork(SvgElement element)
    {
        if (element is not SvgVisualElement && element is not SvgDocument)
            return false;
        for (SvgElement? current = element; current is not null; current = current.Parent)
            if (current is SvgDefinitionList or SvgPaintServer or SvgClipPath or SvgMask or SvgSymbol or SvgMarker)
                return false;
        return true;
    }

    private static SvgPaintServer? ResolvePaint(SvgElement element, SvgPaintServer? paint, bool fill)
    {
        if (paint is null || paint == SvgPaintServer.NotSet || paint == SvgPaintServer.Inherit)
            return fill ? new SvgColourServer(Color.Black) : SvgPaintServer.None;
        if (paint == SvgPaintServer.None)
            return paint;
        var resolved = SvgDeferredPaintServer.TryGet<SvgPaintServer>(paint, element);
        return resolved ?? (fill ? new SvgColourServer(Color.Black) : SvgPaintServer.None);
    }

    private static string? GetSpotName(SvgGradientServer gradient)
    {
        var visited = new HashSet<SvgGradientServer>();
        SvgGradientServer? current = gradient;
        while (current is not null)
        {
            if (!visited.Add(current) || visited.Count > 32)
                throw new InvalidOperationException("Gradient inheritance contains a cycle or exceeds 32 references.");
            if (current.CustomAttributes.TryGetValue(SpotNameAttribute, out var name))
                return name.Trim();
            current = SvgDeferredPaintServer.TryGet<SvgGradientServer>(current.InheritGradient, current);
        }
        return null;
    }

    private static IReadOnlyList<SvgGradientStop> GetStops(SvgGradientServer gradient)
    {
        var visited = new HashSet<SvgGradientServer>();
        SvgGradientServer? current = gradient;
        while (current is not null)
        {
            if (!visited.Add(current) || visited.Count > 32)
                throw new InvalidOperationException("Gradient inheritance contains a cycle or exceeds 32 references.");
            if (current.Stops.Count > 0)
                return current.Stops;
            current = SvgDeferredPaintServer.TryGet<SvgGradientServer>(current.InheritGradient, current);
        }
        return Array.Empty<SvgGradientStop>();
    }

    private static SvgDocument Snapshot(SvgDocument source)
    {
        // Svg.Custom preserves solid color alpha independently of paint opacity.
        // Reparse a complete SVG snapshot so CSS and resource references are bound to
        // the separation document, without rewriting paint on the source tree.
        return Parse(source.GetXML(), source.BaseUri);
    }

    private static SvgDocument Parse(string xml, Uri? baseUri)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }, baseUri?.AbsoluteUri ?? string.Empty);
        var result = SvgService.Open(reader) ?? throw new FormatException("The SVG could not be read for separation.");
        result.BaseUri = baseUri;
        return result;
    }
}
