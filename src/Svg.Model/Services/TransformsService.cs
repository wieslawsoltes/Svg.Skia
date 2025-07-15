// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using ShimSkiaSharp;
using Svg.Transforms;

namespace Svg.Model.Services;

internal static class TransformsService
{
    internal static float ToDeviceValue(this SvgUnit svgUnit, UnitRenderingType renderType, SvgElement? owner, SKRect skBounds)
    {
        const float cmInInch = 2.54f;
        var ppi = SvgDocument.PointsPerInch;
        var type = svgUnit.Type;
        var value = svgUnit.Value;
        float? _deviceValue;
        float points;
        float? ownerFontSize = owner?.FontSize.ToDeviceValue(UnitRenderingType.Other, null, SKRect.Empty);

        switch (type)
        {
            case SvgUnitType.Em:
                if (ownerFontSize.HasValue)
                {
                    _deviceValue = ownerFontSize.Value * value;
                }
                else
                {
                    points = value * 9;
                    _deviceValue = points / 72.0f * ppi;
                }
                break;

            case SvgUnitType.Ex:
                points = value * 9;
                _deviceValue = points * 0.5f / 72.0f * ppi;
                break;

            case SvgUnitType.Centimeter:
                _deviceValue = value / cmInInch * ppi;
                break;

            case SvgUnitType.Inch:
                _deviceValue = value * ppi;
                break;

            case SvgUnitType.Millimeter:
                _deviceValue = value / 10 / cmInInch * ppi;
                break;

            case SvgUnitType.Pica:
                _deviceValue = value * 12 / 72 * ppi;
                break;

            case SvgUnitType.Point:
                _deviceValue = value / 72 * ppi;
                break;

            case SvgUnitType.Pixel:
                _deviceValue = value;
                break;

            case SvgUnitType.User:
                _deviceValue = value;
                break;

            case SvgUnitType.Percentage:
                var size = skBounds.Size;

                switch (renderType)
                {
                    case UnitRenderingType.Horizontal:
                        _deviceValue = size.Width / 100 * value;
                        break;

                    case UnitRenderingType.HorizontalOffset:
                        _deviceValue = size.Width / 100 * value + skBounds.Location.X;
                        break;

                    case UnitRenderingType.Vertical:
                        _deviceValue = size.Height / 100 * value;
                        break;

                    case UnitRenderingType.VerticalOffset:
                        _deviceValue = size.Height / 100 * value + skBounds.Location.Y;
                        break;

                    default:
                    case UnitRenderingType.Other:
                        if (owner?.OwnerDocument?.ViewBox is { } && owner.OwnerDocument.ViewBox.Width != 0 && owner.OwnerDocument.ViewBox.Height != 0)
                        {
                            _deviceValue = (float)(Math.Sqrt(Math.Pow(owner.OwnerDocument.ViewBox.Width, 2) + Math.Pow(owner.OwnerDocument.ViewBox.Height, 2)) / Math.Sqrt(2) * value / 100.0);
                        }
                        else
                        {
                            _deviceValue = (float)(Math.Sqrt(Math.Pow(size.Width, 2) + Math.Pow(size.Height, 2)) / Math.Sqrt(2) * value / 100.0);
                        }
                        break;
                }
                break;

            default:
                _deviceValue = value;
                break;
        }

        return _deviceValue.Value;
    }

    internal static void GetOptionalNumbers(this SvgNumberCollection? svgNumberCollection, float defaultValue1, float defaultValue2, out float value1, out float value2)
    {
        value1 = defaultValue1;
        value2 = defaultValue2;
        if (svgNumberCollection is null)
        {
            return;
        }
        if (svgNumberCollection.Count == 1)
        {
            value1 = svgNumberCollection[0];
            value2 = value1;
        }
        else if (svgNumberCollection.Count == 2)
        {
            value1 = svgNumberCollection[0];
            value2 = svgNumberCollection[1];
        }
    }

    internal static float CalculateOtherPercentageValue(this SKRect skBounds)
    {
        return (float)(Math.Sqrt(skBounds.Width * skBounds.Width + skBounds.Height * skBounds.Height) / Math.Sqrt(2.0));
    }

    internal static SvgUnit Normalize(this SvgUnit svgUnit, SvgCoordinateUnits svgCoordinateUnits)
    {
        return svgUnit.Type == SvgUnitType.Percentage
               && svgCoordinateUnits == SvgCoordinateUnits.ObjectBoundingBox ?
            new SvgUnit(SvgUnitType.User, svgUnit.Value / 100) : svgUnit;
    }

    internal static SKMatrix ToMatrix(this SvgMatrix svgMatrix)
    {
        return new()
        {
            ScaleX = svgMatrix.Points[0],
            SkewY = svgMatrix.Points[1],
            SkewX = svgMatrix.Points[2],
            ScaleY = svgMatrix.Points[3],
            TransX = svgMatrix.Points[4],
            TransY = svgMatrix.Points[5],
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };
    }

    internal static SKMatrix ToMatrix(this SvgTransformCollection? svgTransformCollection)
    {
        var skMatrixTotal = SKMatrix.CreateIdentity();

        if (svgTransformCollection is null)
        {
            return skMatrixTotal;
        }

        foreach (var svgTransform in svgTransformCollection)
        {
            switch (svgTransform)
            {
                case SvgMatrix svgMatrix:
                {
                    var skMatrix = svgMatrix.ToMatrix();
                    skMatrixTotal = skMatrixTotal.PreConcat(skMatrix);
                }
                    break;

                case SvgRotate svgRotate:
                {
                    var skMatrixRotate = SKMatrix.CreateRotationDegrees(svgRotate.Angle, svgRotate.CenterX, svgRotate.CenterY);
                    skMatrixTotal = skMatrixTotal.PreConcat(skMatrixRotate);
                }
                    break;

                case SvgScale svgScale:
                {
                    var skMatrixScale = SKMatrix.CreateScale(svgScale.X, svgScale.Y);
                    skMatrixTotal = skMatrixTotal.PreConcat(skMatrixScale);
                }
                    break;

                case SvgSkew svgSkew:
                {
                    var sx = (float)Math.Tan(Math.PI * svgSkew.AngleX / 180);
                    var sy = (float)Math.Tan(Math.PI * svgSkew.AngleY / 180);
                    var skMatrixSkew = SKMatrix.CreateSkew(sx, sy);
                    skMatrixTotal = skMatrixTotal.PreConcat(skMatrixSkew);
                }
                    break;

                case SvgTranslate svgTranslate:
                {
                    var skMatrixTranslate = SKMatrix.CreateTranslation(svgTranslate.X, svgTranslate.Y);
                    skMatrixTotal = skMatrixTotal.PreConcat(skMatrixTranslate);
                }
                    break;
            }
        }

        return skMatrixTotal;
    }

    internal static SKMatrix ToMatrix(this SvgViewBox svgViewBox, SvgAspectRatio? svgAspectRatio, float x, float y, float width, float height)
    {
        if (svgViewBox.Equals(SvgViewBox.Empty))
        {
            return SKMatrix.CreateTranslation(x, y);
        }

        var fScaleX = width / svgViewBox.Width;
        var fScaleY = height / svgViewBox.Height;
        var fMinX = -svgViewBox.MinX * fScaleX;
        var fMinY = -svgViewBox.MinY * fScaleY;

        svgAspectRatio ??= new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid);

        if (svgAspectRatio.Align != SvgPreserveAspectRatio.none)
        {
            if (svgAspectRatio.Slice)
            {
                fScaleX = Math.Max(fScaleX, fScaleY);
                fScaleY = Math.Max(fScaleX, fScaleY);
            }
            else
            {
                fScaleX = Math.Min(fScaleX, fScaleY);
                fScaleY = Math.Min(fScaleX, fScaleY);
            }
            var fViewMidX = svgViewBox.Width / 2 * fScaleX;
            var fViewMidY = svgViewBox.Height / 2 * fScaleY;
            var fMidX = width / 2;
            var fMidY = height / 2;
            fMinX = -svgViewBox.MinX * fScaleX;
            fMinY = -svgViewBox.MinY * fScaleY;

            switch (svgAspectRatio.Align)
            {
                case SvgPreserveAspectRatio.xMinYMin:
                    break;

                case SvgPreserveAspectRatio.xMidYMin:
                    fMinX += fMidX - fViewMidX;
                    break;

                case SvgPreserveAspectRatio.xMaxYMin:
                    fMinX += width - svgViewBox.Width * fScaleX;
                    break;

                case SvgPreserveAspectRatio.xMinYMid:
                    fMinY += fMidY - fViewMidY;
                    break;

                case SvgPreserveAspectRatio.xMidYMid:
                    fMinX += fMidX - fViewMidX;
                    fMinY += fMidY - fViewMidY;
                    break;

                case SvgPreserveAspectRatio.xMaxYMid:
                    fMinX += width - svgViewBox.Width * fScaleX;
                    fMinY += fMidY - fViewMidY;
                    break;

                case SvgPreserveAspectRatio.xMinYMax:
                    fMinY += height - svgViewBox.Height * fScaleY;
                    break;

                case SvgPreserveAspectRatio.xMidYMax:
                    fMinX += fMidX - fViewMidX;
                    fMinY += height - svgViewBox.Height * fScaleY;
                    break;

                case SvgPreserveAspectRatio.xMaxYMax:
                    fMinX += width - svgViewBox.Width * fScaleX;
                    fMinY += height - svgViewBox.Height * fScaleY;
                    break;
            }
        }

        var skMatrixTotal = SKMatrix.CreateIdentity();

        var skMatrixXY = SKMatrix.CreateTranslation(x, y);
        skMatrixTotal = skMatrixTotal.PreConcat(skMatrixXY);

        var skMatrixMinXY = SKMatrix.CreateTranslation(fMinX, fMinY);
        skMatrixTotal = skMatrixTotal.PreConcat(skMatrixMinXY);

        var skMatrixScale = SKMatrix.CreateScale(fScaleX, fScaleY);
        skMatrixTotal = skMatrixTotal.PreConcat(skMatrixScale);

        return skMatrixTotal;
    }
 
    internal static SKRect? CalculateRect(SvgUnit xUnit, SvgUnit yUnit, SvgUnit widthUnit, SvgUnit heightUnit, SvgCoordinateUnits coordinateUnits, SKRect skBounds, SKRect skViewport, SvgElement? svgElement)
    {
        var useBoundingBox = coordinateUnits == SvgCoordinateUnits.ObjectBoundingBox;

        var xRenderType  = useBoundingBox ? UnitRenderingType.Horizontal : UnitRenderingType.HorizontalOffset;
        var x = xUnit.ToDeviceValue(xRenderType, svgElement, useBoundingBox ? skBounds : skViewport);

        var yRenderType  = useBoundingBox ? UnitRenderingType.Vertical : UnitRenderingType.VerticalOffset;
        var y = yUnit.ToDeviceValue(yRenderType, svgElement, useBoundingBox ? skBounds : skViewport);

        var width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgElement, useBoundingBox ? skBounds : skViewport);
        var height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgElement, useBoundingBox ? skBounds : skViewport);

        if (width <= 0 || height <= 0)
        {
            return default;
        }

        if (useBoundingBox)
        {
            if (xUnit.Type != SvgUnitType.Percentage)
            {
                x *= skBounds.Width;
            }
            x += skBounds.Left;
        }
            
        if (useBoundingBox)
        {
            if (yUnit.Type != SvgUnitType.Percentage)
            {
                y *= skBounds.Height;
            }
            y += skBounds.Top;
        }
            
        if (useBoundingBox)
        {
            if (widthUnit.Type != SvgUnitType.Percentage)
            {
                width *= skBounds.Width;
            }
        }
            
        if (useBoundingBox)
        {
            if (heightUnit.Type != SvgUnitType.Percentage)
            {
                height *= skBounds.Height;
            }
        }

        return SKRect.Create(x, y, width, height);
    }

    internal static SKRect CalculateRect(SvgAspectRatio svgAspectRatio, SKRect srcRect, SKRect destRect)
    {
        if (svgAspectRatio.Align == SvgPreserveAspectRatio.none)
        {
            return new SKRect(destRect.Left, destRect.Top, destRect.Right, destRect.Bottom);
        }

        var fScaleX = destRect.Width / srcRect.Width;
        var fScaleY = destRect.Height / srcRect.Height;
        var xOffset = 0f;
        var yOffset = 0f;

        if (svgAspectRatio.Slice)
        {
            fScaleX = Math.Max(fScaleX, fScaleY);
            fScaleY = Math.Max(fScaleX, fScaleY);
        }
        else
        {
            fScaleX = Math.Min(fScaleX, fScaleY);
            fScaleY = Math.Min(fScaleX, fScaleY);
        }

        switch (svgAspectRatio.Align)
        {
            case SvgPreserveAspectRatio.xMinYMin:
                break;

            case SvgPreserveAspectRatio.xMidYMin:
                xOffset = (destRect.Width - srcRect.Width * fScaleX) / 2;
                break;

            case SvgPreserveAspectRatio.xMaxYMin:
                xOffset = destRect.Width - srcRect.Width * fScaleX;
                break;

            case SvgPreserveAspectRatio.xMinYMid:
                yOffset = (destRect.Height - srcRect.Height * fScaleY) / 2;
                break;

            case SvgPreserveAspectRatio.xMidYMid:
                xOffset = (destRect.Width - srcRect.Width * fScaleX) / 2;
                yOffset = (destRect.Height - srcRect.Height * fScaleY) / 2;
                break;

            case SvgPreserveAspectRatio.xMaxYMid:
                xOffset = destRect.Width - srcRect.Width * fScaleX;
                yOffset = (destRect.Height - srcRect.Height * fScaleY) / 2;
                break;

            case SvgPreserveAspectRatio.xMinYMax:
                yOffset = destRect.Height - srcRect.Height * fScaleY;
                break;

            case SvgPreserveAspectRatio.xMidYMax:
                xOffset = (destRect.Width - srcRect.Width * fScaleX) / 2;
                yOffset = destRect.Height - srcRect.Height * fScaleY;
                break;

            case SvgPreserveAspectRatio.xMaxYMax:
                xOffset = destRect.Width - srcRect.Width * fScaleX;
                yOffset = destRect.Height - srcRect.Height * fScaleY;
                break;
        }

        return SKRect.Create(
            destRect.Left + xOffset,
            destRect.Top + yOffset,
            srcRect.Width * fScaleX,
            srcRect.Height * fScaleY);
    }
}
