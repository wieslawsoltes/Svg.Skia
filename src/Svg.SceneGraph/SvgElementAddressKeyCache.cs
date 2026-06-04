using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Svg.Skia;

internal sealed class SvgElementAddressKeyCache
{
    private const int LinearChildIndexLookupLimit = 8;
    private const int CachedChildIndexTextCount = 256;
    private static readonly string[] s_cachedChildIndexTexts = CreateCachedChildIndexTexts();
    private static readonly ConditionalWeakTable<SvgElement, CachedAddressKey> s_cachedAddressKeys = new();
    private readonly Dictionary<SvgElement, string?> _addressKeys;
    private Dictionary<SvgElement, ChildIndexLookup>? _childIndexesByParent;

    public SvgElementAddressKeyCache(int initialAddressCapacity = 0)
    {
        _addressKeys = initialAddressCapacity > 0
            ? new Dictionary<SvgElement, string?>(initialAddressCapacity, SvgElementReferenceComparer.Instance)
            : new Dictionary<SvgElement, string?>(SvgElementReferenceComparer.Instance);
    }

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
        if (parent is null)
        {
            return null;
        }

        var childIndex = GetChildIndex(parent, element);
        if (childIndex < 0)
        {
            return null;
        }

        addressKey = GetOrCreateCachedChildAddressKey(element, parent, childIndex, GetOrCreate(parent));

        _addressKeys[element] = addressKey;
        return addressKey;
    }

    public string? GetOrCreateChild(SvgElement parent, int childIndex)
    {
        if (childIndex < 0 || childIndex >= parent.Children.Count)
        {
            return null;
        }

        var child = parent.Children[childIndex];
        if (!ReferenceEquals(child.Parent, parent))
        {
            return GetOrCreate(child);
        }

        if (_addressKeys.TryGetValue(child, out var addressKey))
        {
            return addressKey;
        }

        addressKey = GetOrCreateCachedChildAddressKey(child, parent, childIndex, GetOrCreate(parent));

        _addressKeys[child] = addressKey;
        return addressKey;
    }

    private static string GetOrCreateCachedChildAddressKey(
        SvgElement element,
        SvgElement parent,
        int childIndex,
        string? parentAddressKey)
    {
        var cachedAddressKey = s_cachedAddressKeys.GetOrCreateValue(element);
        if (cachedAddressKey.TryGet(parent, childIndex, parentAddressKey, out var addressKey))
        {
            return addressKey;
        }

        addressKey = CreateChildAddressKey(parentAddressKey, childIndex);
        cachedAddressKey.Set(parent, childIndex, parentAddressKey, addressKey);
        return addressKey;
    }

    private int GetChildIndex(SvgElement parent, SvgElement child)
    {
        if (parent.Children.Count <= LinearChildIndexLookupLimit)
        {
            for (var i = 0; i < parent.Children.Count; i++)
            {
                if (ReferenceEquals(parent.Children[i], child))
                {
                    return i;
                }
            }

            return -1;
        }

        var childIndexesByParent = _childIndexesByParent;
        if (childIndexesByParent is null ||
            !childIndexesByParent.TryGetValue(parent, out var lookup) ||
            lookup.ChildCount != parent.Children.Count)
        {
            lookup = BuildChildIndexLookup(parent);
            (_childIndexesByParent ??= new Dictionary<SvgElement, ChildIndexLookup>(SvgElementReferenceComparer.Instance))[parent] = lookup;
        }

        if (lookup.Indexes.TryGetValue(child, out var childIndex) &&
            childIndex >= 0 &&
            childIndex < parent.Children.Count &&
            ReferenceEquals(parent.Children[childIndex], child))
        {
            return childIndex;
        }

        lookup = BuildChildIndexLookup(parent);
        _childIndexesByParent![parent] = lookup;
        return lookup.Indexes.TryGetValue(child, out childIndex) &&
               childIndex >= 0 &&
               childIndex < parent.Children.Count &&
               ReferenceEquals(parent.Children[childIndex], child)
            ? childIndex
            : -1;
    }

    private static string CreateChildAddressKey(string? parentAddressKey, int childIndex)
    {
        var indexText = GetChildIndexText(childIndex);
        return parentAddressKey is null
            ? indexText
            : string.Concat(parentAddressKey, "/", indexText);
    }

    private static string GetChildIndexText(int childIndex)
    {
        return (uint)childIndex < (uint)s_cachedChildIndexTexts.Length
            ? s_cachedChildIndexTexts[childIndex]
            : childIndex.ToString(CultureInfo.InvariantCulture);
    }

    private static string[] CreateCachedChildIndexTexts()
    {
        var values = new string[CachedChildIndexTextCount];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = i.ToString(CultureInfo.InvariantCulture);
        }

        return values;
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

    private sealed class CachedAddressKey
    {
        private Entry? _entry;

        public bool TryGet(SvgElement parent, int childIndex, string? parentAddressKey, out string addressKey)
        {
            var entry = Volatile.Read(ref _entry);
            if (entry is not null &&
                ReferenceEquals(entry.Parent, parent) &&
                entry.ChildIndex == childIndex &&
                string.Equals(entry.ParentAddressKey, parentAddressKey, StringComparison.Ordinal))
            {
                addressKey = entry.AddressKey;
                return true;
            }

            addressKey = string.Empty;
            return false;
        }

        public void Set(SvgElement parent, int childIndex, string? parentAddressKey, string addressKey)
        {
            Volatile.Write(ref _entry, new Entry(parent, childIndex, parentAddressKey, addressKey));
        }

        private sealed class Entry(SvgElement parent, int childIndex, string? parentAddressKey, string addressKey)
        {
            public SvgElement Parent { get; } = parent;
            public int ChildIndex { get; } = childIndex;
            public string? ParentAddressKey { get; } = parentAddressKey;
            public string AddressKey { get; } = addressKey;
        }
    }

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
