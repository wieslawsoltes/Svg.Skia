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
        public static Uri? GetMaskUri(SvgVisualElement svgVisualElement)
        {
            if (svgVisualElement.TryGetAttribute("mask", out string maskUrl))
            {
                return new Uri(maskUrl, UriKind.RelativeOrAbsolute);
            }
            return null;
        }

        public static SvgMask? GetMask(SvgVisualElement svgVisualElement, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var maskUri = GetMaskUri(svgVisualElement);
            if (maskUri != null)
            {
                if (SvgExtensions.HasRecursiveReference(svgVisualElement, (e) => GetMaskUri(e), uris))
                {
                    return null;
                }

                var svgMask = SvgExtensions.GetReference<SvgMask>(svgVisualElement, maskUri);
                if (svgMask == null || svgMask.Children == null)
                {
                    return null;
                }
                return svgMask;
            }
            return null;
        }

        public static SKPicture? GetSvgVisualElementMask(SvgVisualElement svgVisualElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var svgMask = GetMask(svgVisualElement, uris, disposable);
            if (svgMask == null)
            {
                return null;
            }
            return SKPaintUtil.CreatePicture(svgMask, skBounds, disposable);
        }
    }
}
