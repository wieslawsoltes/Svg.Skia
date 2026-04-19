using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;

namespace SvgML;

public abstract partial class element
{
    protected virtual void ChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                LogicalChildren.InsertRange(e.NewStartingIndex, e.NewItems!.OfType<Control>().ToList());
                VisualChildren.InsertRange(e.NewStartingIndex, e.NewItems!.OfType<Visual>());
                break;

            case NotifyCollectionChangedAction.Move:
                LogicalChildren.MoveRange(e.OldStartingIndex, e.OldItems!.Count, e.NewStartingIndex);
                VisualChildren.MoveRange(e.OldStartingIndex, e.OldItems!.Count, e.NewStartingIndex);
                break;

            case NotifyCollectionChangedAction.Remove:
                LogicalChildren.RemoveAll(e.OldItems!.OfType<Control>().ToList());
                VisualChildren.RemoveAll(e.OldItems!.OfType<Visual>());
                break;

            case NotifyCollectionChangedAction.Replace:
                for (var i = 0; i < e.OldItems!.Count; ++i)
                {
                    var index = i + e.OldStartingIndex;
                    var child = (Control)e.NewItems![i]!;
                    LogicalChildren[index] = child;
                    VisualChildren[index] = child;
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                throw new NotSupportedException();
        }

        Invalidate();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        Invalidate();
    }

    protected virtual void Invalidate()
    {
        (Parent as element)?.Invalidate();
    }
}
