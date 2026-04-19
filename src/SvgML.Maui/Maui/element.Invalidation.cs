using System.Collections.Specialized;

namespace SvgML;

public abstract partial class element
{
    protected virtual void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                // TODO:
                // LogicalChildren.InsertRange(e.NewStartingIndex, e.NewItems!.OfType<Control>().ToList());
                // VisualChildren.InsertRange(e.NewStartingIndex, e.NewItems!.OfType<Visual>());

                foreach (var element in e.NewItems!.OfType<Element>().ToList())
                {
                    AddLogicalChild(element);
                }
                break;

            case NotifyCollectionChangedAction.Move:
                // TODO:
                // LogicalChildren.MoveRange(e.OldStartingIndex, e.OldItems!.Count, e.NewStartingIndex);
                // VisualChildren.MoveRange(e.OldStartingIndex, e.OldItems!.Count, e.NewStartingIndex);

                break;

            case NotifyCollectionChangedAction.Remove:
                // TODO:
                // LogicalChildren.RemoveAll(e.OldItems!.OfType<Control>().ToList());
                // VisualChildren.RemoveAll(e.OldItems!.OfType<Visual>());
                
                foreach (var element in e.NewItems!.OfType<Element>().ToList())
                {
                    RemoveLogicalChild(element);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // TODO:
                for (var i = 0; i < e.OldItems!.Count; ++i)
                {
                    var index = i + e.OldStartingIndex;
                    var child = (Element)e.NewItems![i]!;
                    // InsertLogicalChild(index, child);

                    // LogicalChildren[index] = child;
                    // VisualChildren[index] = child;
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                throw new NotSupportedException();
        }

        Invalidate();
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        Invalidate();
    }

    protected virtual void Invalidate()
    {
        (Parent as element)?.Invalidate();
    }
}
