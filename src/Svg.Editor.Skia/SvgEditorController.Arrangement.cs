using System;
using System.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>Selected descendants are excluded when their selected ancestor moves with them.</summary>
    public SvgVisualElement[] GetArrangementElements()
    {
        ThrowIfDisposed(); return TopLevelSelection();
    }

    public void AlignSelection(SvgAlignment alignment, SvgAlignmentTarget target = SvgAlignmentTarget.Selection,
        SvgVisualElement? keyObject = null)
    {
        ThrowIfDisposed();
        if (!Enum.IsDefined(typeof(SvgAlignmentTarget), target)) throw new ArgumentOutOfRangeException(nameof(target));
        if (!Enum.IsDefined(typeof(SvgAlignment), alignment)) throw new ArgumentOutOfRangeException(nameof(alignment));
        var targets = CaptureArrangement();
        if (targets.Length == 0) return;
        var bounds = targets.Select(item => item.Bounds).ToArray();
        var fixedIndex = -1;
        SKRect reference;
        switch (target)
        {
            case SvgAlignmentTarget.Artboard:
                reference = Renderer.Picture?.CullRect ?? throw new InvalidOperationException("The document has no rendered artboard.");
                break;
            case SvgAlignmentTarget.KeyObject:
                fixedIndex = Array.FindIndex(targets, item => ReferenceEquals(item.Element, keyObject));
                if (fixedIndex < 0) throw new ArgumentException("Choose a top-level selected object as the fixed key.", nameof(keyObject));
                reference = bounds[fixedIndex];
                break;
            default:
                reference = SvgArrangementService.Union(bounds);
                break;
        }
        ApplyArrangement("Align objects to " + target, targets.Select(item => item.Element).ToArray(),
            SvgArrangementService.Align(bounds, reference, alignment, fixedIndex));
    }

    public void DistributeSelectionCenters(SvgArrangementAxis axis)
    {
        ThrowIfDisposed();
        var targets = CaptureArrangement();
        var deltas = SvgArrangementService.DistributeCenters(targets.Select(item => item.Bounds).ToArray(), axis);
        ApplyArrangement("Distribute object centers", targets.Select(item => item.Element).ToArray(), deltas);
    }

    public void SpaceSelection(SvgArrangementAxis axis, double? gap = null, SvgVisualElement? keyObject = null)
    {
        ThrowIfDisposed();
        var targets = CaptureArrangement();
        var fixedIndex = keyObject is null ? -1 : Array.FindIndex(targets, item => ReferenceEquals(item.Element, keyObject));
        if (keyObject is not null && fixedIndex < 0) throw new ArgumentException("Choose a top-level selected object as the fixed key.", nameof(keyObject));
        var deltas = SvgArrangementService.DistributeGaps(targets.Select(item => item.Bounds).ToArray(), axis, gap, fixedIndex);
        ApplyArrangement(gap is null ? "Equalize object gaps" : "Set object spacing", targets.Select(item => item.Element).ToArray(), deltas);
    }

    private (SvgVisualElement Element, SKRect Bounds)[] CaptureArrangement()
    {
        if (IsEditing) throw new InvalidOperationException("Complete the current gesture before arranging objects.");
        EnsureSelectionEditable();
        var elements = TopLevelSelection();
        var geometry = GetSelectionGeometry();
        if (geometry.Length != elements.Length)
            throw new InvalidOperationException("Every selected object must have rendered bounds before arranging it.");
        return geometry;
    }

    private void ApplyArrangement(string label, SvgVisualElement[] elements, SKPoint[] translations)
    {
        // Preflight every local matrix before any live mutation. In particular, a
        // later singular parent must not leave earlier objects partially translated.
        var changes = elements.Select((element, index) => (Element: element, Delta: translations[index]))
            .Where(item => item.Delta.X != 0 || item.Delta.Y != 0).Select(item =>
            {
                var node = GetSceneNode(item.Element) ?? throw new InvalidOperationException("The selected object has no retained geometry.");
                var matrix = node.Parent is { } parent ? ToMatrix(parent.TotalTransform) : SKMatrix.Identity;
                ValidateMatrix(matrix);
                if (!matrix.TryInvert(out var inverse)) throw new InvalidOperationException("An object inside a singular transform cannot be arranged.");
                var transform = Multiply(Multiply(inverse, SKMatrix.CreateTranslation(item.Delta.X, item.Delta.Y)), matrix);
                ValidateMatrix(transform);
                return (item.Element, Transform: transform);
            }).ToArray();
        if (changes.Length == 0) return;
        Execute(label, () =>
        {
            foreach (var change in changes) PrependTransform(change.Element, change.Transform);
        });
    }
}
