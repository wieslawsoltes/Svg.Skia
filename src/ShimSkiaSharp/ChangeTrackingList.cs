// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections;
using System.Collections.Generic;

namespace ShimSkiaSharp;

internal sealed class ChangeTrackingList<T> : IList<T>
{
    private const int InlineCapacity = 2;

    private readonly Action _onChanged;
    private List<T>? _items;
    private T? _item0;
    private T? _item1;
    private int _count;

    public ChangeTrackingList(Action onChanged)
        : this(null, onChanged)
    {
    }

    public ChangeTrackingList(IEnumerable<T>? items, Action onChanged)
    {
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            AddInitial(item);
        }
    }

    public T this[int index]
    {
        get
        {
            if (_items is not null)
            {
                return _items[index];
            }

            return index switch
            {
                0 when _count > 0 => _item0!,
                1 when _count > 1 => _item1!,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }
        set
        {
            if (_items is not null)
            {
                _items[index] = value;
                _onChanged();
                return;
            }

            switch (index)
            {
                case 0 when _count > 0:
                    _item0 = value;
                    break;
                case 1 when _count > 1:
                    _item1 = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }

            _onChanged();
        }
    }

    public int Count => _items?.Count ?? _count;

    public bool IsReadOnly => false;

    public void Add(T item)
    {
        if (_items is not null)
        {
            _items.Add(item);
            _onChanged();
            return;
        }

        if (_count < InlineCapacity)
        {
            SetInline(_count, item);
            _count++;
            _onChanged();
            return;
        }

        PromoteToList(_count + 1);
        _items!.Add(item);
        _onChanged();
    }

    public void Clear()
    {
        if (Count == 0)
        {
            return;
        }

        if (_items is not null)
        {
            _items.Clear();
        }
        else
        {
            ClearInline();
        }

        _onChanged();
    }

    public bool Contains(T item)
    {
        if (_items is not null)
        {
            return _items.Contains(item);
        }

        return IndexOfInline(item) >= 0;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (_items is not null)
        {
            _items.CopyTo(array, arrayIndex);
            return;
        }

        if (array is null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (arrayIndex < 0 || array.Length - arrayIndex < _count)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        if (_count > 0)
        {
            array[arrayIndex] = _item0!;
        }

        if (_count > 1)
        {
            array[arrayIndex + 1] = _item1!;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _items is not null
            ? _items.GetEnumerator()
            : new InlineEnumerator(_item0, _item1, _count);
    }

    public int IndexOf(T item)
    {
        return _items is not null
            ? _items.IndexOf(item)
            : IndexOfInline(item);
    }

    public void Insert(int index, T item)
    {
        if (_items is not null)
        {
            _items.Insert(index, item);
            _onChanged();
            return;
        }

        if (index < 0 || index > _count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_count >= InlineCapacity)
        {
            PromoteToList(_count + 1);
            _items!.Insert(index, item);
            _onChanged();
            return;
        }

        if (index == 0)
        {
            _item1 = _item0;
            _item0 = item;
        }
        else
        {
            _item1 = item;
        }

        _count++;
        _onChanged();
    }

    public bool Remove(T item)
    {
        if (_items is not null)
        {
            var removed = _items.Remove(item);
            if (removed)
            {
                _onChanged();
            }

            return removed;
        }

        var index = IndexOfInline(item);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        if (_items is not null)
        {
            _items.RemoveAt(index);
            _onChanged();
            return;
        }

        if (index < 0 || index >= _count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index == 0 && _count > 1)
        {
            _item0 = _item1;
        }

        _item1 = default;
        _count--;
        if (_count == 0)
        {
            _item0 = default;
        }

        _onChanged();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void AddInitial(T item)
    {
        if (_items is not null)
        {
            _items.Add(item);
            return;
        }

        if (_count < InlineCapacity)
        {
            SetInline(_count, item);
            _count++;
            return;
        }

        PromoteToList(_count + 1);
        _items!.Add(item);
    }

    private void SetInline(int index, T item)
    {
        if (index == 0)
        {
            _item0 = item;
        }
        else
        {
            _item1 = item;
        }
    }

    private void ClearInline()
    {
        _item0 = default;
        _item1 = default;
        _count = 0;
    }

    private int IndexOfInline(T item)
    {
        var comparer = EqualityComparer<T>.Default;
        if (_count > 0 && comparer.Equals(_item0!, item))
        {
            return 0;
        }

        if (_count > 1 && comparer.Equals(_item1!, item))
        {
            return 1;
        }

        return -1;
    }

    private void PromoteToList(int capacity)
    {
        var items = new List<T>(capacity);
        if (_count > 0)
        {
            items.Add(_item0!);
        }

        if (_count > 1)
        {
            items.Add(_item1!);
        }

        _item0 = default;
        _item1 = default;
        _count = 0;
        _items = items;
    }

    private sealed class InlineEnumerator : IEnumerator<T>
    {
        private readonly T? _item0;
        private readonly T? _item1;
        private readonly int _count;
        private int _index = -1;

        public InlineEnumerator(T? item0, T? item1, int count)
        {
            _item0 = item0;
            _item1 = item1;
            _count = count;
        }

        public T Current
        {
            get
            {
                return _index switch
                {
                    0 => _item0!,
                    1 => _item1!,
                    _ => throw new InvalidOperationException()
                };
            }
        }

        object? IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_index + 1 >= _count)
            {
                return false;
            }

            _index++;
            return true;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
        }
    }
}
