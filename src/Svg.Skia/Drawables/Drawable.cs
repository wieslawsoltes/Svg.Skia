// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;
using Svg.Document_Structure;

namespace Svg.Skia
{
    public abstract class Drawable : SKDrawable, IFilterSource, IPictureSource
    {
        internal CompositeDisposable _disposable = new CompositeDisposable();

        public SvgElement? Element;
        public Drawable? Root;
        public Drawable? Parent;
        public bool IsDrawable;
        public Attributes IgnoreAttributes;
        public bool IsAntialias;
        public SKRect TransformedBounds;
        public SKMatrix Transform;
        public SKRect? Overflow;
        public SKRect? Clip;
        public SKPath? ClipPath;
        public MaskDrawable? MaskDrawable;
        public SKPaint? Mask;
        public SKPaint? MaskDstIn;
        public SKPaint? Opacity;
        public SKPaint? Filter;
        public SKPaint? Fill;
        public SKPaint? Stroke;

        public Drawable(SvgElement? element, Drawable? root, Drawable? parent)
        {
            Element = element;
            Root = root;
            Parent = parent;
        }

        protected override void OnDraw(SKCanvas canvas)
        {
            Draw(canvas, IgnoreAttributes, null);
        }

        protected override SKRect OnGetBounds()
        {
            if (IsDrawable)
            {
                return TransformedBounds;
            }
            return SKRect.Empty;
        }

        public virtual Drawable? HitTest(SKPoint skPoint)
        {
            if (TransformedBounds.Contains(skPoint))
            {
                return this;
            }
            return null;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _disposable?.Dispose();
        }

        protected void CreateMaskPaints()
        {
            Mask = new SKPaint()
            {
                IsAntialias = true,
                Style = SKPaintStyle.StrokeAndFill
            };
            _disposable.Add(Mask);

            var lumaColor = SKColorFilter.CreateLumaColor();
            _disposable.Add(lumaColor);

            MaskDstIn = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.StrokeAndFill,
                BlendMode = SKBlendMode.DstIn,
                Color = SvgPaintingExtensions.TransparentBlack,
                ColorFilter = lumaColor
            };
            _disposable.Add(MaskDstIn);
        }

        protected bool CanDraw(SvgVisualElement svgVisualElement, Attributes ignoreAttributes)
        {
            bool visible = svgVisualElement.Visible == true;
            bool display = ignoreAttributes.HasFlag(Attributes.Display) ? true : !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        public abstract void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until);

        public virtual void Draw(SKCanvas canvas, Attributes ignoreAttributes, Drawable? until)
        {
            if (!IsDrawable)
            {
                return;
            }

            if (until != null && this == until)
            {
                return;
            }

            var enableClip = !ignoreAttributes.HasFlag(Attributes.ClipPath);
            var enableMask = !ignoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !ignoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !ignoreAttributes.HasFlag(Attributes.Filter);

            canvas.Save();

            if (Overflow != null)
            {
                canvas.ClipRect(Overflow.Value, SKClipOperation.Intersect);
            }

            var skMatrixTotal = canvas.TotalMatrix;
            SKMatrix.PreConcat(ref skMatrixTotal, ref Transform);
            canvas.SetMatrix(skMatrixTotal);

            if (Clip != null)
            {
                canvas.ClipRect(Clip.Value, SKClipOperation.Intersect);
            }

            if (ClipPath != null && enableClip == true)
            {
                canvas.ClipPath(ClipPath, SKClipOperation.Intersect, IsAntialias);
            }

            if (MaskDrawable != null && enableMask == true)
            {
                canvas.SaveLayer(Mask);
            }

            if (Opacity != null && enableOpacity == true)
            {
                canvas.SaveLayer(Opacity);
            }

            if (Filter != null && enableFilter == true)
            {
                canvas.SaveLayer(Filter);
            }

            OnDraw(canvas, ignoreAttributes, until);

            if (Filter != null && enableFilter == true)
            {
                canvas.Restore();
            }

            if (Opacity != null && enableOpacity == true)
            {
                canvas.Restore();
            }

            if (MaskDrawable != null && enableMask == true)
            {
                canvas.SaveLayer(MaskDstIn);
                MaskDrawable.Draw(canvas, ignoreAttributes, until);
                canvas.Restore();
                canvas.Restore();
            }

            canvas.Restore();
        }

        public virtual void PostProcess()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var visualElement = element as SvgVisualElement;

            var enableClip = !IgnoreAttributes.HasFlag(Attributes.ClipPath);
            var enableMask = !IgnoreAttributes.HasFlag(Attributes.Mask);
            var enableOpacity = !IgnoreAttributes.HasFlag(Attributes.Opacity);
            var enableFilter = !IgnoreAttributes.HasFlag(Attributes.Filter);

            if (visualElement != null && enableClip == true)
            {
                ClipPath = SvgClippingExtensions.GetSvgVisualElementClipPath(visualElement, TransformedBounds, new HashSet<Uri>(), _disposable);
            }
            else
            {
                ClipPath = null;
            }

            if (enableMask == true)
            {
                MaskDrawable = SvgClippingExtensions.GetSvgElementMask(element, TransformedBounds, new HashSet<Uri>(), _disposable);
                if (MaskDrawable != null)
                {
                    CreateMaskPaints();
                }
            }
            else
            {
                MaskDrawable = null;
            }

            if (enableOpacity == true)
            {
                Opacity = SvgPaintingExtensions.GetOpacitySKPaint(element, _disposable);
            }
            else
            {
                Opacity = null;
            }

            if (visualElement != null && enableFilter == true)
            {
                Filter = SvgFiltersExtensions.GetFilterSKPaint(visualElement, TransformedBounds, this, _disposable);
            }
            else
            {
                Filter = null;
            }
        }

        public bool IsContainerElement(SvgElement svgElement)
        {
            switch (svgElement)
            {
                case SvgAnchor _:
                case SvgDefinitionList _:
                case SvgMissingGlyph _:
                case SvgGlyph _:
                case SvgGroup _:
                case SvgMarker _:
                case SvgMask _:
                case SvgPatternServer _:
                case SvgFragment _:
                case SvgSwitch _:
                case SvgSymbol _:
                    return true;
                default:
                    return false;
            }
        }

        public Drawable? FindContainerParentBackground(Drawable? drawable, out SKRect skClipRect)
        {
            skClipRect = SKRect.Empty;

            if (drawable == null)
            {
                return null;
            }

            var element = drawable.Element;
            if (element == null)
            {
                return null;
            }

            if (IsContainerElement(element))
            {
                if (element.TryGetAttribute("enable-background", out string enableBackground))
                {
                    enableBackground = enableBackground.Trim();

                    if (enableBackground == "accumulate")
                    {
                        // TODO:
                    }
                    else if (enableBackground.StartsWith("new"))
                    {
                        if (enableBackground.Length > 3)
                        {
                            var values = new List<float>();
                            var parts = enableBackground.Substring(4, enableBackground.Length - 4).Split(' ');
                            foreach (var o in parts)
                            {
                                values.Add(float.Parse(o.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture));
                            }

                            if (values.Count != 4)
                            {
                                return null;
                            }

                            skClipRect = SKRect.Create(values[0], values[1], values[2], values[3]);
                        }
                        return drawable;
                    }
                }
            }

            var parent = drawable.Parent;
            if (parent != null)
            {
                return FindContainerParentBackground(parent, out skClipRect);
            }

            return null;
        }
#if USE_EXPERIMENTAL_LINEAR_RGB
        // Precomputed sRGB to LinearRGB table.
        // if (C_srgb <= 0.04045)
        //     C_lin = C_srgb / 12.92;
        //  else
        //     C_lin = pow((C_srgb + 0.055) / 1.055, 2.4);
        public static byte[] s_SRGBtoLinearRGB = new byte[256]
        {
            0,   0,   0,   0,   0,   0,  0,    1,   1,   1,   1,   1,   1,   1,   1,   1,
            1,   1,   2,   2,   2,   2,  2,    2,   2,   2,   3,   3,   3,   3,   3,   3,
            4,   4,   4,   4,   4,   5,  5,    5,   5,   6,   6,   6,   6,   7,   7,   7,
            8,   8,   8,   8,   9,   9,  9,   10,  10,  10,  11,  11,  12,  12,  12,  13,
            13,  13,  14,  14,  15,  15,  16,  16,  17,  17,  17,  18,  18,  19,  19,  20,
            20,  21,  22,  22,  23,  23,  24,  24,  25,  25,  26,  27,  27,  28,  29,  29,
            30,  30,  31,  32,  32,  33,  34,  35,  35,  36,  37,  37,  38,  39,  40,  41,
            41,  42,  43,  44,  45,  45,  46,  47,  48,  49,  50,  51,  51,  52,  53,  54,
            55,  56,  57,  58,  59,  60,  61,  62,  63,  64,  65,  66,  67,  68,  69,  70,
            71,  72,  73,  74,  76,  77,  78,  79,  80,  81,  82,  84,  85,  86,  87,  88,
            90,  91,  92,  93,  95,  96,  97,  99, 100, 101, 103, 104, 105, 107, 108, 109,
            111, 112, 114, 115, 116, 118, 119, 121, 122, 124, 125, 127, 128, 130, 131, 133,
            134, 136, 138, 139, 141, 142, 144, 146, 147, 149, 151, 152, 154, 156, 157, 159,
            161, 163, 164, 166, 168, 170, 171, 173, 175, 177, 179, 181, 183, 184, 186, 188,
            190, 192, 194, 196, 198, 200, 202, 204, 206, 208, 210, 212, 214, 216, 218, 220,
            222, 224, 226, 229, 231, 233, 235, 237, 239, 242, 244, 246, 248, 250, 253, 255,
        };
        
        // Precomputed LinearRGB to sRGB table.
        // if (C_lin <= 0.0031308)
        //     C_srgb = C_lin * 12.92;
        // else
        //     C_srgb = 1.055 * pow(C_lin, 1.0 / 2.4) - 0.055;
        public static byte[] s_LinearRGBtoSRGB = new byte[256]
        {
            0,  13,  22,  28,  34,  38,  42,  46,  50,  53,  56,  59,  61,  64,  66,  69,
            71,  73,  75,  77,  79,  81,  83,  85,  86,  88,  90,  92,  93,  95,  96,  98,
            99, 101, 102, 104, 105, 106, 108, 109, 110, 112, 113, 114, 115, 117, 118, 119,
            120, 121, 122, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136,
            137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 148, 149, 150, 151,
            152, 153, 154, 155, 155, 156, 157, 158, 159, 159, 160, 161, 162, 163, 163, 164,
            165, 166, 167, 167, 168, 169, 170, 170, 171, 172, 173, 173, 174, 175, 175, 176,
            177, 178, 178, 179, 180, 180, 181, 182, 182, 183, 184, 185, 185, 186, 187, 187,
            188, 189, 189, 190, 190, 191, 192, 192, 193, 194, 194, 195, 196, 196, 197, 197,
            198, 199, 199, 200, 200, 201, 202, 202, 203, 203, 204, 205, 205, 206, 206, 207,
            208, 208, 209, 209, 210, 210, 211, 212, 212, 213, 213, 214, 214, 215, 215, 216,
            216, 217, 218, 218, 219, 219, 220, 220, 221, 221, 222, 222, 223, 223, 224, 224,
            225, 226, 226, 227, 227, 228, 228, 229, 229, 230, 230, 231, 231, 232, 232, 233,
            233, 234, 234, 235, 235, 236, 236, 237, 237, 238, 238, 238, 239, 239, 240, 240,
            241, 241, 242, 242, 243, 243, 244, 244, 245, 245, 246, 246, 246, 247, 247, 248,
            248, 249, 249, 250, 250, 251, 251, 251, 252, 252, 253, 253, 254, 254, 255, 255,
        };
#endif
        public SKPicture? RecordGraphic(Drawable? drawable, Attributes ignoreAttributes)
        {
            // TODO: Record using SKColorSpace.CreateSrgbLinear because .color-interpolation-filters. is by default linearRGB.
            if (drawable == null)
            {
                return null;
            }

            if (drawable.TransformedBounds.Width <= 0f && drawable.TransformedBounds.Height <= 0f)
            {
                return null;
            }

            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(drawable.TransformedBounds);

#if USE_EXPERIMENTAL_LINEAR_RGB
            // TODO:
            using var skPaint = new SKPaint();
            using var skColorFilter = SKColorFilter.CreateTable(null, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB);
            using var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter);
            skPaint.ImageFilter = skImageFilter;
            skCanvas.SaveLayer(skPaint);
#endif

            drawable.Draw(skCanvas, ignoreAttributes, null);

#if USE_EXPERIMENTAL_LINEAR_RGB
            // TODO:
            skCanvas.Restore();
#endif

            return skPictureRecorder.EndRecording();
        }

        public SKPicture? RecordBackground(Drawable? drawable, Attributes ignoreAttributes)
        {
            // TODO: Record using SKColorSpace.CreateSrgbLinear because 'color-interpolation-filters' is by default linearRGB.
            if (drawable == null)
            {
                return null;
            }

            var container = FindContainerParentBackground(drawable, out var skClipRect);
            if (container != null)
            {
                using var skPictureRecorder = new SKPictureRecorder();
                using var skCanvas = skPictureRecorder.BeginRecording(container.TransformedBounds);

                if (!skClipRect.IsEmpty)
                {
                    skCanvas.ClipRect(skClipRect, SKClipOperation.Intersect);
                }

#if USE_EXPERIMENTAL_LINEAR_RGB
                // TODO:
                using var skPaint = new SKPaint();
                using var skColorFilter = SKColorFilter.CreateTable(null, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB);
                using var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter);
                skPaint.ImageFilter = skImageFilter;
                skCanvas.SaveLayer(skPaint);
#endif

                container.Draw(skCanvas, ignoreAttributes, drawable);

#if USE_EXPERIMENTAL_LINEAR_RGB
                // TODO:
                skCanvas.Restore();
#endif

                return skPictureRecorder.EndRecording();
            }
            return null;
        }

        public const Attributes FilterInput = Attributes.ClipPath | Attributes.Mask | Attributes.Opacity | Attributes.Filter;

        SKPicture? IFilterSource.SourceGraphic() => RecordGraphic(this, FilterInput);

        SKPicture? IFilterSource.BackgroundImage() => RecordBackground(this, FilterInput);

        SKPaint? IFilterSource.FillPaint() => Fill;

        SKPaint? IFilterSource.StrokePaint() => Stroke;
    }
}
