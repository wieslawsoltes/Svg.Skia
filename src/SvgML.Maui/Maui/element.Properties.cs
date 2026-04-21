namespace SvgML;

[ContentProperty(nameof(Elements))]
public abstract partial class element
{
    private readonly List<element> _attachedChildren = [];
    private elements _children = new();
    private element? _parentElement;
    private svg? _rootSvg;

    public element()
    {
        _children.CollectionChanged += ChildrenChanged;
    }

    public IList<element> Elements => _children;

    public new elements Children
    {
        get => _children;
        set
        {
            if (ReferenceEquals(_children, value))
            {
                return;
            }

            _children.CollectionChanged -= ChildrenChanged;
            _children = value ?? new elements();
            _children.CollectionChanged += ChildrenChanged;
            ReattachChildren();
            OnSvgChanged();
        }
    }

    internal element? ParentElement => _parentElement;

    internal svg? RootSvg => _rootSvg;

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
