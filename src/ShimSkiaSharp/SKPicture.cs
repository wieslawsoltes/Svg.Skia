// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.Generic;

namespace ShimSkiaSharp;

public record SKPicture(SKRect CullRect, IList<CanvasCommand>? Commands) : IDeepCloneable<SKPicture>
{
    public SKPicture DeepClone() => DeepClone(new CloneContext());

    internal SKPicture DeepClone(CloneContext context)
    {
        if (context.TryGet(this, out SKPicture existing))
        {
            return existing;
        }

        context.Enter(this);
        try
        {
            var clone = new SKPicture(CullRect, CloneHelpers.CloneList(Commands, context, command => command.DeepClone(context)));
            context.Add(this, clone);
            return clone;
        }
        finally
        {
            context.Exit(this);
        }
    }
}
