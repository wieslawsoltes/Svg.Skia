using System;
using System.Collections.Generic;
using System.Globalization;
using Svg.FilterEffects;
using Svg.Model.Drawables;
using Svg.Model.Drawables.Elements;
using ShimSkiaSharp.Painting;
using ShimSkiaSharp.Painting.ImageFilters;
using ShimSkiaSharp.Painting.Shaders;
using ShimSkiaSharp.Primitives;

namespace Svg.Model
{
    public static partial class SvgExtensions
    {
        private static readonly char[] s_colorMatrixSplitChars = { ' ', '\t', '\n', '\r', ',' };

        internal static SKColor s_transparentBlack = new(0, 0, 0, 255);

        private const string SourceGraphic = "SourceGraphic";

        private const string SourceAlpha = "SourceAlpha";

        private const string BackgroundImage = "BackgroundImage";

        private const string BackgroundAlpha = "BackgroundAlpha";

        private const string FillPaint = "FillPaint";

        private const string StrokePaint = "StrokePaint";

        private static bool IsStandardInput(string key)
        {
            return key switch
            {
                SourceGraphic => true,
                SourceAlpha => true,
                BackgroundImage => true,
                BackgroundAlpha => true,
                FillPaint => true,
                StrokePaint => true,
                _ => false
            };
        }

        private static SvgFuncA s_identitySvgFuncA = new()
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        private static SvgFuncR s_identitySvgFuncR = new()
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        private static SvgFuncG s_identitySvgFuncG = new()
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        private static SvgFuncB s_identitySvgFuncB = new()
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        internal static double DegreeToRadian(this double degrees)
        {
            return Math.PI * degrees / 180.0;
        }

        internal static double RadianToDegree(this double radians)
        {
            return radians * (180.0 / Math.PI);
        }

        private static bool IsNone(this Uri uri)
        {
            return string.Equals(uri.ToString(), "none", StringComparison.OrdinalIgnoreCase);
        }

        private static SKBlendMode GetBlendMode(SvgBlendMode svgBlendMode)
        {
            return svgBlendMode switch
            {
                SvgBlendMode.Normal => SKBlendMode.SrcOver,
                SvgBlendMode.Multiply => SKBlendMode.Multiply,
                SvgBlendMode.Screen => SKBlendMode.Screen,
                SvgBlendMode.Overlay => SKBlendMode.Overlay,
                SvgBlendMode.Darken => SKBlendMode.Darken,
                SvgBlendMode.Lighten => SKBlendMode.Lighten,
                SvgBlendMode.ColorDodge => SKBlendMode.ColorDodge,
                SvgBlendMode.ColorBurn => SKBlendMode.ColorBurn,
                SvgBlendMode.HardLight => SKBlendMode.HardLight,
                SvgBlendMode.SoftLight => SKBlendMode.SoftLight,
                SvgBlendMode.Difference => SKBlendMode.Difference,
                SvgBlendMode.Exclusion => SKBlendMode.Exclusion,
                SvgBlendMode.Hue => SKBlendMode.Hue,
                SvgBlendMode.Saturation => SKBlendMode.Saturation,
                SvgBlendMode.Color => SKBlendMode.Color,
                SvgBlendMode.Luminosity => SKBlendMode.Luminosity,
                _ => SKBlendMode.SrcOver,
            };
        }

        private static SKImageFilter? CreateBlend(SvgBlend svgBlend, SKImageFilter background, SKImageFilter? foreground = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            var mode = GetBlendMode(svgBlend.Mode);
            return SKImageFilter.CreateBlendMode(mode, background, foreground, cropRect);
        }

        private static float[] CreateIdentityColorMatrixArray()
        {
            return new float[]
            {
                1, 0, 0, 0, 0,
                0, 1, 0, 0, 0,
                0, 0, 1, 0, 0,
                0, 0, 0, 1, 0
            };
        }

        private static SKImageFilter? CreateColorMatrix(SvgColourMatrix svgColourMatrix, SKImageFilter? input = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            SKColorFilter skColorFilter;

            switch (svgColourMatrix.Type)
            {
                case SvgColourMatrixType.HueRotate:
                    {
                        var value = string.IsNullOrEmpty(svgColourMatrix.Values) ? 0 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture);
                        var hue = (float)DegreeToRadian(value);
                        var cosHue = Math.Cos(hue);
                        var sinHue = Math.Sin(hue);
                        float[] matrix = {
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
                    }
                    break;

                case SvgColourMatrixType.LuminanceToAlpha:
                    {
                        float[] matrix = {
                            0, 0, 0, 0, 0,
                            0, 0, 0, 0, 0,
                            0, 0, 0, 0, 0,
                            0.2125f, 0.7154f, 0.0721f, 0, 0
                        };
                        skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                    }
                    break;

                case SvgColourMatrixType.Saturate:
                    {
                        var value = string.IsNullOrEmpty(svgColourMatrix.Values) ? 1 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture);
                        float[] matrix = {
                            (float)(0.213+0.787*value), (float)(0.715-0.715*value), (float)(0.072-0.072*value), 0, 0,
                            (float)(0.213-0.213*value), (float)(0.715+0.285*value), (float)(0.072-0.072*value), 0, 0,
                            (float)(0.213-0.213*value), (float)(0.715-0.715*value), (float)(0.072+0.928*value), 0, 0,
                            0, 0, 0, 1, 0
                        };
                        skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
                    }
                    break;

                default:
                case SvgColourMatrixType.Matrix:
                    {
                        float[] matrix;
                        if (string.IsNullOrEmpty(svgColourMatrix.Values))
                        {
                            matrix = CreateIdentityColorMatrixArray();
                        }
                        else
                        {
                            var parts = svgColourMatrix.Values.Split(s_colorMatrixSplitChars, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 20)
                            {
                                matrix = new float[20];
                                for (var i = 0; i < 20; i++)
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
                    }
                    break;
            }

            return SKImageFilter.CreateColorFilter(skColorFilter, input, cropRect);
        }

        private static void Identity(byte[] values, SvgComponentTransferFunction transferFunction)
        {
        }

        private static void Table(byte[] values, SvgComponentTransferFunction transferFunction)
        {
            var tableValues = transferFunction.TableValues;
            var n = tableValues.Count;
            if (n < 1)
            {
                return;
            }
            for (var i = 0; i < 256; i++)
            {
                var c = i / 255.0;
                var k = (byte)(c * (n - 1));
                double v1 = tableValues[k];
                double v2 = tableValues[Math.Min(k + 1, n - 1)];
                var val = 255.0 * (v1 + (c * (n - 1) - k) * (v2 - v1));
                val = Math.Max(0.0, Math.Min(255.0, val));
                values[i] = (byte)val;
            }
        }

        private static void Discrete(byte[] values, SvgComponentTransferFunction transferFunction)
        {
            var tableValues = transferFunction.TableValues;
            var n = tableValues.Count;
            if (n < 1)
            {
                return;
            }
            for (var i = 0; i < 256; i++)
            {
                var k = (byte)(i * n / 255.0);
                k = (byte)Math.Min(k, n - 1);
                double val = 255 * tableValues[k];
                val = Math.Max(0.0, Math.Min(255.0, val));
                values[i] = (byte)val;
            }
        }

        private static void Linear(byte[] values, SvgComponentTransferFunction transferFunction)
        {
            for (var i = 0; i < 256; i++)
            {
                double val = transferFunction.Slope * i + 255 * transferFunction.Intercept;
                val = Math.Max(0.0, Math.Min(255.0, val));
                values[i] = (byte)val;
            }
        }

        private static void Gamma(byte[] values, SvgComponentTransferFunction transferFunction)
        {
            for (var i = 0; i < 256; i++)
            {
                double exponent = transferFunction.Exponent;
                var val = 255.0 * (transferFunction.Amplitude * Math.Pow(i / 255.0, exponent) + transferFunction.Offset);
                val = Math.Max(0.0, Math.Min(255.0, val));
                values[i] = (byte)val;
            }
        }

        private static void Apply(byte[] values, SvgComponentTransferFunction transferFunction)
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

        private static SKImageFilter? CreateComponentTransfer(SvgComponentTransfer svgComponentTransfer, SKImageFilter? input = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            var svgFuncA = s_identitySvgFuncA;
            var svgFuncR = s_identitySvgFuncR;
            var svgFuncG = s_identitySvgFuncG;
            var svgFuncB = s_identitySvgFuncB;

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

            for (var i = 0; i < 256; i++)
            {
                tableA[i] = tableR[i] = tableG[i] = tableB[i] = (byte)i;
            }

            Apply(tableA, svgFuncA);
            Apply(tableR, svgFuncR);
            Apply(tableG, svgFuncG);
            Apply(tableB, svgFuncB);

            var cf = SKColorFilter.CreateTable(tableA, tableR, tableG, tableB);

            return SKImageFilter.CreateColorFilter(cf, input, cropRect);
        }

        private static SKImageFilter? CreateComposite(SvgComposite svgComposite, SKImageFilter background, SKImageFilter? foreground = default, SKImageFilter.SKCropRect? cropRect = default)
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
                    SvgCompositeOperator.Over => SKBlendMode.SrcOver,
                    SvgCompositeOperator.In => SKBlendMode.SrcIn,
                    SvgCompositeOperator.Out => SKBlendMode.SrcOut,
                    SvgCompositeOperator.Atop => SKBlendMode.SrcATop,
                    SvgCompositeOperator.Xor => SKBlendMode.Xor,
                    _ => SKBlendMode.SrcOver,
                };
                return SKImageFilter.CreateBlendMode(mode, background, foreground, cropRect);
            }
        }

        private static SKImageFilter? CreateConvolveMatrix(SvgConvolveMatrix svgConvolveMatrix, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter? input = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            GetOptionalNumbers(svgConvolveMatrix.Order, 3f, 3f, out var orderX, out var orderY);

            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                orderX *= skBounds.Width;
                orderY *= skBounds.Height;
            }

            if (orderX <= 0f || orderY <= 0f)
            {
                return default;
            }

            var kernelSize = new SKSizeI((int)orderX, (int)orderY);
            var kernelMatrix = svgConvolveMatrix.KernelMatrix;

            if (kernelMatrix is null)
            {
                return default;
            }

            if (kernelSize.Width * kernelSize.Height != kernelMatrix.Count)
            {
                return default;
            }

            float[] kernel = new float[kernelMatrix.Count];

            var count = kernelMatrix.Count;
            for (var i = 0; i < count; i++)
            {
                kernel[i] = kernelMatrix[count - 1 - i];
            }

            var divisor = svgConvolveMatrix.Divisor;
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

            var gain = 1f / divisor;
            var bias = svgConvolveMatrix.Bias * 255f;
            var kernelOffset = new SKPointI(svgConvolveMatrix.TargetX, svgConvolveMatrix.TargetY);
            var tileMode = svgConvolveMatrix.EdgeMode switch
            {
                SvgEdgeMode.Duplicate => SKShaderTileMode.Clamp,
                SvgEdgeMode.Wrap => SKShaderTileMode.Repeat,
                SvgEdgeMode.None => SKShaderTileMode.Decal,
                _ => SKShaderTileMode.Clamp
            };
            var convolveAlpha = !svgConvolveMatrix.PreserveAlpha;

            return SKImageFilter.CreateMatrixConvolution(kernelSize, kernel, gain, bias, kernelOffset, tileMode, convolveAlpha, input, cropRect);
        }

        private static SKPoint3 GetDirection(SvgDistantLight svgDistantLight)
        {
            var azimuth = svgDistantLight.Azimuth;
            var elevation = svgDistantLight.Elevation;
            var azimuthRad = DegreeToRadian(azimuth);
            var elevationRad = DegreeToRadian(elevation);
            var x = (float)(Math.Cos(azimuthRad) * Math.Cos(elevationRad));
            var y = (float)(Math.Sin(azimuthRad) * Math.Cos(elevationRad));
            var z = (float)Math.Sin(elevationRad);
            return new SKPoint3(x, y, z);
        }

        private static SKPoint3 GetPoint3(float x, float y, float z, SKRect skBounds, SvgCoordinateUnits primitiveUnits)
        {
            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                x *= skBounds.Width;
                y *= skBounds.Height;
                z *= CalculateOtherPercentageValue(skBounds);
            }
            return new SKPoint3(x, y, z);
        }

        private static SKImageFilter? CreateDiffuseLighting(SvgDiffuseLighting svgDiffuseLighting, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SvgVisualElement svgVisualElement, SKImageFilter? input = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            var lightColor = GetColor(svgVisualElement, svgDiffuseLighting.LightingColor);
            if (lightColor is null)
            {
                return default;
            }

            var surfaceScale = svgDiffuseLighting.SurfaceScale;
            var diffuseConstant = svgDiffuseLighting.DiffuseConstant;
            // TODO: svgDiffuseLighting.KernelUnitLength

            if (diffuseConstant < 0f)
            {
                diffuseConstant = 0f;
            }

            switch (svgDiffuseLighting.LightSource)
            {
                case SvgDistantLight svgDistantLight:
                    {
                        var direction = GetDirection(svgDistantLight);
                        return SKImageFilter.CreateDistantLitDiffuse(direction, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
                    }
                case SvgPointLight svgPointLight:
                    {
                        var location = GetPoint3(svgPointLight.X, svgPointLight.Y, svgPointLight.Z, skBounds, primitiveUnits);
                        return SKImageFilter.CreatePointLitDiffuse(location, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
                    }
                case SvgSpotLight svgSpotLight:
                    {
                        var location = GetPoint3(svgSpotLight.X, svgSpotLight.Y, svgSpotLight.Z, skBounds, primitiveUnits);
                        var target = GetPoint3(svgSpotLight.PointsAtX, svgSpotLight.PointsAtY, svgSpotLight.PointsAtZ, skBounds, primitiveUnits);
                        var specularExponentSpotLight = svgSpotLight.SpecularExponent;
                        var limitingConeAngle = svgSpotLight.LimitingConeAngle;
                        if (float.IsNaN(limitingConeAngle) || limitingConeAngle > 90f || limitingConeAngle < -90f)
                        {
                            limitingConeAngle = 90f;
                        }
                        return SKImageFilter.CreateSpotLitDiffuse(location, target, specularExponentSpotLight, limitingConeAngle, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
                    }
            }
            return default;
        }

        private static SKColorChannel GetColorChannel(SvgChannelSelector svgChannelSelector)
        {
            return svgChannelSelector switch
            {
                SvgChannelSelector.R => SKColorChannel.R,
                SvgChannelSelector.G => SKColorChannel.G,
                SvgChannelSelector.B => SKColorChannel.B,
                SvgChannelSelector.A => SKColorChannel.A,
                _ => SKColorChannel.A
            };
        }

        private static SKImageFilter? CreateDisplacementMap(SvgDisplacementMap svgDisplacementMap, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter displacement, SKImageFilter? inout = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            var xChannelSelector = GetColorChannel(svgDisplacementMap.XChannelSelector);
            var yChannelSelector = GetColorChannel(svgDisplacementMap.YChannelSelector);
            var scale = svgDisplacementMap.Scale;

            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                scale *= CalculateOtherPercentageValue(skBounds);
            }

            return SKImageFilter.CreateDisplacementMapEffect(xChannelSelector, yChannelSelector, scale, displacement, inout, cropRect);
        }

        private static SKImageFilter? CreateFlood(SvgFlood svgFlood, SvgVisualElement svgVisualElement, SKRect skBounds, SKImageFilter? input = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            var floodColor = GetColor(svgVisualElement, svgFlood.FloodColor);
            if (floodColor is null)
            {
                return default;
            }

            var floodOpacity = svgFlood.FloodOpacity;
            var floodAlpha = CombineWithOpacity(floodColor.Value.Alpha, floodOpacity);
            floodColor = new SKColor(floodColor.Value.Red, floodColor.Value.Green, floodColor.Value.Blue, floodAlpha);

            if (cropRect is null)
            {
                cropRect = new SKImageFilter.SKCropRect(skBounds);
            }

            var cf = SKColorFilter.CreateBlendMode(floodColor.Value, SKBlendMode.Src);

            return SKImageFilter.CreateColorFilter(cf, input, cropRect);
        }

        private static SKImageFilter? CreateBlur(SvgGaussianBlur svgGaussianBlur, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter? input = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            GetOptionalNumbers(svgGaussianBlur.StdDeviation, 0f, 0f, out var sigmaX, out var sigmaY);

            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var value = CalculateOtherPercentageValue(skBounds);
                sigmaX *= value;
                sigmaY *= value;
            }

            if (sigmaX < 0f && sigmaY < 0f)
            {
                return default;
            }

            return SKImageFilter.CreateBlur(sigmaX, sigmaY, input, cropRect);
        }

        private static SKImageFilter? CreateImage(FilterEffects.SvgImage svgImage, SKRect skBounds, IAssetLoader assetLoader, SKImageFilter.SKCropRect? cropRect = default)
        {
            var image = GetImage(svgImage.Href, svgImage.OwnerDocument, assetLoader);
            var skImage = image as SKImage;
            var svgFragment = image as SvgFragment;
            if (skImage is null && svgFragment is null)
            {
                return default;
            }

            var destClip = skBounds;

            var srcRect = default(SKRect);

            if (skImage is { })
            {
                srcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment is { })
            {
                var skSize = GetDimensions(svgFragment);
                srcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            var destRect = CalculateRect(svgImage.AspectRatio, srcRect, destClip);

            if (skImage is { })
            {
                return SKImageFilter.CreateImage(skImage, srcRect, destRect, SKFilterQuality.High);
            }

            if (svgFragment is { })
            {
                var fragmentTransform = SKMatrix.CreateIdentity();
                var dx = destRect.Left;
                var dy = destRect.Top;
                var sx = destRect.Width / srcRect.Width;
                var sy = destRect.Height / srcRect.Height;
                var skTranslationMatrix = SKMatrix.CreateTranslation(dx, dy);
                var skScaleMatrix = SKMatrix.CreateScale(sx, sy);
                fragmentTransform = fragmentTransform.PreConcat(skTranslationMatrix);
                fragmentTransform = fragmentTransform.PreConcat(skScaleMatrix);
                // TODO: fragmentTransform

                var fragmentDrawable = FragmentDrawable.Create(svgFragment, destRect, null, assetLoader, DrawAttributes.None);
                // TODO: fragmentDrawable.Snapshot()
                var skPicture = fragmentDrawable.Snapshot();

                return SKImageFilter.CreatePicture(skPicture, destRect);
            }

            return default;
        }

        private static SKImageFilter? CreateMerge(SvgMerge svgMerge, Dictionary<string, SKImageFilter> results, SKImageFilter? lastResult, IFilterSource filterSource, SKImageFilter.SKCropRect? cropRect = default)
        {
            var children = new List<SvgMergeNode>();

            foreach (var child in svgMerge.Children)
            {
                if (child is SvgMergeNode svgMergeNode)
                {
                    children.Add(svgMergeNode);
                }
            }

            var filters = new SKImageFilter[children.Count];

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var inputKey = child.Input;
                var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, false);
                if (inputFilter is { })
                {
                    filters[i] = inputFilter;
                }
                else
                {
                    return default;
                }
            }

            return SKImageFilter.CreateMerge(filters, cropRect);
        }

        private static SKImageFilter? CreateMorphology(SvgMorphology svgMorphology, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter? input = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            GetOptionalNumbers(svgMorphology.Radius, 0f, 0f, out var radiusX, out var radiusY);

            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var value = CalculateOtherPercentageValue(skBounds);
                radiusX *= value;
                radiusY *= value;
            }

            if (radiusX <= 0f && radiusY <= 0f)
            {
                return default;
            }

            return svgMorphology.Operator switch
            {
                SvgMorphologyOperator.Dilate => SKImageFilter.CreateDilate((int)radiusX, (int)radiusY, input, cropRect),
                SvgMorphologyOperator.Erode => SKImageFilter.CreateErode((int)radiusX, (int)radiusY, input, cropRect),
                _ => null,
            };
        }

        private static SKImageFilter? CreateOffset(SvgOffset svgOffset, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter? input = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            var dxUnit = svgOffset.Dx;
            var dyUnit = svgOffset.Dy;

            var dx = dxUnit.ToDeviceValue(UnitRenderingType.HorizontalOffset, svgOffset, skBounds);
            var dy = dyUnit.ToDeviceValue(UnitRenderingType.VerticalOffset, svgOffset, skBounds);

            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                if (dxUnit.Type != SvgUnitType.Percentage)
                {
                    dx *= skBounds.Width;
                }

                if (dyUnit.Type != SvgUnitType.Percentage)
                {
                    dy *= skBounds.Height;
                }
            }

            return SKImageFilter.CreateOffset(dx, dy, input, cropRect);
        }

        private static SKImageFilter? CreateSpecularLighting(SvgSpecularLighting svgSpecularLighting, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SvgVisualElement svgVisualElement, SKImageFilter? input = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            var lightColor = GetColor(svgVisualElement, svgSpecularLighting.LightingColor);
            if (lightColor is null)
            {
                return default;
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
                        var location = GetPoint3(svgPointLight.X, svgPointLight.Y, svgPointLight.Z, skBounds, primitiveUnits);
                        return SKImageFilter.CreatePointLitSpecular(location, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                    }
                case SvgSpotLight svgSpotLight:
                    {
                        var location = GetPoint3(svgSpotLight.X, svgSpotLight.Y, svgSpotLight.Z, skBounds, primitiveUnits);
                        var target = GetPoint3(svgSpotLight.PointsAtX, svgSpotLight.PointsAtY, svgSpotLight.PointsAtZ, skBounds, primitiveUnits);
                        var specularExponentSpotLight = svgSpotLight.SpecularExponent;
                        var limitingConeAngle = svgSpotLight.LimitingConeAngle;
                        if (float.IsNaN(limitingConeAngle) || limitingConeAngle > 90f || limitingConeAngle < -90f)
                        {
                            limitingConeAngle = 90f;
                        }
                        return SKImageFilter.CreateSpotLitSpecular(location, target, specularExponentSpotLight, limitingConeAngle, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                    }
            }
            return default;
        }

        private static SKImageFilter? CreateTile(SvgTile svgTile, SKRect skBounds, SKImageFilter? input = default, SKImageFilter.SKCropRect? cropRect = default)
        {
            var src = skBounds;
            var dst = cropRect?.Rect ?? skBounds;
            return SKImageFilter.CreateTile(src, dst, input);
        }

        private static SKImageFilter? CreateTurbulence(SvgTurbulence svgTurbulence, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter.SKCropRect? cropRect = default)
        {
            GetOptionalNumbers(svgTurbulence.BaseFrequency, 0f, 0f, out var baseFrequencyX, out var baseFrequencyY);

            if (baseFrequencyX < 0f || baseFrequencyY < 0f)
            {
                return default;
            }

            var numOctaves = svgTurbulence.NumOctaves;

            if (numOctaves < 0)
            {
                return default;
            }

            var seed = svgTurbulence.Seed;

            var skPaint = new SKPaint
            {
                Style = SKPaintStyle.StrokeAndFill
            };

            SKPointI tileSize;
            switch (svgTurbulence.StitchTiles)
            {
                default:
                case SvgStitchType.NoStitch:
                    tileSize = SKPointI.Empty;
                    break;

                case SvgStitchType.Stitch:
                    // TODO: SvgStitchType.Stitch
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

            if (cropRect is null)
            {
                cropRect = new SKImageFilter.SKCropRect(skBounds);
            }

            return SKImageFilter.CreatePaint(skPaint, cropRect);
        }

        private static SKImageFilter? GetGraphic(SKPicture skPicture)
        {
            var skImageFilter = SKImageFilter.CreatePicture(skPicture, skPicture.CullRect);
            return skImageFilter;
        }

        private static SKImageFilter? GetAlpha(SKPicture skPicture)
        {
            var skImageFilterGraphic = GetGraphic(skPicture);

            var matrix = new float[20]
            {
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 1f, 0f
            };

            var skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter, skImageFilterGraphic);

            return skImageFilter;
        }

        private static SKImageFilter? GetPaint(SKPaint skPaint)
        {
            var skImageFilter = SKImageFilter.CreatePaint(skPaint);
            return skImageFilter;
        }

        private static SKImageFilter GetTransparentBlackImage()
        {
            var skPaint = new SKPaint
            {
                Style = SKPaintStyle.StrokeAndFill,
                Color = s_transparentBlack
            };
            var skImageFilter = SKImageFilter.CreatePaint(skPaint);
            return skImageFilter;
        }

        private static SKImageFilter GetTransparentBlackAlpha()
        {
            var skPaint = new SKPaint
            {
                Style = SKPaintStyle.StrokeAndFill,
                Color = s_transparentBlack
            };

            var skImageFilterGraphic = SKImageFilter.CreatePaint(skPaint);

            var matrix = new float[20]
            {
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 1f, 0f
            };

            var skColorFilter = SKColorFilter.CreateColorMatrix(matrix);
            var skImageFilter = SKImageFilter.CreateColorFilter(skColorFilter, skImageFilterGraphic);
            return skImageFilter;
        }

        private static SKImageFilter? GetInputFilter(string inputKey, Dictionary<string, SKImageFilter> results, SKImageFilter? lastResult, IFilterSource filterSource, bool isFirst)
        {
            if (string.IsNullOrWhiteSpace(inputKey))
            {
                if (!isFirst)
                {
                    return lastResult;
                }

                if (results.ContainsKey(SourceGraphic))
                {
                    return results[SourceGraphic];
                }

                var skPicture = filterSource.SourceGraphic();
                if (skPicture is { })
                {
                    var skImageFilter = GetGraphic(skPicture);
                    if (skImageFilter is { })
                    {
                        results[SourceGraphic] = skImageFilter;
                        return skImageFilter;
                    }
                }
                return default;
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
                        if (skPicture is { })
                        {
                            var skImageFilter = GetGraphic(skPicture);
                            if (skImageFilter is { })
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
                        if (skPicture is { })
                        {
                            var skImageFilter = GetAlpha(skPicture);
                            if (skImageFilter is { })
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
                        if (skPicture is { })
                        {
                            var skImageFilter = GetGraphic(skPicture);
                            if (skImageFilter is { })
                            {
                                results[BackgroundImage] = skImageFilter;
                                return skImageFilter;
                            }
                        }
                        else
                        {
                            var skImageFilter = GetTransparentBlackImage();
                            results[BackgroundImage] = skImageFilter;
                            return skImageFilter;
                        }
                    }
                    break;

                case BackgroundAlpha:
                    {
                        var skPicture = filterSource.BackgroundImage();
                        if (skPicture is { })
                        {
                            var skImageFilter = GetAlpha(skPicture);
                            if (skImageFilter is { })
                            {
                                results[BackgroundAlpha] = skImageFilter;
                                return skImageFilter;
                            }
                        }
                        else
                        {
                            var skImageFilter = GetTransparentBlackAlpha();
                            results[BackgroundImage] = skImageFilter;
                            return skImageFilter;
                        }
                    }
                    break;

                case FillPaint:
                    {
                        var skPaint = filterSource.FillPaint();
                        if (skPaint is { })
                        {
                            var skImageFilter = GetPaint(skPaint);
                            if (skImageFilter is { })
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
                        if (skPaint is { })
                        {
                            var skImageFilter = GetPaint(skPaint);
                            if (skImageFilter is { })
                            {
                                results[StrokePaint] = skImageFilter;
                                return skImageFilter;
                            }
                        }
                    }
                    break;
            }

            return default;
        }

        private static SKImageFilter? GetFilterResult(SvgFilterPrimitive svgFilterPrimitive, SKImageFilter? skImageFilter, Dictionary<string, SKImageFilter> results)
        {
            if (skImageFilter is { })
            {
                var key = svgFilterPrimitive.Result;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    results[key] = skImageFilter;
                }
                return skImageFilter;
            }
            return default;
        }

        private static List<SvgFilter>? GetLinkedFilter(SvgVisualElement svgVisualElement, HashSet<Uri> uris)
        {
            var currentFilter = GetReference<SvgFilter>(svgVisualElement, svgVisualElement.Filter);
            if (currentFilter is null)
            {
                return default;
            }

            var svgFilters = new List<SvgFilter>();
            do
            {
                if (currentFilter is { })
                {
                    svgFilters.Add(currentFilter);
                    if (HasRecursiveReference(currentFilter, (e) => e.Href, uris))
                    {
                        return svgFilters;
                    }
                    currentFilter = GetReference<SvgFilter>(currentFilter, currentFilter.Href);
                }
            } while (currentFilter is { });

            return svgFilters;
        }
    }
}
