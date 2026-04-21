using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace SvgML;

[ContentProperty(Name = nameof(ContentNodes))]
[System.Windows.Markup.ContentWrapper(typeof(content))]
public abstract partial class element
{
    private readonly List<element> _attachedChildren = [];
    private element? _parentElement;
    private svg? _rootSvg;

    protected element()
    {
        ContentNodes = new SvgContentCollection(Children);
        Children.CollectionChanged += ChildrenChanged;
    }

    public elements Children { get; } = new elements();

    public SvgContentCollection ContentNodes { get; }

    internal element? ParentElement => _parentElement;

    internal svg? RootSvg => _rootSvg;

    protected static void OnSvgPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is element element)
        {
            element.OnSvgChanged();
        }
    }

    internal void AttachToTree(element? parent, svg? root)
    {
        _parentElement = parent;
        _rootSvg = root ?? this as svg;

        _attachedChildren.Clear();
        foreach (var child in Children)
        {
            child.AttachToTree(this, _rootSvg);
            _attachedChildren.Add(child);
        }
    }

    internal void DetachFromTree()
    {
        foreach (var child in _attachedChildren)
        {
            child.DetachFromTree();
        }

        _attachedChildren.Clear();
        _parentElement = null;
        _rootSvg = null;
    }

    protected virtual void OnSvgChanged()
    {
        (_rootSvg ?? this as svg)?.InvalidateSvgTree();
    }
}

[System.Windows.Markup.WhitespaceSignificantCollection]
public sealed class SvgContentCollection : IList<object>, IList
{
    private readonly elements _children;

    internal SvgContentCollection(elements children)
    {
        _children = children;
    }

    public int Count => _children.Count;

    public bool IsReadOnly => false;

    bool IList.IsFixedSize => false;

    bool IList.IsReadOnly => false;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    public object this[int index]
    {
        get => _children[index];
        set => _children[index] = ToElement(value);
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => this[index] = value!;
    }

    public void Add(object item)
    {
        if (TryCreateElement(item, out var element))
        {
            _children.Add(element);
        }
    }

    int IList.Add(object? value)
    {
        Add(value!);
        return _children.Count - 1;
    }

    public void Clear()
    {
        _children.Clear();
    }

    public bool Contains(object item)
    {
        return item is element element
            ? _children.Contains(element)
            : item is string text && _children.OfType<content>().Any(x => x.Content == text);
    }

    bool IList.Contains(object? value)
    {
        return value is not null && Contains(value);
    }

    public void CopyTo(object[] array, int arrayIndex)
    {
        for (var i = 0; i < _children.Count; i++)
        {
            array[arrayIndex + i] = _children[i];
        }
    }

    void ICollection.CopyTo(Array array, int index)
    {
        for (var i = 0; i < _children.Count; i++)
        {
            array.SetValue(_children[i], index + i);
        }
    }

    public IEnumerator<object> GetEnumerator()
    {
        foreach (var child in _children)
        {
            yield return child;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int IndexOf(object item)
    {
        return item is element element ? _children.IndexOf(element) : -1;
    }

    int IList.IndexOf(object? value)
    {
        return value is not null ? IndexOf(value) : -1;
    }

    public void Insert(int index, object item)
    {
        if (TryCreateElement(item, out var element))
        {
            _children.Insert(index, element);
        }
    }

    void IList.Insert(int index, object? value)
    {
        Insert(index, value!);
    }

    public bool Remove(object item)
    {
        if (item is not element element)
        {
            return false;
        }

        return _children.Remove(element);
    }

    void IList.Remove(object? value)
    {
        if (value is not null)
        {
            Remove(value);
        }
    }

    public void RemoveAt(int index)
    {
        _children.RemoveAt(index);
    }

    private static element ToElement(object? item)
    {
        return TryCreateElement(item, out var element)
            ? element
            : throw new ArgumentException("SvgML content can only contain SvgML elements or non-whitespace text.", nameof(item));
    }

    private static bool TryCreateElement(object? item, out element element)
    {
        switch (item)
        {
            case element child:
                element = child;
                return true;
            case string text when !string.IsNullOrWhiteSpace(text):
                element = new content
                {
                    Content = text
                };
                return true;
            default:
                element = null!;
                return false;
        }
    }
}
