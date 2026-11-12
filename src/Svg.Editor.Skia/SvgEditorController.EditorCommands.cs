using System;
using System.Collections.Generic;
using System.Linq;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

public enum SvgSelectionMatch { Fill, Stroke, StrokeWidth, Opacity, Appearance, ObjectType }
public enum SvgPastePlacement { InPlace, InFront, Behind }

public sealed partial class SvgEditorController
{
    private SK.SKMatrix? _repeatTransform;
    private SK.SKMatrix? _editTransform;

    public bool CanRepeatTransform => _repeatTransform.HasValue && _selection.Count > 0;

    /// <summary>Repeats the last committed transform; canceled previews never replace it.</summary>
    public void RepeatTransform()
    {
        if (_repeatTransform is { } transform)
            TransformSelection(transform);
    }

    /// <summary>Selects the unselected visible branches, preserving group boundaries.</summary>
    public void InvertSelection()
    {
        ThrowIfDisposed();
        var selected = new HashSet<SvgElement>(_selection);
        var result = new List<SvgVisualElement>();
        if (Document is { } document)
            Visit(document);
        _selection.Clear();
        _selection.AddRange(result);
        SyncSelection();
        NotifyChanged();

        void Visit(SvgElement parent)
        {
            foreach (var element in parent.Children.OfType<SvgVisualElement>())
            {
                if (!IsSelectableArtwork(element) || selected.Contains(element))
                    continue;
                if (element.Descendants().Any(selected.Contains))
                    Visit(element);
                else
                    result.Add(element);
            }
        }
    }

    /// <summary>Selects visible, unlocked artwork matching the first selected object.</summary>
    public void SelectMatching(SvgSelectionMatch match)
    {
        if (!Enum.IsDefined(typeof(SvgSelectionMatch), match))
            throw new ArgumentOutOfRangeException(nameof(match));
        ThrowIfDisposed();
        if (_selection.FirstOrDefault() is not { } reference || Document is null)
            return;
        var candidates = Document.Descendants().OfType<SvgVisualElement>()
            .Where(IsSelectableArtwork).Where(element =>
                (reference is SvgGroup || element is not SvgGroup) && Matches(element)).ToArray();
        _selection.Clear();
        _selection.AddRange(candidates.Where(element => !HierarchyAncestors(element).OfType<SvgVisualElement>().Any(candidates.Contains)));
        SyncSelection();
        NotifyChanged();

        bool Matches(SvgVisualElement element)
        {
            var fill = Equals(element.Fill?.ToString(), reference.Fill?.ToString());
            var stroke = Equals(element.Stroke?.ToString(), reference.Stroke?.ToString());
            return match switch
            {
                SvgSelectionMatch.Fill => fill,
                SvgSelectionMatch.Stroke => stroke,
                SvgSelectionMatch.StrokeWidth => element.StrokeWidth.Equals(reference.StrokeWidth),
                SvgSelectionMatch.Opacity => element.Opacity.Equals(reference.Opacity),
                SvgSelectionMatch.ObjectType => element.GetType() == reference.GetType(),
                _ => fill && stroke && element.StrokeWidth.Equals(reference.StrokeWidth)
                    && element.Opacity.Equals(reference.Opacity) && element.FillOpacity.Equals(reference.FillOpacity)
                    && element.StrokeOpacity.Equals(reference.StrokeOpacity)
                    && element.StrokeLineCap == reference.StrokeLineCap && element.StrokeLineJoin == reference.StrokeLineJoin
            };
        }
    }

    public void SelectObjectsOfType(Type elementType)
    {
        if (elementType is null || !typeof(SvgVisualElement).IsAssignableFrom(elementType))
            throw new ArgumentException("Choose an SVG artwork type.", nameof(elementType));
        ThrowIfDisposed();
        _selection.Clear();
        if (Document is { } document)
            _selection.AddRange(document.Descendants().OfType<SvgVisualElement>().Where(element =>
                elementType.IsInstanceOfType(element) && IsSelectableArtwork(element)));
        SyncSelection();
        NotifyChanged();
    }

    private static bool IsSelectableArtwork(SvgVisualElement element)
    {
        if (IsLocked(element))
            return false;
        foreach (var ancestor in new[] { (SvgElement)element }.Concat(HierarchyAncestors(element)))
        {
            if (ancestor is SvgDefinitionList or SvgSymbol or SvgClipPath or SvgMask or SvgPatternServer or SvgMarker)
                return false;
            if (ancestor is SvgVisualElement visual && (visual.Display == "none" || visual.Visibility is "hidden" or "collapse"))
                return false;
        }
        return true;
    }

    /// <summary>Explicitly removes document locks in one history entry, including inherited locks.</summary>
    public void UnlockAll()
    {
        ThrowIfDisposed();
        if (Document is null)
            return;
        var locked = new[] { (SvgElement)Document }.Concat(Document.Descendants()).Where(HasOwnLock).ToArray();
        if (locked.Length == 0)
            return;
        Execute("Unlock all objects", () =>
        {
            foreach (var element in locked)
            {
                if (element.CustomAttributes.ContainsKey("data-unodraw-locked"))
                    element.CustomAttributes["data-unodraw-locked"] = "false";
                if (element.CustomAttributes.ContainsKey("data-lock"))
                    element.CustomAttributes["data-lock"] = "false";
            }
        });
    }

    /// <summary>Shows hidden artwork without changing resource definitions or locked layers.</summary>
    public void ShowAll()
    {
        ThrowIfDisposed();
        if (Document is null)
            return;
        var hidden = Document.Descendants().OfType<SvgVisualElement>().Where(element =>
            !IsEditLocked(element) &&
            !HierarchyAncestors(element).Any(parent => parent is SvgDefinitionList or SvgSymbol or SvgClipPath or SvgMask or SvgPatternServer or SvgMarker) &&
            (element.Display == "none" || element.Visibility is "hidden" or "collapse")).ToArray();
        if (hidden.Length == 0)
            return;
        Execute("Show all objects", () =>
        {
            foreach (var element in hidden)
            {
                element.Display = "inline";
                element.Visibility = "visible";
            }
        });
    }

    /// <summary>Pastes at the original position and optionally beside the selected branch.</summary>
    public void PasteAt(string? svg, SvgPastePlacement placement)
    {
        if (!Enum.IsDefined(typeof(SvgPastePlacement), placement))
            throw new ArgumentOutOfRangeException(nameof(placement));
        ThrowIfDisposed();
        if (Document is null || string.IsNullOrWhiteSpace(svg ?? Session.Clipboard.Xml))
            return;
        var anchors = TopLevelSelection();
        var anchor = placement == SvgPastePlacement.Behind ? anchors.FirstOrDefault() : anchors.LastOrDefault();
        var parent = anchor?.Parent;
        if (placement != SvgPastePlacement.InPlace && parent is not null && IsLocked(parent))
            throw new InvalidOperationException("Unlock the destination layer before pasting into it.");
        Execute(placement switch
        {
            SvgPastePlacement.InFront => "Paste in front",
            SvgPastePlacement.Behind => "Paste behind",
            _ => "Paste in place"
        }, () =>
        {
            Paste(svg, 0, 0);
            if (placement == SvgPastePlacement.InPlace || parent is null || anchor is null)
                return;
            var index = parent.Children.IndexOf(anchor) + (placement == SvgPastePlacement.InFront ? 1 : 0);
            ReparentSelection(parent, index);
        });
    }
}
