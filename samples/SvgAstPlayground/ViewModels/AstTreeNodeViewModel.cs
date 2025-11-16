using System.Collections.ObjectModel;
using Svg.Ast;

namespace SvgAstPlayground.ViewModels;

public sealed class AstTreeNodeViewModel
{
    public AstTreeNodeViewModel(string name, string? details = null, SvgAstNode? node = null)
    {
        Name = name;
        Details = details;
        Node = node;
        Children = new ObservableCollection<AstTreeNodeViewModel>();
    }

    public string Name { get; }

    public string? Details { get; }

    public SvgAstNode? Node { get; }

    public AstTreeNodeViewModel? Parent { get; private set; }

    public ObservableCollection<AstTreeNodeViewModel> Children { get; }

    public void AddChild(AstTreeNodeViewModel child)
    {
        child.Parent = this;
        Children.Add(child);
    }
}
