// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using A = Avalonia;

namespace Avalonia.Svg.Commands;

public sealed class PushTransformDrawCommand : DrawCommand
{
    public A.Matrix Matrix { get; }

    public PushTransformDrawCommand(A.Matrix matrix)
    {
        Matrix = matrix;
    }
}
