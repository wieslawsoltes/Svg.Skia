using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Skia;

public partial class SKSvg
{
    public IEnumerable<SvgSceneNode> HitTestSceneNodes(SKPoint point)
    {
        if (TryEnsureRetainedSceneGraph(out var sceneDocument) && sceneDocument is not null)
        {
            foreach (var node in sceneDocument.HitTest(point))
            {
                yield return node;
            }
        }
    }

    public IEnumerable<SvgSceneNode> HitTestSceneNodes(SKRect rect)
    {
        if (TryEnsureRetainedSceneGraph(out var sceneDocument) && sceneDocument is not null)
        {
            foreach (var node in sceneDocument.HitTest(rect))
            {
                yield return node;
            }
        }
    }

    public SvgSceneNode? HitTestTopmostSceneNode(SKPoint point)
    {
        if (!TryEnsureRetainedSceneGraph(out var sceneDocument) || sceneDocument is null)
        {
            return null;
        }

        return sceneDocument.HitTestTopmostNode(point);
    }

    public SvgSceneNode? HitTestTopmostSceneNode(SKPoint point, SKMatrix canvasMatrix)
    {
        return TryGetPicturePoint(point, canvasMatrix, out var picturePoint)
            ? HitTestTopmostSceneNode(picturePoint)
            : null;
    }

    public SvgElement? HitTestTopmostElement(SKPoint point)
    {
        return HitTestTopmostSceneNode(point)?.HitTestTargetElement;
    }

    public SvgElement? HitTestTopmostElement(SKPoint point, SKMatrix canvasMatrix)
    {
        return TryGetPicturePoint(point, canvasMatrix, out var picturePoint)
            ? HitTestTopmostElement(picturePoint)
            : null;
    }
}
