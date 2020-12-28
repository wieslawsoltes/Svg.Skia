using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Svg.Model.Painting;
using Svg.Model.Primitives;

namespace Svg.Model.Drawables.Elements
{
    public sealed class TextDrawable : DrawableBase
    {
        private static readonly Regex s_multipleSpaces = new(@" {2,}", RegexOptions.Compiled);

        public SvgText? Text { get; set; }

        public Rect OwnerBounds { get; set; }

        private TextDrawable(IAssetLoader assetLoader)
            : base(assetLoader)
        {
        }

        public static TextDrawable Create(SvgText svgText, Rect skOwnerBounds, DrawableBase? parent, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            return new(assetLoader)
            {
                Element = svgText,
                Parent = parent,
                Text = svgText,
                OwnerBounds = skOwnerBounds,
                IgnoreAttributes = ignoreAttributes
            };
        }

        internal void GetPositionsX(SvgTextBase svgTextBase, Rect skBounds, List<float> xs)
        {
            var _x = svgTextBase.X;

            for (var i = 0; i < _x.Count; i++)
            {
                xs.Add(_x[i].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsY(SvgTextBase svgTextBase, Rect skBounds, List<float> ys)
        {
            var _y = svgTextBase.Y;

            for (var i = 0; i < _y.Count; i++)
            {
                ys.Add(_y[i].ToDeviceValue(UnitRenderingType.VerticalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsDX(SvgTextBase svgTextBase, Rect skBounds, List<float> dxs)
        {
            var _dx = svgTextBase.Dx;

            for (var i = 0; i < _dx.Count; i++)
            {
                dxs.Add(_dx[i].ToDeviceValue(UnitRenderingType.HorizontalOffset, svgTextBase, skBounds));
            }
        }

        internal void GetPositionsDY(SvgTextBase svgTextBase, Rect skBounds, List<float> dys)
        {
            var _dy = svgTextBase.Dy;

            for (var i = 0; i < _dy.Count; i++)
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

        internal void BeginDraw(SvgTextBase svgTextBase, Canvas skCanvas, Rect skBounds, Attributes ignoreAttributes, out MaskDrawable? maskDrawable, out Paint? maskDstIn, out Paint? skPaintOpacity, out Paint? skPaintFilter)
        {
            var enableClip = !ignoreAttributes.HasFlag(Attributes.ClipPath);
            var enableMask = !ignoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(Attributes.Filter);

            skCanvas.Save();

            var skMatrix = SvgModelExtensions.ToMatrix(svgTextBase.Transforms);

            var skMatrixTotal = skCanvas.TotalMatrix;
            skMatrixTotal = skMatrixTotal.PreConcat(skMatrix);
            skCanvas.SetMatrix(skMatrixTotal);

            if (enableClip)
            {
                var clipPath = new ClipPath
                {
                    Clip = new ClipPath()
                };
                SvgModelExtensions.GetSvgVisualElementClipPath(svgTextBase, TransformedBounds, new HashSet<Uri>(), clipPath);
                if (clipPath.Clips is { } && clipPath.Clips.Count > 0 && !IgnoreAttributes.HasFlag(Attributes.ClipPath))
                {
                    var antialias = SvgModelExtensions.IsAntialias(svgTextBase);
                    skCanvas.ClipPath(clipPath, ClipOperation.Intersect, antialias);
                }
            }

            if (enableMask)
            {
                var mask = default(Paint);
                maskDstIn = default(Paint);
                maskDrawable = SvgModelExtensions.GetSvgElementMask(svgTextBase, skBounds, new HashSet<Uri>(), AssetLoader);
                if (maskDrawable is { })
                {
                    mask = new Paint
                    {
                        IsAntialias = true,
                        Style = PaintStyle.StrokeAndFill
                    };

                    var lumaColor = ColorFilter.CreateLumaColor();

                    maskDstIn = new Paint
                    {
                        IsAntialias = true,
                        Style = PaintStyle.StrokeAndFill,
                        BlendMode = BlendMode.DstIn,
                        Color = SvgModelExtensions.s_transparentBlack,
                        ColorFilter = lumaColor
                    };
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
                skPaintOpacity = SvgModelExtensions.GetOpacityPaint(svgTextBase);
                if (skPaintOpacity is { } && !IgnoreAttributes.HasFlag(Attributes.Opacity))
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
                skPaintFilter = SvgModelExtensions.GetFilterPaint(svgTextBase, skBounds, this, AssetLoader, out var isValid);
                if (skPaintFilter is { } && !IgnoreAttributes.HasFlag(Attributes.Filter))
                {
                    skCanvas.SaveLayer(skPaintFilter);
                }
            }
            else
            {
                skPaintFilter = null;
            }
        }

        internal void EndDraw(Canvas skCanvas, Attributes ignoreAttributes, MaskDrawable? maskDrawable, Paint? maskDstIn, Paint? skPaintOpacity, Paint? skPaintFilter, DrawableBase? until)
        {
            var enableMask = !ignoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(Attributes.Filter);

            if (skPaintFilter is { } && enableFilter)
            {
                skCanvas.Restore();
            }

            if (skPaintOpacity is { } && enableOpacity)
            {
                skCanvas.Restore();
            }

            if (maskDrawable is { } && enableMask && maskDstIn is { })
            {
                skCanvas.SaveLayer(maskDstIn);
                maskDrawable.Draw(skCanvas, ignoreAttributes, until);
                skCanvas.Restore();
                skCanvas.Restore();
            }

            skCanvas.Restore();
        }

        internal void DrawTextString(SvgTextBase svgTextBase, string text, float x, float y, Rect skOwnerBounds, Attributes ignoreAttributes, Canvas skCanvas, DrawableBase? until)
        {
            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            if (SvgModelExtensions.IsValidFill(svgTextBase))
            {
                var skPaint = SvgModelExtensions.GetFillPaint(svgTextBase, skBounds, AssetLoader, ignoreAttributes);
                if (skPaint is { })
                {
                    SvgModelExtensions.SetPaintText(svgTextBase, skBounds, skPaint);
#if USE_TEXT_SHAPER
                    var typeface = skPaint.Typeface;
                    if (typeface is { })
                    {
                        using var skShaper = new SKShaper(skPaint.Typeface);
                        skCanvas.DrawShapedText(skShaper, text, x, y, skPaint);
                    }
#else
                    skCanvas.DrawText(text, x, y, skPaint);
#endif
                }
            }

            if (SvgModelExtensions.IsValidStroke(svgTextBase, skBounds))
            {
                var skPaint = SvgModelExtensions.GetStrokePaint(svgTextBase, skBounds, AssetLoader, ignoreAttributes);
                if (skPaint is { })
                {
                    SvgModelExtensions.SetPaintText(svgTextBase, skBounds, skPaint);
#if USE_TEXT_SHAPER
                    var typeface = skPaint.Typeface;
                    if (typeface is { })
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

        internal void DrawTextBase(SvgTextBase svgTextBase, string? text, float currentX, float currentY, Rect skOwnerBounds, Attributes ignoreAttributes, Canvas skCanvas, DrawableBase? until)
        {
            // TODO: Fix SvgTextBase rendering.
            var isValidFill = SvgModelExtensions.IsValidFill(svgTextBase);
            var isValidStroke = SvgModelExtensions.IsValidStroke(svgTextBase, skOwnerBounds);

            if (!isValidFill && !isValidStroke || text is null || string.IsNullOrEmpty(text))
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
                var points = new Point[xs.Count];

                for (var i = 0; i < xs.Count; i++)
                {
                    var x = xs[i];
                    var y = ys[i];
                    float dx = 0;
                    float dy = 0;
                    if (dxs.Count >= 1 && xs.Count >= dxs.Count)
                    {
                        dx = dxs[i];
                    }
                    if (dys.Count >= 1 && ys.Count >= dys.Count)
                    {
                        dy = dys[i];
                    }
                    points[i] = new Point(x + dx, y + dy);
                }

                // TODO: Calculate correct bounds.
                var skBounds = skOwnerBounds;

                if (SvgModelExtensions.IsValidFill(svgTextBase))
                {
                    var skPaint = SvgModelExtensions.GetFillPaint(svgTextBase, skBounds, AssetLoader, ignoreAttributes);
                    if (skPaint is { })
                    {
                        SvgModelExtensions.SetPaintText(svgTextBase, skBounds, skPaint);

                        var textBlob = new TextBlob
                        {
                            Text = text,
                            Points = points
                        };
                        skCanvas.DrawText(textBlob, 0, 0, skPaint);
                    }
                }

                if (SvgModelExtensions.IsValidStroke(svgTextBase, skBounds))
                {
                    var skPaint = SvgModelExtensions.GetStrokePaint(svgTextBase, skBounds, AssetLoader, ignoreAttributes);
                    if (skPaint is { })
                    {
                        SvgModelExtensions.SetPaintText(svgTextBase, skBounds, skPaint);
                        var textBlob = new TextBlob
                        {
                            Text = text,
                            Points = points
                        };
                        skCanvas.DrawText(textBlob, 0, 0, skPaint);
                    }
                }
            }
            else
            {
                var x = xs.Count >= 1 ? xs[0] : currentX;
                var y = ys.Count >= 1 ? ys[0] : currentY;
                var dx = dxs.Count >= 1 ? dxs[0] : 0f;
                var dy = dys.Count >= 1 ? dys[0] : 0f;

                DrawTextString(svgTextBase, text, x + dx, y + dy, skOwnerBounds, ignoreAttributes, skCanvas, until);
            }
        }

        internal void DrawTextPath(SvgTextPath svgTextPath, float currentX, float currentY, Rect skOwnerBounds, Attributes ignoreAttributes, Canvas skCanvas, DrawableBase? until)
        {
            if (!CanDraw(svgTextPath, ignoreAttributes) || !HasFeatures(svgTextPath, ignoreAttributes))
            {
                return;
            }

            if (SvgModelExtensions.HasRecursiveReference(svgTextPath, (e) => e.ReferencedPath, new HashSet<Uri>()))
            {
                return;
            }

            var svgPath = SvgModelExtensions.GetReference<SvgPath>(svgTextPath, svgTextPath.ReferencedPath);
            if (svgPath is null)
            {
                return;
            }

            var skPath = svgPath.PathData?.ToPath(svgPath.FillRule);
            if (skPath is null || skPath.IsEmpty)
            {
                return;
            }

            // TODO:
            // var skMatrixPath = SvgModelExtensions.ToSKMatrix(svgPath.Transforms);
            // skPath.Transform(skMatrixPath);

            // TODO: Implement StartOffset
            var startOffset = svgTextPath.StartOffset.ToDeviceValue(UnitRenderingType.Other, svgTextPath, skOwnerBounds);

            var hOffset = currentX + startOffset;
            var vOffset = currentY;

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            BeginDraw(svgTextPath, skCanvas, skBounds, ignoreAttributes, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter);

            // TODO: Fix SvgTextPath rendering.
            var isValidFill = SvgModelExtensions.IsValidFill(svgTextPath);
            var isValidStroke = SvgModelExtensions.IsValidStroke(svgTextPath, skBounds);

            if (isValidFill || isValidStroke)
            {
                if (!string.IsNullOrEmpty(svgTextPath.Text))
                {
                    var text = PrepareText(svgTextPath, svgTextPath.Text);

                    if (SvgModelExtensions.IsValidFill(svgTextPath))
                    {
                        var skPaint = SvgModelExtensions.GetFillPaint(svgTextPath, skBounds, AssetLoader, ignoreAttributes);
                        if (skPaint is { })
                        {
                            SvgModelExtensions.SetPaintText(svgTextPath, skBounds, skPaint);
                            skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                        }
                    }

                    if (SvgModelExtensions.IsValidStroke(svgTextPath, skBounds))
                    {
                        var skPaint = SvgModelExtensions.GetStrokePaint(svgTextPath, skBounds, AssetLoader, ignoreAttributes);
                        if (skPaint is { })
                        {
                            SvgModelExtensions.SetPaintText(svgTextPath, skBounds, skPaint);
                            skCanvas.DrawTextOnPath(text, skPath, hOffset, vOffset, skPaint);
                        }
                    }
                }
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, until);
        }

        internal void DrawTextRef(SvgTextRef svgTextRef, float currentX, float currentY, Rect skOwnerBounds, Attributes ignoreAttributes, Canvas skCanvas, DrawableBase? until)
        {
            if (!CanDraw(svgTextRef, ignoreAttributes) || !HasFeatures(svgTextRef, ignoreAttributes))
            {
                return;
            }

            if (SvgModelExtensions.HasRecursiveReference(svgTextRef, (e) => e.ReferencedElement, new HashSet<Uri>()))
            {
                return;
            }

            var svgReferencedText = SvgModelExtensions.GetReference<SvgText>(svgTextRef, svgTextRef.ReferencedElement);
            if (svgReferencedText is null)
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            BeginDraw(svgTextRef, skCanvas, skBounds, ignoreAttributes, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter);

            // TODO: Draw svgReferencedText
            if (!string.IsNullOrEmpty(svgReferencedText.Text))
            {
                var text = PrepareText(svgReferencedText, svgReferencedText.Text);
                DrawTextBase(svgReferencedText, svgReferencedText.Text, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, until);
        }

        internal void DrawTextSpan(SvgTextSpan svgTextSpan, float currentX, float currentY, Rect skOwnerBounds, Attributes ignoreAttributes, Canvas skCanvas, DrawableBase? until)
        {
            if (!CanDraw(svgTextSpan, ignoreAttributes) || !HasFeatures(svgTextSpan, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            BeginDraw(svgTextSpan, skCanvas, skBounds, ignoreAttributes, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter);

            // TODO: Implement SvgTextSpan drawing.
            if (!string.IsNullOrEmpty(svgTextSpan.Text))
            {
                var text = PrepareText(svgTextSpan, svgTextSpan.Text);
                DrawTextBase(svgTextSpan, text, currentX, currentY, skOwnerBounds, ignoreAttributes, skCanvas, until);
            }

            EndDraw(skCanvas, ignoreAttributes, maskDrawable, maskDstIn, skPaintOpacity, skPaintFilter, until);
        }

        internal void DrawText(SvgText svgText, Rect skOwnerBounds, Attributes ignoreAttributes, Canvas skCanvas, DrawableBase? until)
        {
            if (!CanDraw(svgText, ignoreAttributes) || !HasFeatures(svgText, ignoreAttributes))
            {
                return;
            }

            // TODO: Calculate correct bounds.
            var skBounds = skOwnerBounds;

            BeginDraw(svgText, skCanvas, skBounds, ignoreAttributes, out var maskDrawable, out var maskDstIn, out var skPaintOpacity, out var skPaintFilter);

            var xs = new List<float>();
            var ys = new List<float>();
            var dxs = new List<float>();
            var dys = new List<float>();
            GetPositionsX(svgText, skOwnerBounds, xs);
            GetPositionsY(svgText, skOwnerBounds, ys);
            GetPositionsDX(svgText, skOwnerBounds, dxs);
            GetPositionsDY(svgText, skOwnerBounds, dys);

            var x = xs.Count >= 1 ? xs[0] : 0f;
            var y = ys.Count >= 1 ? ys[0] : 0f;
            var dx = dxs.Count >= 1 ? dxs[0] : 0f;
            var dy = dys.Count >= 1 ? dys[0] : 0f;

            var currentX = x + dx;
            var currentY = y + dy;

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

        public override void OnDraw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until)
        {
            if (until is { } && this == until)
            {
                return;
            }

            // TODO: Currently using custom OnDraw override.
        }

        public override void Draw(Canvas canvas, Attributes ignoreAttributes, DrawableBase? until)
        {
            if (until is { } && this == until)
            {
                return;
            }

            if (Text is { })
            {
                DrawText(Text, OwnerBounds, ignoreAttributes, canvas, until);
            }
        }

        protected override void OnDraw(Canvas canvas)
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
