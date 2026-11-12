using System;
using System.Collections.Generic;
using Svg.FilterEffects;

namespace Svg.Editor.Skia;

/// <summary>
/// An exact snapshot of the inputs displayed by the editor's document outline.
/// Geometry and paint edits do not replace otherwise unchanged outline controls.
/// </summary>
public sealed class SvgEditorHierarchyPresentation
{
    private readonly SvgDocument? _document;
    private readonly string _filter;
    private readonly Entry[] _entries;

    private SvgEditorHierarchyPresentation(SvgDocument? document, string filter, Entry[] entries)
    {
        _document = document;
        _filter = filter;
        _entries = entries;
    }

    public static SvgEditorHierarchyPresentation Capture(SvgDocument? document, string? filter)
    {
        var entries = new List<Entry>();
        if (document is not null)
            Capture(document, entries);
        return new SvgEditorHierarchyPresentation(document, (filter ?? string.Empty).Trim(), entries.ToArray());
    }

    /// <summary>
    /// Checks the live tree without building another collection or allocating a
    /// normalized search string. Identity and ordered child counts distinguish
    /// reparenting and replacement even when IDs or descendant counts match.
    /// </summary>
    public bool IsCurrent(SvgDocument? document, string? filter)
    {
        if (!ReferenceEquals(document, _document) || !SameFilter(filter))
            return false;
        var index = 0;
        return (document is null || IsCurrent(document, ref index)) && index == _entries.Length;
    }

    private bool SameFilter(string? filter)
    {
        var value = filter ?? string.Empty;
        var start = 0;
        var end = value.Length;
        while (start < end && char.IsWhiteSpace(value[start]))
            start++;
        while (end > start && char.IsWhiteSpace(value[end - 1]))
            end--;
        return end - start == _filter.Length && string.CompareOrdinal(value, start, _filter, 0, _filter.Length) == 0;
    }

    private static void Capture(SvgElement element, List<Entry> entries)
    {
        // Include the document itself: its lock can affect every descendant's
        // controls even though the outline does not display a root row.
        entries.Add(new Entry(element));
        for (var index = 0; index < element.Children.Count; index++)
            Capture(element.Children[index], entries);
    }

    private bool IsCurrent(SvgElement element, ref int index)
    {
        if (index >= _entries.Length || !_entries[index++].Matches(element))
            return false;
        for (var child = 0; child < element.Children.Count; child++)
            if (!IsCurrent(element.Children[child], ref index))
                return false;
        return true;
    }

    private readonly struct Entry
    {
        private readonly SvgElement _element;
        private readonly int _childCount;
        private readonly string? _id;
        private readonly string? _display;
        private readonly string? _visibility;
        private readonly string? _lock;
        private readonly string? _legacyLock;

        public Entry(SvgElement element)
        {
            _element = element;
            _childCount = element.Children.Count;
            _id = element.ID;
            _display = element.Display;
            _visibility = element.Visibility;
            element.CustomAttributes.TryGetValue("data-unodraw-locked", out _lock);
            element.CustomAttributes.TryGetValue("data-lock", out _legacyLock);
        }

        public bool Matches(SvgElement element)
        {
            if (!ReferenceEquals(element, _element) || element.Children.Count != _childCount
                || element.ID != _id || element.Display != _display || element.Visibility != _visibility)
                return false;
            element.CustomAttributes.TryGetValue("data-unodraw-locked", out var currentLock);
            element.CustomAttributes.TryGetValue("data-lock", out var currentLegacyLock);
            return currentLock == _lock && currentLegacyLock == _legacyLock;
        }
    }
}

/// <summary>
/// An exact snapshot of resource-picker membership, ordering and displayed
/// labels. Resource contents remain live and do not require new picker items.
/// </summary>
public sealed class SvgEditorResourcePresentation
{
    private readonly SvgDocument? _document;
    private readonly Entry[] _entries;

    private SvgEditorResourcePresentation(SvgDocument? document, Entry[] entries)
    {
        _document = document;
        _entries = entries;
    }

    public static SvgEditorResourcePresentation Capture(SvgDocument? document)
    {
        var entries = new List<Entry>();
        if (document is not null)
            Capture(document, false, entries);
        return new SvgEditorResourcePresentation(document, entries.ToArray());
    }

    /// <summary>Checks the ordered resources without allocating a replacement item list.</summary>
    public bool IsCurrent(SvgDocument? document)
    {
        if (!ReferenceEquals(document, _document))
            return false;
        var index = 0;
        return (document is null || IsCurrent(document, false, ref index)) && index == _entries.Length;
    }

    /// <summary>Matches the resource categories shown by the editor's resource picker.</summary>
    public static bool IsResource(SvgElement element)
        => element is SvgGradientServer or SvgPatternServer or SvgSymbol or SvgClipPath or SvgFilter;

    private static bool IsOwnLocked(SvgElement element)
        => (element.CustomAttributes.TryGetValue("data-unodraw-locked", out var current) && current == "true")
            || (element.CustomAttributes.TryGetValue("data-lock", out var legacy) && legacy == "true");

    private static void Capture(SvgElement element, bool inheritedLock, List<Entry> entries)
    {
        var locked = inheritedLock || IsOwnLocked(element);
        if (IsResource(element))
            entries.Add(new Entry(element, locked));
        for (var index = 0; index < element.Children.Count; index++)
            Capture(element.Children[index], locked, entries);
    }

    private bool IsCurrent(SvgElement element, bool inheritedLock, ref int index)
    {
        var locked = inheritedLock || IsOwnLocked(element);
        if (IsResource(element) && (index >= _entries.Length || !_entries[index++].Matches(element, locked)))
            return false;
        for (var child = 0; child < element.Children.Count; child++)
            if (!IsCurrent(element.Children[child], locked, ref index))
                return false;
        return true;
    }

    private readonly struct Entry
    {
        private readonly SvgElement _element;
        private readonly string? _id;
        private readonly bool _hidden;
        private readonly bool _locked;

        public Entry(SvgElement element, bool locked)
        {
            _element = element;
            _id = element.ID;
            _hidden = element.Display == "none" || element.Visibility == "hidden";
            _locked = locked;
        }

        public bool Matches(SvgElement element, bool locked)
            => ReferenceEquals(element, _element) && element.ID == _id && locked == _locked
                && (element.Display == "none" || element.Visibility == "hidden") == _hidden;
    }
}
