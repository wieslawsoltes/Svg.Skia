// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    [Flags]
    public enum PathPointType : byte
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

    public static class SKPathExtensions
    {
        public static List<(SKPoint Point, byte Type)> GetPathTypes(this SKPath path)
        {
            // System.Drawing.Drawing2D.GraphicsPath.PathTypes
            // System.Drawing.Drawing2D.PathPointType
            // byte -> PathPointType
            var pathTypes = new List<(SKPoint Point, byte Type)>();
            using (var iterator = path.CreateRawIterator())
            {
                var points = new SKPoint[4];
                var pathVerb = SKPathVerb.Move;
                (SKPoint Point, byte Type) lastPoint = (default, 0);
                while ((pathVerb = iterator.Next(points)) != SKPathVerb.Done)
                {
                    switch (pathVerb)
                    {
                        case SKPathVerb.Move:
                            {
                                pathTypes.Add((points[0], (byte)PathPointType.Start));
                                lastPoint = (points[0], (byte)PathPointType.Start);
                            }
                            break;
                        case SKPathVerb.Line:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Line));
                                lastPoint = (points[1], (byte)PathPointType.Line);
                            }
                            break;
                        case SKPathVerb.Cubic:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[2], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[3], (byte)PathPointType.Bezier));
                                lastPoint = (points[3], (byte)PathPointType.Bezier);
                            }
                            break;
                        case SKPathVerb.Quad:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[2], (byte)PathPointType.Bezier));
                                lastPoint = (points[2], (byte)PathPointType.Bezier);
                            }
                            break;
                        case SKPathVerb.Conic:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[2], (byte)PathPointType.Bezier));
                                lastPoint = (points[2], (byte)PathPointType.Bezier);
                            }
                            break;
                        case SKPathVerb.Close:
                            {
                                lastPoint = (lastPoint.Point, (byte)((lastPoint.Type | (byte)PathPointType.CloseSubpath)));
                                pathTypes[pathTypes.Count - 1] = lastPoint;
                            }
                            break;
                    }
                }
            }
            return pathTypes;
        }
    }
}
