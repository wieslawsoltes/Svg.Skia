// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SvgMaskExtensions
    {
        public static MaskDrawable? GetSvgVisualElementMask(SvgElement svgElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var svgMaskRef = svgElement.GetUriElementReference<SvgMask>("mask", uris);
            if (svgMaskRef == null || svgMaskRef.Children == null)
            {
                return null;
            }
            var maskDrawable = new MaskDrawable(svgMaskRef, skBounds, IgnoreAttributes.None);
            disposable.Add(maskDrawable);
            return maskDrawable;
        }
    }
}
