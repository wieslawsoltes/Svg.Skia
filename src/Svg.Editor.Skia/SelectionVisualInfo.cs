using Svg;
using Svg.Skia;

namespace Svg.Editor.Skia;

public readonly struct SelectionVisualInfo
{
    public SelectionVisualInfo(SvgVisualElement element, SvgSceneNode sceneNode)
    {
        Element = element;
        SceneNode = sceneNode;
    }

    public SvgVisualElement Element { get; }

    public SvgSceneNode SceneNode { get; }
}
