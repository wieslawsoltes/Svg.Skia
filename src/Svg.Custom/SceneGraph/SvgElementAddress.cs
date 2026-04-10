using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#nullable enable

namespace Svg;

internal sealed class SvgElementAddress
{
    public SvgElementAddress(int[] childIndexes)
    {
        ChildIndexes = childIndexes;
    }

    public int[] ChildIndexes { get; }

    public string Key => string.Join("/", ChildIndexes.Select(static index => index.ToString(CultureInfo.InvariantCulture)));

    public static SvgElementAddress Create(SvgElement element)
    {
        var indexes = new Stack<int>();
        var current = element;

        while (current.Parent is { } parent)
        {
            indexes.Push(parent.Children.IndexOf(current));
            current = parent;
        }

        return new SvgElementAddress(indexes.ToArray());
    }

    public SvgElement? Resolve(SvgDocument document)
    {
        SvgElement current = document;

        foreach (var childIndex in ChildIndexes)
        {
            if (childIndex < 0 || childIndex >= current.Children.Count)
            {
                return null;
            }

            current = current.Children[childIndex];
        }

        return current;
    }
}
