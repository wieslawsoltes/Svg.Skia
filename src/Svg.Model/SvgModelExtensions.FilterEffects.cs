using System;
using System.Collections.Generic;
using System.Globalization;
using Svg.FilterEffects;
using Svg.Model.Drawables;
using Svg.Model.Drawables.Elements;
using Svg.Model.Painting.ImageFilters;
using Svg.Model.Painting;
using Svg.Model.Primitives;
using Svg.Model.Painting.Shaders;

namespace Svg.Model
{
    public static partial class SvgModelExtensions
    {
        private static readonly char[] s_colorMatrixSplitChars = { ' ', '\t', '\n', '\r', ',' };

        internal static Color s_transparentBlack = new(0, 0, 0, 255);

        internal const string SourceGraphic = "SourceGraphic";

        internal const string SourceAlpha = "SourceAlpha";

        internal const string BackgroundImage = "BackgroundImage";

        internal const string BackgroundAlpha = "BackgroundAlpha";

        internal const string FillPaint = "FillPaint";

        internal const string StrokePaint = "StrokePaint";

        internal static bool IsStandardInput(string key)
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

        internal static SvgFuncA s_identitySvgFuncA = new()
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        internal static SvgFuncR s_identitySvgFuncR = new()
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        internal static SvgFuncG s_identitySvgFuncG = new()
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        internal static SvgFuncB s_identitySvgFuncB = new()
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

        internal static bool IsNone(this Uri uri)
        {
            return string.Equals(uri.ToString(), "none", StringComparison.OrdinalIgnoreCase);
        }

        internal static BlendMode GetBlendMode(SvgBlendMode svgBlendMode)
        {
            return svgBlendMode switch
            {
                SvgBlendMode.Normal => BlendMode.SrcOver,
                SvgBlendMode.Multiply => BlendMode.Multiply,
                SvgBlendMode.Screen => BlendMode.Screen,
                SvgBlendMode.Overlay => BlendMode.Overlay,
                SvgBlendMode.Darken => BlendMode.Darken,
                SvgBlendMode.Lighten => BlendMode.Lighten,
                SvgBlendMode.ColorDodge => BlendMode.ColorDodge,
                SvgBlendMode.ColorBurn => BlendMode.ColorBurn,
                SvgBlendMode.HardLight => BlendMode.HardLight,
                SvgBlendMode.SoftLight => BlendMode.SoftLight,
                SvgBlendMode.Difference => BlendMode.Difference,
                SvgBlendMode.Exclusion => BlendMode.Exclusion,
                SvgBlendMode.Hue => BlendMode.Hue,
                SvgBlendMode.Saturation => BlendMode.Saturation,
                SvgBlendMode.Color => BlendMode.Color,
                SvgBlendMode.Luminosity => BlendMode.Luminosity,
                _ => BlendMode.SrcOver,
            };
        }

        internal static ImageFilter? CreateBlend(SvgBlend svgBlend, ImageFilter background, ImageFilter? foreground = default, CropRect? cropRect = default)
        {
            var mode = GetBlendMode(svgBlend.Mode);
            return ImageFilter.CreateBlendMode(mode, background, foreground, cropRect);
        }

        internal static float[] CreateIdentityColorMatrixArray()
        {
            return new float[]
            {
                1, 0, 0, 0, 0,
                0, 1, 0, 0, 0,
                0, 0, 1, 0, 0,
                0, 0, 0, 1, 0
            };
        }

        internal static ImageFilter? CreateColorMatrix(SvgColourMatrix svgColourMatrix, ImageFilter? input = default, CropRect? cropRect = default)
        {
            ColorFilter skColorFilter;

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
                        skColorFilter = ColorFilter.CreateColorMatrix(matrix);
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
                        skColorFilter = ColorFilter.CreateColorMatrix(matrix);
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
                        skColorFilter = ColorFilter.CreateColorMatrix(matrix);
                    };
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
                        skColorFilter = ColorFilter.CreateColorMatrix(matrix);
                    }
                    break;
            }

            return ImageFilter.CreateColorFilter(skColorFilter, input, cropRect);
        }

        internal static void Identity(byte[] values, SvgComponentTransferFunction transferFunction)
        {
        }

        internal static void Table(byte[] values, SvgComponentTransferFunction transferFunction)
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

        internal static void Discrete(byte[] values, SvgComponentTransferFunction transferFunction)
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

        internal static void Linear(byte[] values, SvgComponentTransferFunction transferFunction)
        {
            for (var i = 0; i < 256; i++)
            {
                double val = transferFunction.Slope * i + 255 * transferFunction.Intercept;
                val = Math.Max(0.0, Math.Min(255.0, val));
                values[i] = (byte)val;
            }
        }

        internal static void Gamma(byte[] values, SvgComponentTransferFunction transferFunction)
        {
            for (var i = 0; i < 256; i++)
            {
                double exponent = transferFunction.Exponent;
                var val = 255.0 * (transferFunction.Amplitude * Math.Pow(i / 255.0, exponent) + transferFunction.Offset);
                val = Math.Max(0.0, Math.Min(255.0, val));
                values[i] = (byte)val;
            }
        }

        internal static void Apply(byte[] values, SvgComponentTransferFunction transferFunction)
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

        internal static ImageFilter? CreateComponentTransfer(SvgComponentTransfer svgComponentTransfer, ImageFilter? input = default, CropRect? cropRect = default)
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

            var cf = ColorFilter.CreateTable(tableA, tableR, tableG, tableB);

            return ImageFilter.CreateColorFilter(cf, input, cropRect);
        }

        internal static ImageFilter? CreateComposite(SvgComposite svgComposite, ImageFilter background, ImageFilter? foreground = default, CropRect? cropRect = default)
        {
            var oper = svgComposite.Operator;
            if (oper == SvgCompositeOperator.Arithmetic)
            {
                var k1 = svgComposite.K1;
                var k2 = svgComposite.K2;
                var k3 = svgComposite.K3;
                var k4 = svgComposite.K4;
                return ImageFilter.CreateArithmetic(k1, k2, k3, k4, false, background, foreground, cropRect);
            }
            else
            {
                var mode = oper switch
                {
                    SvgCompositeOperator.Over => BlendMode.SrcOver,
                    SvgCompositeOperator.In => BlendMode.SrcIn,
                    SvgCompositeOperator.Out => BlendMode.SrcOut,
                    SvgCompositeOperator.Atop => BlendMode.SrcATop,
                    SvgCompositeOperator.Xor => BlendMode.Xor,
                    _ => BlendMode.SrcOver,
                };
                return ImageFilter.CreateBlendMode(mode, background, foreground, cropRect);
            }
        }

        internal static ImageFilter? CreateConvolveMatrix(SvgConvolveMatrix svgConvolveMatrix, Rect skBounds, SvgCoordinateUnits primitiveUnits, ImageFilter? input = default, CropRect? cropRect = default)
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

            var kernelSize = new SizeI((int)orderX, (int)orderY);
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
            var kernelOffset = new PointI(svgConvolveMatrix.TargetX, svgConvolveMatrix.TargetY);
            var tileMode = svgConvolveMatrix.EdgeMode switch
            {
                SvgEdgeMode.Duplicate => ShaderTileMode.Clamp,
                SvgEdgeMode.Wrap => ShaderTileMode.Repeat,
                SvgEdgeMode.None => ShaderTileMode.Decal,
                _ => ShaderTileMode.Clamp
            };
            var convolveAlpha = !svgConvolveMatrix.PreserveAlpha;

            return ImageFilter.CreateMatrixConvolution(kernelSize, kernel, gain, bias, kernelOffset, tileMode, convolveAlpha, input, cropRect);
        }

        internal static Point3 GetDirection(SvgDistantLight svgDistantLight)
        {
            var azimuth = svgDistantLight.Azimuth;
            var elevation = svgDistantLight.Elevation;
            var azimuthRad = DegreeToRadian(azimuth);
            var elevationRad = DegreeToRadian(elevation);
            var x = (float)(Math.Cos(azimuthRad) * Math.Cos(elevationRad));
            var y = (float)(Math.Sin(azimuthRad) * Math.Cos(elevationRad));
            var z = (float)Math.Sin(elevationRad);
            return new Point3(x, y, z);
        }

        internal static Point3 GetPoint3(float x, float y, float z, Rect skBounds, SvgCoordinateUnits primitiveUnits)
        {
            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                x *= skBounds.Width;
                y *= skBounds.Height;
                z *= CalculateOtherPercentageValue(skBounds);
            }
            return new Point3(x, y, z);
        }

        internal static ImageFilter? CreateDiffuseLighting(SvgDiffuseLighting svgDiffuseLighting, Rect skBounds, SvgCoordinateUnits primitiveUnits, SvgVisualElement svgVisualElement, ImageFilter? input = default, CropRect? cropRect = default)
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
                        return ImageFilter.CreateDistantLitDiffuse(direction, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
                    }
                case SvgPointLight svgPointLight:
                    {
                        var location = GetPoint3(svgPointLight.X, svgPointLight.Y, svgPointLight.Z, skBounds, primitiveUnits);
                        return ImageFilter.CreatePointLitDiffuse(location, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
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
                        return ImageFilter.CreateSpotLitDiffuse(location, target, specularExponentSpotLight, limitingConeAngle, lightColor.Value, surfaceScale, diffuseConstant, input, cropRect);
                    }
            }
            return default;
        }

        internal static ColorChannel GetColorChannel(SvgChannelSelector svgChannelSelector)
        {
            return svgChannelSelector switch
            {
                SvgChannelSelector.R => ColorChannel.R,
                SvgChannelSelector.G => ColorChannel.G,
                SvgChannelSelector.B => ColorChannel.B,
                SvgChannelSelector.A => ColorChannel.A,
                _ => ColorChannel.A
            };
        }

        internal static ImageFilter? CreateDisplacementMap(SvgDisplacementMap svgDisplacementMap, Rect skBounds, SvgCoordinateUnits primitiveUnits, ImageFilter displacement, ImageFilter? inout = default, CropRect? cropRect = default)
        {
            var xChannelSelector = GetColorChannel(svgDisplacementMap.XChannelSelector);
            var yChannelSelector = GetColorChannel(svgDisplacementMap.YChannelSelector);
            var scale = svgDisplacementMap.Scale;

            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                scale *= CalculateOtherPercentageValue(skBounds);
            }

            return ImageFilter.CreateDisplacementMapEffect(xChannelSelector, yChannelSelector, scale, displacement, inout, cropRect);
        }

        internal static ImageFilter? CreateFlood(SvgFlood svgFlood, SvgVisualElement svgVisualElement, Rect skBounds, ImageFilter? input = default, CropRect? cropRect = default)
        {
            var floodColor = GetColor(svgVisualElement, svgFlood.FloodColor);
            if (floodColor is null)
            {
                return default;
            }

            var floodOpacity = svgFlood.FloodOpacity;
            var floodAlpha = CombineWithOpacity(floodColor.Value.Alpha, floodOpacity);
            floodColor = new Color(floodColor.Value.Red, floodColor.Value.Green, floodColor.Value.Blue, floodAlpha);

            if (cropRect is null)
            {
                cropRect = new CropRect(skBounds);
            }

            var cf = ColorFilter.CreateBlendMode(floodColor.Value, BlendMode.Src);

            return ImageFilter.CreateColorFilter(cf, input, cropRect);
        }

        internal static ImageFilter? CreateBlur(SvgGaussianBlur svgGaussianBlur, Rect skBounds, SvgCoordinateUnits primitiveUnits, ImageFilter? input = default, CropRect? cropRect = default)
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

            return ImageFilter.CreateBlur(sigmaX, sigmaY, input, cropRect);
        }

        internal static ImageFilter? CreateImage(FilterEffects.SvgImage svgImage, Rect skBounds, IAssetLoader assetLoader, CropRect? cropRect = default)
        {
            var image = GetImage(svgImage.Href, svgImage.OwnerDocument, assetLoader);
            var skImage = image as Image;
            var svgFragment = image as SvgFragment;
            if (skImage is null && svgFragment is null)
            {
                return default;
            }

            var destClip = skBounds;

            var srcRect = default(Rect);
            var destRect = default(Rect);

            if (skImage is { })
            {
                srcRect = Rect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment is { })
            {
                var skSize = GetDimensions(svgFragment);
                srcRect = Rect.Create(0f, 0f, skSize.Width, skSize.Height);
            }

            var aspectRatio = svgImage.AspectRatio;
            if (aspectRatio.Align != SvgPreserveAspectRatio.none)
            {
                var fScaleX = destClip.Width / srcRect.Width;
                var fScaleY = destClip.Height / srcRect.Height;
                var xOffset = 0f;
                var yOffset = 0f;

                if (aspectRatio.Slice)
                {
                    fScaleX = Math.Max(fScaleX, fScaleY);
                    fScaleY = Math.Max(fScaleX, fScaleY);
                }
                else
                {
                    fScaleX = Math.Min(fScaleX, fScaleY);
                    fScaleY = Math.Min(fScaleX, fScaleY);
                }

                switch (aspectRatio.Align)
                {
                    case SvgPreserveAspectRatio.xMinYMin:
                        break;

                    case SvgPreserveAspectRatio.xMidYMin:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX) / 2;
                        break;

                    case SvgPreserveAspectRatio.xMaxYMin:
                        xOffset = destClip.Width - srcRect.Width * fScaleX;
                        break;

                    case SvgPreserveAspectRatio.xMinYMid:
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;

                    case SvgPreserveAspectRatio.xMidYMid:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;

                    case SvgPreserveAspectRatio.xMaxYMid:
                        xOffset = destClip.Width - srcRect.Width * fScaleX;
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;

                    case SvgPreserveAspectRatio.xMinYMax:
                        yOffset = destClip.Height - srcRect.Height * fScaleY;
                        break;

                    case SvgPreserveAspectRatio.xMidYMax:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX) / 2;
                        yOffset = destClip.Height - srcRect.Height * fScaleY;
                        break;

                    case SvgPreserveAspectRatio.xMaxYMax:
                        xOffset = destClip.Width - srcRect.Width * fScaleX;
                        yOffset = destClip.Height - srcRect.Height * fScaleY;
                        break;
                }

                destRect = Rect.Create(
                    destClip.Left + xOffset,
                    destClip.Top + yOffset,
                    srcRect.Width * fScaleX,
                    srcRect.Height * fScaleY);
            }
            else
            {
                destRect = destClip;
            }

            if (skImage is { })
            {
                return ImageFilter.CreateImage(skImage, srcRect, destRect, FilterQuality.High);
            }

            if (svgFragment is { })
            {
                var fragmentTransform = Matrix.CreateIdentity();
                var dx = destRect.Left;
                var dy = destRect.Top;
                var sx = destRect.Width / srcRect.Width;
                var sy = destRect.Height / srcRect.Height;
                var skTranslationMatrix = Matrix.CreateTranslation(dx, dy);
                var skScaleMatrix = Matrix.CreateScale(sx, sy);
                fragmentTransform = fragmentTransform.PreConcat(skTranslationMatrix);
                fragmentTransform = fragmentTransform.PreConcat(skScaleMatrix);

                var fragmentDrawable = FragmentDrawable.Create(svgFragment, destRect, null, assetLoader, Attributes.None);
                // TODO:
                var skPicture = fragmentDrawable.Snapshot(); 

                return ImageFilter.CreatePicture(skPicture, destRect);
            }

            return default;
        }

        internal static ImageFilter? CreateMerge(SvgMerge svgMerge, Dictionary<string, ImageFilter> results, ImageFilter? lastResult, IFilterSource filterSource, CropRect? cropRect = default)
        {
            var children = new List<SvgMergeNode>();

            foreach (var child in svgMerge.Children)
            {
                if (child is SvgMergeNode svgMergeNode)
                {
                    children.Add(svgMergeNode);
                }
            }

            var filters = new ImageFilter[children.Count];

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

            return ImageFilter.CreateMerge(filters, cropRect);
        }

        internal static ImageFilter? CreateMorphology(SvgMorphology svgMorphology, Rect skBounds, SvgCoordinateUnits primitiveUnits, ImageFilter? input = default, CropRect? cropRect = default)
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
                SvgMorphologyOperator.Dilate => ImageFilter.CreateDilate((int)radiusX, (int)radiusY, input, cropRect),
                SvgMorphologyOperator.Erode => ImageFilter.CreateErode((int)radiusX, (int)radiusY, input, cropRect),
                _ => null,
            };
        }

        internal static ImageFilter? CreateOffset(SvgOffset svgOffset, Rect skBounds, SvgCoordinateUnits primitiveUnits, ImageFilter? input = default, CropRect? cropRect = default)
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

            return ImageFilter.CreateOffset(dx, dy, input, cropRect);
        }

        internal static ImageFilter? CreateSpecularLighting(SvgSpecularLighting svgSpecularLighting, Rect skBounds, SvgCoordinateUnits primitiveUnits, SvgVisualElement svgVisualElement, ImageFilter? input = default, CropRect? cropRect = default)
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
                        return ImageFilter.CreateDistantLitSpecular(direction, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                    }
                case SvgPointLight svgPointLight:
                    {
                        var location = GetPoint3(svgPointLight.X, svgPointLight.Y, svgPointLight.Z, skBounds, primitiveUnits);
                        return ImageFilter.CreatePointLitSpecular(location, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
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
                        return ImageFilter.CreateSpotLitSpecular(location, target, specularExponentSpotLight, limitingConeAngle, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                    }
            }
            return default;
        }

        internal static ImageFilter? CreateTile(SvgTile svgTile, Rect skBounds, ImageFilter? input = default, CropRect? cropRect = default)
        {
            var src = skBounds;
            var dst = cropRect is { } ? cropRect.Rect : skBounds;
            return ImageFilter.CreateTile(src, dst, input);
        }

        internal static ImageFilter? CreateTurbulence(SvgTurbulence svgTurbulence, Rect skBounds, SvgCoordinateUnits primitiveUnits, CropRect? cropRect = default)
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

            var skPaint = new Paint
            {
                Style = PaintStyle.StrokeAndFill
            };

            PointI tileSize;
            switch (svgTurbulence.StitchTiles)
            {
                default:
                case SvgStitchType.NoStitch:
                    tileSize = PointI.Empty;
                    break;

                case SvgStitchType.Stitch:
                    // TODO:
                    tileSize = new PointI();
                    break;
            }

            Shader skShader;
            switch (svgTurbulence.Type)
            {
                default:
                case SvgTurbulenceType.FractalNoise:
                    skShader = Shader.CreatePerlinNoiseFractalNoise(baseFrequencyX, baseFrequencyY, numOctaves, seed, tileSize);
                    break;

                case SvgTurbulenceType.Turbulence:
                    skShader = Shader.CreatePerlinNoiseTurbulence(baseFrequencyX, baseFrequencyY, numOctaves, seed, tileSize);
                    break;
            }

            skPaint.Shader = skShader;

            if (cropRect is null)
            {
                cropRect = new CropRect(skBounds);
            }

            return ImageFilter.CreatePaint(skPaint, cropRect);
        }

        internal static ImageFilter? GetGraphic(Picture skPicture)
        {
            var skImageFilter = ImageFilter.CreatePicture(skPicture, skPicture.CullRect);
            return skImageFilter;
        }

        internal static ImageFilter? GetAlpha(Picture skPicture)
        {
            var skImageFilterGraphic = GetGraphic(skPicture);

            var matrix = new float[20]
            {
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 1f, 0f
            };

            var skColorFilter = ColorFilter.CreateColorMatrix(matrix);
            var skImageFilter = ImageFilter.CreateColorFilter(skColorFilter, skImageFilterGraphic);

            return skImageFilter;
        }

        internal static ImageFilter? GetPaint(Paint skPaint)
        {
            var skImageFilter = ImageFilter.CreatePaint(skPaint);
            return skImageFilter;
        }

        internal static ImageFilter GetTransparentBlackImage()
        {
            var skPaint = new Paint
            {
                Style = PaintStyle.StrokeAndFill,
                Color = s_transparentBlack
            };
            var skImageFilter = ImageFilter.CreatePaint(skPaint);
            return skImageFilter;
        }

        internal static ImageFilter GetTransparentBlackAlpha()
        {
            var skPaint = new Paint
            {
                Style = PaintStyle.StrokeAndFill,
                Color = s_transparentBlack
            };

            var skImageFilterGraphic = ImageFilter.CreatePaint(skPaint);

            var matrix = new float[20]
            {
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f, 0f,
                0f, 0f, 0f, 1f, 0f
            };

            var skColorFilter = ColorFilter.CreateColorMatrix(matrix);
            var skImageFilter = ImageFilter.CreateColorFilter(skColorFilter, skImageFilterGraphic);
            return skImageFilter;
        }

        internal static ImageFilter? GetInputFilter(string inputKey, Dictionary<string, ImageFilter> results, ImageFilter? lastResult, IFilterSource filterSource, bool isFirst)
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
                return lastResult;
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

        internal static ImageFilter? GetFilterResult(SvgFilterPrimitive svgFilterPrimitive, ImageFilter? skImageFilter, Dictionary<string, ImageFilter> results)
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

        internal static Paint? GetFilterPaint(SvgVisualElement svgVisualElement, Rect skBounds, IFilterSource filterSource, IAssetLoader assetLoader, out bool isValid)
        {
            var filter = svgVisualElement.Filter;
            if (filter is null || IsNone(filter))
            {
                isValid = true;
                return default;
            }

            var svgReferencedFilters = GetLinkedFilter(svgVisualElement, new HashSet<Uri>());
            if (svgReferencedFilters is null || svgReferencedFilters.Count < 0)
            {
                isValid = false;
                return default;
            }

            var svgFirstFilter = svgReferencedFilters[0];

            SvgFilter? firstChildren = default;
            SvgFilter? firstX = default;
            SvgFilter? firstY = default;
            SvgFilter? firstWidth = default;
            SvgFilter? firstHeight = default;
            SvgFilter? firstFilterUnits = default;
            SvgFilter? firstPrimitiveUnits = default;

            foreach (var p in svgReferencedFilters)
            {
                if (firstChildren is null && p.Children.Count > 0)
                {
                    firstChildren = p;
                }

                if (firstX is null && TryGetAttribute(p, "x", out _))
                {
                    firstX = p;
                }

                if (firstY is null && TryGetAttribute(p, "y", out _))
                {
                    firstY = p;
                }

                if (firstWidth is null && TryGetAttribute(p, "width", out _))
                {
                    firstWidth = p;
                }

                if (firstHeight is null && TryGetAttribute(p, "height", out _))
                {
                    firstHeight = p;
                }

                if (firstFilterUnits is null && TryGetAttribute(p, "filterUnits", out _))
                {
                    firstFilterUnits = p;
                }

                if (firstPrimitiveUnits is null && TryGetAttribute(p, "primitiveUnits", out _))
                {
                    firstPrimitiveUnits = p;
                }
            }

            if (firstChildren is null)
            {
                isValid = false;
                return default;
            }

            var xUnit = firstX is null ? new SvgUnit(SvgUnitType.Percentage, -10f) : firstX.X;
            var yUnit = firstY is null ? new SvgUnit(SvgUnitType.Percentage, -10f) : firstY.Y;
            var widthUnit = firstWidth is null ? new SvgUnit(SvgUnitType.Percentage, 120f) : firstWidth.Width;
            var heightUnit = firstHeight is null ? new SvgUnit(SvgUnitType.Percentage, 120f) : firstHeight.Height;
            var filterUnits = firstFilterUnits is null ? SvgCoordinateUnits.ObjectBoundingBox : firstFilterUnits.FilterUnits;
            var primitiveUnits = firstPrimitiveUnits is null ? SvgCoordinateUnits.UserSpaceOnUse : firstPrimitiveUnits.FilterUnits;

            var x = xUnit.ToDeviceValue(UnitRenderingType.HorizontalOffset, svgFirstFilter, skBounds);
            var y = yUnit.ToDeviceValue(UnitRenderingType.VerticalOffset, svgFirstFilter, skBounds);
            var width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgFirstFilter, skBounds);
            var height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgFirstFilter, skBounds);

            if (width <= 0f || height <= 0f)
            {
                isValid = false;
                return default;
            }

            if (filterUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                // TODO: FilterUnits
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

            var skFilterRegion = Rect.Create(x, y, width, height);
            
            var items = new List<(SvgFilterPrimitive primitive, Rect region)>();

            foreach (var child in firstChildren.Children)
            {
                if (child is SvgFilterPrimitive svgFilterPrimitive)
                {
                    var skPrimitiveBounds = skFilterRegion;

                    var xUnitChild = svgFilterPrimitive.X;
                    var yUnitChild = svgFilterPrimitive.Y;
                    var widthUnitChild = svgFilterPrimitive.Width;
                    var heightUnitChild = svgFilterPrimitive.Height;

                    var xChild = xUnitChild.ToDeviceValue(UnitRenderingType.HorizontalOffset, svgFilterPrimitive,
                        skPrimitiveBounds);
                    var yChild = yUnitChild.ToDeviceValue(UnitRenderingType.VerticalOffset, svgFilterPrimitive,
                        skPrimitiveBounds);
                    var widthChild = widthUnitChild.ToDeviceValue(UnitRenderingType.Horizontal, svgFilterPrimitive,
                        skPrimitiveBounds);
                    var heightChild = heightUnitChild.ToDeviceValue(UnitRenderingType.Vertical, svgFilterPrimitive,
                        skPrimitiveBounds);

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

                    var skFilterPrimitiveRegion = Rect.Create(xChild, yChild, widthChild, heightChild);

                    items.Add((svgFilterPrimitive, skFilterPrimitiveRegion));
                }
            }

            var results = new Dictionary<string, ImageFilter>();
            var regions = new Dictionary<ImageFilter, Rect>();
            var lastResult = default(ImageFilter);

            for (var i = 0; i < items.Count; i++)
            {
                var (svgFilterPrimitive, skFilterPrimitiveRegion) = items[i];
                var isFirst = i == 0;

                switch (svgFilterPrimitive)
                {
                    case SvgBlend svgBlend:
                        {
                            var input1Key = svgBlend.Input;
                            var input1Filter = GetInputFilter(input1Key, results, lastResult, filterSource, isFirst);
                            var input2Key = svgBlend.Input2;
                            var input2Filter = GetInputFilter(input2Key, results, lastResult, filterSource, false);
                            if (input2Filter is null)
                            {
                                break;
                            }
                            if (!(string.IsNullOrWhiteSpace(input1Key) && isFirst) && !IsStandardInput(input1Key) && input1Filter is { } && !IsStandardInput(input2Key))
                            {
                                skFilterPrimitiveRegion = Rect.Union(regions[input1Filter], regions[input2Filter]);
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateBlend(svgBlend, input2Filter, input1Filter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgColourMatrix svgColourMatrix:
                        {
                            var inputKey = svgColourMatrix.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateColorMatrix(svgColourMatrix, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgComponentTransfer svgComponentTransfer:
                        {
                            var inputKey = svgComponentTransfer.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateComponentTransfer(svgComponentTransfer, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgComposite svgComposite:
                        {
                            var input1Key = svgComposite.Input;
                            var input1Filter = GetInputFilter(input1Key, results, lastResult, filterSource, isFirst);
                            var input2Key = svgComposite.Input2;
                            var input2Filter = GetInputFilter(input2Key, results, lastResult, filterSource, false);
                            if (input2Filter is null)
                            {
                                break;
                            }
                            if (!(string.IsNullOrWhiteSpace(input1Key) && isFirst) && !IsStandardInput(input1Key) && input1Filter is { } && !IsStandardInput(input2Key))
                            {
                                skFilterPrimitiveRegion = Rect.Union(regions[input1Filter], regions[input2Filter]);
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateComposite(svgComposite, input2Filter, input1Filter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgConvolveMatrix svgConvolveMatrix:
                        {
                            var inputKey = svgConvolveMatrix.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateConvolveMatrix(svgConvolveMatrix, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgDiffuseLighting svgDiffuseLighting:
                        {
                            var inputKey = svgDiffuseLighting.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateDiffuseLighting(svgDiffuseLighting, skFilterPrimitiveRegion, primitiveUnits, svgVisualElement, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgDisplacementMap svgDisplacementMap:
                        {
                            var input1Key = svgDisplacementMap.Input;
                            var input1Filter = GetInputFilter(input1Key, results, lastResult, filterSource, isFirst);
                            var input2Key = svgDisplacementMap.Input2;
                            var input2Filter = GetInputFilter(input2Key, results, lastResult, filterSource, false);
                            if (input2Filter is null)
                            {
                                break;
                            }
                            if (!(string.IsNullOrWhiteSpace(input1Key) && isFirst) && !IsStandardInput(input1Key) && input1Filter is { } && !IsStandardInput(input2Key))
                            {
                                skFilterPrimitiveRegion = Rect.Union(regions[input1Filter], regions[input2Filter]);
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateDisplacementMap(svgDisplacementMap, skFilterPrimitiveRegion, primitiveUnits, input2Filter, input1Filter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgFlood svgFlood:
                        {
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateFlood(svgFlood, svgVisualElement, skFilterPrimitiveRegion, null, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgGaussianBlur svgGaussianBlur:
                        {
                            var inputKey = svgGaussianBlur.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateBlur(svgGaussianBlur, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case FilterEffects.SvgImage svgImage:
                        {
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateImage(svgImage, skFilterPrimitiveRegion, assetLoader, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgMerge svgMerge:
                        {
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateMerge(svgMerge, results, lastResult, filterSource, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgMorphology svgMorphology:
                        {
                            var inputKey = svgMorphology.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateMorphology(svgMorphology, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgOffset svgOffset:
                        {
                            var inputKey = svgOffset.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateOffset(svgOffset, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgSpecularLighting svgSpecularLighting:
                        {
                            var inputKey = svgSpecularLighting.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                skFilterPrimitiveRegion = regions[inputFilter];
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateSpecularLighting(svgSpecularLighting, skFilterPrimitiveRegion, primitiveUnits, svgVisualElement, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgTile svgTile:
                        {
                            var inputKey = svgTile.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            var tileBounds = skFilterPrimitiveRegion;
                            if (!(string.IsNullOrWhiteSpace(inputKey) && isFirst) && !IsStandardInput(inputKey) && inputFilter is { })
                            {
                                tileBounds = regions[inputFilter];
                            }
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateTile(svgTile, tileBounds, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;

                    case SvgTurbulence svgTurbulence:
                        {
                            // if (!skFilterRegion.Contains(skFilterPrimitiveRegion))
                            // {
                            //     break;
                            // }
                            var skCropRect = new CropRect(skFilterPrimitiveRegion);
                            var skImageFilter = CreateTurbulence(svgTurbulence, skFilterPrimitiveRegion, primitiveUnits, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
                            if (skImageFilter is { })
                            {
                                regions[skImageFilter] = skFilterPrimitiveRegion;
                            }
                        }
                        break;
                }
            }

            if (lastResult is { })
            {
                var skPaint = new Paint
                {
                    Style = PaintStyle.StrokeAndFill
                };
                skPaint.ImageFilter = lastResult;

                isValid = true;
                return skPaint;
            }

            isValid = false;
            return default;
        }
    }
}
