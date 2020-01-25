// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Svg.Skia
{
    public class TextDrawable : Drawable
    {
        private static readonly Regex s_multipleSpaces = new Regex(@" {2,}", RegexOptions.Compiled);

        // TODO: Implement drawable.

        private readonly SvgText _svgText;
        private SKRect _skOwnerBounds;

        public TextDrawable(SvgText svgText, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes = IgnoreAttributes.None)
        {
            _svgText = svgText;
            _skOwnerBounds = skOwnerBounds;
            IgnoreAttributes = ignoreAttributes;
        }

        internal void GetPositionsX(SvgTextBase svgTextBase, SKRect skBounds, List<float> xs)
        {
            var _x = svgTextBase.X;

            for (int i = 0; i < _x.Count; i++)
            {
                xs.Add(_x[i].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsY(SvgTextBase svgTextBase, SKRect skBounds, List<float> ys)
        {
            var _y = svgTextBase.Y;

            for (int i = 0; i < _y.Count; i++)
            {
                ys.Add(_y[i].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsDX(SvgTextBase svgTextBase, SKRect skBounds, List<float> dxs)
        {
            var _dx = svgTextBase.Dx;

            for (int i = 0; i < _dx.Count; i++)
            {
                dxs.Add(_dx[i].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsDY(SvgTextBase svgTextBase, SKRect skBounds, List<float> dys)
        {
            var _dy = svgTextBase.Dy;

            for (int i = 0; i < _dy.Count; i++)
            {
                dys.Add(_dy[i].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, skBounds));
            }
        }

        internal virtual IEnumerable<ISvgNode> GetContentNodes(SvgTextBase svgTextBase)
        {
            return svgTextBase.Nodes == null || svgTextBase.Nodes.Count < 1 ?
                svgTextBase.Children.OfType<ISvgNode>().Where(o => !(o is ISvgDescriptiveElement)) :
                svgTextBase.Nodes;
        }

        internal string PrepareText(SvgTextBase svgTextBase, string value)
        {
            value = ApplyTransformation(svgTextBase, value);
            value = new StringBuilder(value).Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').ToString();
            return svgTextBase.SpaceHandling == XmlSpaceHandling.preserve ? value : s_multipleSpaces.Replace(value.Trim(), " ");
        }

        internal string ApplyTransformation(SvgTextBase svgTextBase, string value)
        {
            return svgTextBase.TextTransformation switch
            {
                SvgTextTransformation.Capitalize => value.ToUpper(),
                SvgTextTransformation.Uppercase => value.ToUpper(),
                SvgTextTransformation.Lowercase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value),
                _ => value,
            };
        }

        internal void BeginDraw(SvgTextBase svgTextBase, SKCanvas skCanvas, SKRect skBounds, IgnoreAttributes ignoreAttributes, CompositeDisposable disposable, out MaskDrawable? maskDrawable, out SKPaint? maskDstIn, out SKPaint? skPaintOpacity, out SKPaint? skPaintFilter)
        {
            var enableClip = !ignoreAttributes.HasFlag(IgnoreAttributes.Clip);
            var enableMask = !ignoreAttributes.HasFlag(IgnoreAttributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(IgnoreAttributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(IgnoreAttributes.Filter);

            skCanvas.Save();

            var skMatrix = SvgTransformsExtensions.ToSKMatrix(svgTextBase.Transforms);

            var skMatrixTotal = skCanvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
            skCanvas.SetMatrix(skMatrixTotal);

            if (enableClip == true)
            {
                var skPathClip = SvgClippingExtensions.GetSvgVisualElementClipPath(svgTextBase, skBounds, new HashSet<Uri>(), disposable);
                if (skPathClip != null && !IgnoreAttributes.HasFlag(IgnoreAttributes.Clip))
                {
                    bool antialias = SvgPaintingExtensions.IsAntialias(svgTextBase);
                    skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
                } 
            }

            if (enableMask == true)
            {
                var mask = default(SKPaint);
                maskDstIn = default(SKPaint);
                maskDrawable = SvgClippingExtensions.GetSvgVisualElementMask(svgTextBase, skBounds, new HashSet<Uri>(), disposable);
                if (maskDrawable != null)
                {
                    mask = new SKPaint()
                    {
                        IsAntialias = true,
                        Style = SKPaintStyle.StrokeAndFill
                    };
                    disposable.Add(mask);

                    maskDstIn = new SKPaint
                    {
                        IsAntialias = true,
                        Style = SKPaintStyle.StrokeAndFill,
                        BlendMode = SKBlendMode.DstIn,
                        Color = new SKColor(0, 0, 0, 255),
                        ColorFilter = SKColorFilter.CreateLumaColor()
                    };
                    disposable.Add(maskDstIn);
                    skCanvas.SaveLayer(mask);
                } 
            }
            else
            {
                maskDstIn = null;
                maskDrawable = null;
            }

            if (enableOpacity == true)
            {
                skPaintOpacity = SvgPaintingExtensions.GetOpacitySKPaint(svgTextBase, disposable);
                if (skPaintOpacity != null && !IgnoreAttributes.HasFlag(IgnoreAttributes.Opacity))
                {
                    skCanvas.SaveLayer(skPaintOpacity);
                }
            }
            else
            {
                skPaintOpacity = null;
            }

            if (enableFilter == true)
            {
                skPaintFilter = SvgFiltersExtensions.GetFilterSKPaint(svgTextBase, skBounds, this, disposable);
                if (skPaintFilter != null && !IgnoreAttributes.HasFlag(IgnoreAttributes.Filter))
                {
                    skCanvas.SaveLayer(skPaintFilter);
                }
            }
            else
            {
                skPaintFilter = null;
            }
        }

        internal void EndDraw(SKCanvas skCanvas, IgnoreAttributes ignoreAttributes, MaskDrawable? maskDrawable, SKPaint? maskDstIn, SKPaint? skPaintOpacity, SKPaint? skPaintFilter)
        {
            var enableMask = !ignoreAttributes.HasFlag(IgnoreAttributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(IgnoreAttributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(IgnoreAttributes.Filter);

            if (skPaintFilter != null && enableFilter == true)
            {
                skCanvas.Restore();
            }

            if (skPaintOpacity != null && enableOpacity == true)
            {
                skCanvas.Restore();
            }

            if (maskDrawable != null && enableMask == true)
            {
                skCanvas.SaveLayer(maskDstIn);
                maskDrawable.Draw(skCanvas, ignoreAttributes);
                skCanvas.Restore();
                skCanvas.Restore();
            }

            skCanvas.Restore();
        }

        internal void DrawTextString(SvgTextBase svgTextBase, string text, float x, float y, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            if (SvgPaintingExtensions.IsValidFill(svgTextBase))
            {
                var skPaint = SvgPaintingExtensions.GetFillSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                if (skPaint != null)
                {
                    SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                    skCanvas.DrawText(text, x, y, skPaint);
                }
            }

            if (SvgPaintingExtensions.IsValidStroke(svgTextBase, skBounds))
            {
                var skPaint = SvgPaintingExtensions.GetStrokeSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                if (skPaint != null)
                {
                    SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                    skCanvas.DrawText(text, x, y, skPaint);
                }
            }
        }

        internal void DrawTextBase(SvgTextBase svgTextBase, string? text, float currentX, float currentY, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            // TODO: Fix SvgTextBase rendering.
            bool isValidFill = SvgPaintingExtensions.IsValidFill(svgTextBase);
            bool isValidStroke = SvgPaintingExtensions.IsValidStroke(svgTextBase, skOwnerBounds);

            if ((!isValidFill && !isValidStroke) || text == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            var xs = new List<float>();
            var ys = new List<float>();
            var dxs = new List<float>();
            var dys = new List<float>();

            GetPositionsX(svgTextBase, skOwnerBounds, xs);
            GetPositionsY(svgTextBase, skOwnerBounds, ys);
            GetPositionsDX(svgTextBase, skOwnerBounds, dxs);
            GetPositionsDY(svgTextBase, skOwnerBounds, dys);

            if (xs.Count >= 1 && ys.Count >= 1 && xs.Count == ys.Count && xs.Count == text.Length)
            {
                // TODO: Fix text position rendering.
                var points = new SKPoint[xs.Count];

                for (int i = 0; i < xs.Count; i++)
                {
                    float x = xs[i];
                    float y = ys[i];
                    points[i] = new SKPoint(x, y);
                }

                // TODO: Calculate correct bounds.
                var skBounds = skOwnerBounds;

                if (SvgPaintingExtensions.IsValidFill(svgTextBase))
                {
                    var skPaint = SvgPaintingExtensions.GetFillSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                    if (skPaint != null)
                    {
                        SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                        skCanvas.DrawPositionedText(text, points, skPaint);
                    }
                }

                if (SvgPaintingExtensions.IsValidStroke(svgTextBase, skBounds))
                {
                    var skPaint = SvgPaintingExtensions.GetStrokeSKPaint(svgTextBase, skBounds, ignoreAttributes, _disposable);
                    if (skPaint != null)
                    {
                        SvgTextExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, _disposable);
                        skCanvas.DrawPositionedText(text, points, skPaint);
                    }
                }
            }
            else
            {
                float x = (xs.Count >= 1) ? xs[0] : currentX;
                float y = (ys.Count >= 1) ? ys[0] : currentY;
                float dx = (dxs.Count >= 1) ? dxs[0] : 0f;
                float dy = (dys.Count >= 1) ? dys[0] : 0f;

                DrawTextString(svgTextBase, text, x + dx, y + dy, skOwnerBounds, ignoreAttributes, skCanvas);
            }
        }

        internal void DrawTextPath(SvgTextPath svgTextPath, float currentX, float currentY, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            if (!CanDraw(svgTextPath, ignoreAttributes))
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgTextPath, (e) => e.ReferencedPath, new HashSet<Uri>()))
            {
                return;
            }

            var svgPath = SvgExtensions.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
            if (svgPath == null)
            {
                return;
            }

            var skPath = svgPath.PathData?.ToSKPath(svgPath.FillRule, _disposable);
            if (skPath == null || skPath.IsEmpty)
            {
                return;
            }

            var skMatrixPath = SvgTransformsExtensions.ToSKMatrix(svgPath.Transforms);
            skPath.Transform(skMatrixPath);

            // TODO: Implement StartOffset
            var startOffset = svgTextPath.StartOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, skOwnerBounds);

            float hOffset = currentX + startOffset;
            float vOffset = currentY;

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgTextPath, skCanvas, skBounds, ignoreAttributes, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            // TODO: Fix SvgTextPath rendering.
            bool isValidFill = SvgPaintingExtensions.IsValidFill(svgTextPath);
            bool isValidStroke = SvgPaintingExtensions.IsValidStroke(svgTextPath, skBounds);

            if (isValidFill || isValidStroke)
            {
                if (!string.IsNullOrEmpty(svgTextPath.Text))
                {
                    var text = PrepareText(svgTextPath, svgTextPath.Text);

                    if (SvgPaintingExtensions.IsValidFill(svgTextPath))
                    {
                        var skPaint = SvgPaintingExtensions.GetFillSKPaint(svgTextPath, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SvgTextExtensions.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                            skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                        }
                    }

                    if (SvgPaintingExtensions.IsValidStroke(svgTextPath, skBounds))
                    {
                        var skPaint = SvgPaintingExtensions.GetStrokeSKPaint(svgTextPath, skBounds, ignoreAttributes, _disposable);
                        if (skPaint != null)
                        {
                            SvgTextExtensions.SetSKPaintText(svgTextPath, skBounds, skPaint, _disposable);
                            skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                        }
                    }
                }
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter);
        }

        internal void DrawTextRef(SvgTextRef svgTextRef, float currentX, float currentY, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            if (!CanDraw(svgTextRef, ignoreAttributes))
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgTextRef, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                return;
            }

            var svgReferencedText = SvgExtensions.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
            if (svgReferencedText == null)
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgTextRef, skCanvas, skBounds, ignoreAttributes, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            // TODO: Draw svgReferencedText
            if (!string.IsNullOrEmpty(svgReferencedText.Text))
            {
                var text = PrepareText(svgReferencedText, svgReferencedText.Text);
                DrawTextBase(svgReferencedText, svgReferencedText.Text, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas);
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter);
        }

        internal void DrawTextSpan(SvgTextSpan svgTextSpan, float currentX, float currentY, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            if (!CanDraw(svgTextSpan, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgTextSpan, skCanvas, skBounds, ignoreAttributes, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            // TODO: Implement SvgTextSpan drawing.
            if (!string.IsNullOrEmpty(svgTextSpan.Text))
            {
                var text = PrepareText(svgTextSpan, svgTextSpan.Text);
                DrawTextBase(svgTextSpan, text, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas);
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter);
        }

        internal void DrawText(SvgText svgText, SKRect skOwnerBounds, IgnoreAttributes ignoreAttributes, SKCanvas skCanvas)
        {
            if (!CanDraw(svgText, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            SKPaint? maskDstIn, skPaintOpacity, skPaintFilter;
            MaskDrawable? maskDrawable;
            BeginDraw(svgText, skCanvas, skBounds, ignoreAttributes, _disposable, out maskDrawable, out maskDstIn, out skPaintOpacity, out skPaintFilter);

            var xs = new List<float>();
            var ys = new List<float>();
            var dxs = new List<float>();
            var dys = new List<float>();
            GetPositionsX(svgText, skOwnerBounds, xs);
            GetPositionsY(svgText, skOwnerBounds, ys);
            GetPositionsDX(svgText, skOwnerBounds, dxs);
            GetPositionsDY(svgText, skOwnerBounds, dys);

            float x = (xs.Count >= 1) ? xs[0] : 0f;
            float y = (ys.Count >= 1) ? ys[0] : 0f;
            float dx = (dxs.Count >= 1) ? dxs[0] : 0f;
            float dy = (dys.Count >= 1) ? dys[0] : 0f;

            float currentX = x + dx;
            float currentY = y + dy;

            foreach (var node in GetContentNodes(svgText))
            {
                if (!(node is SvgTextBase textNode))
                {
                    if (!string.IsNullOrEmpty(node.Content))
                    {
                        var text = PrepareText(svgText, node.Content);
                        DrawTextBase(svgText, text, 0f, 0f, skOwnerBounds, ignoreAttributes, skCanvas);
                    }
                }
                else
                {
                    switch (textNode)
                    {
                        case SvgTextPath svgTextPath:
                            DrawTextPath(svgTextPath, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas);
                            break;
                        case SvgTextRef svgTextRef:
                            DrawTextRef(svgTextRef, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas);
                            break;
                        case SvgTextSpan svgTextSpan:
                            DrawTextSpan(svgTextSpan, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas);
                            break;
                        default:
                            break;
                    }
                }
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter);
        }

        public override void OnDraw(SKCanvas canvas, IgnoreAttributes ignoreAttributes)
        {
            // TODO: Currently using custom OnDraw override.
        }

        public override void Draw(SKCanvas canvas, IgnoreAttributes ignoreAttributes)
        {
            DrawText(_svgText, _skOwnerBounds, ignoreAttributes, canvas);
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            // TODO:
            Draw(canvas, IgnoreAttributes);
        }
    }
}
