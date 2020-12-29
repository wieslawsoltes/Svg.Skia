using System;
using System.Collections.Generic;
using System.Diagnostics;
using Svg.Model.Drawables;
using Svg.Model.Drawables.Elements;
using Svg.Model.Primitives;
using Svg.Model.Primitives.PathCommands;

namespace Svg.Model
{
    public static partial class SvgModelExtensions
    {
        [Flags]
        internal enum PathPointType : byte
        {
            Start = 0,
            Line = 1,
            Bezier = 3,
            Bezier3 = 3,
            PathTypeMask = 0x7,
            DashMode = 0x10,
            PathMarker = 0x20,
            CloseSubpath = 0x80
        }

        internal static List<(Point Point, byte Type)> GetPathTypes(this Path path)
        {
            // System.Drawing.Drawing2D.GraphicsPath.PathTypes
            // System.Drawing.Drawing2D.PathPointType
            // byte -> PathPointType
            var pathTypes = new List<(Point Point, byte Type)>();

            if (path.Commands is null)
            {
                return pathTypes;
            }
            (Point Point, byte Type) lastPoint = (default, 0);
            foreach (var pathCommand in path.Commands)
            {
                switch (pathCommand)
                {
                    case MoveToPathCommand moveToPathCommand:
                        {
                            var point0 = new Point(moveToPathCommand.X, moveToPathCommand.Y);
                            pathTypes.Add((point0, (byte)PathPointType.Start));
                            lastPoint = (point0, (byte)PathPointType.Start);
                        }
                        break;

                    case LineToPathCommand lineToPathCommand:
                        {
                            var point1 = new Point(lineToPathCommand.X, lineToPathCommand.Y);
                            pathTypes.Add((point1, (byte)PathPointType.Line));
                            lastPoint = (point1, (byte)PathPointType.Line);
                        }
                        break;

                    case CubicToPathCommand cubicToPathCommand:
                        {
                            var point1 = new Point(cubicToPathCommand.X0, cubicToPathCommand.Y0);
                            var point2 = new Point(cubicToPathCommand.X1, cubicToPathCommand.Y1);
                            var point3 = new Point(cubicToPathCommand.X2, cubicToPathCommand.Y2);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            pathTypes.Add((point2, (byte)PathPointType.Bezier));
                            pathTypes.Add((point3, (byte)PathPointType.Bezier));
                            lastPoint = (point3, (byte)PathPointType.Bezier);
                        }
                        break;

                    case QuadToPathCommand quadToPathCommand:
                        {
                            var point1 = new Point(quadToPathCommand.X0, quadToPathCommand.Y0);
                            var point2 = new Point(quadToPathCommand.X1, quadToPathCommand.Y1);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            pathTypes.Add((point2, (byte)PathPointType.Bezier));
                            lastPoint = (point2, (byte)PathPointType.Bezier);
                        }
                        break;

                    case ArcToPathCommand arcToPathCommand:
                        {
                            var point1 = new Point(arcToPathCommand.X, arcToPathCommand.Y);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            lastPoint = (point1, (byte)PathPointType.Bezier);
                        }
                        break;

                    case ClosePathCommand closePathCommand:
                        {
                            lastPoint = (lastPoint.Point, (byte)(lastPoint.Type | (byte)PathPointType.CloseSubpath));
                            pathTypes[pathTypes.Count - 1] = lastPoint;
                        }
                        break;

                    case AddPolyPathCommand addPolyPathCommand:
                        {
                            if (addPolyPathCommand.Points is { } && addPolyPathCommand.Points.Count > 0)
                            {
                                foreach (var nexPoint in addPolyPathCommand.Points)
                                {
                                    var point1 = new Point(nexPoint.X, nexPoint.Y);
                                    pathTypes.Add((point1, (byte)PathPointType.Start));
                                    lastPoint = (point1, (byte)PathPointType.Start);
                                }

                                var point = addPolyPathCommand.Points[addPolyPathCommand.Points.Count - 1];
                                lastPoint = (point, (byte)PathPointType.Line);
                            }
                        }
                        break;

                    default:
                        Debug.WriteLine($"Not implemented path point for {pathCommand?.GetType()} type.");
                        break;
                }
            }

            return pathTypes;
        }

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
                if (svgElement is SvgMarkerElement svgMarkerElement)
                {
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
        }

        internal static void CreateMarker(this SvgMarker svgMarker, SvgVisualElement pOwner, Point pRefPoint, Point pMarkerPoint1, Point pMarkerPoint2, bool isStartMarker, Rect skOwnerBounds, IMarkerHost markerHost, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
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

            var markerDrawable = MarkerDrawable.Create(svgMarker, pOwner, pRefPoint, fAngle1, skOwnerBounds, null, assetLoader, ignoreAttributes);
            markerHost.AddMarker(markerDrawable);
        }

        internal static void CreateMarker(this SvgMarker svgMarker, SvgVisualElement pOwner, Point pRefPoint, Point pMarkerPoint1, Point pMarkerPoint2, Point pMarkerPoint3, Rect skOwnerBounds, IMarkerHost markerHost, IAssetLoader assetLoader)
        {
            var xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
            var yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
            var fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            xDiff = pMarkerPoint3.X - pMarkerPoint2.X;
            yDiff = pMarkerPoint3.Y - pMarkerPoint2.Y;
            var fAngle2 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);

            var markerDrawable = MarkerDrawable.Create(svgMarker, pOwner, pRefPoint, (fAngle1 + fAngle2) / 2, skOwnerBounds, null, assetLoader);
            markerHost.AddMarker(markerDrawable);
        }

        internal static void CreateMarkers(this SvgMarkerElement svgMarkerElement, Path skPath, Rect skOwnerBounds, IMarkerHost markerHost, IAssetLoader assetLoader)
        {
            var pathTypes = skPath.GetPathTypes();
            var pathLength = pathTypes.Count;

            var markerStart = svgMarkerElement.MarkerStart;
            if (markerStart is { } && pathLength > 0 && !HasRecursiveReference(svgMarkerElement, (e) => e.MarkerStart, new HashSet<Uri>()))
            {
                var marker = GetReference<SvgMarker>(svgMarkerElement, markerStart);
                if (marker is { })
                {
                    var refPoint1 = pathTypes[0].Point;
                    var index = 1;
                    while (index < pathLength && pathTypes[index].Point.X == refPoint1.X && pathTypes[index].Point.Y == refPoint1.Y)
                    {
                        ++index;
                    }
                    var refPoint2 = pathLength == 1 ? refPoint1 : pathTypes[index].Point;
                    CreateMarker(marker, svgMarkerElement, refPoint1, refPoint1, refPoint2, true, skOwnerBounds, markerHost, assetLoader);
                }
            }

            var markerMid = svgMarkerElement.MarkerMid;
            if (markerMid is { } && pathLength > 0 && !HasRecursiveReference(svgMarkerElement, (e) => e.MarkerMid, new HashSet<Uri>()))
            {
                var marker = GetReference<SvgMarker>(svgMarkerElement, markerMid);
                if (marker is { })
                {
                    var bezierIndex = -1;
                    for (var i = 1; i <= pathLength - 2; i++)
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
                            CreateMarker(marker, svgMarkerElement, pathTypes[i].Point, pathTypes[i - 1].Point, pathTypes[i].Point, pathTypes[i + 1].Point, skOwnerBounds, markerHost, assetLoader);
                        }
                    }
                }
            }

            var markerEnd = svgMarkerElement.MarkerEnd;
            if (markerEnd is { } && pathLength > 0 && !HasRecursiveReference(svgMarkerElement, (e) => e.MarkerEnd, new HashSet<Uri>()))
            {
                var marker = GetReference<SvgMarker>(svgMarkerElement, markerEnd);
                if (marker is { })
                {
                    var index = pathLength - 1;
                    var refPoint1 = pathTypes[index].Point;
                    if (pathLength > 1)
                    {
                        --index;
                        while (index > 0 && pathTypes[index].Point.X == refPoint1.X && pathTypes[index].Point.Y == refPoint1.Y)
                        {
                            --index;
                        }
                    }
                    var refPoint2 = pathLength == 1 ? refPoint1 : pathTypes[index].Point;
                    CreateMarker(marker, svgMarkerElement, refPoint1, refPoint2, pathTypes[pathLength - 1].Point, false, skOwnerBounds, markerHost, assetLoader);
                }
            }
        }
    }
}
