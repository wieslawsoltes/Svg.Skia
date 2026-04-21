namespace SvgML;

[ContentProperty("Children")]
public abstract partial class element
{
    private readonly List<element> _attachedChildren = [];
    private element? _parentElement;
    private svg? _rootSvg;

    public element()
    {
        Children.CollectionChanged += ChildrenChanged;
    }

    public new elements Children { get; } = new elements();

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
