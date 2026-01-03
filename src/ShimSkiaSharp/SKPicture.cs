// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;

namespace ShimSkiaSharp;

public record SKPicture(SKRect CullRect, IList<CanvasCommand>? Commands) : IDeepCloneable<SKPicture>
{
    public SKPicture DeepClone()
        => new SKPicture(CullRect, CloneHelpers.CloneList(Commands, command => command.DeepClone()));
}
