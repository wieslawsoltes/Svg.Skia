#nullable enable
using System.Collections.Generic;

namespace Svg;

public abstract partial class SvgElement
{
    internal bool HasStagedStylesInSubtree { get; private set; }

    internal void AddStyleCompatibility(string name, string value, int specificity)
    {
        AddStyle(name, value, specificity);
        MarkStyleSubtreeDirtyCompatibility();
    }

    internal void MarkStyleSubtreeDirtyCompatibility()
    {
        var current = this;
        while (current is not null && !current.HasStagedStylesInSubtree)
        {
            current.HasStagedStylesInSubtree = true;
            current = current._parent;
        }
    }

    internal void MergeChildStyleSubtreeCompatibility(SvgElement child)
    {
        if (child.HasStagedStylesInSubtree)
        {
            MarkStyleSubtreeDirtyCompatibility();
        }
    }

    internal bool FlushStylesCompatibility(bool children = false)
    {
        if (!HasStagedStylesInSubtree && _styles.Count == 0)
        {
            return false;
        }

        var hasUnresolvedStyles = FlushStagedStylesCompatibility();
        if (!children)
        {
            HasStagedStylesInSubtree = hasUnresolvedStyles;
            return hasUnresolvedStyles;
        }

        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (!child.HasStagedStylesInSubtree && child._styles.Count == 0)
            {
                continue;
            }

            if (child.FlushStylesCompatibility(true))
            {
                hasUnresolvedStyles = true;
            }
        }

        HasStagedStylesInSubtree = hasUnresolvedStyles;
        return hasUnresolvedStyles;
    }

    private bool FlushStagedStylesCompatibility()
    {
        if (_styles.Count == 0)
        {
            return false;
        }

        Dictionary<string, SortedDictionary<int, string>>? unresolvedStyles = null;

        foreach (var styleRule in _styles)
        {
            if (!SvgElementFactory.SetPropertyValue(this, string.Empty, styleRule.Key, GetHighestSpecificityValue(styleRule.Value), OwnerDocument, true))
            {
                unresolvedStyles ??= new Dictionary<string, SortedDictionary<int, string>>();
                unresolvedStyles.Add(styleRule.Key, styleRule.Value);
            }
        }

        if (unresolvedStyles is null)
        {
            _styles.Clear();
            return false;
        }

        _styles = unresolvedStyles;
        return true;
    }

    private static string GetHighestSpecificityValue(SortedDictionary<int, string> rules)
    {
        string? highestSpecificityValue = null;

        foreach (var styleRule in rules)
        {
            highestSpecificityValue = styleRule.Value;
        }

        return highestSpecificityValue ?? string.Empty;
    }
}
