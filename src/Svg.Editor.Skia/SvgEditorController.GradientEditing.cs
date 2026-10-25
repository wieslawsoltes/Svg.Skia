using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace Svg.Editor.Skia;

public enum SvgGradientKind { Linear, Radial }
public enum SvgGradientSpread { Pad, Reflect, Repeat }

/// <summary>
/// Nonmodal paint draft. Immutable ramp snapshots make local undo cheap. Existing
/// coordinate systems, transforms and href inheritance remain intact unless the user
/// explicitly switches to object-bounds geometry. Apply is guarded by document revision
/// and ordered selection identity; previewing never touches live editor state.
/// </summary>
public sealed class SvgGradientEdit
{
    internal SvgGradientEdit(SvgEditorController owner, string source, string[] selection,
        XElement? original, SvgGradientRamp ramp, IReadOnlyDictionary<Guid, XElement> prototypes, bool stroke, bool mixed)
    {
        Owner = owner; Context = new SvgEditorSelectionContext(owner); Revision = owner.DocumentRevision;
        Source = source; Selection = selection; Original = original; Ramp = ramp; Prototypes = prototypes;
        IsStroke = stroke; IsMixed = mixed; UsesObjectBounds = original is null;
        Kind = original?.Name.LocalName == "radialGradient" ? SvgGradientKind.Radial : SvgGradientKind.Linear;
        OriginalGradientId = (string?)original?.Attribute("id");
    }
    internal SvgEditorController Owner { get; }
    internal SvgEditorSelectionContext Context { get; }
    internal long Revision { get; }
    internal string Source { get; }
    internal string[] Selection { get; }
    internal XElement? Original { get; }
    internal IReadOnlyDictionary<Guid, XElement> Prototypes { get; }
    internal bool Applied { get; set; }
    public SvgGradientRamp Ramp { get; set; }
    public SvgGradientKind Kind { get; private set; }
    public SvgGradientSpread Spread { get; set; }
    public bool UsesObjectBounds { get; private set; }
    public double Angle { get; private set; }
    public bool IsStroke { get; }
    public bool IsMixed { get; }
    public string? OriginalGradientId { get; }

    public bool IsCurrent(SvgEditorController? controller)
        => !Applied && ReferenceEquals(controller, Owner) && controller is not null && !controller.IsEditing
            && Context.IsCurrent(controller) && controller.DocumentRevision == Revision;

    /// <summary>Explicitly replaces imported gradient geometry with normalized object-bounds geometry.</summary>
    public void UseObjectBounds(SvgGradientKind kind, double angle = 0)
    {
        if (!Enum.IsDefined(typeof(SvgGradientKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (double.IsNaN(angle) || double.IsInfinity(angle)) throw new ArgumentOutOfRangeException(nameof(angle));
        Kind = kind; Angle = angle % 360; UsesObjectBounds = true;
    }
    public void PreserveOriginalGeometry()
    {
        if (Original is null) throw new InvalidOperationException("This new gradient has no imported geometry.");
        Kind = Original.Name.LocalName == "radialGradient" ? SvgGradientKind.Radial : SvgGradientKind.Linear;
        UsesObjectBounds = false;
    }
}

public sealed partial class SvgEditorController
{
    private static readonly XNamespace GradientNamespace = "http://www.w3.org/2000/svg";
    private static readonly XNamespace GradientXLink = "http://www.w3.org/1999/xlink";

    public SvgGradientEdit BeginGradientEdit(bool stroke = false, bool createNew = false)
    {
        ThrowIfDisposed();
        if (Document is null || _selection.Count == 0) throw new InvalidOperationException("Select artwork to edit a gradient.");
        if (IsEditing) throw new InvalidOperationException("Finish the current gesture before editing a gradient.");
        EnsureSelectionEditable();
        var source = GetSvg();
        var tree = ReadEffectDocument(source);
        var paints = _selection.Select(element => GradientPaintKey(stroke ? element.Stroke : element.Fill)).ToArray();
        var mixed = paints.Skip(1).Any(value => value != paints[0]);
        var id = !createNew && !mixed ? LocalEffectId(paints[0]) : null;
        var original = id is null ? null : tree.Root!.DescendantsAndSelf().FirstOrDefault(element => IsGradient(element)
            && (string?)element.Attribute("id") == id);
        var prototypes = new Dictionary<Guid, XElement>();
        var ramp = SvgGradientRamp.Default;
        var spread = SvgGradientSpread.Pad;
        if (original is not null)
        {
            var chain = GradientChain(tree, original).ToArray();
            if (chain.Any(element => element.Descendants().Any(child => child.Name.Namespace == GradientNamespace
                && child.Name.LocalName is "animate" or "animateTransform" or "set")))
                throw new InvalidOperationException("This gradient is animated. Use the timeline/source editor, or explicitly create a new static gradient.");
            var spreadText = chain.Select(element => (string?)element.Attribute("spreadMethod")).FirstOrDefault(value => value is not null);
            spread = spreadText == "repeat" ? SvgGradientSpread.Repeat : spreadText == "reflect" ? SvgGradientSpread.Reflect : SvgGradientSpread.Pad;
            var stopOwner = chain.FirstOrDefault(element => element.Elements(GradientNamespace + "stop").Any());
            if (stopOwner is null)
                ramp = new SvgGradientRamp(new[] { (0d, SKColors.Transparent, 1d), (1d, SKColors.Transparent, 1d) });
            else
            {
                var xmlStops = stopOwner.Elements(GradientNamespace + "stop").ToArray();
                if (xmlStops.Length > SvgGradientRamp.MaximumStops) throw new InvalidOperationException("This gradient exceeds the inline editor's 256-stop limit.");
                var typedOwner = Document.GetElementById<SvgGradientServer>((string)stopOwner.Attribute("id")!);
                var stops = typedOwner?.Children.OfType<SvgGradientStop>().ToArray()
                    ?? throw new InvalidOperationException("The gradient's stops could not be resolved.");
                if (stops.Length != xmlStops.Length) throw new InvalidOperationException("The SVG source and resolved gradient stops disagree.");
                var samples = new List<(double Offset, SKColor Color, double Opacity)>();
                var previous = 0d;
                foreach (var stop in stops)
                {
                    var offset = stop.Offset.Type == SvgUnitType.Percentage ? stop.Offset.Value / 100d : stop.Offset.Value;
                    if (!IsFinite((float)offset) || !IsFinite(stop.StopOpacity)) throw new InvalidOperationException("Gradient stops require finite offsets and opacity.");
                    // SVG's monotone offset rule: a later decreasing offset has the
                    // preceding offset, rather than being sorted into a new color order.
                    previous = Math.Max(previous, Math.Max(0, Math.Min(1, offset)));
                    var color = stop.GetColor(stop);
                    samples.Add((previous, new SKColor(color.R, color.G, color.B, color.A), Math.Max(0, Math.Min(1, stop.StopOpacity))));
                }
                if (samples.Count == 1) samples.Add((1, samples[0].Color, samples[0].Opacity));
                ramp = new SvgGradientRamp(samples);
                for (var index = 0; index < xmlStops.Length; index++) prototypes[ramp.Stops[index].Id] = new XElement(xmlStops[index]);
            }
        }
        return new SvgGradientEdit(this, source, CaptureSelection(), original is null ? null : new XElement(original), ramp, prototypes, stroke, mixed)
        { Spread = spread };
    }

    /// <summary>Creates preview source. The live SVG, renderer and history remain untouched.</summary>
    public string BuildGradientPreviewSvg(SvgGradientEdit edit, bool editSharedResource = false)
    {
        ThrowIfDisposed();
        if (edit is null) throw new ArgumentNullException(nameof(edit));
        if (!ReferenceEquals(edit.Owner, this)) throw new ArgumentException("This gradient draft belongs to another editor.", nameof(edit));
        if (edit.Ramp is null) throw new ArgumentException("The gradient draft has no ramp.", nameof(edit));
        if (!Enum.IsDefined(typeof(SvgGradientSpread), edit.Spread)) throw new ArgumentOutOfRangeException(nameof(edit.Spread));
        var tree = ReadEffectDocument(edit.Source);
        var selected = edit.Selection.Select(key => ResolveEffectElement(tree, key)
            ?? throw new InvalidOperationException("The original gradient target could not be resolved.")).ToArray();
        var existing = edit.OriginalGradientId is null ? null : tree.Root!.DescendantsAndSelf().FirstOrDefault(element => IsGradient(element)
            && (string?)element.Attribute("id") == edit.OriginalGradientId);
        if (editSharedResource && existing is null) throw new InvalidOperationException("A new gradient has no shared definition to edit.");
        var id = editSharedResource ? edit.OriginalGradientId! : UniqueEffectId(tree, "gradient");
        var resource = edit.Original is null ? new XElement(GradientNamespace + "linearGradient") : new XElement(edit.Original);
        resource.Elements(GradientNamespace + "stop").Remove();
        resource.Name = GradientNamespace + (edit.Kind == SvgGradientKind.Linear ? "linearGradient" : "radialGradient");
        if (edit.UsesObjectBounds)
        {
            foreach (var name in new[] { "x1", "y1", "x2", "y2", "cx", "cy", "r", "fx", "fy", "fr", "gradientTransform", "href" }) resource.Attribute(name)?.Remove();
            resource.Attribute(GradientXLink + "href")?.Remove();
            resource.SetAttributeValue("gradientUnits", "objectBoundingBox");
            if (edit.Kind == SvgGradientKind.Linear)
            {
                var radians = edit.Angle * Math.PI / 180;
                var dx = Math.Cos(radians) / 2; var dy = Math.Sin(radians) / 2;
                resource.SetAttributeValue("x1", 0.5 - dx); resource.SetAttributeValue("y1", 0.5 - dy);
                resource.SetAttributeValue("x2", 0.5 + dx); resource.SetAttributeValue("y2", 0.5 + dy);
            }
            else
            {
                resource.SetAttributeValue("cx", "50%"); resource.SetAttributeValue("cy", "50%");
                resource.SetAttributeValue("r", "50%"); resource.SetAttributeValue("fx", "50%"); resource.SetAttributeValue("fy", "50%");
            }
        }
        resource.SetAttributeValue("spreadMethod", edit.Spread.ToString().ToLowerInvariant());
        foreach (var stop in edit.Ramp.Stops)
        {
            var element = edit.Prototypes.TryGetValue(stop.Id, out var prototype) ? new XElement(prototype) : new XElement(GradientNamespace + "stop");
            element.SetAttributeValue("offset", stop.Offset.ToString("R", CultureInfo.InvariantCulture));
            SetEffectReference(element, "stop-color", SvgSolidPaintDraft.FormatColor(stop.Color));
            SetEffectReference(element, "stop-opacity", stop.Opacity.ToString("R", CultureInfo.InvariantCulture));
            resource.Add(element);
        }
        // Copy-on-write is the default. Reserve IDs against the complete document and
        // remap nested metadata references without mutating shared source definitions.
        if (!editSharedResource)
            resource = SvgXmlIdRemapper.Clone(resource, tree.Root!.DescendantsAndSelf().Attributes("id").Select(attribute => attribute.Value).Concat(new[] { id }));
        var previousId = (string?)resource.Attribute("id");
        resource.SetAttributeValue("id", id);
        if (previousId is not null && previousId != id)
        {
            var fragment = new XDocument(resource);
            RewriteTreeReferences(fragment, new Dictionary<string, string>(StringComparer.Ordinal) { [previousId] = id });
            resource.Remove();
        }
        if (editSharedResource)
        {
            var replaced = new HashSet<XElement>(existing!.DescendantsAndSelf());
            var reserved = new HashSet<string>(tree.Root!.DescendantsAndSelf().Where(element => !replaced.Contains(element)).Attributes("id").Select(attribute => attribute.Value), StringComparer.Ordinal);
            foreach (var attribute in resource.DescendantsAndSelf().Attributes("id"))
                if (!reserved.Add(attribute.Value)) throw new InvalidOperationException("The edited gradient contains an ID used by another object.");
            existing.ReplaceWith(resource);
        }
        else if (existing is not null) existing.Parent!.Add(resource);
        else
        {
            var definitions = tree.Root!.Elements(GradientNamespace + "defs").FirstOrDefault();
            if (definitions is null) { definitions = new XElement(GradientNamespace + "defs"); tree.Root.Add(definitions); }
            definitions.Add(resource);
        }
        foreach (var element in selected) SetEffectReference(element, edit.IsStroke ? "stroke" : "fill", "url(#" + id + ")");
        return tree.ToString(SaveOptions.DisableFormatting);
    }

    public SKSvg CreateGradientPreviewRenderer(SvgGradientEdit edit, bool editSharedResource = false)
    {
        var source = BuildGradientPreviewSvg(edit, editSharedResource);
        var renderer = CreateRenderer(ParseDocument(source, Document?.BaseUri));
        try { renderer.SetAnimationTime(Renderer.AnimationTime); return renderer; }
        catch { renderer.Dispose(); throw; }
    }

    public void ApplyGradientEdit(SvgGradientEdit edit, bool editSharedResource = false)
    {
        ThrowIfDisposed();
        if (edit is null) throw new ArgumentNullException(nameof(edit));
        if (!edit.IsCurrent(this)) throw new InvalidOperationException("The gradient target or document changed. Reset the draft before applying it.");
        EnsureSelectionEditable();
        if (editSharedResource && edit.OriginalGradientId is { } id)
        {
            var definition = Document!.GetElementById(id) ?? throw new InvalidOperationException("The gradient definition no longer exists.");
            EnsureElementEditable(definition);
            var source = ReadEffectDocument(edit.Source);
            foreach (var consumer in Document.Descendants().OfType<SvgVisualElement>())
            {
                bool References(SvgPaintServer? paint)
                {
                    var reference = LocalEffectId(GradientPaintKey(paint));
                    if (reference == id) return true;
                    var gradient = source.Root!.DescendantsAndSelf().FirstOrDefault(element => IsGradient(element) && (string?)element.Attribute("id") == reference);
                    return gradient is not null && GradientChain(source, gradient).Any(element => (string?)element.Attribute("id") == id);
                }
                if (References(consumer.Fill) || References(consumer.Stroke)) EnsureElementEditable(consumer);
            }
        }
        var xml = BuildGradientPreviewSvg(edit, editSharedResource);
        var time = Renderer.AnimationTime;
        ReplaceSvg(xml, edit.IsStroke ? "Edit stroke gradient" : "Edit fill gradient");
        Renderer.SetAnimationTime(time);
        edit.Applied = true;
    }

    private static string GradientPaintKey(SvgPaintServer? paint)
        => paint is SvgGradientServer gradient && !string.IsNullOrEmpty(gradient.ID) ? "url(#" + gradient.ID + ")"
            : paint is null ? "none" : TypeDescriptor.GetConverter(typeof(SvgPaintServer)).ConvertToInvariantString(paint) ?? "none";
    private static bool IsGradient(XElement element)
        => element.Name.Namespace == GradientNamespace && element.Name.LocalName is "linearGradient" or "radialGradient";
    private static IEnumerable<XElement> GradientChain(XDocument document, XElement first)
    {
        var visited = new HashSet<XElement>();
        XElement? current = first;
        while (current is not null)
        {
            if (visited.Count >= 32 || !visited.Add(current)) throw new InvalidOperationException("Gradient inheritance is cyclic or exceeds 32 definitions.");
            yield return current;
            var href = (string?)current.Attribute("href") ?? (string?)current.Attribute(GradientXLink + "href");
            if (string.IsNullOrWhiteSpace(href)) yield break;
            var id = LocalEffectId(href);
            if (id is null) throw new InvalidOperationException("External gradient templates require source editing or a new local gradient.");
            current = document.Root!.DescendantsAndSelf().FirstOrDefault(element => IsGradient(element) && (string?)element.Attribute("id") == id)
                ?? throw new InvalidOperationException("A referenced gradient template is missing.");
        }
    }
}
