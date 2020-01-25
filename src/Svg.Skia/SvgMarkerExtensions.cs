// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SvgMarkerExtensions
    {
        public static void CreateMarker(this SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, bool isStartMarker, SKRect skOwnerBounds, ref List<Drawable>? markerDrawables, CompositeDisposable disposable, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            float fAngle1 = 0f;
            if (svgMarker.Orient.IsAuto)
            {
                float xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
                float yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
                fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
                if (isStartMarker && svgMarker.Orient.IsAutoStartReverse)
                {
                    fAngle1 += 180;
                }
            }

            var markerDrawable = new MarkerDrawable(svgMarker, pOwner, pRefPoint, fAngle1, skOwnerBounds, ignoreAttributes);
            if (markerDrawables == null)
            {
                markerDrawables = new List<Drawable>();
            }
            markerDrawables.Add(markerDrawable);
            disposable.Add(markerDrawable);
        }

        public static void CreateMarker(this SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, SKPoint pMarkerPoint3, SKRect skOwnerBounds, ref List<Drawable>? markerDrawables, CompositeDisposable disposable)
        {
            float xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
            float yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
            float fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            xDiff = pMarkerPoint3.X - pMarkerPoint2.X;
            yDiff = pMarkerPoint3.Y - pMarkerPoint2.Y;
            float fAngle2 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);

            var markerDrawable = new MarkerDrawable(svgMarker, pOwner, pRefPoint, (fAngle1 + fAngle2) / 2, skOwnerBounds);
            if (markerDrawables == null)
            {
                markerDrawables = new List<Drawable>();
            }
            markerDrawables.Add(markerDrawable);
            disposable.Add(markerDrawable);
        }

        public static void CreateMarkers(this SvgMarkerElement svgMarkerElement, SKPath skPath, SKRect skOwnerBounds, ref List<Drawable>? markerDrawables, CompositeDisposable disposable)
        {
            var pathTypes = skPath.GetPathTypes();
            var pathLength = pathTypes.Count;

            var markerStart = svgMarkerElement.MarkerStart;
            if (markerStart != null && !SvgExtensions.HasRecursiveReference(svgMarkerElement, (e) => e.MarkerStart, new HashSet<Uri>()))
            {
                var marker = SvgExtensions.GetReference<SvgMarker>(svgMarkerElement, markerStart);
                if (marker != null)
                {
                    var refPoint1 = pathTypes[0].Point;
                    var index = 1;
                    while (index < pathLength && pathTypes[index].Point == refPoint1)
                    {
                        ++index;
                    }
                    var refPoint2 = pathTypes[index].Point;
                    CreateMarker(marker, svgMarkerElement, refPoint1, refPoint1, refPoint2, true, skOwnerBounds, ref markerDrawables, disposable);
                }
            }

            var markerMid = svgMarkerElement.MarkerMid;
            if (markerMid != null && !SvgExtensions.HasRecursiveReference(svgMarkerElement, (e) => e.MarkerMid, new HashSet<Uri>()))
            {
                var marker = SvgExtensions.GetReference<SvgMarker>(svgMarkerElement, markerMid);
                if (marker != null)
                {
                    int bezierIndex = -1;
                    for (int i = 1; i <= pathLength - 2; i++)
                    {
                        // for Bezier curves, the marker shall only been shown at the last point
                        if ((pathTypes[i].Type & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Bezier)
                        {
                            bezierIndex = (bezierIndex + 1) % 3;
                        }
                        else
                        {
                            bezierIndex = -1;
                        }

                        if (bezierIndex == -1 || bezierIndex == 2)
                        {
                            CreateMarker(marker, svgMarkerElement, pathTypes[i].Point, pathTypes[i - 1].Point, pathTypes[i].Point, pathTypes[i + 1].Point, skOwnerBounds, ref markerDrawables, disposable);
                        }
                    }
                }
            }

            var markerEnd = svgMarkerElement.MarkerEnd;
            if (markerEnd != null && !SvgExtensions.HasRecursiveReference(svgMarkerElement, (e) => e.MarkerEnd, new HashSet<Uri>()))
            {
                var marker = SvgExtensions.GetReference<SvgMarker>(svgMarkerElement, markerEnd);
                if (marker != null)
                {
                    var index = pathLength - 1;
                    var refPoint1 = pathTypes[index].Point;
                    --index;
                    while (index > 0 && pathTypes[index].Point == refPoint1)
                    {
                        --index;
                    }
                    var refPoint2 = pathTypes[index].Point;
                    CreateMarker(marker, svgMarkerElement, refPoint1, refPoint2, pathTypes[pathLength - 1].Point, false, skOwnerBounds, ref markerDrawables, disposable);
                }
            }
        }
    }
}
