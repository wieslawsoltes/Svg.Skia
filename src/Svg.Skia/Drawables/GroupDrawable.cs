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
            _ignoreDisplay = ignoreDisplay;
            _canDraw = CanDraw(svgGroup, _ignoreDisplay);

            if (!_canDraw)
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
                    _childrenDrawables.Add(drawable);
                    _disposable.Add(drawable);
                }
            }

            _antialias = SkiaUtil.IsAntialias(svgGroup);

            _skBounds = SKRect.Empty;

            foreach (var drawable in _childrenDrawables)
            {
                if (_skBounds.IsEmpty)
                {
                    _skBounds = drawable._skBounds;
                }
                else
                {
                    if (!drawable._skBounds.IsEmpty)
                    {
                        _skBounds = SKRect.Union(_skBounds, drawable._skBounds);
                    }
                }
            }

            // TODO: Transform _skBounds using _skMatrix.

            _skMatrix = SkiaUtil.GetSKMatrix(svgGroup.Transforms);
            _skPathClip = SkiaUtil.GetSvgVisualElementClipPath(svgGroup, _skBounds, new HashSet<Uri>(), _disposable);
            _skPaintOpacity = SkiaUtil.GetOpacitySKPaint(svgGroup, _disposable);
            _skPaintFilter = SkiaUtil.GetFilterSKPaint(svgGroup, _disposable);

            _skPaintFill = null;
            _skPaintStroke = null;
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
