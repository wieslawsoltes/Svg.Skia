using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Svg.Skia;

internal sealed class SvgElementAddressKeyCache
{
    private readonly Dictionary<SvgElement, string?> _addressKeys = new(SvgElementReferenceComparer.Instance);
    private readonly Dictionary<SvgElement, ChildIndexLookup> _childIndexesByParent = new(SvgElementReferenceComparer.Instance);

    public string? GetOrCreate(SvgElement? element)
    {
        if (element is null || element is SvgDocument)
        {
            return null;
        }

        if (_addressKeys.TryGetValue(element, out var addressKey))
        {
            return addressKey;
        }

        var parent = element.Parent;
        var childIndex = parent is null ? -1 : GetChildIndex(parent, element);
        if (childIndex < 0)
        {
            return null;
        }

        var indexText = childIndex.ToString(CultureInfo.InvariantCulture);
        var parentAddressKey = GetOrCreate(parent);
        addressKey = parentAddressKey is null
            ? indexText
            : string.Concat(parentAddressKey, "/", indexText);

        _addressKeys[element] = addressKey;
        return addressKey;
    }

    private int GetChildIndex(SvgElement parent, SvgElement child)
    {
        if (!_childIndexesByParent.TryGetValue(parent, out var lookup) ||
            lookup.ChildCount != parent.Children.Count)
        {
            lookup = BuildChildIndexLookup(parent);
            _childIndexesByParent[parent] = lookup;
        }

        if (lookup.Indexes.TryGetValue(child, out var childIndex) &&
            childIndex >= 0 &&
            childIndex < parent.Children.Count &&
            ReferenceEquals(parent.Children[childIndex], child))
        {
            return childIndex;
        }

        lookup = BuildChildIndexLookup(parent);
        _childIndexesByParent[parent] = lookup;
        return lookup.Indexes.TryGetValue(child, out childIndex) &&
               childIndex >= 0 &&
               childIndex < parent.Children.Count &&
               ReferenceEquals(parent.Children[childIndex], child)
            ? childIndex
            : -1;
    }

    private static ChildIndexLookup BuildChildIndexLookup(SvgElement parent)
    {
        var indexes = new Dictionary<SvgElement, int>(parent.Children.Count, SvgElementReferenceComparer.Instance);
        for (var i = 0; i < parent.Children.Count; i++)
        {
            indexes[parent.Children[i]] = i;
        }

        return new ChildIndexLookup(parent.Children.Count, indexes);
    }

    private readonly record struct ChildIndexLookup(
        int ChildCount,
        Dictionary<SvgElement, int> Indexes);

    private sealed class SvgElementReferenceComparer : IEqualityComparer<SvgElement>
    {
        public static readonly SvgElementReferenceComparer Instance = new();

        public bool Equals(SvgElement? x, SvgElement? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(SvgElement obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
