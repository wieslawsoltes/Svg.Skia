// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Parts of this source file are adapted from the https://github.com/vvvv/SVG
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Svg.Skia
{
    public class GroupDrawable : DrawableContainer
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

            IsAntialias = SKPaintUtil.IsAntialias(svgGroup);

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

            Transform = SKMatrixUtil.GetSKMatrix(svgGroup.Transforms);

            PathClip = SvgClipPathUtil.GetSvgVisualElementClipPath(svgGroup, TransformedBounds, new HashSet<Uri>(), _disposable);
            PictureMask = SvgMaskUtil.GetSvgVisualElementMask(svgGroup, TransformedBounds, new HashSet<Uri>(), _disposable);
            CreateMaskPaints();
            PaintOpacity = SKPaintUtil.GetOpacitySKPaint(svgGroup, _disposable);
            PaintFilter = SKPaintUtil.GetFilterSKPaint(svgGroup, TransformedBounds, _disposable);

            PaintFill = null;
            PaintStroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            SKMatrix.MapRect(ref Transform, out TransformedBounds, ref TransformedBounds);
        }

        internal void AddMarkers(SvgGroup svgGroup)
        {
            Uri? marker = null;
            // TODO: The marker can not be set as presentation attribute.
            //if (svgGroup.TryGetAttribute("marker", out string markerUrl))
            //{
            //    marker = new Uri(markerUrl, UriKind.RelativeOrAbsolute);
            //}

            var groupMarkerStart = svgGroup.MarkerStart;
            var groupMarkerMid = svgGroup.MarkerMid;
            var groupMarkerEnd = svgGroup.MarkerEnd;

            if (groupMarkerStart == null && groupMarkerMid == null && groupMarkerEnd == null && marker == null)
            {
                return;
            }

            foreach (var svgElement in svgGroup.Children)
            {
                if (svgElement is SvgMarkerElement svgMarkerElement)
                {
                    if (svgMarkerElement.MarkerStart == null)
                    {
                        if (groupMarkerStart != null)
                        {
                            svgMarkerElement.MarkerStart = groupMarkerStart;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerStart = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerMid == null)
                    {
                        if (groupMarkerMid != null)
                        {
                            svgMarkerElement.MarkerMid = groupMarkerMid;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerMid = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerEnd == null)
                    {
                        if (groupMarkerEnd != null)
                        {
                            svgMarkerElement.MarkerEnd = groupMarkerEnd;
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
