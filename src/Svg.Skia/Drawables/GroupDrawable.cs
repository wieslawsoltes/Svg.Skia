// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    internal class GroupDrawable : DrawableContainer
    {
        public GroupDrawable(SvgGroup svgGroup, SKRect skOwnerBounds, bool ignoreDisplay)
        {
            IgnoreDisplay = ignoreDisplay;
            IsDrawable = CanDraw(svgGroup, IgnoreDisplay);

            if (!IsDrawable)
            {
                return;
            }

            // TODO: Call AddMarkers only once.
            AddMarkers(svgGroup);

            foreach (var svgElement in svgGroup.Children)
            {
                var drawable = DrawableFactory.Create(svgElement, skOwnerBounds, ignoreDisplay);
                if (drawable != null)
                {
                    ChildrenDrawables.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            IsAntialias = SkiaUtil.IsAntialias(svgGroup);

            TransformedBounds = SKRect.Empty;

            foreach (var drawable in ChildrenDrawables)
            {
                if (TransformedBounds.IsEmpty)
                {
                    TransformedBounds = drawable.TransformedBounds;
                }
                else
                {
                    if (!drawable.TransformedBounds.IsEmpty)
                    {
                        TransformedBounds = SKRect.Union(TransformedBounds, drawable.TransformedBounds);
                    }
                }
            }

            Transform = SkiaUtil.GetSKMatrix(svgGroup.Transforms);

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);

            PathClip = SkiaUtil.GetSvgVisualElementClipPath(svgGroup, TransformedBounds, new HashSet<Uri>(), _disposable);
            PaintOpacity = SkiaUtil.GetOpacitySKPaint(svgGroup, _disposable);
            PaintFilter = SkiaUtil.GetFilterSKPaint(svgGroup, _disposable);

            PaintFill = null;
            PaintStroke = null;
        }

        internal void AddMarkers(SvgGroup svgGroup)
        {
            Uri? marker = null;
            // TODO: The marker can not be set as presentation attribute.
            //if (svgGroup.TryGetAttribute("marker", out string markerUrl))
            //{
            //    marker = new Uri(markerUrl, UriKind.RelativeOrAbsolute);
            //}

            if (svgGroup.MarkerStart == null && svgGroup.MarkerMid == null && svgGroup.MarkerEnd == null && marker == null)
            {
                return;
            }

            foreach (var svgElement in svgGroup.Children)
            {
                if (svgElement is SvgMarkerElement svgMarkerElement)
                {
                    if (svgMarkerElement.MarkerStart == null)
                    {
                        if (svgGroup.MarkerStart != null)
                        {
                            svgMarkerElement.MarkerStart = svgGroup.MarkerStart;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerStart = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerMid == null)
                    {
                        if (svgGroup.MarkerMid != null)
                        {
                            svgMarkerElement.MarkerMid = svgGroup.MarkerMid;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerMid = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerEnd == null)
                    {
                        if (svgGroup.MarkerEnd != null)
                        {
                            svgMarkerElement.MarkerEnd = svgGroup.MarkerEnd;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerEnd = marker;
                        }
                    }
                }
            }
        }
    }
}
