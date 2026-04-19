using Avalonia.Metadata;

namespace SvgML;

public abstract partial class element
{
    public element()
    {
        Children.CollectionChanged += ChildrenChanged;
    }

    [Content]
    public elements Children { get; } = new elements();
}
