namespace SvgML;

[ContentProperty("Children")]
public abstract partial class element
{
    public element()
    {
        Children.CollectionChanged += ChildrenChanged;
    }

    public elements Children { get; } = new elements();
}
