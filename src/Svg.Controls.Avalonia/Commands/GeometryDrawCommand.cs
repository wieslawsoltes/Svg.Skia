// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using AM = Avalonia.Media;
using AP = Avalonia.Platform;

namespace Avalonia.Svg.Commands;

public sealed class GeometryDrawCommand : DrawCommand
{
    public AM.IBrush? Brush { get; }
    public AM.IPen? Pen { get; }
    public AM.Geometry? Geometry { get; }

    public GeometryDrawCommand(AM.IBrush? brush, AM.IPen? pen, AM.Geometry? geometry)
    {
        Brush = brush;
        Pen = pen;
        Geometry = geometry;
    }
}
