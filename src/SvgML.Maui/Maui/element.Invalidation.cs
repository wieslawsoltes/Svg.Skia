using System.Collections;
using System.Collections.Specialized;

namespace SvgML;

public abstract partial class element
{
    protected virtual void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                AttachItems(e.NewItems);
                break;

            case NotifyCollectionChangedAction.Remove:
                DetachItems(e.OldItems);
                break;

            case NotifyCollectionChangedAction.Replace:
                DetachItems(e.OldItems);
                AttachItems(e.NewItems);
                break;

            case NotifyCollectionChangedAction.Move:
                break;

            case NotifyCollectionChangedAction.Reset:
                ReattachChildren();
                break;
        }

        OnSvgChanged();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (this is not svg)
        {
            OnSvgChanged();
        }
    }

    private void AttachItems(IList? items)
    {
        if (items is null)
        {
            return;
        }

        var root = RootSvg ?? this as svg;
        foreach (var item in items)
        {
            if (item is element child)
            {
                child.AttachToTree(this, root);
                if (!_attachedChildren.Contains(child))
                {
                    _attachedChildren.Add(child);
                }
            }
        }
    }

    private void DetachItems(IList? items)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item is element child)
            {
                child.DetachFromTree();
                _attachedChildren.Remove(child);
            }
        }
    }

    private void ReattachChildren()
    {
        foreach (var child in _attachedChildren.ToArray())
        {
            child.DetachFromTree();
        }

        _attachedChildren.Clear();

        var root = RootSvg ?? this as svg;
        foreach (var child in Children)
        {
            child.AttachToTree(this, root);
            _attachedChildren.Add(child);
        }
    }
}
