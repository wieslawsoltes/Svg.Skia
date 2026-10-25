using System;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>Changes an outline row's visibility while retaining the current selection.</summary>
    public void SetHierarchyVisibility(SvgVisualElement element, bool visible)
    {
        ValidateHierarchyStateTarget(element);
        EnsureElementEditable(element);
        var display = visible ? "inline" : "none";
        var visibility = visible ? "visible" : "hidden";
        if (element.Display == display && element.Visibility == visibility) return;
        Execute(visible ? "Show layer object" : "Hide layer object", () =>
        {
            element.Display = display;
            element.Visibility = visibility;
        });
    }

    /// <summary>
    /// Changes an outline row's own lock. An inherited lock must be removed on its
    /// owning ancestor; clicking a child must not silently unlock an entire layer.
    /// </summary>
    public void SetHierarchyLocked(SvgElement element, bool locked)
    {
        ValidateHierarchyStateTarget(element);
        if (element.Parent is { } parent && IsLocked(parent))
            throw new InvalidOperationException("This lock is inherited. Unlock the parent layer first.");
        // Reassigning this object's existing state is a no-op, not an edit of a
        // locked object. Descendant-lock validation still protects an actual change.
        if (HasOwnLock(element) == locked) return;
        if (locked) EnsureElementEditable(element);
        Execute(locked ? "Lock layer object" : "Unlock layer object", () =>
        {
            element.CustomAttributes["data-unodraw-locked"] = locked ? "true" : "false";
            if (element.CustomAttributes.ContainsKey("data-lock"))
                element.CustomAttributes["data-lock"] = locked ? "true" : "false";
        });
    }

    private void ValidateHierarchyStateTarget(SvgElement element)
    {
        ThrowIfDisposed();
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (Document is null || ReferenceEquals(element, Document) || !BelongsToDocument(element))
            throw new ArgumentException("The layer object must belong to the current document.", nameof(element));
    }
}
