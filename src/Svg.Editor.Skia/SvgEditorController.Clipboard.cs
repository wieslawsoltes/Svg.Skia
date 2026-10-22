using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
        var tree = XDocument.Parse(Document!.GetXML(), LoadOptions.PreserveWhitespace);
        var root = tree.Root!;
        var selected = new HashSet<XElement>(TopLevelSelection().Select(element =>
        {
            var key = GetSelectionKey(element);
            if (key.StartsWith("#", StringComparison.Ordinal))
                return root.Descendants().Single(e => (string?)e.Attribute("id") == key.Substring(1));
            var node = root;
            foreach (var part in key.Split('/'))
                node = node.Elements().ElementAt(int.Parse(part, CultureInfo.InvariantCulture));
            return node;
        }));
        var selectedBranches = new HashSet<XElement>(selected.SelectMany(e => e.AncestorsAndSelf()));
        var resources = new HashSet<XElement>(root.Descendants().Where(IsClipboardResource));
        var resourceBranches = new HashSet<XElement>(resources.SelectMany(e => e.AncestorsAndSelf()));

        void Prune(XElement parent)
        {
            if (selected.Contains(parent) || resources.Contains(parent))
                return;
            foreach (var child in parent.Elements().ToArray())
            {
                if (!selectedBranches.Contains(child) && !resourceBranches.Contains(child))
                {
                    child.Remove();
                    continue;
                }
                Prune(child);
                // Retain resource ancestry for inherited paint and xml:base, while preventing
                // resource-only groups from becoming visible or selected after pasting.
                if (!selectedBranches.Contains(child) && !IsClipboardResource(child))
                    child.ReplaceWith(new XElement(root.Name.Namespace + "defs", child));
            }
        }
        Prune(root);

        // Preserve shared ancestor compositing as shared groups: applying group opacity or
        // clipping separately to each copied child changes overlapping artwork.
        var clipboard = XDocument.Parse(CreateFragmentDocument().GetXML());
        var presentation = root.Attributes().Where(a => !a.IsNamespaceDeclaration &&
            a.Name.LocalName is not ("width" or "height" or "x" or "y" or "viewBox" or "preserveAspectRatio" or "version" or "baseProfile")).ToArray();
        if (presentation.Length > 0)
            clipboard.Root!.Add(new XElement(root.Name.Namespace + "g", presentation, root.Nodes()));
        else
            clipboard.Root!.Add(root.Nodes());
        return ParseDocument(clipboard.ToString(SaveOptions.DisableFormatting), Document!.BaseUri);
    }

    private static bool IsClipboardResource(XElement element)
        => element.Name.LocalName is "defs" or "style" or "symbol" or "clipPath" or "mask" or "filter"
            or "linearGradient" or "radialGradient" or "pattern" or "marker" or "font" or "font-face"
            or "color-profile" or "metadata" or "title" or "desc";

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
        RewriteTreeReferences(tree, mapping);
        return tree.ToString(SaveOptions.DisableFormatting);
    }

    private static void RewriteTreeReferences(XDocument tree, Dictionary<string, string> mapping)
    {
        foreach (var element in tree.Root!.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().Where(a => a.Name.LocalName != "id" && !a.IsNamespaceDeclaration))
                attribute.Value = RemapReferences(attribute.Value, mapping,
                    attribute.Name.LocalName is "href" or "begin" or "end");
            if (element.Name.LocalName == "style")
                element.Value = RemapReferences(element.Value, mapping, false, true);
        }
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

}
