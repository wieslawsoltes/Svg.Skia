// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SkiaSharp;
using Svg.FilterEffects;

namespace Svg.Skia
{
    public static class SvgFiltersExtensions
    {
        public const string SourceGraphic = "SourceGraphic";
        public const string SourceAlpha = "SourceAlpha";
        public const string BackgroundImage = "BackgroundImage";
        public const string BackgroundAlpha = "BackgroundAlpha";
        public const string FillPaint = "FillPaint";
        public const string StrokePaint = "StrokePaint";

        public static void GetOptionalNumbers(SvgNumberCollection svgNumberCollection, float defaultValue1, float defaultValue2, out float value1, out float value2)
        {
            value1 = defaultValue1;
            value2 = defaultValue2;
            if (svgNumberCollection == null)
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

        public static double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        public static double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }

        public static SKImageFilter? GetGraphic(SKPicture skPicture, CompositeDisposable disposable)
        {
            var skImageFilter = SKImageFilter.CreatePicture(skPicture, skPicture.CullRect);
            disposable.Add(skImageFilter);
            return skImageFilter;
        }

        public static SKImageFilter? GetAlpha(SKPicture skPicture, CompositeDisposable disposable)
        {
            var skImageFilterGraphic = GetGraphic(skPicture, disposable);

            var matrix = new float[20]
            {
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 1f, 0f
            };

            var skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            disposable.Add(skColorFilter);

            var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter, skImageFilterGraphic);
            disposable.Add(skImageFilter);
            return skImageFilter;
        }

        public static SKImageFilter? GetPaint(SKPaint skPaint, CompositeDisposable disposable)
        {
            var skImageFilter = SKImageFilter.CreatePaint(skPaint);
            disposable.Add(skImageFilter);
            return skImageFilter;
        }

        public static SKImageFilter GetTransparentBlackImage(CompositeDisposable disposable)
        {
            var skPaint = new SKPaint()
            {
                Style = SKPaintStyle.StrokeAndFill,
                Color = SvgPaintingExtensions.TransparentBlack
            };
            disposable.Add(skPaint);

            var skImageFilter = SKImageFilter.CreatePaint(skPaint);
            disposable.Add(skImageFilter);
            return skImageFilter;
        }

        public static SKImageFilter GetTransparentBlackAlpha(CompositeDisposable disposable)
        {
            var skPaint = new SKPaint()
            {
                Style = SKPaintStyle.StrokeAndFill,
                Color = SvgPaintingExtensions.TransparentBlack
            };
            disposable.Add(skPaint);

            var skImageFilterGraphic = SKImageFilter.CreatePaint(skPaint);
            disposable.Add(skImageFilterGraphic);

            var matrix = new float[20]
            {
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 1f, 0f
            };

            var skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            disposable.Add(skColorFilter);

            var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter, skImageFilterGraphic);
            disposable.Add(skImageFilter);
            return skImageFilter;
        }

        public static SKImageFilter? GetInputFilter(string inputKey, Dictionary<string, SKImageFilter> results, SKImageFilter? lastResult, IFilterSource filterSource, CompositeDisposable disposable, bool isFirst)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                if (isFirst)
                {
                    if (results.ContainsKey(SourceGraphic))
                    {
                        return results[SourceGraphic];
                    }
                    var skPicture = filterSource.SourceGraphic();
                    if (skPicture != null)
                    {
                        var skImageFilter = GetGraphic(skPicture, disposable);
                        if (skImageFilter != null)
                        {
                            results[SourceGraphic] = skImageFilter;
                            return skImageFilter;
                        }
                    }
                    return null;
                }
                else
                {
                    return lastResult;
                }
            }

            if (results.ContainsKey(inputKey))
            {
                return results[inputKey];
            }

            switch (inputKey)
            {
                case SourceGraphic:
                    {
                        var skPicture = filterSource.SourceGraphic();
                        if (skPicture != null)
                        {
                            var skImageFilter = GetGraphic(skPicture, disposable);
                            if (skImageFilter != null)
                            {
                                results[SourceGraphic] = skImageFilter;
                                return skImageFilter;
                            }
                        }
                    }
                    break;
                case SourceAlpha:
                    {
                        var skPicture = filterSource.SourceGraphic();
                        if (skPicture != null)
                        {
                            var skImageFilter = GetAlpha(skPicture, disposable);
                            if (skImageFilter != null)
                            {
                                results[SourceAlpha] = skImageFilter;
                                return skImageFilter;
                            }
                        }
                    }
                    break;
                case BackgroundImage:
                    {
                        var skPicture = filterSource.BackgroundImage();
                        if (skPicture != null)
                        {
                            var skImageFilter = GetGraphic(skPicture, disposable);
                            if (skImageFilter != null)
                            {
                                results[BackgroundImage] = skImageFilter;
                                return skImageFilter;
                            }
                        }
                        else
                        {
                            var skImageFilter = GetTransparentBlackImage(disposable);
                            results[BackgroundImage] = skImageFilter;
                            return skImageFilter;
                        }
                    }
                    break;
                case BackgroundAlpha:
                    {
                        var skPicture = filterSource.BackgroundImage();
                        if (skPicture != null)
                        {
                            var skImageFilter = GetAlpha(skPicture, disposable);
                            if (skImageFilter != null)
                            {
                                results[BackgroundAlpha] = skImageFilter;
                                return skImageFilter;
                            }
                        }
                        else
                        {
                            var skImageFilter = GetTransparentBlackAlpha(disposable);
                            results[BackgroundImage] = skImageFilter;
                            return skImageFilter;
                        }
                    }
                    break;
                case FillPaint:
                    {
                        var skPaint = filterSource.FillPaint();
                        if (skPaint != null)
                        {
                            var skImageFilter = GetPaint(skPaint, disposable);
                            if (skImageFilter != null)
                            {
                                results[FillPaint] = skImageFilter;
                                return skImageFilter;
                            }
                        }
                    }
                    break;
                case StrokePaint:
                    {
                        var skPaint = filterSource.StrokePaint();
                        if (skPaint != null)
                        {
                            var skImageFilter = GetPaint(skPaint, disposable);
                            if (skImageFilter != null)
                            {
                                results[StrokePaint] = skImageFilter;
                                return skImageFilter;
                            }
                        }
                    }
                    break;
            }

            return null;
        }

        public static SKImageFilter? SetImageFilter(FilterEffects.SvgFilterPrimitive svgFilterPrimitive, SKPaint skPaint, SKImageFilter skImageFilter, Dictionary<string, SKImageFilter> results, CompositeDisposable disposable)
        {
            var key = svgFilterPrimitive.Result;
            if (!string.IsNullOrWhiteSpace(key))
            {
                results[key] = skImageFilter;
            }
            disposable.Add(skImageFilter);
            skPaint.ImageFilter = skImageFilter;
            return skImageFilter;
        }

        public static SKBlendMode GetSKBlendMode(FilterEffects.SvgBlendMode svgBlendMode)
        {
            return svgBlendMode switch
            {
                FilterEffects.SvgBlendMode.Normal => SKBlendMode.SrcOver,
                FilterEffects.SvgBlendMode.Multiply => SKBlendMode.Multiply,
                FilterEffects.SvgBlendMode.Screen => SKBlendMode.Screen,
                FilterEffects.SvgBlendMode.Overlay => SKBlendMode.Overlay,
                FilterEffects.SvgBlendMode.Darken => SKBlendMode.Darken,
                FilterEffects.SvgBlendMode.Lighten => SKBlendMode.Lighten,
                FilterEffects.SvgBlendMode.ColorDodge => SKBlendMode.ColorDodge,
                FilterEffects.SvgBlendMode.ColorBurn => SKBlendMode.ColorBurn,
                FilterEffects.SvgBlendMode.HardLight => SKBlendMode.HardLight,
                FilterEffects.SvgBlendMode.SoftLight => SKBlendMode.SoftLight,
                FilterEffects.SvgBlendMode.Difference => SKBlendMode.Difference,
                FilterEffects.SvgBlendMode.Exclusion => SKBlendMode.Exclusion,
                FilterEffects.SvgBlendMode.Hue => SKBlendMode.Hue,
                FilterEffects.SvgBlendMode.Saturation => SKBlendMode.Saturation,
                FilterEffects.SvgBlendMode.Color => SKBlendMode.Color,
                FilterEffects.SvgBlendMode.Luminosity => SKBlendMode.Luminosity,
                _ => SKBlendMode.SrcOver,
            };
        }

        public static SKImageFilter? CreateBlend(FilterEffects.SvgBlend svgBlend, SKImageFilter background, SKImageFilter? foreground = null, SKImageFilter.CropRect? cropRect = null)
        {
            var mode = GetSKBlendMode(svgBlend.Mode);
            return SKImageFilter.CreateBlendMode(mode, background, foreground, cropRect);
        }

        public static float[] CreateIdentityColorMatrixArray()
        {
            return new float[]
            {
                1, 0, 0, 0, 0,
                0, 1, 0, 0, 0,
                0, 0, 1, 0, 0,
                0, 0, 0, 1, 0
            };
        }

        public static SKImageFilter? CreateColorMatrix(FilterEffects.SvgColourMatrix svgColourMatrix, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            SKColorFilter skColorFilter;

            switch (svgColourMatrix.Type)
            {
                case FilterEffects.SvgColourMatrixType.HueRotate:
                    {
                        float value = (string.IsNullOrEmpty(svgColourMatrix.Values) ? 0 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture));
                        var hue = (float)DegreeToRadian(value);
                        var cosHue = Math.Cos(hue);
                        var sinHue = Math.Sin(hue);
                        float[] matrix = new float[]
                        {
                            (float)(0.213 + cosHue * 0.787 - sinHue * 0.213),
                            (float)(0.715 - cosHue * 0.715 - sinHue * 0.715),
                            (float)(0.072 - cosHue * 0.072 + sinHue * 0.928), 0, 0,
                            (float)(0.213 - cosHue * 0.213 + sinHue * 0.143),
                            (float)(0.715 + cosHue * 0.285 + sinHue * 0.140),
                            (float)(0.072 - cosHue * 0.072 - sinHue * 0.283), 0, 0,
                            (float)(0.213 - cosHue * 0.213 - sinHue * 0.787),
                            (float)(0.715 - cosHue * 0.715 + sinHue * 0.715),
                            (float)(0.072 + cosHue * 0.928 + sinHue * 0.072), 0, 0,
                            0, 0, 0, 1, 0
                        };
                        skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                        disposable.Add(skColorFilter);
                    }
                    break;
                case FilterEffects.SvgColourMatrixType.LuminanceToAlpha:
                    {
                        float[] matrix = new float[]
                        {
                            0, 0, 0, 0, 0,
                            0, 0, 0, 0, 0,
                            0, 0, 0, 0, 0,
                            0.2125f, 0.7154f, 0.0721f, 0, 0
                        };
                        skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                        disposable.Add(skColorFilter);
                    }
                    break;
                case FilterEffects.SvgColourMatrixType.Saturate:
                    {
                        float value = (string.IsNullOrEmpty(svgColourMatrix.Values) ? 1 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture));
                        float[] matrix = new float[]
                        {
                            (float)(0.213+0.787*value), (float)(0.715-0.715*value), (float)(0.072-0.072*value), 0, 0,
                            (float)(0.213-0.213*value), (float)(0.715+0.285*value), (float)(0.072-0.072*value), 0, 0,
                            (float)(0.213-0.213*value), (float)(0.715-0.715*value), (float)(0.072+0.928*value), 0, 0,
                            0, 0, 0, 1, 0
                        };
                        skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                        disposable.Add(skColorFilter);
                    };
                    break;
                default:
                case FilterEffects.SvgColourMatrixType.Matrix:
                    {
                        float[] matrix;
                        if (string.IsNullOrEmpty(svgColourMatrix.Values))
                        {
                            matrix = CreateIdentityColorMatrixArray();
                        }
                        else
                        {
                            var parts = svgColourMatrix.Values.Split(new char[] { ' ', '\t', '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Count() == 20)
                            {
                                matrix = new float[20];
                                for (int i = 0; i < 20; i++)
                                {
                                    matrix[i] = float.Parse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture);
                                }
                                matrix[4] *= 255f;
                                matrix[9] *= 255f;
                                matrix[14] *= 255f;
                                matrix[19] *= 255f;
                            }
                            else
                            {
                                matrix = CreateIdentityColorMatrixArray();
                            }
                        }
                        skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                        disposable.Add(skColorFilter);
                    }
                    break;
            }

            return SKImageFilter.CreateColorFilter(skColorFilter, input, cropRect);
        }

        public static SvgFuncA s_identitySvgFuncA = new SvgFuncA()
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        public static SvgFuncR s_identitySvgFuncR = new SvgFuncR()
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        public static SvgFuncG s_identitySvgFuncG = new SvgFuncG()
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        public static SvgFuncB s_identitySvgFuncB = new SvgFuncB()
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        public static void Identity(byte[] values, SvgComponentTransferFunction transferFunction)
        {
        }

        public static void Table(byte[] values, SvgComponentTransferFunction transferFunction)
        {
            var tableValues = transferFunction.TableValues;
            int n = tableValues.Count;
            if (n < 1)
            {
                return;
            }
            for (int i = 0; i < 256; i++)
            {
                double c = i / 255.0;
                byte k = (byte)(c * (n - 1));
                double v1 = tableValues[k];
                double v2 = tableValues[Math.Min((k + 1), (n - 1))];
                double val = 255.0 * (v1 + (c * (n - 1) - k) * (v2 - v1));
                val = Math.Max(0.0, Math.Min(255.0, val));
                values[i] = (byte)val;
            }
        }

        public static void Discrete(byte[] values, SvgComponentTransferFunction transferFunction)
        {
            var tableValues = transferFunction.TableValues;
            int n = tableValues.Count;
            if (n < 1)
            {
                return;
            }
            for (int i = 0; i < 256; i++)
            {
                byte k = (byte)((i * n) / 255.0);
                k = (byte)Math.Min(k, n - 1);
                double val = 255 * tableValues[k];
                val = Math.Max(0.0, Math.Min(255.0, val));
                values[i] = (byte)val;
            }
        }

        public static void Linear(byte[] values, SvgComponentTransferFunction transferFunction)
        {
            for (int i = 0; i < 256; i++)
            {
                double val = transferFunction.Slope * i + 255 * transferFunction.Intercept;
                val = Math.Max(0.0, Math.Min(255.0, val));
                values[i] = (byte)val;
            }
        }

        public static void Gamma(byte[] values, SvgComponentTransferFunction transferFunction)
        {
            for (int i = 0; i < 256; i++)
            {
                double exponent = transferFunction.Exponent;
                double val = 255.0 * (transferFunction.Amplitude * Math.Pow((i / 255.0), exponent) + transferFunction.Offset);
                val = Math.Max(0.0, Math.Min(255.0, val));
                values[i] = (byte)val;
            }
        }

        public static void Apply(byte[] values, SvgComponentTransferFunction transferFunction)
        {
            switch (transferFunction.Type)
            {
                case SvgComponentTransferType.Identity:
                    Identity(values, transferFunction);
                    break;
                case SvgComponentTransferType.Table:
                    Table(values, transferFunction);
                    break;
                case SvgComponentTransferType.Discrete:
                    Discrete(values, transferFunction);
                    break;
                case SvgComponentTransferType.Linear:
                    Linear(values, transferFunction);
                    break;
                case SvgComponentTransferType.Gamma:
                    Gamma(values, transferFunction);
                    break;
            }
        }

        public static SKImageFilter? CreateComponentTransfer(FilterEffects.SvgComponentTransfer svgComponentTransfer, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            SvgFuncA? svgFuncA = s_identitySvgFuncA;
            SvgFuncR? svgFuncR = s_identitySvgFuncR;
            SvgFuncG? svgFuncG = s_identitySvgFuncG;
            SvgFuncB? svgFuncB = s_identitySvgFuncB;

            foreach (var child in svgComponentTransfer.Children)
            {
                switch (child)
                {
                    case SvgFuncA a:
                        svgFuncA = a;
                        break;
                    case SvgFuncR r:
                        svgFuncR = r;
                        break;
                    case SvgFuncG g:
                        svgFuncG = g;
                        break;
                    case SvgFuncB b:
                        svgFuncB = b;
                        break;
                }
            }

            byte[] tableA = new byte[256];
            byte[] tableR = new byte[256];
            byte[] tableG = new byte[256];
            byte[] tableB = new byte[256];

            for (int i = 0; i < 256; i++)
            {
                tableA[i] = tableR[i] = tableG[i] = tableB[i] = (byte)i;
            }

            Apply(tableA, svgFuncA);
            Apply(tableR, svgFuncR);
            Apply(tableG, svgFuncG);
            Apply(tableB, svgFuncB);

            var cf = SKColorFilter.CreateTable(tableA, tableR, tableG, tableB);
            disposable.Add(cf);

            return SKImageFilter.CreateColorFilter(cf, input, cropRect);
        }

        public static SKImageFilter? CreateComposite(FilterEffects.SvgComposite svgComposite, SKImageFilter background, SKImageFilter? foreground = null, SKImageFilter.CropRect? cropRect = null)
        {
            var oper = svgComposite.Operator;
            if (oper == SvgCompositeOperator.Arithmetic)
            {
                var k1 = svgComposite.K1;
                var k2 = svgComposite.K2;
                var k3 = svgComposite.K3;
                var k4 = svgComposite.K4;
                return SKImageFilter.CreateArithmetic(k1, k2, k3, k4, false, background, foreground, cropRect);
            }
            else
            {
                var mode = oper switch
                {
                    FilterEffects.SvgCompositeOperator.Over => SKBlendMode.SrcOver,
                    FilterEffects.SvgCompositeOperator.In => SKBlendMode.SrcIn,
                    FilterEffects.SvgCompositeOperator.Out => SKBlendMode.SrcOut,
                    FilterEffects.SvgCompositeOperator.Atop => SKBlendMode.SrcATop,
                    FilterEffects.SvgCompositeOperator.Xor => SKBlendMode.Xor,
                    _ => SKBlendMode.SrcOver,
                };
                return SKImageFilter.CreateBlendMode(mode, background, foreground, cropRect);
            }
        }

        public static SKImageFilter? CreateConvolveMatrix(FilterEffects.SvgConvolveMatrix svgConvolveMatrix, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            GetOptionalNumbers(svgConvolveMatrix.Order, 3f, 3f, out var orderX, out var orderY);

            if (orderX <= 0f || orderY <= 0f)
            {
                return null;
            }

            var kernelSize = new SKSizeI((int)orderX, (int)orderY);
            var kernelMatrix = svgConvolveMatrix.KernelMatrix;

            if (kernelMatrix == null)
            {
                return null;
            }

            if ((kernelSize.Width * kernelSize.Height) != kernelMatrix.Count)
            {
                return null;
            }

            float[] kernel = new float[kernelMatrix.Count];

            int count = kernelMatrix.Count;
            for (int i = 0; i < count; i++)
            {
                kernel[i] = kernelMatrix[count - 1 - i];
            }

            float divisor = svgConvolveMatrix.Divisor;
            if (divisor == 0f)
            {
                foreach (var value in kernel)
                {
                    divisor += value;
                }
                if (divisor == 0f)
                {
                    divisor = 1f;
                }
            }

            float gain = 1f / divisor;
            float bias = svgConvolveMatrix.Bias * 255f;
            var kernelOffset = new SKPointI(svgConvolveMatrix.TargetX, svgConvolveMatrix.TargetY);
            var tileMode = svgConvolveMatrix.EdgeMode switch
            {
                SvgEdgeMode.Duplicate => SKMatrixConvolutionTileMode.Clamp,
                SvgEdgeMode.Wrap => SKMatrixConvolutionTileMode.Repeat,
                SvgEdgeMode.None => SKMatrixConvolutionTileMode.ClampToBlack,
                _ => SKMatrixConvolutionTileMode.Clamp
            };
            bool convolveAlpha = !svgConvolveMatrix.PreserveAlpha;

            return SKImageFilter.CreateMatrixConvolution(kernelSize, kernel, gain, bias, kernelOffset, tileMode, convolveAlpha, input, cropRect);
        }

        public static SKPoint3 GetDirection(SvgDistantLight svgDistantLight)
        {
            float azimuth = svgDistantLight.Azimuth;
            float elevation = svgDistantLight.Elevation;
            double azimuthRad = DegreeToRadian(azimuth);
            double elevationRad = DegreeToRadian(elevation);
            SKPoint3 direction = new SKPoint3(
                (float)(Math.Cos(azimuthRad) * Math.Cos(elevationRad)),
                (float)(Math.Sin(azimuthRad) * Math.Cos(elevationRad)),
                (float)Math.Sin(elevationRad));
            return direction;
        }

        public static SKImageFilter? CreateDiffuseLighting(FilterEffects.SvgDiffuseLighting svgDiffuseLighting, SvgVisualElement svgVisualElement, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            var lightColor = SvgPaintingExtensions.GetColor(svgVisualElement, svgDiffuseLighting.LightingColor);
            if (lightColor == null)
            {
                return null;
            }

            var surfaceScale = svgDiffuseLighting.SurfaceScale;
            var diffuseConstant = svgDiffuseLighting.DiffuseConstant;
            // TODO: svgDiffuseLighting.KernelUnitLength

            switch (svgDiffuseLighting.LightSource)
            {
                case SvgDistantLight svgDistantLight:
                    {
                        var direction = GetDirection(svgDistantLight);
                        return SKImageFilter.CreateDistantLitDiffuse(direction, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
                    }
                case SvgPointLight svgPointLight:
                    {
                        var location = new SKPoint3(svgPointLight.X, svgPointLight.Y, svgPointLight.Z);
                        return SKImageFilter.CreatePointLitDiffuse(location, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
                    }
                case SvgSpotLight svgSpotLight:
                    {
                        var location = new SKPoint3(svgSpotLight.X, svgSpotLight.Y, svgSpotLight.Z);
                        var target = new SKPoint3(svgSpotLight.PointsAtX, svgSpotLight.PointsAtY, svgSpotLight.PointsAtZ);
                        float specularExponentSpotLight = svgSpotLight.SpecularExponent;
                        float limitingConeAngle = svgSpotLight.LlimitingConeAngle;
                        if (float.IsNaN(limitingConeAngle) || limitingConeAngle > 90f || limitingConeAngle < -90f)
                        {
                            limitingConeAngle = 90f;
                        }
                        return SKImageFilter.CreateSpotLitDiffuse(location, target, specularExponentSpotLight, limitingConeAngle, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
                    }
            }
            return null;
        }

        public static SKDisplacementMapEffectChannelSelectorType GetSKDisplacementMapEffectChannelSelectorType(SvgChannelSelector svgChannelSelector)
        {
            return svgChannelSelector switch
            {
                SvgChannelSelector.R => SKDisplacementMapEffectChannelSelectorType.R,
                SvgChannelSelector.G => SKDisplacementMapEffectChannelSelectorType.G,
                SvgChannelSelector.B => SKDisplacementMapEffectChannelSelectorType.B,
                SvgChannelSelector.A => SKDisplacementMapEffectChannelSelectorType.A,
                _ => SKDisplacementMapEffectChannelSelectorType.A
            };
        }

        public static SKImageFilter? CreateDisplacementMap(FilterEffects.SvgDisplacementMap svgDisplacementMap, SKImageFilter displacement, SKImageFilter? inout = null, SKImageFilter.CropRect? cropRect = null)
        {
            var xChannelSelector = GetSKDisplacementMapEffectChannelSelectorType(svgDisplacementMap.XChannelSelector);
            var yChannelSelector = GetSKDisplacementMapEffectChannelSelectorType(svgDisplacementMap.YChannelSelector);
            var scale = svgDisplacementMap.Scale;
            return SKImageFilter.CreateDisplacementMapEffect(xChannelSelector, yChannelSelector, scale, displacement, inout, cropRect);
        }

        public static SKImageFilter? CreateFlood(FilterEffects.SvgFlood svgFlood, SvgVisualElement svgVisualElement, SKRect skBounds, CompositeDisposable disposable, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            var floodColor = SvgPaintingExtensions.GetColor(svgVisualElement, svgFlood.FloodColor);
            if (floodColor == null)
            {
                return null;
            }

            var floodOpacity = svgFlood.FloodOpacity;
            var floodAlpha = SvgPaintingExtensions.CombineWithOpacity(floodColor.Value.Alpha, floodOpacity);
            floodColor = floodColor.Value.WithAlpha(floodAlpha);

            if (cropRect == null)
            {
                cropRect = new SKImageFilter.CropRect(skBounds);
            }

            var cf = SKColorFilter.CreateBlendMode(floodColor.Value, SKBlendMode.Src);
            disposable.Add(cf);

            return SKImageFilter.CreateColorFilter(cf, input, cropRect);
        }

        public static SKImageFilter? CreateBlur(FilterEffects.SvgGaussianBlur svgGaussianBlur, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            GetOptionalNumbers(svgGaussianBlur.StdDeviation, 0f, 0f, out var sigmaX, out var sigmaY);

            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                sigmaX = (skBounds.Width / 100) * sigmaX;
                sigmaY = (skBounds.Height / 100) * sigmaY;
            }

            if (sigmaX < 0f || sigmaY < 0f)
            {
                return null;
            }

            return SKImageFilter.CreateBlur(sigmaX, sigmaY, input, cropRect);
        }

        public static SKImageFilter? CreateImage(FilterEffects.SvgImage svgImage, SKRect skBounds, CompositeDisposable disposable, SKImageFilter.CropRect? cropRect = null)
        {
            var srcRect = default(SKRect);
            var image = SvgImageExtensions.GetImage(svgImage.Href, svgImage.OwnerDocument);
            var skImage = image as SKImage;
            var svgFragment = image as SvgFragment;
            if (skImage == null && svgFragment == null)
            {
                return null;
            }

            if (skImage != null)
            {
                srcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment != null)
            {
                var skSize = SvgExtensions.GetDimensions(svgFragment);
                srcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            if (skImage != null)
            {
                disposable.Add(skImage);
                return SKImageFilter.CreateImage(skImage, srcRect, skBounds, SKFilterQuality.None);
            }

            if (svgFragment != null)
            {
                using var fragmentDrawable = new FragmentDrawable(svgFragment, skBounds, null, null, Attributes.None);
                var skPicture = fragmentDrawable.Snapshot();
                disposable.Add(skPicture);

                if (cropRect == null)
                {
                    cropRect = new SKImageFilter.CropRect(skBounds);
                }

                return SKImageFilter.CreatePicture(skPicture, cropRect.Rect);
            }

            return null;
        }

        public static SKImageFilter? CreateMerge(FilterEffects.SvgMerge svgMerge, Dictionary<string, SKImageFilter> results, SKImageFilter? lastResult, IFilterSource filterSource, CompositeDisposable disposable, SKImageFilter.CropRect? cropRect = null)
        {
            var children = svgMerge.Children.OfType<FilterEffects.SvgMergeNode>().ToList();
            var filters = new SKImageFilter[children.Count];

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var inputKey = child.Input;
                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, false);
                if (inputFilter != null)
                {
                    filters[i] = inputFilter;
                }
                else
                {
                    return null;
                }
            }

            return SKImageFilter.CreateMerge(filters, cropRect);
        }

        public static SKImageFilter? CreateMorphology(FilterEffects.SvgMorphology svgMorphology, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            GetOptionalNumbers(svgMorphology.Radius, 0f, 0f, out var radiusX, out var radiusY);

            return svgMorphology.Operator switch
            {
                SvgMorphologyOperator.Dilate => SKImageFilter.CreateDilate((int)radiusX, (int)radiusY, input, cropRect),
                SvgMorphologyOperator.Erode => SKImageFilter.CreateErode((int)radiusX, (int)radiusY, input, cropRect),
                _ => null,
            };
        }

        public static SKImageFilter? CreateOffset(FilterEffects.SvgOffset svgOffset, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            var dxUnit = svgOffset.Dx;
            var dyUnit = svgOffset.Dy;

            float dx = dxUnit.ToDeviceValue(UnitRenderingType.HorizontalOffset, svgOffset, skBounds);
            float dy = dyUnit.ToDeviceValue(UnitRenderingType.VerticalOffset, svgOffset, skBounds);

            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                if (dxUnit.Type != SvgUnitType.Percentage)
                {
                    dx *= skBounds.Width;
                    dx += skBounds.Left;
                }

                if (dyUnit.Type != SvgUnitType.Percentage)
                {
                    dy *= skBounds.Height;
                    dy += skBounds.Top;
                }
            }

            return SKImageFilter.CreateOffset(dx, dy, input, cropRect);
        }

        public static SKImageFilter? CreateSpecularLighting(FilterEffects.SvgSpecularLighting svgSpecularLighting, SvgVisualElement svgVisualElement, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            var lightColor = SvgPaintingExtensions.GetColor(svgVisualElement, svgSpecularLighting.LightingColor);
            if (lightColor == null)
            {
                return null;
            }

            var surfaceScale = svgSpecularLighting.SurfaceScale;
            var specularConstant = svgSpecularLighting.SpecularConstant;
            var specularExponent = svgSpecularLighting.SpecularExponent;
            // TODO: svgSpecularLighting.KernelUnitLength

            switch (svgSpecularLighting.LightSource)
            {
                case SvgDistantLight svgDistantLight:
                    {
                        var direction = GetDirection(svgDistantLight);
                        return SKImageFilter.CreateDistantLitSpecular(direction, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                    }
                case SvgPointLight svgPointLight:
                    {
                        var location = new SKPoint3(svgPointLight.X, svgPointLight.Y, svgPointLight.Z);
                        return SKImageFilter.CreatePointLitSpecular(location, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                    }
                case SvgSpotLight svgSpotLight:
                    {
                        var location = new SKPoint3(svgSpotLight.X, svgSpotLight.Y, svgSpotLight.Z);
                        var target = new SKPoint3(svgSpotLight.PointsAtX, svgSpotLight.PointsAtY, svgSpotLight.PointsAtZ);
                        float specularExponentSpotLight = svgSpotLight.SpecularExponent;
                        float limitingConeAngle = svgSpotLight.LlimitingConeAngle;
                        if (float.IsNaN(limitingConeAngle) || limitingConeAngle > 90f || limitingConeAngle < -90f)
                        {
                            limitingConeAngle = 90f;
                        }
                        return SKImageFilter.CreateSpotLitSpecular(location, target, specularExponentSpotLight, limitingConeAngle, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                    }
            }
            return null;
        }

        public static SKImageFilter? CreateTile(FilterEffects.SvgTile svgTile, SKRect skBounds, SKImageFilter? input = null, SKImageFilter.CropRect? cropRect = null)
        {
            return SKImageFilter.CreateTile(skBounds, cropRect != null ? cropRect.Rect : skBounds, input);
        }

        public static SKImageFilter? CreateTurbulence(FilterEffects.SvgTurbulence svgTurbulence, SKRect skBounds, CompositeDisposable disposable, SKImageFilter.CropRect? cropRect = null)
        {
            GetOptionalNumbers(svgTurbulence.BaseFrequency, 0f, 0f, out var baseFrequencyX, out var baseFrequencyY);

            var numOctaves = svgTurbulence.NumOctaves;
            var seed = svgTurbulence.Seed;

            var skPaint = new SKPaint()
            {
                Style = SKPaintStyle.StrokeAndFill
            };
            disposable.Add(skPaint);

            SKPointI tileSize;
            switch (svgTurbulence.StitchTiles)
            {
                default:
                case SvgStitchType.NoStitch:
                    tileSize = SKPointI.Empty;
                    break;
                case SvgStitchType.Stitch:
                    // TODO:
                    tileSize = new SKPointI();
                    break;
            }

            SKShader skShader;
            switch (svgTurbulence.Type)
            {
                default:
                case SvgTurbulenceType.FractalNoise:
                    skShader = SKShader.CreatePerlinNoiseFractalNoise(baseFrequencyX, baseFrequencyY, numOctaves, seed, tileSize);
                    break;
                case SvgTurbulenceType.Turbulence:
                    skShader = SKShader.CreatePerlinNoiseTurbulence(baseFrequencyX, baseFrequencyY, numOctaves, seed, tileSize);
                    break;
            }

            skPaint.Shader = skShader;
            disposable.Add(skShader);

            if (cropRect == null)
            {
                cropRect = new SKImageFilter.CropRect(skBounds);
            }

            return SKImageFilter.CreatePaint(skPaint, cropRect);
        }

        public static bool IsNone(Uri uri)
        {
            return string.Equals(uri.ToString(), "none", StringComparison.OrdinalIgnoreCase);
        }

        public static SKPaint? GetFilterSKPaint(SvgVisualElement svgVisualElement, SKRect skBounds, IFilterSource filterSource, CompositeDisposable disposable, out bool isValid)
        {
            var filter = svgVisualElement.Filter;
            if (filter == null || IsNone(filter))
            {
                isValid = true;
                return null;
            }

            if (SvgExtensions.HasRecursiveReference(svgVisualElement, (e) => e.Filter, new HashSet<Uri>()))
            {
                isValid = false;
                return null;
            }

            var svgFilter = SvgExtensions.GetReference<FilterEffects.SvgFilter>(svgVisualElement, svgVisualElement.Filter);
            if (svgFilter == null)
            {
                isValid = false;
                return null;
            }

            var results = new Dictionary<string, SKImageFilter>();
            var lastResult = default(SKImageFilter);
            var prevoiusFilterPrimitiveRegion = SKRect.Empty;

            var xUnit = svgFilter.X;
            var yUnit = svgFilter.Y;
            var widthUnit = svgFilter.Width;
            var heightUnit = svgFilter.Height;

            float x = xUnit.ToDeviceValue(UnitRenderingType.HorizontalOffset, svgFilter, skBounds);
            float y = yUnit.ToDeviceValue(UnitRenderingType.VerticalOffset, svgFilter, skBounds);
            float width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgFilter, skBounds);
            float height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgFilter, skBounds);

            if (width <= 0f || height <= 0f)
            {
                isValid = false;
                return null;
            }

            var filterUnits = svgFilter.FilterUnits;
            var primitiveUnits = svgFilter.PrimitiveUnits;

            if (filterUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                // TOOD: FilterUnits
                if (xUnit.Type != SvgUnitType.Percentage)
                {
                    x *= skBounds.Width;
                    x += skBounds.Left;
                }

                if (yUnit.Type != SvgUnitType.Percentage)
                {
                    y *= skBounds.Height;
                    y += skBounds.Top;
                }

                if (widthUnit.Type != SvgUnitType.Percentage)
                {
                    width *= skBounds.Width;
                }

                if (heightUnit.Type != SvgUnitType.Percentage)
                {
                    height *= skBounds.Height;
                }
            }

            var skFilterRegion = SKRect.Create(x, y, width, height);

            var skPaint = new SKPaint
            {
                Style = SKPaintStyle.StrokeAndFill
            };

            int count = 0;
            foreach (var child in svgFilter.Children)
            {
                if (child is FilterEffects.SvgFilterPrimitive svgFilterPrimitive)
                {
                    count++;
                    bool isFirst = count == 1;
                    var skPrimitiveBounds = skBounds;

                    // TOOD: PrimitiveUnits
                    if (primitiveUnits == SvgCoordinateUnits.UserSpaceOnUse)
                    {
                        skPrimitiveBounds = skFilterRegion;
                    }

                    var xUnitChild = svgFilterPrimitive.X;
                    var yUnitChild = svgFilterPrimitive.Y;
                    var widthUnitChild = svgFilterPrimitive.Width;
                    var heightUnitChild = svgFilterPrimitive.Height;

                    float xChild = xUnitChild.ToDeviceValue(UnitRenderingType.HorizontalOffset, svgFilterPrimitive, skPrimitiveBounds);
                    float yChild = yUnitChild.ToDeviceValue(UnitRenderingType.VerticalOffset, svgFilterPrimitive, skPrimitiveBounds);
                    float widthChild = widthUnitChild.ToDeviceValue(UnitRenderingType.Horizontal, svgFilterPrimitive, skPrimitiveBounds);
                    float heightChild = heightUnitChild.ToDeviceValue(UnitRenderingType.Vertical, svgFilterPrimitive, skPrimitiveBounds);

                    if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
                    {
                        if (xUnitChild.Type != SvgUnitType.Percentage)
                        {
                            xChild *= skPrimitiveBounds.Width;
                            xChild += skPrimitiveBounds.Left;
                        }

                        if (yUnitChild.Type != SvgUnitType.Percentage)
                        {
                            yChild *= skPrimitiveBounds.Height;
                            yChild += skPrimitiveBounds.Top;
                        }

                        if (widthUnitChild.Type != SvgUnitType.Percentage)
                        {
                            widthChild *= skPrimitiveBounds.Width;
                        }

                        if (heightUnitChild.Type != SvgUnitType.Percentage)
                        {
                            heightChild *= skPrimitiveBounds.Height;
                        }
                    }

                    var skFilterPrimitiveRegion = SKRect.Create(xChild, yChild, widthChild, heightChild);
                    var skCropRect = new SKImageFilter.CropRect(skFilterPrimitiveRegion);

                    switch (svgFilterPrimitive)
                    {
                        case FilterEffects.SvgBlend svgBlend:
                            {
                                var input1Key = svgBlend.Input;
                                var input1Filter = GetInputFilter(input1Key, results, lastResult, filterSource, disposable, isFirst);
                                var input2Key = svgBlend.Input2;
                                var input2Filter = GetInputFilter(input2Key, results, lastResult, filterSource, disposable, false);
                                if (input2Filter == null)
                                {
                                    break;
                                }
                                var skImageFilter = CreateBlend(svgBlend, input2Filter, input1Filter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgBlend, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgColourMatrix svgColourMatrix:
                            {
                                var inputKey = svgColourMatrix.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateColorMatrix(svgColourMatrix, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgColourMatrix, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgComponentTransfer svgComponentTransfer:
                            {
                                var inputKey = svgComponentTransfer.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateComponentTransfer(svgComponentTransfer, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgComponentTransfer, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgComposite svgComposite:
                            {
                                var input1Key = svgComposite.Input;
                                var input1Filter = GetInputFilter(input1Key, results, lastResult, filterSource, disposable, isFirst);
                                var input2Key = svgComposite.Input2;
                                var input2Filter = GetInputFilter(input2Key, results, lastResult, filterSource, disposable, false);
                                if (input2Filter == null)
                                {
                                    break;
                                }
                                var skImageFilter = CreateComposite(svgComposite, input2Filter, input1Filter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgComposite, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgConvolveMatrix svgConvolveMatrix:
                            {
                                var inputKey = svgConvolveMatrix.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateConvolveMatrix(svgConvolveMatrix, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgConvolveMatrix, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgDiffuseLighting svgDiffuseLighting:
                            {
                                var inputKey = svgDiffuseLighting.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateDiffuseLighting(svgDiffuseLighting, svgVisualElement, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgDiffuseLighting, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgDisplacementMap svgDisplacementMap:
                            {
                                var input1Key = svgDisplacementMap.Input;
                                var input1Filter = GetInputFilter(input1Key, results, lastResult, filterSource, disposable, isFirst);
                                var input2Key = svgDisplacementMap.Input2;
                                var input2Filter = GetInputFilter(input2Key, results, lastResult, filterSource, disposable, false);
                                if (input2Filter == null)
                                {
                                    break;
                                }
                                var skImageFilter = CreateDisplacementMap(svgDisplacementMap, input2Filter, input1Filter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgDisplacementMap, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgFlood svgFlood:
                            {
                                var inputKey = svgFlood.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateFlood(svgFlood, svgVisualElement, skFilterPrimitiveRegion, disposable, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgFlood, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgGaussianBlur svgGaussianBlur:
                            {
                                var inputKey = svgGaussianBlur.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateBlur(svgGaussianBlur, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgGaussianBlur, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgImage svgImage:
                            {
                                var inputKey = svgImage.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateImage(svgImage, skFilterPrimitiveRegion, disposable, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgImage, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgMerge svgMerge:
                            {
                                var skImageFilter = CreateMerge(svgMerge, results, lastResult, filterSource, disposable, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgMerge, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgMorphology svgMorphology:
                            {
                                var inputKey = svgMorphology.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateMorphology(svgMorphology, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgMorphology, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgOffset svgOffset:
                            {
                                var inputKey = svgOffset.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateOffset(svgOffset, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgOffset, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgSpecularLighting svgSpecularLighting:
                            {
                                var inputKey = svgSpecularLighting.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateSpecularLighting(svgSpecularLighting, svgVisualElement, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgSpecularLighting, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgTile svgTile:
                            {
                                var inputKey = svgTile.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateTile(svgTile, prevoiusFilterPrimitiveRegion, inputFilter, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgTile, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        case FilterEffects.SvgTurbulence svgTurbulence:
                            {
                                var inputKey = svgTurbulence.Input;
                                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                                var skImageFilter = CreateTurbulence(svgTurbulence, skFilterPrimitiveRegion, disposable, skCropRect);
                                if (skImageFilter != null)
                                {
                                    lastResult = SetImageFilter(svgTurbulence, skPaint, skImageFilter, results, disposable);
                                }
                                else
                                {
                                    isValid = false;
                                    return null;
                                }
                            }
                            break;
                        default:
                            break;
                    }

                    prevoiusFilterPrimitiveRegion = skFilterPrimitiveRegion;
                }
            }

            disposable.Add(skPaint);
            isValid = true;
            return skPaint;
        }
    }
}
