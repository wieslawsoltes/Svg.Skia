// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections;
using System.Collections.Generic;

namespace ShimSkiaSharp;

internal sealed class ChangeTrackingList<T> : IList<T>
{
    private readonly List<T> _items;
    private readonly Action _onChanged;

    public ChangeTrackingList(Action onChanged)
        : this(null, onChanged)
    {
    }

    public ChangeTrackingList(IEnumerable<T>? items, Action onChanged)
    {
        _items = items is null ? new List<T>() : new List<T>(items);
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
    }

    public T this[int index]
    {
        get => _items[index];
        set
        {
            _items[index] = value;
            _onChanged();
        }
    }

    public int Count => _items.Count;

    public bool IsReadOnly => false;

    public void Add(T item)
    {
        _items.Add(item);
        _onChanged();
    }

    public void Clear()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _items.Clear();
        _onChanged();
    }

    public bool Contains(T item) => _items.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    public int IndexOf(T item) => _items.IndexOf(item);

    public void Insert(int index, T item)
    {
        _items.Insert(index, item);
        _onChanged();
    }

    public bool Remove(T item)
    {
        var removed = _items.Remove(item);
        if (removed)
        {
            _onChanged();
        }

        return removed;
    }

    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        _onChanged();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
