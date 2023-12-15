/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using ShimSkiaSharp;

namespace Svg.Model.Drawables.Elements;

public sealed class MaskDrawable : DrawableContainer
{
    private MaskDrawable(IAssetLoader assetLoader, HashSet<Uri>? references)
        : base(assetLoader, references)
    {
    }

    public static MaskDrawable Create(SvgMask svgMask, SKRect skViewport, DrawableBase? parent, IAssetLoader assetLoader, HashSet<Uri>? references, DrawAttributes ignoreAttributes = DrawAttributes.None)
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
        var skRectTransformed = SvgExtensions.CalculateRect(xUnit, yUnit, widthUnit, heightUnit, maskUnits, skViewport, skViewport, svgMask);
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

        IsAntialias = SvgExtensions.IsAntialias(svgMask);

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
            MaskDrawable = SvgExtensions.GetSvgElementMask(element, GeometryBounds, new HashSet<Uri>(), AssetLoader, References);
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
