// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using Svg.Model.Drawables;
using Svg.Model.Drawables.Elements;

namespace Svg.Model.Editing;

public static class DrawableWalker
{
    public static IEnumerable<DrawableBase> Traverse(DrawableBase root)
    {
        if (root is null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        var visited = new HashSet<DrawableBase>(ReferenceEqualityComparer.Instance);
        return TraverseIterator(new[] { root }, visited);
    }

    public static IEnumerable<DrawableBase> Traverse(IEnumerable<DrawableBase> roots)
    {
        if (roots is null)
        {
            throw new ArgumentNullException(nameof(roots));
        }

        var visited = new HashSet<DrawableBase>(ReferenceEqualityComparer.Instance);
        return TraverseIterator(roots, visited);
    }

    private static IEnumerable<DrawableBase> TraverseIterator(IEnumerable<DrawableBase> roots, HashSet<DrawableBase> visited)
    {
        var stack = new Stack<DrawableBase>();
        foreach (var root in roots)
        {
            if (root is { })
            {
                stack.Push(root);
            }
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            yield return current;

            foreach (var child in EnumerateChildren(current))
            {
                if (child is { })
                {
                    stack.Push(child);
                }
            }
        }
    }

    private static IEnumerable<DrawableBase> EnumerateChildren(DrawableBase drawable)
    {
        if (drawable.MaskDrawable is { } maskDrawable)
        {
            yield return maskDrawable;
        }

        switch (drawable)
        {
            case DrawableContainer container:
                for (var i = container.ChildrenDrawables.Count - 1; i >= 0; i--)
                {
                    yield return container.ChildrenDrawables[i];
                }
                break;
            case DrawablePath pathDrawable:
                if (pathDrawable.MarkerDrawables is { })
                {
                    for (var i = pathDrawable.MarkerDrawables.Count - 1; i >= 0; i--)
                    {
                        yield return pathDrawable.MarkerDrawables[i];
                    }
                }
                break;
            case UseDrawable useDrawable:
                if (useDrawable.ReferencedDrawable is { } referenced)
                {
                    yield return referenced;
                }
                break;
            case SwitchDrawable switchDrawable:
                if (switchDrawable.FirstChild is { } firstChild)
                {
                    yield return firstChild;
                }
                break;
            case MarkerDrawable markerDrawable:
                if (markerDrawable.MarkerElementDrawable is { } markerElement)
                {
                    yield return markerElement;
                }
                break;
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<DrawableBase>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public bool Equals(DrawableBase? x, DrawableBase? y) => ReferenceEquals(x, y);

        public int GetHashCode(DrawableBase obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
