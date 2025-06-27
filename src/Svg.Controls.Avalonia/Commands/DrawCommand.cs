// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;

namespace Avalonia.Svg.Commands;

public abstract class DrawCommand : IDisposable
{
    public virtual void Dispose()
    {
    }
}
