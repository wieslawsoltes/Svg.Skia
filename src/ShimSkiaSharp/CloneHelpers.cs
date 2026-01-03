// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;

namespace ShimSkiaSharp;

internal static class CloneHelpers
{
    public static T[]? CloneArray<T>(T[]? source)
    {
        if (source is null)
        {
            return null;
        }

        return (T[])source.Clone();
    }

    public static T[]? CloneArray<T>(T[]? source, Func<T, T> clone)
    {
        if (source is null)
        {
            return null;
        }

        var result = new T[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            result[i] = clone(source[i]);
        }

        return result;
    }

    public static IList<T>? CloneList<T>(IList<T>? source, Func<T, T> clone)
    {
        if (source is null)
        {
            return null;
        }

        var result = new List<T>(source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            result.Add(clone(source[i]));
        }

        return result;
    }

    public static IList<T>? CloneList<T>(IList<T>? source)
    {
        if (source is null)
        {
            return null;
        }

        return new List<T>(source);
    }
}
