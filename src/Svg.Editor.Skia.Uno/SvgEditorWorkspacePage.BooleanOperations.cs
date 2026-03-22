using SkiaSharp;
using Svg;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public partial class SvgEditorWorkspacePage
{
    public bool CanBooleanCombineSelection => CanApplyBooleanPathOperation(GetSelectionRoots());

    public bool CanFlattenSelectionToPath => CanFlattenPathSelection(GetSelectionRoots());

    public bool CanShowVectorOperations => CanBooleanCombineSelection || CanFlattenSelectionToPath;

    private static bool CanConvertSelectionToPaths(IReadOnlyList<SvgVisualElement> selectionRoots)
    {
        return selectionRoots.Count > 0
            && selectionRoots.All(static element => PathService.ElementToPath(element) is not null);
    }

    private static bool CanApplyBooleanPathOperation(IReadOnlyList<SvgVisualElement> selectionRoots)
    {
        return selectionRoots.Count >= 2
            && TryGetSelectionParent(selectionRoots, out _)
            && CanConvertSelectionToPaths(selectionRoots);
    }

    private static bool CanFlattenPathSelection(IReadOnlyList<SvgVisualElement> selectionRoots)
    {
        return selectionRoots.Count > 0
            && TryGetSelectionParent(selectionRoots, out _)
            && CanConvertSelectionToPaths(selectionRoots);
    }

    private void ApplyBooleanSelection(SKPathOp op, string operationLabel, string idPrefix)
    {
        var selectionRoots = GetSelectionRoots();
        if (selectionRoots.Count < 2)
        {
            CanvasStatus = $"{operationLabel} requires at least two selected sibling layers.";
            return;
        }

        if (!TryGetSelectionParent(selectionRoots, out var parent))
        {
            CanvasStatus = $"{operationLabel} requires the selected layers to share a single parent.";
            return;
        }

        var ordered = selectionRoots.OrderBy(parent!.Children.IndexOf).ToList();
        var converted = new List<SvgPath>(ordered.Count);
        foreach (var element in ordered)
        {
            if (!TryConvertElementToPath(element, out var path))
            {
                CanvasStatus = "Only geometric vector layers can be combined right now.";
                return;
            }

            converted.Add(path);
        }

        var result = converted[0];
        for (int index = 1; index < converted.Count; index++)
        {
            var updated = _pathService.ApplyPathOp(result, converted[index], op);
            if (updated is null)
            {
                CanvasStatus = $"Unable to apply {operationLabel.ToLowerInvariant()} to the current selection.";
                return;
            }

            result = updated;
        }

        if (string.IsNullOrWhiteSpace(result.ID))
        {
            result.ID = CreateUniqueId(idPrefix);
        }

        var insertIndex = ordered.Min(parent.Children.IndexOf);
        foreach (var element in ordered.OrderByDescending(parent.Children.IndexOf))
        {
            parent.Children.Remove(element);
        }

        if (insertIndex >= parent.Children.Count)
        {
            parent.Children.Add(result);
        }
        else
        {
            parent.Children.Insert(insertIndex, result);
        }

        RefreshDocumentVisual(rebuildOutline: true, reloadProperties: true);
        ApplySelection([result], result);
        CanvasStatus = $"Applied {operationLabel.ToLowerInvariant()} to {ordered.Count} layers.";
    }
}
