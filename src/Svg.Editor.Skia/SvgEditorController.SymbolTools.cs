using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Editor.Svg;
using Svg.FilterEffects;
using Svg.Transforms;

namespace Svg.Editor.Skia;

/// <summary>A read-only symbol brush preview with detached replacements and staged resources.</summary>
public sealed class SvgSymbolBrushPreview : IDisposable
{
    internal SvgSymbolBrushPreview(SvgEditorController owner, string sourceXml, SvgEditorSelectionContext selection, SvgSymbolBrushKind kind,
        SvgUse[] sources, SvgUse[] replacements, SKRect[] bounds, SvgFilter[] filters, Dictionary<SvgUse, SvgSymbol> styles)
    { Owner = owner; SourceXml = sourceXml; Selection = selection; Kind = kind; Sources = sources; Replacements = replacements; Bounds = bounds; Filters = filters; Styles = styles; }
    internal SvgEditorController Owner { get; }
    internal string SourceXml { get; }
    internal SvgEditorSelectionContext Selection { get; }
    internal SvgUse[] Sources { get; }
    internal SvgUse[] Replacements { get; }
    internal SvgFilter[] Filters { get; }
    internal Dictionary<SvgUse, SvgSymbol> Styles { get; }
    internal bool Applied { get; set; }
    internal bool IsDisposed { get; private set; }
    public SvgSymbolBrushKind Kind { get; }
    public bool HasChanges => Sources.Length > 0;
    public IReadOnlyList<SKRect> Bounds { get; }
    public void Dispose() => IsDisposed = true;
}

public sealed partial class SvgEditorController
{
    private const string SymbolTintBase = "data-unodraw-symbol-tint-base";
    private const string SymbolTintAmount = "data-unodraw-symbol-tint-amount";
    private const string SymbolStyleBase = "data-unodraw-symbol-style-base";

    /// <summary>Places independent native use references in one undo entry, preserving the symbol definition.</summary>
    public IReadOnlyList<SvgUse> SpraySymbols(string symbolId, IReadOnlyList<SKPoint> centers, SvgSymbolBrushOptions? options = null)
    {
        ThrowIfDisposed();
        if (Document is not { } document)
            throw new InvalidOperationException("Open a document before spraying symbols.");
        if (IsLocked(document))
            throw new InvalidOperationException("Unlock the document before spraying symbols.");
        var symbol = document.GetElementById(symbolId) as SvgSymbol ?? throw new InvalidOperationException("Choose a symbol from the Symbols panel before spraying.");
        var placements = SvgSymbolSprayService.CreatePlacements(centers, options);
        if (placements.Count == 0)
            return Array.Empty<SvgUse>();
        var root = GetSceneNode(document) ?? Renderer.RetainedSceneGraph?.Root;
        var rootMatrix = root is null ? SKMatrix.Identity : ToMatrix(root.TotalTransform);
        if (!rootMatrix.TryInvert(out var inverse))
            throw new InvalidOperationException("The document viewport has a singular transform.");
        var ratio = SymbolAspectRatio(symbol);
        var instances = new List<SvgUse>(placements.Count);
        foreach (var placement in placements)
        {
            var width = ratio >= 1 ? placement.Size : placement.Size * ratio;
            var height = ratio >= 1 ? placement.Size / ratio : placement.Size;
            var instance = new SvgUse
            {
                ID = CreateId("symbol-instance"),
                ReferencedElement = new Uri("#" + symbolId, UriKind.Relative),
                X = -width / 2,
                Y = -height / 2,
                Width = width,
                Height = height
            };
            var transform = Multiply(SKMatrix.CreateTranslation(placement.Position.X, placement.Position.Y), SKMatrix.CreateRotationDegrees(placement.Rotation));
            PrependTransform(instance, Multiply(inverse, transform));
            instances.Add(instance);
        }
        Execute("Spray symbols", () =>
        {
            _selection.Clear();
            foreach (var instance in instances)
            { document.Children.Add(instance); _selection.Add(instance); }
        });
        return instances.AsReadOnly();
    }

    /// <summary>A detached native preview document; resource definitions and explicit symbol paints are preserved.</summary>
    public SvgDocument CreateSymbolPreviewDocument(string symbolId, float size = 64)
    {
        ThrowIfDisposed();
        ValidatePositive(size, nameof(size));
        if (Document?.GetElementById(symbolId) is not SvgSymbol symbol)
            throw new InvalidOperationException("The selected symbol is no longer available.");
        var preview = (SvgDocument)Document.DeepCopy();
        var definitions = new SvgDefinitionList();
        foreach (var child in preview.Children.ToArray())
        { preview.Children.Remove(child); definitions.Children.Add(child); }
        preview.Children.Add(definitions);
        preview.Width = preview.Height = size;
        preview.ViewBox = new SvgViewBox(0, 0, size, size);
        preview.Transforms = new SvgTransformCollection();
        var ratio = SymbolAspectRatio(symbol);
        var width = ratio >= 1 ? size : size * ratio;
        var height = ratio >= 1 ? size / ratio : size;
        preview.Children.Add(new SvgUse
        {
            ReferencedElement = new Uri("#" + symbolId, UriKind.Relative),
            X = (size - width) / 2,
            Y = (size - height) / 2,
            Width = width,
            Height = height
        });
        return preview;
    }

    /// <summary>Prepares local influence on selected symbol instances, or all unlocked instances when nothing is selected.</summary>
    public SvgSymbolBrushPreview PrepareSymbolBrush(SvgSymbolBrushKind kind, IReadOnlyList<SKPoint> points,
        SvgSymbolBrushOptions? options = null, bool reverse = false)
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        if (!Enum.IsDefined(typeof(SvgSymbolBrushKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (points is null)
            throw new ArgumentNullException(nameof(points));
        if (points.Count > 4096)
            throw new ArgumentException("Use at most 4,096 brush samples per gesture.", nameof(points));
        foreach (var point in points)
        if (!IsFinite(point.X) || !IsFinite(point.Y))
            throw new ArgumentException("Symbol brush coordinates must be finite.", nameof(points));
        options ??= new SvgSymbolBrushOptions();
        options.Validate();
        var selection = new HashSet<SvgElement>(_selection);
        var targets = Document?.Descendants().OfType<SvgUse>().Where(IsSelectableArtwork).Where(use => !IsEditLocked(use))
            .Where(use => selection.Count == 0 || selection.Contains(use) || HierarchyAncestors(use).Any(selection.Contains)).ToArray() ?? Array.Empty<SvgUse>();
        var sources = new List<SvgUse>();
        var copies = new List<SvgUse>();
        var bounds = new List<SKRect>();
        var filters = new List<SvgFilter>();
        var styledSymbols = new Dictionary<string, SvgSymbol>(StringComparer.Ordinal);
        var styles = new Dictionary<SvgUse, SvgSymbol>();
        global::Svg.Editor.Svg.Models.StyleEntry? style = null;
        if (kind == SvgSymbolBrushKind.Style && !reverse)
        {
            var appearance = new AppearanceService();
            appearance.Load(Document);
            style = appearance.Styles.FirstOrDefault(item => item.Name == options.GraphicStyleName)
                ?? throw new InvalidOperationException("Choose a graphic style in Symbol Tool Options first.");
            if (!IsFinite(style.Opacity) || style.Opacity < 0 || style.Opacity > 1)
                throw new InvalidOperationException("The graphic style has invalid opacity.");
            if (style.Fill is not null)
                ParsePaint(style.Fill);
            if (style.Stroke is not null)
                ParsePaint(style.Stroke);
        }
        var tint = kind == SvgSymbolBrushKind.Stain && !reverse ? ParsePaint(CurrentFill) as SvgColourServer
            ?? throw new InvalidOperationException("Choose a solid fill color before staining symbols.") : null;
        if (options.Strength > 0)
        foreach (var source in targets)
        {
            if (GetSceneNode(source) is not { } scene)
                continue;
            var originalBounds = new SKRect(scene.TransformedBounds.Left, scene.TransformedBounds.Top, scene.TransformedBounds.Right, scene.TransformedBounds.Bottom);
            var center = new SKPoint(originalBounds.MidX, originalBounds.MidY);
            var matrix = SKMatrix.Identity;
            var opacity = source.Opacity;
            var amount = 0f;
            var changed = false;
            for (var index = 0; index < points.Count; index++)
            {
                var distance = SKPoint.Distance(center, points[index]);
                if (distance >= options.Radius)
                    continue;
                var falloff = 1 - distance / options.Radius;
                var weight = options.Strength * falloff * falloff;
                var direction = reverse ? -1 : 1;
                var delta = SKMatrix.Identity;
                switch (kind)
                {
                    case SvgSymbolBrushKind.Shift when index > 0:
                        delta = SKMatrix.CreateTranslation((points[index].X - points[index - 1].X) * weight * direction,
                            (points[index].Y - points[index - 1].Y) * weight * direction);
                        break;
                    case SvgSymbolBrushKind.Scrunch:
                        delta = SKMatrix.CreateTranslation((points[index].X - center.X) * weight * 0.3f * direction,
                            (points[index].Y - center.Y) * weight * 0.3f * direction);
                        break;
                    case SvgSymbolBrushKind.Size:
                        var scale = (float)Math.Exp(weight * 0.2f * direction);
                        delta = SKMatrix.CreateScale(scale, scale, center.X, center.Y);
                        break;
                    case SvgSymbolBrushKind.Spin:
                        delta = SKMatrix.CreateRotationDegrees(weight * 20 * direction, center.X, center.Y);
                        break;
                    case SvgSymbolBrushKind.Screen:
                        var nextOpacity = Math.Max(0, Math.Min(1, opacity - weight * 0.15f * direction));
                        changed |= nextOpacity != opacity;
                        opacity = nextOpacity;
                        break;
                    case SvgSymbolBrushKind.Stain:
                    case SvgSymbolBrushKind.Style:
                        amount = 1 - (1 - amount) * (1 - weight);
                        break;
                }
                if (!delta.IsIdentity)
                { matrix = Multiply(delta, matrix); center = delta.MapPoint(center); changed = true; }
            }
            if (!changed && amount <= 0)
                continue;
            var copy = (SvgUse)source.DeepCopy();
            if (!matrix.IsIdentity)
            {
                var parent = scene.Parent is null ? SKMatrix.Identity : ToMatrix(scene.Parent.TotalTransform);
                if (!parent.TryInvert(out var inverse))
                    throw new InvalidOperationException("A symbol inside a singular transform cannot be edited.");
                PrependTransform(copy, Multiply(Multiply(inverse, matrix), parent));
            }
            if (kind == SvgSymbolBrushKind.Screen)
                copy.Opacity = opacity;
            if (kind == SvgSymbolBrushKind.Stain && amount > 0)
            {
                if (reverse && !copy.CustomAttributes.ContainsKey(SymbolTintAmount))
                    continue;
                PrepareSymbolTint(copy, tint, amount, reverse, filters);
            }
            if (kind == SvgSymbolBrushKind.Style && amount > 0)
            {
                if (reverse)
                {
                    if (copy.CustomAttributes.TryGetValue(SymbolStyleBase, out var original))
                    { copy.ReferencedElement = new Uri(original, UriKind.Relative); copy.CustomAttributes.Remove(SymbolStyleBase); }
                    else
                        continue;
                }
                else
                {
                    var reference = copy.CustomAttributes.TryGetValue(SymbolStyleBase, out var original) ? original : copy.ReferencedElement.OriginalString;
                    var id = LocalEffectId(reference);
                    if (id is null || Document?.GetElementById(id) is not SvgSymbol symbol)
                        continue;
                    if (!styledSymbols.TryGetValue(id, out var styled))
                    {
                        styled = (SvgSymbol)symbol.DeepCopy();
                        styled.ID = CreateId("styled-symbol");
                        foreach (var visual in styled.Descendants().OfType<SvgVisualElement>())
                        {
                            if (style!.Fill is { } fill)
                                visual.Fill = ParsePaint(fill);
                            if (style.Stroke is { } stroke)
                                visual.Stroke = ParsePaint(stroke);
                        }
                        styled.Opacity = symbol.Opacity * style!.Opacity;
                        styledSymbols.Add(id, styled);
                    }
                    copy.CustomAttributes[SymbolStyleBase] = reference;
                    styles.Add(copy, styled);
                }
            }
            sources.Add(source);
            copies.Add(copy);
            bounds.Add(matrix.MapRect(originalBounds));
        }
        return new SvgSymbolBrushPreview(this, GetSvg(), new SvgEditorSelectionContext(this), kind, sources.ToArray(), copies.ToArray(), bounds.ToArray(), filters.ToArray(), styles);
    }

    public bool ApplySymbolBrush(SvgSymbolBrushPreview prepared)
    {
        ThrowIfDisposed();
        if (prepared is null)
            throw new ArgumentNullException(nameof(prepared));
        if (prepared.IsDisposed || prepared.Applied || !ReferenceEquals(prepared.Owner, this) || !prepared.Selection.IsCurrent(this)
            || !string.Equals(prepared.SourceXml, GetSvg(), StringComparison.Ordinal))
            throw new InvalidOperationException("The symbol preview is stale. Start the gesture again.");
        EnsureSelectionEditable();
        if (!prepared.HasChanges)
            return false;
        Execute("Brush symbols: " + prepared.Kind, () =>
        {
            if (prepared.Filters.Length > 0)
            {
                var definitions = Document!.Children.OfType<SvgDefinitionList>().FirstOrDefault();
                if (definitions is null)
                { definitions = new SvgDefinitionList(); Document.Children.Add(definitions); }
                foreach (var filter in prepared.Filters)
                    definitions.Children.Add(filter);
            }
            var adopted = new Dictionary<SvgSymbol, SvgSymbol>();
            foreach (var symbol in prepared.Styles.Values.Distinct())
                adopted.Add(symbol, (SvgSymbol)AddResource(symbol));
            _selection.Clear();
            for (var index = 0; index < prepared.Sources.Length; index++)
            {
                var source = prepared.Sources[index];
                var copy = prepared.Replacements[index];
                if (prepared.Styles.TryGetValue(copy, out var style))
                    copy.ReferencedElement = new Uri("#" + adopted[style].ID, UriKind.Relative);
                var parent = source.Parent!;
                var position = parent.Children.IndexOf(source);
                parent.Children.Remove(source);
                parent.Children.Insert(position, copy);
                RebindPaintServers(copy);
                _selection.Add(copy);
            }
        });
        prepared.Applied = true;
        return true;
    }

    private void PrepareSymbolTint(SvgUse copy, SvgColourServer? paint, float amount, bool reverse, List<SvgFilter> filters)
    {
        var original = copy.CustomAttributes.TryGetValue(SymbolTintBase, out var saved) ? saved : copy.Filter?.OriginalString ?? string.Empty;
        var previous = copy.CustomAttributes.TryGetValue(SymbolTintAmount, out var stored)
            && float.TryParse(stored, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
        var strength = reverse ? Math.Max(0, previous - amount) : 1 - (1 - previous) * (1 - amount);
        if (strength <= 0)
        {
            copy.Filter = new Uri(original.Length == 0 ? "none" : original, UriKind.Relative);
            copy.CustomAttributes.Remove(SymbolTintBase);
            copy.CustomAttributes.Remove(SymbolTintAmount);
            return;
        }
        if (paint is null)
        {
            // Reverse painting retains the last stain color while reducing its amount.
            paint = ParsePaint(copy.CustomAttributes.TryGetValue("data-unodraw-symbol-tint-color", out var color) ? color : CurrentFill) as SvgColourServer
                ?? throw new InvalidOperationException("The saved symbol stain is not a solid color.");
        }
        var baseId = LocalEffectId(original);
        var filter = baseId is not null && Document?.GetElementById(baseId) is SvgFilter existing ? (SvgFilter)existing.DeepCopy() : new SvgFilter();
        filter.ID = CreateId("symbol-tint");
        filter.CustomAttributes["color-interpolation-filters"] = "sRGB";
        var keep = 1 - strength;
        var colorValue = paint.Colour;
        filter.Children.Add(new SvgColourMatrix
        {
            Type = SvgColourMatrixType.Matrix,
            Values = string.Format(CultureInfo.InvariantCulture,
                "{0:R} 0 0 0 {1:R} 0 {0:R} 0 0 {2:R} 0 0 {0:R} 0 {3:R} 0 0 0 1 0",
                keep, colorValue.R / 255f * strength, colorValue.G / 255f * strength, colorValue.B / 255f * strength)
        });
        // A cloned filter can contain its own fragment IDs. Reserve them against both
        // the live document and this gesture's other staged filters before insertion.
        var fragment = CreateFragmentDocument();
        fragment.Children.Add(filter);
        var element = XDocument.Parse(fragment.GetXML()).Root!.Elements().Single();
        var reserved = Document!.Descendants().Select(item => item.ID).Where(id => !string.IsNullOrEmpty(id))
            .Concat(filters.SelectMany(item => new[] { (SvgElement)item }.Concat(item.Descendants())).Select(item => item.ID).Where(id => !string.IsNullOrEmpty(id)));
        var remapped = SvgXmlIdRemapper.Clone(element, reserved);
        var parsed = ParseDocument(new XDocument(new XElement(SvgFilterCatalog.Namespace + "svg", remapped)).ToString(SaveOptions.DisableFormatting), Document.BaseUri);
        filter = (SvgFilter)parsed.Children[0];
        parsed.Children.Remove(filter);
        copy.Filter = new Uri("#" + filter.ID, UriKind.Relative);
        copy.CustomAttributes[SymbolTintBase] = original;
        copy.CustomAttributes[SymbolTintAmount] = strength.ToString("R", CultureInfo.InvariantCulture);
        copy.CustomAttributes["data-unodraw-symbol-tint-color"] = PaintKey(paint);
        filters.Add(filter);
    }

    private static float SymbolAspectRatio(SvgSymbol symbol)
        => symbol.ViewBox.Width > 0 && symbol.ViewBox.Height > 0 ? symbol.ViewBox.Width / symbol.ViewBox.Height : 1;
}
