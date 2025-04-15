using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Model.Services;

namespace Svg.Model.Drawables.Elements;

public sealed class MaskDrawable : DrawableContainer
{
    private MaskDrawable(ISvgAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static MaskDrawable Create(SvgMask svgMask, SKRect skViewport, DrawableBase? parent, ISvgAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var drawable = new MaskDrawable(assetLoader, references)
        {
            Element = svgMask,
            Parent = parent,
            IgnoreAttributes = ignoreAttributes,
            IsDrawable = true
        };

        if (!drawable.IsDrawable)
        {
            return drawable;
        }

        var maskUnits = svgMask.MaskUnits;
        var maskContentUnits = svgMask.MaskContentUnits;
        var xUnit = svgMask.X;
        var yUnit = svgMask.Y;
        var widthUnit = svgMask.Width;
        var heightUnit = svgMask.Height;

        // TODO: Pass correct skViewport
        var skRectTransformed = TransformsService.CalculateRect(xUnit, yUnit, widthUnit, heightUnit, maskUnits, skViewport, skViewport, svgMask);
        if (skRectTransformed is null)
        {
            drawable.IsDrawable = false;
            return drawable;
        }

        var skMatrix = SKMatrix.CreateIdentity();

        if (maskContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
        {
            var skBoundsTranslateTransform = SKMatrix.CreateTranslation(skViewport.Left, skViewport.Top);
            skMatrix = skMatrix.PreConcat(skBoundsTranslateTransform);

            var skBoundsScaleTransform = SKMatrix.CreateScale(skViewport.Width, skViewport.Height);
            skMatrix = skMatrix.PreConcat(skBoundsScaleTransform);
        }

        drawable.CreateChildren(svgMask, skViewport, drawable, assetLoader, references, ignoreAttributes);

        drawable.Initialize(skRectTransformed.Value, skMatrix);

        return drawable;
    }

    private void Initialize(SKRect skRectTransformed, SKMatrix skMatrix)
    {
        if (Element is not SvgMask svgMask)
        {
            return;
        }

        Overflow = skRectTransformed;

        IsAntialias = PaintingService.IsAntialias(svgMask);

        GeometryBounds = skRectTransformed;

        Transform = skMatrix;

        Fill = null;
        Stroke = null;
    }
    
    public override void PostProcess(SKRect? viewport, SKMatrix totalMatrix)
    {
        var element = Element;
        if (element is null)
        {
            return;
        }

        var enableMask = !IgnoreAttributes.HasFlag(DrawAttributes.Mask);

        ClipPath = null;

        if (enableMask)
        {
            MaskDrawable = MaskingService.GetSvgElementMask(element, GeometryBounds, new HashSet<Uri>(), AssetLoader, References);
            if (MaskDrawable is { })
            {
                CreateMaskPaints();
            }
        }
        else
        {
            MaskDrawable = null;
        }

        Opacity = null;
        Filter = null;

        TotalTransform = totalMatrix.PreConcat(Transform);
        TransformedBounds = TotalTransform.MapRect(GeometryBounds);
    }
}
