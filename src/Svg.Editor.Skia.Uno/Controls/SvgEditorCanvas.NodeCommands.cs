using SkiaSharp;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    /// <summary>Whether a path point or polygon/polyline vertex owns node-editing commands.</summary>
    public bool HasActiveNode => IsNodeTool(Tool) && CanEditSelection && ActiveNodeIndex >= 0;

    /// <summary>Moves the active path point or vertex by a picture-space keyboard increment.</summary>
    public bool NudgeActiveNode(float dx, float dy)
    {
        if (!HasActiveNode || Controller is not { } controller)
            return false;
        SvgVisualElement? element = _vertexOriginal ?? (SvgVisualElement?)_nodeOriginal;
        return element is not null && controller.NudgeNode(element, ActiveNodeIndex, new SKPoint(dx, dy));
    }
}
