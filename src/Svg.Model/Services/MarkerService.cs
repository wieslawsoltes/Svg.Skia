using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Model.Drawables;
using Svg.Model.Drawables.Elements;

namespace Svg.Model.Services;

internal static class MarkerService
{
    internal static void AddMarkers(this SvgGroup svgGroup)
    {
        Uri? marker = default;

        // TODO: The marker can not be set as presentation attribute.
        //if (svgGroup.TryGetAttribute("marker", out string markerUrl))
        //{
        //    marker = new Uri(markerUrl, UriKind.RelativeOrAbsolute);
        //}

        var groupMarkerStart = svgGroup.MarkerStart;
        var groupMarkerMid = svgGroup.MarkerMid;
        var groupMarkerEnd = svgGroup.MarkerEnd;

        if (groupMarkerStart is null && groupMarkerMid is null && groupMarkerEnd is null && marker is null)
        {
            return;
        }

        foreach (var svgElement in svgGroup.Children)
        {
            if (svgElement is not SvgMarkerElement svgMarkerElement)
            {
                continue;
            }

            if (svgMarkerElement.MarkerStart is null)
            {
                if (groupMarkerStart is { })
                {
                    svgMarkerElement.MarkerStart = groupMarkerStart;
                }
                else if (marker is { })
                {
                    svgMarkerElement.MarkerStart = marker;
                }
            }

            if (svgMarkerElement.MarkerMid is null)
            {
                if (groupMarkerMid is { })
                {
                    svgMarkerElement.MarkerMid = groupMarkerMid;
                }
                else if (marker is { })
                {
                    svgMarkerElement.MarkerMid = marker;
                }
            }

            if (svgMarkerElement.MarkerEnd is null)
            {
                if (groupMarkerEnd is { })
                {
                    svgMarkerElement.MarkerEnd = groupMarkerEnd;
                }
                else if (marker is { })
                {
                    svgMarkerElement.MarkerEnd = marker;
                }
            }
        }
    }

    internal static void CreateMarker(this SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, bool isStartMarker, SKRect skViewport, IMarkerHost markerHost, ISvgAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var fAngle1 = 0f;
        if (svgMarker.Orient.IsAuto)
        {
            var xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
            var yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
            fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            if (isStartMarker && svgMarker.Orient.IsAutoStartReverse)
            {
                fAngle1 += 180;
            }
        }

        var markerDrawable = MarkerDrawable.Create(svgMarker, pOwner, pRefPoint, fAngle1, skViewport, null, assetLoader, references, ignoreAttributes);
        markerHost.AddMarker(markerDrawable);
    }

    internal static void CreateMarker(this SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, SKPoint pMarkerPoint3, SKRect skViewport, IMarkerHost markerHost, ISvgAssetLoader assetLoader, HashSet<Uri>? references)
    {
        var xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
        var yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
        var fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
        xDiff = pMarkerPoint3.X - pMarkerPoint2.X;
        yDiff = pMarkerPoint3.Y - pMarkerPoint2.Y;
        var fAngle2 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);

        var markerDrawable = MarkerDrawable.Create(svgMarker, pOwner, pRefPoint, (fAngle1 + fAngle2) / 2, skViewport, null, assetLoader, references);
        markerHost.AddMarker(markerDrawable);
    }

    internal static void CreateMarkers(this SvgMarkerElement svgMarkerElement, SKPath skPath, SKRect skViewport, IMarkerHost markerHost, ISvgAssetLoader assetLoader, HashSet<Uri>? references)
    {
        var pathTypes = skPath.GetPathTypes();
        var pathLength = pathTypes.Count;

        var markerStart = svgMarkerElement.MarkerStart;
        if (markerStart is { } && pathLength > 0 && !SvgService.HasRecursiveReference(svgMarkerElement, (e) => e.MarkerStart, new HashSet<Uri>()))
        {
            var marker = SvgService.GetReference<SvgMarker>(svgMarkerElement, markerStart);
            if (marker is { })
            {
                var refPoint1 = pathTypes[0].Point;
                var index = 1;
                // ReSharper disable CompareOfFloatsByEqualityOperator
                while (index < pathLength && pathTypes[index].Point.X == refPoint1.X && pathTypes[index].Point.Y == refPoint1.Y)
                {
                    ++index;
                }
                // ReSharper restore CompareOfFloatsByEqualityOperator
                var refPoint2 = pathLength == 1 ? refPoint1 : pathTypes[index].Point;
                CreateMarker(marker, svgMarkerElement, refPoint1, refPoint1, refPoint2, true, skViewport, markerHost, assetLoader, references);
            }
        }

        var markerMid = svgMarkerElement.MarkerMid;
        if (markerMid is { } && pathLength > 0 && !SvgService.HasRecursiveReference(svgMarkerElement, (e) => e.MarkerMid, new HashSet<Uri>()))
        {
            var marker = SvgService.GetReference<SvgMarker>(svgMarkerElement, markerMid);
            if (marker is { })
            {
                var bezierIndex = -1;
                for (var i = 1; i <= pathLength - 2; i++)
                {
                    // for Bezier curves, the marker shall only been shown at the last point
                    if ((pathTypes[i].Type & (byte)PathingService.PathPointType.PathTypeMask) == (byte)PathingService.PathPointType.Bezier)
                    {
                        bezierIndex = (bezierIndex + 1) % 3;
                    }
                    else
                    {
                        bezierIndex = -1;
                    }

                    if (bezierIndex == -1 || bezierIndex == 2)
                    {
                        CreateMarker(marker, svgMarkerElement, pathTypes[i].Point, pathTypes[i - 1].Point, pathTypes[i].Point, pathTypes[i + 1].Point, skViewport, markerHost, assetLoader, references);
                    }
                }
            }
        }

        var markerEnd = svgMarkerElement.MarkerEnd;
        if (markerEnd is { } && pathLength > 0 && !SvgService.HasRecursiveReference(svgMarkerElement, (e) => e.MarkerEnd, new HashSet<Uri>()))
        {
            var marker = SvgService.GetReference<SvgMarker>(svgMarkerElement, markerEnd);
            if (marker is { })
            {
                var index = pathLength - 1;
                var refPoint1 = pathTypes[index].Point;
                if (pathLength > 1)
                {
                    --index;
                    // ReSharper disable CompareOfFloatsByEqualityOperator
                    while (index > 0 && pathTypes[index].Point.X == refPoint1.X && pathTypes[index].Point.Y == refPoint1.Y)
                    {
                        --index;
                    }
                    // ReSharper restore CompareOfFloatsByEqualityOperator
                }
                var refPoint2 = pathLength == 1 ? refPoint1 : pathTypes[index].Point;
                CreateMarker(marker, svgMarkerElement, refPoint1, refPoint2, pathTypes[pathLength - 1].Point, false, skViewport, markerHost, assetLoader, references);
            }
        }
    }
}
