// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SvgMaskUtil
    {
        public static SKPicture? GetSvgVisualElementMask(SvgVisualElement svgVisualElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var svgMaskRef = svgVisualElement.GetUriElementReference<SvgMask>("mask", uris);
            if (svgMaskRef == null || svgMaskRef.Children == null)
            {
                return null;
            }
            return SKPaintUtil.CreatePicture(svgMaskRef, skBounds, disposable);
        }
    }
}
