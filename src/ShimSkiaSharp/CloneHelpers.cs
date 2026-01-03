// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ShimSkiaSharp;

internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static readonly ReferenceEqualityComparer Instance = new();

    private ReferenceEqualityComparer()
    {
    }

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

    public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}

internal sealed class CloneContext
{
    private readonly Dictionary<object, object> _clones = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _inProgress = new(ReferenceEqualityComparer.Instance);

    public bool TryGet<T>(T source, out T clone) where T : class
    {
        if (_clones.TryGetValue(source, out var existing))
        {
            clone = (T)existing;
            return true;
        }

        clone = null!;
        return false;
    }

    public void Add<T>(T source, T clone) where T : class
    {
        _clones[source] = clone;
    }

    public T GetOrAdd<T>(T source, Func<T> create) where T : class
    {
        if (TryGet(source, out T existing))
        {
            return existing;
        }

        var clone = create();
        Add(source, clone);
        return clone;
    }

    public void Enter(object source)
    {
        if (!_inProgress.Add(source))
        {
            throw new NotSupportedException($"Cyclic clone detected for {source.GetType().Name}.");
        }
    }

    public void Exit(object source)
    {
        _inProgress.Remove(source);
    }
}

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

    public static T[]? CloneArray<T>(T[]? source, CloneContext context)
    {
        if (source is null)
        {
            return null;
        }

        if (context.TryGet(source, out T[] existing))
        {
            return existing;
        }

        var result = (T[])source.Clone();
        context.Add(source, result);
        return result;
    }

    public static T[]? CloneArray<T>(T[]? source, CloneContext context, Func<T, T> clone)
    {
        if (source is null)
        {
            return null;
        }

        if (context.TryGet(source, out T[] existing))
        {
            return existing;
        }

        var result = new T[source.Length];
        context.Add(source, result);
        for (var i = 0; i < source.Length; i++)
        {
            var item = source[i];
            result[i] = item is null ? item! : clone(item);
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

    public static IList<T>? CloneList<T>(IList<T>? source, CloneContext context)
    {
        if (source is null)
        {
            return null;
        }

        if (context.TryGet(source, out IList<T> existing))
        {
            return existing;
        }

        var result = new List<T>(source);
        context.Add(source, result);
        return result;
    }

    public static IList<T>? CloneList<T>(IList<T>? source, CloneContext context, Func<T, T> clone)
    {
        if (source is null)
        {
            return null;
        }

        if (context.TryGet(source, out IList<T> existing))
        {
            return existing;
        }

        var result = new List<T>(source.Count);
        context.Add(source, result);
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            result.Add(item is null ? item! : clone(item));
        }

        return result;
    }
}
