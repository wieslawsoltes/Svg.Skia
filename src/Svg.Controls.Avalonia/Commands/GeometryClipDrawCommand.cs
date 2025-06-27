// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using AM = Avalonia.Media;

namespace Avalonia.Svg.Commands;

public sealed class GeometryClipDrawCommand : DrawCommand
{
    public AM.Geometry? Clip { get; }

    public GeometryClipDrawCommand(AM.Geometry? clip)
    {
        Clip = clip;
    }
}
