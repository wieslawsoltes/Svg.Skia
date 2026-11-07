using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>A named, nonprinting SVG view in the caller's coordinate system.</summary>
public readonly struct SvgEditorArtboard
{
    public SvgEditorArtboard(string id, string name, SKRect bounds, bool isImplicit = false)
    { Id = id; Name = name; Bounds = bounds; IsImplicit = isImplicit; }
    public string Id { get; }
    public string Name { get; }
    public SKRect Bounds { get; }
    public bool IsImplicit { get; }
    public override string ToString() => Name;
}

/// <summary>
/// Persists editor artboards as ordinary SVG view elements. Geometry is always in
/// root user units; it never becomes artwork or a second document/scene model.
/// </summary>
public static class SvgEditorArtboardService
{
    public const string Attribute = "data-unodraw-artboard";
    public const string NameAttribute = "data-unodraw-name";
    public const int MaximumArtboards = 1000;

    public static IReadOnlyList<SvgEditorArtboard> Read(SvgDocument? document)
    {
        var result = new List<SvgEditorArtboard>();
        if (document is null)
            return result;
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in document.Children.Where(IsArtboardView))
        {
            if (string.IsNullOrEmpty(element.ID) || !identifiers.Add(element.ID)
                || !element.TryGetAttribute("viewBox", out var value) || !TryParseBounds(value, out var bounds))
                continue;
            var name = element.CustomAttributes.TryGetValue(NameAttribute, out var label) && !string.IsNullOrWhiteSpace(label)
                ? label : element.Children.OfType<SvgTitle>().FirstOrDefault()?.Content ?? element.ID;
            result.Add(new SvgEditorArtboard(element.ID, name, bounds));
            if (result.Count == MaximumArtboards)
                break;
        }
        return result;
    }

    public static void Write(SvgDocument document, IReadOnlyList<SvgEditorArtboard> artboards)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        if (artboards is null)
            throw new ArgumentNullException(nameof(artboards));
        if (artboards.Count is < 1 or > MaximumArtboards)
            throw new ArgumentException($"Keep between one and {MaximumArtboards} artboards in a document.", nameof(artboards));
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var old = document.Children.Where(IsArtboardView).ToArray();
        var outside = new HashSet<string>(document.Descendants().Where(element => !old.Contains(element))
            .Select(element => element.ID).Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(document.ID))
            outside.Add(document.ID);
        foreach (var artboard in artboards)
        {
            if (string.IsNullOrEmpty(artboard.Id) || artboard.IsImplicit || !identifiers.Add(artboard.Id) || outside.Contains(artboard.Id))
                throw new ArgumentException("Artboards need unique document identifiers.", nameof(artboards));
            XmlConvert.VerifyNCName(artboard.Id);
            ValidateName(artboard.Name);
            ValidateBounds(artboard.Bounds);
        }

        // The SVG DOM currently represents <view> as SvgUnknownElement. The explicit
        // marker distinguishes our nonvisual views from unrelated imported metadata.
        var ordered = new List<SvgElement>();
        foreach (var artboard in artboards)
        {
            var view = old.FirstOrDefault(element => element.ID == artboard.Id) ?? new SvgUnknownElement("view") { ID = artboard.Id };
            view.CustomAttributes[Attribute] = "true";
            view.CustomAttributes[NameAttribute] = artboard.Name;
            view.CustomAttributes["viewBox"] = string.Join(" ", Number(artboard.Bounds.Left), Number(artboard.Bounds.Top),
                Number(artboard.Bounds.Width), Number(artboard.Bounds.Height));
            ordered.Add(view);
        }
        var slots = document.Children.Select((element, index) => (element, index)).Where(item => old.Contains(item.element))
            .Select(item => item.index).ToArray();
        foreach (var view in old)
            document.Children.Remove(view);
        for (var index = 0; index < ordered.Count; index++)
        {
            // Reuse the old metadata slots so reordering views never changes artwork
            // painter order or the structural addresses of anonymous siblings.
            var position = index < slots.Length ? Math.Min(slots[index], document.Children.Count) : document.Children.Count;
            document.Children.Insert(position, ordered[index]);
        }
    }

    private static bool IsArtboardView(SvgElement element) => element is SvgUnknownElement
        && element.CustomAttributes.TryGetValue(Attribute, out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseBounds(string value, out SKRect bounds)
    {
        bounds = default;
        var fields = value.Split(new[] { ' ', '\t', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 4)
            return false;
        var values = new float[4];
        for (var index = 0; index < values.Length; index++)
            if (!float.TryParse(fields[index], NumberStyles.Float, CultureInfo.InvariantCulture, out values[index]) || !Finite(values[index]))
                return false;
        if (values[2] <= 0 || values[3] <= 0)
            return false;
        bounds = SKRect.Create(values[0], values[1], values[2], values[3]);
        return Finite(bounds.Right) && Finite(bounds.Bottom);
    }

    public static void ValidateBounds(SKRect bounds)
    {
        if (!Finite(bounds.Left) || !Finite(bounds.Top) || !Finite(bounds.Right) || !Finite(bounds.Bottom)
            || !Finite(bounds.Width) || !Finite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Artboard bounds must be finite with positive width and height.");
    }

    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
            throw new ArgumentException("Enter an artboard name between 1 and 256 characters.", nameof(name));
        XmlConvert.VerifyXmlChars(name);
    }

    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
