using System;
using System.Linq;

namespace Svg.Editor.Skia;

/// <summary>
/// A borrowed target identity for nonmodal editors. Geometry/style changes on the same
/// objects are allowed; document replacement, selection changes and another controller
/// invalidate the context. Capturing and checking must happen on the editor thread.
/// </summary>
public sealed class SvgEditorSelectionContext
{
    private readonly SvgEditorController _owner;
    private readonly SvgDocument? _document;
    private readonly SvgVisualElement[] _selection;

    public SvgEditorSelectionContext(SvgEditorController owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _document = owner.Document;
        _selection = owner.SelectedElements.ToArray();
    }

    public bool IsCurrent(SvgEditorController? controller)
    {
        if (controller is null || _document is null || !ReferenceEquals(_owner, controller)
            || !ReferenceEquals(_document, controller.Document))
            return false;
        var current = controller.SelectedElements;
        if (current.Count != _selection.Length)
            return false;
        for (var index = 0; index < _selection.Length; index++)
            if (!ReferenceEquals(_selection[index], current[index]))
                return false;
        return true;
    }

    public void Validate(SvgEditorController? controller)
    {
        if (!IsCurrent(controller))
            throw new InvalidOperationException("The document or selection changed while this field was being edited. Reopen or reset the field before applying it.");
    }
}
