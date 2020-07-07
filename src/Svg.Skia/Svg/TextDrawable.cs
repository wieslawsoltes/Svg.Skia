using System;
using System.Collections.Generic;
using System.Globalization;
#if !USE_PICTURE
using SkiaSharp;
using CropRect = SkiaSharp.SKImageFilter.CropRect;
#endif
using System.Text;
using System.Text.RegularExpressions;
#if USE_PICTURE
using SKBlendMode = Svg.Picture.BlendMode;
using SKCanvas = Svg.Picture.Canvas;
using SKClipOperation = Svg.Picture.ClipOperation;
using SKColorFilter = Svg.Picture.ColorFilter;
using SKPaint = Svg.Picture.Paint;
using SKPaintStyle = Svg.Picture.PaintStyle;
using SKPoint = Svg.Picture.Point;
using SKRect = Svg.Picture.Rect;
#endif

namespace Svg.Skia
{
    internal sealed class TextDrawable : DrawableBase
    {
        private static readonly Regex s_multipleSpaces = new Regex(@" {2,}", RegexOptions.Compiled);

        public SvgText? Text;

        public SKRect OwnerBounds;

        private TextDrawable()
            : base()
        {
        }

        public static TextDrawable Create(SvgText svgText, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            return new TextDrawable
            {
                Element = svgText,
                Parent = parent,
                Text = svgText,
                OwnerBounds = skOwnerBounds,
                IgnoreAttributes = ignoreAttributes
            };
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

        internal IEnumerable<ISvgNode> GetContentNodes(SvgTextBase svgTextBase)
        {
            if (svgTextBase.Nodes is null || svgTextBase.Nodes.Count < 1)
            {
                foreach (var child in svgTextBase.Children)
                {
                    if (child is ISvgNode svgNode && !(svgNode is ISvgDescriptiveElement))
                    {
                        yield return svgNode;
                    }
                }
            }
            else
            {
                foreach (var node in svgTextBase.Nodes)
                {
                    yield return node;
                }
            }
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

        internal void BeginDraw(SvgTextBase svgTextBase, SKCanvas skCanvas, SKRect skBounds, Attributes ignoreAttributes, CompositeDisposable disposable, out MaskDrawable? maskDrawable, out SKPaint? maskDstIn, out SKPaint? skPaintOpacity, out SKPaint? skPaintFilter)
        {
            var enableClip = !ignoreAttributes.HasFlag(Attributes.ClipPath);
            var enableMask = !ignoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(Attributes.Filter);

            skCanvas.Save();

            var skMatrix = SvgExtensions.ToSKMatrix(svgTextBase.Transforms);

            var skMatrixTotal = skCanvas.TotalMatrix;
            skMatrixTotal = skMatrixTotal.PreConcat(skMatrix);
            skCanvas.SetMatrix(skMatrixTotal);

            if (enableClip)
            {
#if USE_PICTURE
                var clipPath = new Svg.Picture.ClipPath()
                {
                    Clip = new Svg.Picture.ClipPath()
                };
                SvgExtensions.GetSvgVisualElementClipPath(svgTextBase, TransformedBounds, new HashSet<Uri>(), disposable, clipPath);
                if (clipPath.Clips != null && clipPath.Clips.Count > 0 && !IgnoreAttributes.HasFlag(Attributes.ClipPath))
                {
                    bool antialias = SvgExtensions.IsAntialias(svgTextBase);
                    skCanvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias);
                }
#else
                var skPathClip = SvgExtensions.GetSvgVisualElementClipPath(svgTextBase, skBounds, new HashSet<Uri>(), disposable);
                if (skPathClip != null && !IgnoreAttributes.HasFlag(Attributes.ClipPath))
                {
                    bool antialias = SvgExtensions.IsAntialias(svgTextBase);
                    skCanvas.ClipPath(skPathClip, SKClipOperation.Intersect, antialias);
                }
#endif
            }

            if (enableMask)
            {
                var mask = default(SKPaint);
                maskDstIn = default(SKPaint);
                maskDrawable = SvgExtensions.GetSvgElementMask(svgTextBase, skBounds, new HashSet<Uri>(), disposable);
                if (maskDrawable != null)
                {
                    mask = new SKPaint()
                    {
                        IsAntialias = true,
                        Style = SKPaintStyle.StrokeAndFill
                    };
                    disposable.Add(mask);

                    var lumaColor = SKColorFilter.CreateLumaColor();
                    Disposable.Add(lumaColor);

                    maskDstIn = new SKPaint
                    {
                        IsAntialias = true,
                        Style = SKPaintStyle.StrokeAndFill,
                        BlendMode = SKBlendMode.DstIn,
                        Color = SvgExtensions.s_transparentBlack,
                        ColorFilter = lumaColor
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

            if (enableOpacity)
            {
                skPaintOpacity = SvgExtensions.GetOpacitySKPaint(svgTextBase, disposable);
                if (skPaintOpacity != null && !IgnoreAttributes.HasFlag(Attributes.Opacity))
                {
                    skCanvas.SaveLayer(skPaintOpacity);
                }
            }
            else
            {
                skPaintOpacity = null;
            }

            if (enableFilter)
            {
                skPaintFilter = SvgExtensions.GetFilterSKPaint(svgTextBase, skBounds, this, disposable, out var isValid);
                if (skPaintFilter != null && !IgnoreAttributes.HasFlag(Attributes.Filter))
                {
                    skCanvas.SaveLayer(skPaintFilter);
                }
            }
            else
            {
                skPaintFilter = null;
            }
        }

        internal void EndDraw(SKCanvas skCanvas, Attributes ignoreAttributes, MaskDrawable? maskDrawable, SKPaint? maskDstIn, SKPaint? skPaintOpacity, SKPaint? skPaintFilter, DrawableBase? until)
        {
            var enableMask = !ignoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(Attributes.Filter);

            if (skPaintFilter != null && enableFilter)
            {
                skCanvas.Restore();
            }

            if (skPaintOpacity != null && enableOpacity)
            {
                skCanvas.Restore();
            }

            if (maskDrawable != null && enableMask && maskDstIn != null)
            {
                skCanvas.SaveLayer(maskDstIn);
                maskDrawable.Draw(skCanvas, ignoreAttributes, until);
                skCanvas.Restore();
                skCanvas.Restore();
            }

            skCanvas.Restore();
        }

        internal void DrawTextString(SvgTextBase svgTextBase, string text, float x, float y, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, DrawableBase? until)
        {
            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            if (SvgExtensions.IsValidFill(svgTextBase))
            {
                var skPaint = SvgExtensions.GetFillSKPaint(svgTextBase, skBounds, ignoreAttributes, Disposable);
                if (skPaint != null)
                {
                    SvgExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, Disposable);
#if USE_TEXT_SHAPER
                    var typeface = skPaint.Typeface;
                    if (typeface != null)
                    {
                        using var skShaper = new SKShaper(skPaint.Typeface);
                        skCanvas.DrawShapedText(skShaper, text, x, y, skPaint);
                    }
#else
                    skCanvas.DrawText(text, x, y, skPaint);
#endif
                }
            }

            if (SvgExtensions.IsValidStroke(svgTextBase, skBounds))
            {
                var skPaint = SvgExtensions.GetStrokeSKPaint(svgTextBase, skBounds, ignoreAttributes, Disposable);
                if (skPaint != null)
                {
                    SvgExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, Disposable);
#if USE_TEXT_SHAPER
                    var typeface = skPaint.Typeface;
                    if (typeface != null)
                    {
                        using var skShaper = new SKShaper(skPaint.Typeface);
                        skCanvas.DrawShapedText(skShaper, text, x, y, skPaint);
                    }
#else
                    skCanvas.DrawText(text, x, y, skPaint);
#endif
                }
            }
        }

        internal void DrawTextBase(SvgTextBase svgTextBase, string? text, float currentX, float currentY, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, DrawableBase? until)
        {
            // TODO: Fix SvgTextBase rendering.
            bool isValidFill = SvgExtensions.IsValidFill(svgTextBase);
            bool isValidStroke = SvgExtensions.IsValidStroke(svgTextBase, skOwnerBounds);

            if ((!isValidFill && !isValidStroke) || text is null || string.IsNullOrEmpty(text))
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

                if (SvgExtensions.IsValidFill(svgTextBase))
                {
                    var skPaint = SvgExtensions.GetFillSKPaint(svgTextBase, skBounds, ignoreAttributes, Disposable);
                    if (skPaint != null)
                    {
                        SvgExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, Disposable);
                        skCanvas.DrawPositionedText(text, points, skPaint);
                    }
                }

                if (SvgExtensions.IsValidStroke(svgTextBase, skBounds))
                {
                    var skPaint = SvgExtensions.GetStrokeSKPaint(svgTextBase, skBounds, ignoreAttributes, Disposable);
                    if (skPaint != null)
                    {
                        SvgExtensions.SetSKPaintText(svgTextBase, skBounds, skPaint, Disposable);
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

                DrawTextString(svgTextBase, text, x + dx, y + dy, skOwnerBounds, ignoreAttributes, skCanvas, until);
            }
        }

        internal void DrawTextPath(SvgTextPath svgTextPath, float currentX, float currentY, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, DrawableBase? until)
        {
            if (!CanDraw(svgTextPath, ignoreAttributes) || !HasFeatures(svgTextPath, ignoreAttributes))
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgTextPath, (e) => e.ReferencedPath, new HashSet<Uri>()))
            {
                return;
            }

            var svgPath = SvgExtensions.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
            if (svgPath is null)
            {
                return;
            }

            var skPath = svgPath.PathData?.ToSKPath(svgPath.FillRule, Disposable);
            if (skPath is null || skPath.IsEmpty)
            {
                return;
            }

#if !USE_PICTURE // TODO:
            var skMatrixPath = SvgExtensions.ToSKMatrix(svgPath.Transforms);
            skPath.Transform(skMatrixPath); 
#endif

            // TODO: Implement StartOffset
            var startOffset = svgTextPath.StartOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, skOwnerBounds);

            float hOffset = currentX + startOffset;
            float vOffset = currentY;

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            BeginDraw(svgTextPath, skCanvas, skBounds, ignoreAttributes, Disposable, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter);

            // TODO: Fix SvgTextPath rendering.
            bool isValidFill = SvgExtensions.IsValidFill(svgTextPath);
            bool isValidStroke = SvgExtensions.IsValidStroke(svgTextPath, skBounds);

            if (isValidFill || isValidStroke)
            {
                if (!string.IsNullOrEmpty(svgTextPath.Text))
                {
                    var text = PrepareText(svgTextPath, svgTextPath.Text);

                    if (SvgExtensions.IsValidFill(svgTextPath))
                    {
                        var skPaint = SvgExtensions.GetFillSKPaint(svgTextPath, skBounds, ignoreAttributes, Disposable);
                        if (skPaint != null)
                        {
                            SvgExtensions.SetSKPaintText(svgTextPath, skBounds, skPaint, Disposable);
                            skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                        }
                    }

                    if (SvgExtensions.IsValidStroke(svgTextPath, skBounds))
                    {
                        var skPaint = SvgExtensions.GetStrokeSKPaint(svgTextPath, skBounds, ignoreAttributes, Disposable);
                        if (skPaint != null)
                        {
                            SvgExtensions.SetSKPaintText(svgTextPath, skBounds, skPaint, Disposable);
                            skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                        }
                    }
                }
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, until);
        }

        internal void DrawTextRef(SvgTextRef svgTextRef, float currentX, float currentY, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, DrawableBase? until)
        {
            if (!CanDraw(svgTextRef, ignoreAttributes) || !HasFeatures(svgTextRef, ignoreAttributes))
            {
                return;
            }

            if (SvgExtensions.HasRecursiveReference(svgTextRef, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                return;
            }

            var svgReferencedText = SvgExtensions.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
            if (svgReferencedText is null)
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            BeginDraw(svgTextRef, skCanvas, skBounds, ignoreAttributes, Disposable, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter);

            // TODO: Draw svgReferencedText
            if (!string.IsNullOrEmpty(svgReferencedText.Text))
            {
                var text = PrepareText(svgReferencedText, svgReferencedText.Text);
                DrawTextBase(svgReferencedText, svgReferencedText.Text, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, until);
        }

        internal void DrawTextSpan(SvgTextSpan svgTextSpan, float currentX, float currentY, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, DrawableBase? until)
        {
            if (!CanDraw(svgTextSpan, ignoreAttributes) || !HasFeatures(svgTextSpan, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            BeginDraw(svgTextSpan, skCanvas, skBounds, ignoreAttributes, Disposable, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter);

            // TODO: Implement SvgTextSpan drawing.
            if (!string.IsNullOrEmpty(svgTextSpan.Text))
            {
                var text = PrepareText(svgTextSpan, svgTextSpan.Text);
                DrawTextBase(svgTextSpan, text, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, until);
        }

        internal void DrawText(SvgText svgText, SKRect skOwnerBounds, Attributes ignoreAttributes, SKCanvas skCanvas, DrawableBase? until)
        {
            if (!CanDraw(svgText, ignoreAttributes) || !HasFeatures(svgText, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            BeginDraw(svgText, skCanvas, skBounds, ignoreAttributes, Disposable, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter);

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
                        DrawTextBase(svgText, text, 0f, 0f, skOwnerBounds, ignoreAttributes, skCanvas, until);
                    }
                }
                else
                {
                    switch (textNode)
                    {
                        case SvgTextPath svgTextPath:
                            DrawTextPath(svgTextPath, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
                            break;
                        case SvgTextRef svgTextRef:
                            DrawTextRef(svgTextRef, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
                            break;
                        case SvgTextSpan svgTextSpan:
                            DrawTextSpan(svgTextSpan, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
                            break;
                        default:
                            break;
                    }
                }
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, until);
        }

        public override void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            // TODO: Currently using custom OnDraw override.
        }

        public override void Draw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until)
        {
            if (until != null && this == until)
            {
                return;
            }

            if (Text != null)
            {
                DrawText(Text, OwnerBounds, ignoreAttributes, canvas, until);
            }
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            // TODO:
            Draw(canvas, IgnoreAttributes, null);
        }

        public override void PostProcess()
        {
            // TODO:
        }
    }
}
