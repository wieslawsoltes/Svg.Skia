using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace SvgML;

[ContentProperty(Name = nameof(Children))]
public abstract partial class element : SKCanvasElement
{
    private element? _parentElement;
    private svg? _rootSvg;

    protected element()
    {
        Children.CollectionChanged += ChildrenChanged;
    }

    public elements Children { get; } = new elements();

    internal element? ParentElement => _parentElement;

    internal svg? RootSvg => _rootSvg;

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
    }

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
