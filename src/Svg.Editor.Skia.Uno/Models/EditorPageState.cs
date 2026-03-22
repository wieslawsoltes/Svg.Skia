using System.Collections.Generic;
using System.Collections.ObjectModel;
using Svg;
using Svg.Editor.Core;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorPageState
{
    public EditorPageState(string title, string subtitle, SvgDocument document)
    {
        Page = new EditorPageItem(title, subtitle);
        Document = document;
    }

    public EditorPageItem Page { get; }

    public SvgDocument Document { get; }

    public List<SvgVisualElement> SelectedElements { get; } = [];

    public SvgVisualElement? PrimarySelection { get; set; }

    public ObservableCollection<EditorCommentThread> CommentThreads { get; } = [];

    public EditorCommentThread? SelectedCommentThread { get; set; }

    public HashSet<SvgElement> CollapsedElements { get; } = new(ReferenceEqualityComparer.Instance);

    public double Zoom { get; set; } = 1.0;

    public double PanX { get; set; }

    public double PanY { get; set; }

    public double GridSize { get; set; } = 16.0;

    public bool IsGridVisible { get; set; } = true;

    public bool IsSnapEnabled { get; set; }

    public string? LayoutGuideStyleId { get; set; }

    public string? LayoutGuideStyleLibraryId { get; set; }
}
