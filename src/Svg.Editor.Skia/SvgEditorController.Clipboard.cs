using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Svg.Model.Services;
using Svg.Transforms;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>Returns a standalone SVG clipboard with referenced document resources.</summary>
    public string CopySelection()
    {
        if (Document is null || _selection.Count == 0)
            return string.Empty;
        var clipboard = CreateClipboardDocument();
        var xml = clipboard.GetXML();
        Session.SetClipboard(null, xml);
        return xml;
    }

    public string CutSelection()
    {
        var xml = CopySelection();
        if (xml.Length != 0)
            DeleteSelection();
        return xml;
    }

    public void Paste(string? svg = null, float dx = 16, float dy = 16)
    {
        var xml = svg ?? Session.Clipboard.Xml;
        if (string.IsNullOrWhiteSpace(xml) || Document is null)
            return;
        if (!IsFinite(dx) || !IsFinite(dy))
            throw new ArgumentOutOfRangeException(nameof(dx));
        var parsed = ParseDocument(xml!, Document.BaseUri);
        var imported = ParseDocument(RemapCloneIds(parsed.GetXML()), Document.BaseUri);
        var children = imported.Children.ToArray();
        Execute("Paste objects", () =>
        {
            _selection.Clear();
            foreach (var child in children)
            {
                imported.Children.Remove(child);
                if (child is SvgVisualElement visual)
                {
                    if (string.IsNullOrEmpty(visual.ID))
                        visual.ID = CreateId("object");
                    PrependTransform(visual, SK.SKMatrix.CreateTranslation(dx, dy));
                    _selection.Add(visual);
                }
                Document.Children.Add(child);
                RebindPaintServers(child);
            }
        });
    }

    /// <summary>Duplicates in place in the original parents, preserving z-order and references.</summary>
    public void DuplicateSelection(float dx = 16, float dy = 16)
    {
        if (Document is null || _selection.Count == 0)
            return;
        var originals = TopLevelSelection();
        var temporary = CreateFragmentDocument();
        foreach (var original in originals)
            temporary.Children.Add(original.DeepCopy());
        var remapped = ParseDocument(RemapCloneIds(temporary.GetXML()), Document.BaseUri);
        var clones = remapped.Children.OfType<SvgVisualElement>().ToArray();
        var deltas = originals.Select(element =>
        {
            var matrix = GetSceneNode(element)?.Parent is { } parent ? ToMatrix(parent.TotalTransform) : SK.SKMatrix.Identity;
            if (!matrix.TryInvert(out var inverse))
                throw new InvalidOperationException("An object inside a singular transform cannot be duplicated with an offset.");
            return Multiply(Multiply(inverse, SK.SKMatrix.CreateTranslation(dx, dy)), matrix);
        }).ToArray();
        Execute("Duplicate objects", () =>
        {
            _selection.Clear();
            for (var i = 0; i < originals.Length; i++)
            {
                var original = originals[i];
                var clone = clones[i];
                remapped.Children.Remove(clone);
                if (string.IsNullOrEmpty(clone.ID))
                    clone.ID = CreateId("object");
                PrependTransform(clone, deltas[i]);
                var parent = original.Parent!;
                parent.Children.Insert(parent.Children.IndexOf(original) + 1, clone);
                RebindPaintServers(clone);
                _selection.Add(clone);
            }
        });
    }

    private SvgDocument CreateClipboardDocument()
    {
        var clipboard = CreateFragmentDocument();
        var selected = TopLevelSelection();
        var rootMatrix = GetSceneNode(Document!) is { } root ? ToMatrix(root.TotalTransform) : SK.SKMatrix.Identity;
        if (!rootMatrix.TryInvert(out var rootInverse))
            throw new InvalidOperationException("The document viewport has a singular transform.");
        // Resources remain real SVG definitions. Copying them also makes clipboard data portable.
        var definitions = new SvgDefinitionList();
        foreach (var source in Document!.Descendants().OfType<SvgDefinitionList>())
        {
            if (selected.Any(e => IsDescendantOf(source, e)))
                continue;
            foreach (var resource in source.Children)
                definitions.Children.Add(resource.DeepCopy());
        }
        if (definitions.Children.Count > 0)
            clipboard.Children.Add(definitions);
        foreach (var element in selected)
        {
            var clone = (SvgVisualElement)element.DeepCopy();
            MaterializePresentation(element, clone);
            if (GetSceneNode(element)?.Parent is { } parent)
                PrependTransform(clone, Multiply(rootInverse, ToMatrix(parent.TotalTransform)));
            clipboard.Children.Add(clone);
        }
        return clipboard;
    }

    private SvgDocument CreateFragmentDocument()
    {
        var document = new SvgDocument
        {
            Width = Document!.Width,
            Height = Document.Height,
            ViewBox = Document.ViewBox,
            AspectRatio = Document.AspectRatio,
            BaseUri = Document.BaseUri
        };
        foreach (var ns in Document.Namespaces)
            document.Namespaces[ns.Key] = ns.Value;
        return document;
    }

    private string RemapCloneIds(string xml)
    {
        var tree = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var used = new HashSet<string>(Document!.Descendants().Select(e => e.ID).Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in tree.Root!.Descendants())
        {
            if (element.Attribute("id") is not { } id || id.Value.Length == 0)
                continue;
            var original = id.Value;
            var prefix = original + "-copy";
            var candidate = prefix;
            var suffix = 1;
            while (!used.Add(candidate))
                candidate = prefix + (++suffix).ToString(CultureInfo.InvariantCulture);
            if (mapping.ContainsKey(original))
                throw new InvalidOperationException("The clipboard contains duplicate SVG IDs.");
            mapping.Add(original, candidate);
            id.Value = candidate;
        }
        foreach (var element in tree.Root.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().Where(a => a.Name.LocalName != "id" && !a.IsNamespaceDeclaration))
                attribute.Value = RemapReferences(attribute.Value, mapping,
                    attribute.Name.LocalName is "href" or "begin" or "end");
            if (element.Name.LocalName == "style")
                element.Value = RemapReferences(element.Value, mapping, false, true);
        }
        return tree.ToString(SaveOptions.DisableFormatting);
    }

    private static string RemapReferences(string value, Dictionary<string, string> mapping, bool directReference, bool css = false)
    {
        if (directReference && value.StartsWith("#", StringComparison.Ordinal) && mapping.TryGetValue(value.Substring(1), out var href))
            return "#" + href;
        value = Regex.Replace(value, @"(url\(\s*['"" ]?#)([^\s'""()]+)(['"" ]?\s*\))", match =>
            mapping.TryGetValue(match.Groups[2].Value, out var replacement)
                ? match.Groups[1].Value + replacement + match.Groups[3].Value
                : match.Value, RegexOptions.CultureInvariant);
        if (directReference && mapping.Count > 0)
        {
            var alternatives = string.Join("|", mapping.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape));
            value = Regex.Replace(value, @"(^|[;\s])(" + alternatives + @")(?=\.)",
                match => match.Groups[1].Value + mapping[match.Groups[2].Value], RegexOptions.CultureInvariant);
        }
        if (css)
        {
            // Only rewrite ID tokens in selector preludes, never hexadecimal declaration colors.
            value = Regex.Replace(value, @"([^{}]+)\{", rule =>
                Regex.Replace(rule.Groups[1].Value, @"#([\w-]+)", selector =>
                    mapping.TryGetValue(selector.Groups[1].Value, out var id) ? "#" + id : selector.Value,
                    RegexOptions.CultureInvariant) + "{", RegexOptions.CultureInvariant);
        }
        return value;
    }

    private void RebindPaintServers(SvgElement root)
    {
        foreach (var element in new[] { root }.Concat(root.Descendants()))
        {
            foreach (var property in element.GetType().GetProperties().Where(p => p.CanRead && p.CanWrite && typeof(SvgPaintServer).IsAssignableFrom(p.PropertyType)))
            {
                if (property.GetValue(element, null) is SvgDeferredPaintServer deferred)
                    property.SetValue(element, RebindPaint(deferred), null);
            }
        }
    }

    private SvgDeferredPaintServer RebindPaint(SvgDeferredPaintServer deferred)
        => new(Document!, deferred.DeferredId,
            deferred.FallbackServer is SvgDeferredPaintServer fallback ? RebindPaint(fallback) : deferred.FallbackServer);

    private static bool IsDescendantOf(SvgElement element, SvgElement ancestor)
    {
        for (var current = element; current is not null; current = current.Parent)
            if (ReferenceEquals(current, ancestor))
                return true;
        return false;
    }
}
