// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using A = Avalonia;

namespace Avalonia.Svg.Commands;

public sealed class ClipDrawCommand : DrawCommand
{
    public A.Rect Clip { get; }

    public ClipDrawCommand(A.Rect clip)
    {
        Clip = clip;
    }
}
