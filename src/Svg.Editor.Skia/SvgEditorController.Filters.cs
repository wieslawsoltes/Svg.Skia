using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Svg.Editor.Skia;

/// <summary>A detached filter edit; disposing or abandoning it never changes the live document.</summary>
public sealed class SvgFilterEdit
{
    internal SvgFilterEdit(SvgEditorController owner, SvgDocument document, string source, string[] selection,
        string? originalId, string resourceId, SvgFilterGraph graph)
    {
        Owner = owner; Document = document; Source = source; Selection = selection;
        OriginalFilterId = originalId; ResourceId = resourceId; Graph = graph;
    }
    internal SvgEditorController Owner { get; }
    internal SvgDocument Document { get; }
    internal string Source { get; }
    internal string[] Selection { get; }
    public string? OriginalFilterId { get; }
    public string ResourceId { get; }
    public SvgFilterGraph Graph { get; set; }
    public Uri? BaseUri => Document.BaseUri;
}

public sealed partial class SvgEditorController
{
    public SvgFilterEdit BeginFilterEdit()
    {
        ThrowIfDisposed();
        if (Document is null || _selection.Count == 0)
            throw new InvalidOperationException("Select artwork to edit its filter graph.");
        if (IsEditing) throw new InvalidOperationException("Complete the current gesture before editing a filter.");
        EnsureSelectionEditable();
        var source = GetSvg();
        var tree = ReadEffectDocument(source);
        var reference = _selection.Count == 1 ? LocalEffectId(_selection[0].Filter?.OriginalString) : null;
        var existing = reference is null ? null : tree.Root!.DescendantsAndSelf()
            .FirstOrDefault(element => element.Name == SvgFilterCatalog.Namespace + "filter" && (string?)element.Attribute("id") == reference);
        var graph = existing is null ? new SvgFilterGraph() : new SvgFilterGraph(existing);
        if (existing is null) graph.Add("feGaussianBlur");
        return new SvgFilterEdit(this, Document, source, CaptureSelection(), existing is null ? null : reference,
            UniqueEffectId(tree, "filter"), graph);
    }

    /// <summary>Builds preview source without touching live SVG, selection, history or renderer.</summary>
    public string BuildFilterPreviewSvg(SvgFilterEdit edit, bool editSharedResource = false)
    {
        if (edit is null) throw new ArgumentNullException(nameof(edit));
        if (!ReferenceEquals(edit.Owner, this)) throw new ArgumentException("This filter edit belongs to another editor.", nameof(edit));
        if (edit.Graph is null) throw new ArgumentException("The filter draft has no graph.", nameof(edit));
        edit.Graph.ThrowIfInvalid();
        var tree = ReadEffectDocument(edit.Source);
        var selected = edit.Selection.Select(key => ResolveEffectElement(tree, key)
            ?? throw new InvalidOperationException("The original filter target could not be resolved.")).ToArray();
        var existing = edit.OriginalFilterId is null ? null : tree.Root!.DescendantsAndSelf()
            .FirstOrDefault(element => element.Name == SvgFilterCatalog.Namespace + "filter" && (string?)element.Attribute("id") == edit.OriginalFilterId);
        var shared = editSharedResource && existing is not null;
        var resourceId = shared ? edit.OriginalFilterId! : edit.ResourceId;
        var resource = edit.Graph.ToElement();
        if (!shared)
            resource = SvgXmlIdRemapper.Clone(resource, tree.Root!.DescendantsAndSelf().Attributes("id").Select(id => id.Value).Concat(new[] { resourceId }));
        var previousRootId = (string?)resource.Attribute("id");
        resource.SetAttributeValue("id", resourceId);
        if (previousRootId is not null && previousRootId != resourceId)
        {
            var fragment = new XDocument(resource);
            RewriteTreeReferences(fragment, new Dictionary<string, string>(StringComparer.Ordinal) { [previousRootId] = resourceId });
            resource.Remove();
        }
        if (shared)
        {
            // Keep IDs stable for outside consumers, but do not let imported source
            // introduce duplicate IDs belonging to unrelated artwork or definitions.
            var replaced = new HashSet<XElement>(existing!.DescendantsAndSelf());
            var reserved = new HashSet<string>(tree.Root!.DescendantsAndSelf().Where(element => !replaced.Contains(element))
                .Attributes("id").Select(id => id.Value), StringComparer.Ordinal);
            foreach (var id in resource.DescendantsAndSelf().Attributes("id"))
                if (reserved.Contains(id.Value))
                    throw new InvalidOperationException($"The shared filter ID '{id.Value}' belongs to another object. Rename it or apply an isolated filter copy.");
            existing.ReplaceWith(resource);
        }
        else if (existing is not null)
        {
            // Retain inherited resource presentation/xml:base without shifting sibling
            // indices used to restore anonymous selected artwork after ReplaceSvg.
            existing.Parent!.Add(resource);
        }
        else
        {
            var definitions = tree.Root!.Elements(SvgFilterCatalog.Namespace + "defs").FirstOrDefault();
            if (definitions is null)
            {
                definitions = new XElement(SvgFilterCatalog.Namespace + "defs");
                tree.Root.Add(definitions);
            }
            definitions.Add(resource);
        }
        foreach (var element in selected) SetEffectReference(element, "filter", "url(#" + resourceId + ")");
        return tree.ToString(SaveOptions.DisableFormatting);
    }

    public void ApplyFilterEdit(SvgFilterEdit edit, bool editSharedResource = false)
    {
        ValidateFilterEditCurrent(edit);
        EnsureSelectionEditable();
        if (editSharedResource && edit.OriginalFilterId is { } resourceId)
        {
            var definition = Document!.GetElementById(resourceId);
            if (definition is not null) EnsureElementEditable(definition);
            foreach (var consumer in Document.Descendants().OfType<SvgVisualElement>())
                if (LocalEffectId(consumer.Filter?.OriginalString) == resourceId) EnsureElementEditable(consumer);
        }
        var source = BuildFilterPreviewSvg(edit, editSharedResource);
        var time = Renderer.AnimationTime;
        ReplaceSvg(source, "Edit filter graph");
        Renderer.SetAnimationTime(time);
    }

    public void RemoveSelectionFilters()
    {
        ThrowIfDisposed();
        if (_selection.Count == 0) return;
        EnsureSelectionEditable();
        var tree = ReadEffectDocument(GetSvg());
        foreach (var key in CaptureSelection())
            if (ResolveEffectElement(tree, key) is { } element) SetEffectReference(element, "filter", "none");
        var time = Renderer.AnimationTime;
        ReplaceSvg(tree.ToString(SaveOptions.DisableFormatting), "Remove filters");
        Renderer.SetAnimationTime(time);
    }

    private void ValidateFilterEditCurrent(SvgFilterEdit edit)
    {
        ThrowIfDisposed();
        if (edit is null) throw new ArgumentNullException(nameof(edit));
        if (!ReferenceEquals(edit.Owner, this) || !ReferenceEquals(edit.Document, Document) || IsEditing
            || !string.Equals(edit.Source, GetSvg(), StringComparison.Ordinal) || !edit.Selection.SequenceEqual(CaptureSelection()))
            throw new InvalidOperationException("The document or selection changed while the filter was being edited. Reopen Filter Studio to avoid overwriting newer edits.");
    }

    private static string? LocalEffectId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value!.Trim();
        if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(")", StringComparison.Ordinal))
            value = value.Substring(4, value.Length - 5).Trim().Trim('\'', '"');
        return value.StartsWith("#", StringComparison.Ordinal) ? Uri.UnescapeDataString(value.Substring(1)) : null;
    }
    private static XDocument ReadEffectDocument(string source)
    {
        using var reader = XmlReader.Create(new System.IO.StringReader(source), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }
    private static XElement? ResolveEffectElement(XDocument tree, string key)
    {
        if (key.StartsWith("#", StringComparison.Ordinal))
            return tree.Root!.DescendantsAndSelf().FirstOrDefault(element => (string?)element.Attribute("id") == key.Substring(1));
        XElement? current = tree.Root;
        foreach (var part in key.Split('/'))
        {
            if (!int.TryParse(part, out var index) || index < 0) return null;
            current = current?.Elements().ElementAtOrDefault(index);
        }
        return current;
    }
    private static string UniqueEffectId(XDocument tree, string prefix)
    {
        var used = new HashSet<string>(tree.Root!.DescendantsAndSelf().Attributes("id").Select(id => id.Value), StringComparer.Ordinal);
        for (var index = 1; ; index++)
        { var id = prefix + index; if (!used.Contains(id)) return id; }
    }
    private static void SetEffectReference(XElement element, string property, string value)
    {
        element.SetAttributeValue(property, value);
        element.SetAttributeValue("style", SvgInlineStyleService.SetProperty((string?)element.Attribute("style"), property, value, true));
    }
}
