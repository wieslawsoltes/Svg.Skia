namespace SvgML;

[ContentProperty("Children")]
public abstract partial class element
{
    private element? _parentElement;
    private svg? _rootSvg;

    public element()
    {
        Children.CollectionChanged += ChildrenChanged;
    }

    public elements Children { get; } = new elements();

    internal element? ParentElement => _parentElement;

    internal svg? RootSvg => _rootSvg;

    internal void AttachToTree(element? parent, svg? root)
    {
        _parentElement = parent;
        _rootSvg = root ?? this as svg;

        foreach (var child in Children)
        {
            child.AttachToTree(this, _rootSvg);
        }
    }

    internal void DetachFromTree()
    {
        foreach (var child in Children)
        {
            child.DetachFromTree();
        }

        _parentElement = null;
        _rootSvg = null;
    }

    protected virtual void OnSvgChanged()
    {
        (_rootSvg ?? this as svg)?.InvalidateSvgTree();
    }
}
