﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using A = Avalonia;
using AM = Avalonia.Media;

namespace Avalonia.Svg.Commands;

public sealed class TextDrawCommand : DrawCommand
{
    public A.Point Origin { get; }
    public AM.FormattedText? FormattedText { get; }

    public TextDrawCommand(A.Point origin, AM.FormattedText? formattedText)
    {
        Origin = origin;
        FormattedText = formattedText;
    }
}
