// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using SkiaSharp;
using Svg.Transforms;

namespace Svg.Skia
{
    public static class SvgTransformsExtensions
    {
        public static SKMatrix ToSKMatrix(this SvgMatrix svgMatrix)
        {
            return new SKMatrix()
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

        public static SKMatrix ToSKMatrix(this SvgTransformCollection svgTransformCollection)
        {
            var skMatrixTotal = SKMatrix.MakeIdentity();

            if (svgTransformCollection == null)
            {
                return skMatrixTotal;
            }

            foreach (var svgTransform in svgTransformCollection)
            {
                switch (svgTransform)
                {
                    case SvgMatrix svgMatrix:
                        {
                            var skMatrix = svgMatrix.ToSKMatrix();
                            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrix);
                        }
                        break;
                    case SvgRotate svgRotate:
                        {
                            var skMatrixRotate = SKMatrix.MakeRotationDegrees(svgRotate.Angle, svgRotate.CenterX, svgRotate.CenterY);
                            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrixRotate);
                        }
                        break;
                    case SvgScale svgScale:
                        {
                            var skMatrixScale = SKMatrix.MakeScale(svgScale.X, svgScale.Y);
                            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrixScale);
                        }
                        break;
                    case SvgShear svgShear:
                        {
                            // Not in the svg specification.
                        }
                        break;
                    case SvgSkew svgSkew:
                        {
                            float sx = (float)Math.Tan(Math.PI * svgSkew.AngleX / 180);
                            float sy = (float)Math.Tan(Math.PI * svgSkew.AngleY / 180);
                            var skMatrixSkew = SKMatrix.MakeSkew(sx, sy);
                            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrixSkew);
                        }
                        break;
                    case SvgTranslate svgTranslate:
                        {
                            var skMatrixTranslate = SKMatrix.MakeTranslation(svgTranslate.X, svgTranslate.Y);
                            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrixTranslate);
                        }
                        break;
                    default:
                        break;
                }
            }

            return skMatrixTotal;
        }

        public static SKMatrix ToSKMatrix(this SvgViewBox svgViewBox, SvgAspectRatio svgAspectRatio, float x, float y, float width, float height)
        {
            if (svgViewBox.Equals(SvgViewBox.Empty))
            {
                return SKMatrix.MakeTranslation(x, y);
            }

            float fScaleX = width / svgViewBox.Width;
            float fScaleY = height / svgViewBox.Height;
            float fMinX = -svgViewBox.MinX * fScaleX;
            float fMinY = -svgViewBox.MinY * fScaleY;

            if (svgAspectRatio == null)
            {
                svgAspectRatio = new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid);
            }

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
                float fViewMidX = (svgViewBox.Width / 2) * fScaleX;
                float fViewMidY = (svgViewBox.Height / 2) * fScaleY;
                float fMidX = width / 2;
                float fMidY = height / 2;
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
                    default:
                        break;
                }
            }

            var skMatrixTotal = SKMatrix.MakeIdentity();

            var skMatrixXY = SKMatrix.MakeTranslation(x, y);
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrixXY);

            var skMatrixMinXY = SKMatrix.MakeTranslation(fMinX, fMinY);
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrixMinXY);

            var skMatrixScale = SKMatrix.MakeScale(fScaleX, fScaleY);
            SKMatrix.PreConcat(ref skMatrixTotal, ref skMatrixScale);

            return skMatrixTotal;
        }
    }
}
