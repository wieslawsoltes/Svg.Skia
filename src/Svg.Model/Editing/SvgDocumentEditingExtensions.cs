// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using Svg;

namespace Svg.Model.Editing;

public static class SvgDocumentEditingExtensions
{
    public static IEnumerable<SvgElement> TraverseElements(this SvgDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var stack = new Stack<SvgElement>();
        stack.Push(document);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            if (current.Children is null || current.Children.Count == 0)
            {
                continue;
            }

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    public static int UpdateStyleAttributes(
        this SvgDocument document,
        Func<SvgVisualElement, bool> predicate,
        Action<SvgVisualElement> update)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        var count = 0;
        foreach (var element in document.TraverseElements())
        {
            if (element is SvgVisualElement visual && predicate(visual))
            {
                update(visual);
                count++;
            }
        }

        return count;
    }
}
