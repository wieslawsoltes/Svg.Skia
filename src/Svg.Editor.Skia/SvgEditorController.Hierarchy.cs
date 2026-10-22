using System;
using System.Collections.Generic;
using System.Linq;
using Svg.Editor.Core;
using Svg.Transforms;
using SK = SkiaSharp;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>Moves the selected branches to a group or layer in one undoable operation.</summary>
    /// <param name="parent">The destination container in this document.</param>
    /// <param name="index">Insertion index after removing moved branches; -1 appends.</param>
    /// <param name="preserveWorldPosition">Compensates for the change of parent transform.</param>
    public void ReparentSelection(SvgElement parent, int index = -1, bool preserveWorldPosition = true)
        => MoveHierarchyElements(_selection, parent, index, preserveWorldPosition);

    /// <summary>
    /// Reorders or reparents SVG branches, retaining identity, references, painter order and
    /// inherited paint. Ancestor/descendant input is reduced to its outermost branches.
    /// Destination group compositing intentionally applies to its new children.
    /// </summary>
    public void MoveHierarchyElements(IEnumerable<SvgElement> elements, SvgElement parent, int index = -1,
        bool preserveWorldPosition = true)
    {
        ThrowIfDisposed();
        if (elements is null)
            throw new ArgumentNullException(nameof(elements));
        if (parent is null)
            throw new ArgumentNullException(nameof(parent));
        if (Document is null || (!ReferenceEquals(parent, Document) && !BelongsToDocument(parent)))
            throw new InvalidOperationException("The destination must belong to the current document.");
        if (IsLocked(parent))
            throw new InvalidOperationException("Unlock the destination layer before moving objects into it.");

        var requested = new HashSet<SvgElement>(elements);
        if (requested.Count == 0)
            return;
        foreach (var element in requested)
        {
            if (ReferenceEquals(element, Document) || !BelongsToDocument(element) || element.Parent is null)
                throw new InvalidOperationException("Only attached document branches can be moved.");
            EnsureElementEditable(element);
        }
        var moving = Document.Descendants().Where(element => requested.Contains(element) &&
            !HierarchyAncestors(element).Any(requested.Contains)).ToArray();
        foreach (var element in moving)
        {
            if (ReferenceEquals(element, parent) || HierarchyAncestors(parent).Contains(element))
                throw new InvalidOperationException("An object cannot be moved into itself or one of its descendants.");
            if (!CanContainHierarchyElement(parent, element))
                throw new InvalidOperationException("This SVG element cannot contain the dragged content.");
        }
        var movingSet = new HashSet<SvgElement>(moving);
        var remaining = parent.Children.Where(element => !movingSet.Contains(element)).ToList();
        if (index == -1)
            index = remaining.Count;
        if (index < 0 || index > remaining.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var finalOrder = remaining.ToList();
        finalOrder.InsertRange(index, moving);
        if (moving.All(element => ReferenceEquals(element.Parent, parent)) && parent.Children.SequenceEqual(finalOrder))
            return;

        var deltas = new Dictionary<SvgVisualElement, SK.SKMatrix>();
        if (preserveWorldPosition && moving.Any(element => element is SvgVisualElement && !ReferenceEquals(element.Parent, parent)))
        {
            var destination = GetHierarchyWorldTransform(parent);
            if (!destination.TryInvert(out var inverse))
                throw new InvalidOperationException("The destination has a singular transform. Restore its scale before moving objects into it.");
            foreach (var visual in moving.OfType<SvgVisualElement>())
            {
                if (ReferenceEquals(visual.Parent, parent))
                    continue;
                var oldParent = GetSceneNode(visual)?.Parent is { } node
                    ? ToMatrix(node.TotalTransform) : GetHierarchyWorldTransform(visual.Parent!);
                var delta = Multiply(inverse, oldParent);
                ValidateMatrix(delta);
                deltas.Add(visual, delta);
            }
        }
        Execute("Move document branches", () =>
        {
            // Resolve inherited values before detaching any branch, including descendants
            // selected by ancestor-dependent CSS. Existing explicit style declarations stay intact.
            foreach (var element in moving.Where(element => !ReferenceEquals(element.Parent, parent)))
                foreach (var visual in new[] { element }.Concat(element.Descendants()).OfType<SvgVisualElement>())
                    MaterializePresentation(visual, visual);
            foreach (var element in moving)
                element.Parent!.Children.Remove(element);
            foreach (var element in moving)
            {
                if (element is SvgVisualElement visual && deltas.TryGetValue(visual, out var delta))
                    PrependTransform(visual, delta);
                parent.Children.Insert(index++, element);
            }
            _selection.Clear();
            _selection.AddRange(moving.OfType<SvgVisualElement>());
        });
    }

    /// <summary>Checks SVG content categories without changing the document.</summary>
    public static bool CanContainHierarchyElement(SvgElement parent, SvgElement child)
    {
        if (child is SvgDocument || ReferenceEquals(parent, child))
            return false;
        if (ReferenceEquals(parent, child.Parent))
            return true;
        if (parent is SvgGradientServer)
            return child is SvgGradientStop;
        if (child is SvgGradientStop)
            return false;
        if (parent is SvgDefinitionList)
            return true;
        if (parent is SvgTextBase)
            return child is SvgTextSpan or SvgTextPath;
        if (parent is SvgDocument or SvgGroup or SvgSymbol or SvgPatternServer or SvgClipPath or SvgMask)
            return child is SvgVisualElement or SvgDefinitionList ||
                SvgElementInfo.GetElementName(child.GetType()) is "style" or "metadata" or "title" or "desc";
        return false;
    }

    private static IEnumerable<SvgElement> HierarchyAncestors(SvgElement element)
    {
        for (var current = element.Parent; current is not null; current = current.Parent)
            yield return current;
    }

    private SK.SKMatrix GetHierarchyWorldTransform(SvgElement element)
    {
        if (GetSceneNode(element) is { } scene)
            return ToMatrix(scene.TotalTransform);
        if (ReferenceEquals(element, Document))
            return Renderer.RetainedSceneGraph is { } graph ? ToMatrix(graph.Root.TotalTransform) : SK.SKMatrix.Identity;
        var parent = element.Parent is { } ancestor ? GetHierarchyWorldTransform(ancestor) : SK.SKMatrix.Identity;
        if (element is not SvgVisualElement visual || visual.Transforms is null)
            return parent;
        // Hidden and resource-only branches may not have a retained scene node.
        var local = SK.SKMatrix.Identity;
        foreach (var transform in visual.Transforms)
        {
            var next = transform switch
            {
                SvgMatrix m when m.Points.Count == 6 => new SK.SKMatrix(m.Points[0], m.Points[2], m.Points[4], m.Points[1], m.Points[3], m.Points[5], 0, 0, 1),
                SvgTranslate t => SK.SKMatrix.CreateTranslation(t.X, t.Y),
                SvgScale s => SK.SKMatrix.CreateScale(s.X, s.Y),
                SvgRotate r => SK.SKMatrix.CreateRotationDegrees(r.Angle, r.CenterX, r.CenterY),
                SvgSkew s => SK.SKMatrix.CreateSkew((float)Math.Tan(s.AngleX * Math.PI / 180), (float)Math.Tan(s.AngleY * Math.PI / 180)),
                _ => throw new InvalidOperationException("The branch contains an unsupported transform.")
            };
            local = Multiply(local, next);
        }
        return Multiply(parent, local);
    }
}
