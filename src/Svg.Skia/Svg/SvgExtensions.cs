using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
#if !USE_PICTURE
using SkiaSharp;
using CropRect = SkiaSharp.SKImageFilter.CropRect;
#endif
using System.Text;
using System.Text.RegularExpressions;
using Svg.DataTypes;
using Svg.Document_Structure;
using Svg.FilterEffects;
using Svg.Pathing;
using Svg.Transforms;
#if USE_PICTURE
using SKBlendMode = Svg.Picture.BlendMode;
using SKCanvas = Svg.Picture.Canvas;
using SKClipOperation = Svg.Picture.ClipOperation;
using SKColor = Svg.Picture.Color;
using SKColorFilter = Svg.Picture.ColorFilter;
using SKDisplacementMapEffectChannelSelectorType = Svg.Picture.DisplacementMapEffectChannelSelectorType;
using SKDrawable = Svg.Picture.Drawable;
using SKFilterQuality = Svg.Picture.FilterQuality;
using SKFontStyleSlant = Svg.Picture.FontStyleSlant;
using SKFontStyleWeight = Svg.Picture.FontStyleWeight;
using SKFontStyleWidth = Svg.Picture.FontStyleWidth;
using SKImage = Svg.Picture.Image;
using SKImageFilter = Svg.Picture.ImageFilter;
using CropRect = Svg.Picture.CropRect;
using SKMatrix = Svg.Picture.Matrix;
using SKMatrixConvolutionTileMode = Svg.Picture.MatrixConvolutionTileMode;
using SKPaint = Svg.Picture.Paint;
using SKPaintStyle = Svg.Picture.PaintStyle;
using SKPath = Svg.Picture.Path;
using SKPathArcSize = Svg.Picture.PathArcSize;
using SKPathDirection = Svg.Picture.PathDirection;
using SKPathEffect = Svg.Picture.PathEffect;
using SKPathFillType = Svg.Picture.PathFillType;
using SKPicture = Svg.Picture.Picture;
using SKPictureRecorder = Svg.Picture.PictureRecorder;
using SKPoint = Svg.Picture.Point;
using SKPoint3 = Svg.Picture.Point3;
using SKPointI = Svg.Picture.PointI;
using SKRect = Svg.Picture.Rect;
using SKShader = Svg.Picture.Shader;
using SKShaderTileMode = Svg.Picture.ShaderTileMode;
using SKSize = Svg.Picture.Size;
using SKSizeI = Svg.Picture.SizeI;
using SKStrokeCap = Svg.Picture.StrokeCap;
using SKStrokeJoin = Svg.Picture.StrokeJoin;
using SKTextAlign = Svg.Picture.TextAlign;
using SKTextEncoding = Svg.Picture.TextEncoding;
#endif

namespace Svg.Skia
{
    internal static class SvgExtensions
    {
        private static readonly char[] s_space_tab = new char[2] { ' ', '\t' };

        private static readonly char[] s_comma = new char[] { ',' };

        public static HashSet<string> s_supportedExtensions = new HashSet<string>()
        {
        };

        // Precomputed sRGB to LinearRGB table.
        // if (C_srgb <= 0.04045)
        //     C_lin = C_srgb / 12.92;
        //  else
        //     C_lin = pow((C_srgb + 0.055) / 1.055, 2.4);
        public static readonly byte[] s_SRGBtoLinearRGB = new byte[256]
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
        public static readonly byte[] s_LinearRGBtoSRGB = new byte[256]
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

        public static readonly SKColor s_transparentBlack = new SKColor(0, 0, 0, 255);

        private const string MimeTypeSvg = "image/svg+xml";

        private static readonly byte[] s_gZipMagicHeaderBytes = { 0x1f, 0x8b };

        public const string SourceGraphic = "SourceGraphic";

        public const string SourceAlpha = "SourceAlpha";

        public const string BackgroundImage = "BackgroundImage";

        public const string BackgroundAlpha = "BackgroundAlpha";

        public const string FillPaint = "FillPaint";

        public const string StrokePaint = "StrokePaint";

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

        public static double DegreeToRadian(this double degrees)
        {
            return Math.PI * degrees / 180.0;
        }

        public static double RadianToDegree(this double radians)
        {
            return radians * (180.0 / Math.PI);
        }

        public static bool IsNone(this Uri uri)
        {
            return string.Equals(uri.ToString(), "none", StringComparison.OrdinalIgnoreCase);
        }

        public static void GetOptionalNumbers(this SvgNumberCollection svgNumberCollection, float defaultValue1, float defaultValue2, out float value1, out float value2)
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

        public static float CalculateOtherPercentageValue(this SKRect skBounds)
        {
            return (float)(Math.Sqrt((skBounds.Width * skBounds.Width) + (skBounds.Width * skBounds.Height)) / Math.Sqrt(2.0));
        }

        public static float ToDeviceValue(this SvgUnit svgUnit, UnitRenderingType renderType, SvgElement? owner, SKRect skBounds)
        {
            const float cmInInch = 2.54f;
            int ppi = SvgDocument.PointsPerInch;
            var type = svgUnit.Type;
            var value = svgUnit.Value;
            float? _deviceValue;
            float points;

            switch (type)
            {
                case SvgUnitType.Em:
                    points = value * 9;
                    _deviceValue = (points / 72.0f) * ppi;
                    break;
                case SvgUnitType.Ex:
                    points = value * 9;
                    _deviceValue = (points * 0.5f / 72.0f) * ppi;
                    break;
                case SvgUnitType.Centimeter:
                    _deviceValue = (value / cmInInch) * ppi;
                    break;
                case SvgUnitType.Inch:
                    _deviceValue = value * ppi;
                    break;
                case SvgUnitType.Millimeter:
                    _deviceValue = (value / 10) / cmInInch * ppi;
                    break;
                case SvgUnitType.Pica:
                    _deviceValue = ((value * 12) / 72) * ppi;
                    break;
                case SvgUnitType.Point:
                    _deviceValue = (value / 72) * ppi;
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
                            _deviceValue = (size.Width / 100) * value;
                            break;
                        case UnitRenderingType.HorizontalOffset:
                            _deviceValue = (size.Width / 100) * value + skBounds.Location.X;
                            break;
                        case UnitRenderingType.Vertical:
                            _deviceValue = (size.Height / 100) * value;
                            break;
                        case UnitRenderingType.VerticalOffset:
                            _deviceValue = (size.Height / 100) * value + skBounds.Location.Y;
                            break;
                        default:
                        case UnitRenderingType.Other:
                            if (owner?.OwnerDocument != null && owner.OwnerDocument.ViewBox != null && owner.OwnerDocument.ViewBox.Width != 0 && owner.OwnerDocument.ViewBox.Height != 0)
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

        public static SvgUnit Normalize(this SvgUnit svgUnit, SvgCoordinateUnits svgCoordinateUnits)
        {
            return svgUnit.Type == SvgUnitType.Percentage
                && svgCoordinateUnits == SvgCoordinateUnits.ObjectBoundingBox ?
                    new SvgUnit(SvgUnitType.User, svgUnit.Value / 100) : svgUnit;
        }

        public static SKSize GetDimensions(SvgFragment svgFragment)
        {
            float w, h;
            var isWidthperc = svgFragment.Width.Type == SvgUnitType.Percentage;
            var isHeightperc = svgFragment.Height.Type == SvgUnitType.Percentage;

            var bounds = new SKRect();
            if (isWidthperc || isHeightperc)
            {
                if (svgFragment.ViewBox.Width > 0 && svgFragment.ViewBox.Height > 0)
                {
                    bounds = new SKRect(
                        svgFragment.ViewBox.MinX, svgFragment.ViewBox.MinY,
                        svgFragment.ViewBox.Width, svgFragment.ViewBox.Height);
                }
                else
                {
                    // TODO: Calculate correct bounds using Children bounds.
                }
            }

            if (isWidthperc)
            {
                w = (bounds.Width + bounds.Left) * (svgFragment.Width.Value * 0.01f);
            }
            else
            {
                // NOTE: Pass bounds as SKRect.Empty because percentage case is handled before.
                w = svgFragment.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, SKRect.Empty);
            }
            if (isHeightperc)
            {
                h = (bounds.Height + bounds.Top) * (svgFragment.Height.Value * 0.01f);
            }
            else
            {
                // NOTE: Pass bounds as SKRect.Empty because percentage case is handled before.
                h = svgFragment.Height.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, SKRect.Empty);
            }

            return new SKSize(w, h);
        }

        public static T? GetReference<T>(this SvgElement svgElement, Uri uri) where T : SvgElement
        {
            if (uri is null)
            {
                return null;
            }

            var svgElementById = svgElement.OwnerDocument?.GetElementById(uri.ToString());
            if (svgElementById != null)
            {
                return svgElementById as T;
            }

            return null;
        }

        public static bool ElementReferencesUri<T>(this T svgElement, Func<T, Uri?> getUri, HashSet<Uri> uris, SvgElement? svgReferencedElement) where T : SvgElement
        {
            if (svgReferencedElement is null)
            {
                return false;
            }

            if (svgReferencedElement is T svgElementT)
            {
                var referencedElementUri = getUri(svgElementT);

                if (referencedElementUri is null)
                {
                    return false;
                }

                if (uris.Contains(referencedElementUri))
                {
                    return true;
                }

                if (GetReference<T>(svgElement, referencedElementUri) != null)
                {
                    uris.Add(referencedElementUri);
                }

                return ElementReferencesUri(
                    svgElementT,
                    getUri,
                    uris,
                    GetReference<SvgElement>(svgElementT, referencedElementUri));
            }

            foreach (var svgChildElement in svgReferencedElement.Children)
            {
                if (ElementReferencesUri(svgElement, getUri, uris, svgChildElement))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasRecursiveReference<T>(this T svgElement, Func<T, Uri?> getUri, HashSet<Uri> uris) where T : SvgElement
        {
            var referencedElementUri = getUri(svgElement);
            if (referencedElementUri is null)
            {
                return false;
            }
            var svgReferencedElement = GetReference<SvgElement>(svgElement, referencedElementUri);
            if (uris.Contains(referencedElementUri))
            {
                return true;
            }
            uris.Add(referencedElementUri);
            return ElementReferencesUri<T>(svgElement, getUri, uris, svgReferencedElement);
        }

        public static Uri? GetUri(this SvgElement svgElement, string name)
        {
            if (svgElement.TryGetAttribute(name, out string uriString))
            {
                return new Uri(uriString, UriKind.RelativeOrAbsolute);
            }
            return null;
        }

        public static bool TryGetAttribute(this SvgElement svgElement, string name, out string value)
        {
            return svgElement.TryGetAttribute(name, out value);
        }

        public static T? GetUriElementReference<T>(this SvgElement svgOwnerElement, string name, HashSet<Uri> uris) where T : SvgElement
        {
            var uri = svgOwnerElement.GetUri(name);
            if (uri != null)
            {
                if (HasRecursiveReference(svgOwnerElement, (e) => e.GetUri(name), uris))
                {
                    return null;
                }

                var svgElement = GetReference<T>(svgOwnerElement, uri);
                if (svgElement is null)
                {
                    return null;
                }
                return svgElement;
            }
            return null;
        }

        public static bool HasRequiredFeatures(this SvgElement svgElement)
        {
            bool hasRequiredFeatures = true;

            if (TryGetAttribute(svgElement, "requiredFeatures", out var requiredFeaturesString))
            {
                if (string.IsNullOrEmpty(requiredFeaturesString))
                {
                    hasRequiredFeatures = false;
                }
                else
                {
                    var features = requiredFeaturesString.Trim().Split(s_space_tab, StringSplitOptions.RemoveEmptyEntries);
                    if (features.Length > 0)
                    {
                        foreach (var feature in features)
                        {
                            if (!SvgFeatures.Contains(feature))
                            {
                                hasRequiredFeatures = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        hasRequiredFeatures = false;
                    }
                }
            }

            return hasRequiredFeatures;
        }

        public static bool HasRequiredExtensions(this SvgElement svgElement)
        {
            bool hasRequiredExtensions = true;

            if (TryGetAttribute(svgElement, "requiredExtensions", out var requiredExtensionsString))
            {
                if (string.IsNullOrEmpty(requiredExtensionsString))
                {
                    hasRequiredExtensions = false;
                }
                else
                {
                    var extensions = requiredExtensionsString.Trim().Split(s_space_tab, StringSplitOptions.RemoveEmptyEntries);
                    if (extensions.Length > 0)
                    {
                        foreach (var extension in extensions)
                        {
                            if (!s_supportedExtensions.Contains(extension))
                            {
                                hasRequiredExtensions = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        hasRequiredExtensions = false;
                    }
                }
            }

            return hasRequiredExtensions;
        }

        public static bool HasSystemLanguage(this SvgElement svgElement)
        {
            bool hasSystemLanguage = true;

            if (TryGetAttribute(svgElement, "systemLanguage", out var systemLanguageString))
            {
                if (string.IsNullOrEmpty(systemLanguageString))
                {
                    hasSystemLanguage = false;
                }
                else
                {
                    var languages = systemLanguageString.Trim().Split(s_comma, StringSplitOptions.RemoveEmptyEntries);
                    if (languages.Length > 0)
                    {
                        hasSystemLanguage = false;
                        var systemLanguage = SKSvgSettings.s_systemLanguageOverride ?? CultureInfo.InstalledUICulture;

                        foreach (var language in languages)
                        {
                            try
                            {
                                var languageCultureInfo = CultureInfo.CreateSpecificCulture(language.Trim());
                                if (systemLanguage.Equals(languageCultureInfo) || systemLanguage.TwoLetterISOLanguageName == languageCultureInfo.TwoLetterISOLanguageName)
                                {
                                    hasSystemLanguage = true;
                                }
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        hasSystemLanguage = false;
                    }
                }
            }

            return hasSystemLanguage;
        }

        public static bool IsContainerElement(this SvgElement svgElement)
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

        public static bool IsKnownElement(this SvgElement svgElement)
        {
            switch (svgElement)
            {
                case SvgAnchor _:
                case SvgCircle _:
                case SvgEllipse _:
                case SvgFragment _:
                case SvgGroup _:
                case SvgImage _:
                case SvgLine _:
                case SvgPath _:
                case SvgPolyline _:
                case SvgPolygon _:
                case SvgRectangle _:
                case SvgSwitch _:
                case SvgText _:
                case SvgUse _:
                    return true;
                default:
                    return false;
            }
        }

        public static float AdjustSvgOpacity(float opacity)
        {
            return Math.Min(Math.Max(opacity, 0), 1);
        }

        public static byte CombineWithOpacity(byte alpha, float opacity)
        {
            return (byte)Math.Round((opacity * (alpha / 255.0)) * 255);
        }

        public static SKColor GetColor(SvgColourServer svgColourServer, float opacity, Attributes ignoreAttributes)
        {
            var colour = svgColourServer.Colour;
            byte alpha = ignoreAttributes.HasFlag(Attributes.Opacity) ?
                svgColourServer.Colour.A :
                CombineWithOpacity(svgColourServer.Colour.A, opacity);

            return new SKColor(colour.R, colour.G, colour.B, alpha);
        }

        public static SKColor? GetColor(SvgVisualElement svgVisualElement, SvgPaintServer server)
        {
            if (server is SvgDeferredPaintServer svgDeferredPaintServer)
            {
                server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgVisualElement);
            }

            if (server is SvgColourServer stopColorSvgColourServer)
            {
                return GetColor(stopColorSvgColourServer, 1f, Attributes.None);
            }

            return new SKColor(0x00, 0x00, 0x00, 0xFF);
        }

        public static SKPathEffect? CreateDash(SvgElement svgElement, SKRect skBounds)
        {
            var strokeDashArray = svgElement.StrokeDashArray;
            var strokeDashOffset = svgElement.StrokeDashOffset;
            var count = strokeDashArray.Count;

            if (strokeDashArray != null && count > 0)
            {
                bool isOdd = count % 2 != 0;
                float sum = 0f;
                float[] intervals = new float[isOdd ? count * 2 : count];
                for (int i = 0; i < count; i++)
                {
                    var dash = strokeDashArray[i].ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds);
                    if (dash < 0f)
                    {
                        return null;
                    }

                    intervals[i] = dash;

                    if (isOdd)
                    {
                        intervals[i + count] = intervals[i];
                    }

                    sum += dash;
                }

                if (sum <= 0f)
                {
                    return null;
                }

                float phase = strokeDashOffset != null ? strokeDashOffset.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds) : 0f;

                return SKPathEffect.CreateDash(intervals, phase);
            }

            return null;
        }

        private static List<SvgPatternServer> GetLinkedPatternServer(SvgPatternServer svgPatternServer, SvgVisualElement svgVisualElement)
        {
            var svgPatternServers = new List<SvgPatternServer>();
            var currentPatternServer = svgPatternServer;
            do
            {
                svgPatternServers.Add(currentPatternServer);
                currentPatternServer = SvgDeferredPaintServer.TryGet<SvgPatternServer>(currentPatternServer.InheritGradient, svgVisualElement);
            } while (currentPatternServer != null);
            return svgPatternServers;
        }

        private static List<SvgGradientServer> GetLinkedGradientServer(SvgGradientServer svgGradientServer, SvgVisualElement svgVisualElement)
        {
            var svgGradientServers = new List<SvgGradientServer>();
            var currentGradientServer = svgGradientServer;
            do
            {
                svgGradientServers.Add(currentGradientServer);
                currentGradientServer = SvgDeferredPaintServer.TryGet<SvgGradientServer>(currentGradientServer.InheritGradient, svgVisualElement);
            } while (currentGradientServer != null);
            return svgGradientServers;
        }

        private static void GetStopsImpl(SvgGradientServer svgGradientServer, SKRect skBounds, List<SKColor> colors, List<float> colorPos, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
        {
            foreach (var child in svgGradientServer.Children)
            {
                if (child is SvgGradientStop svgGradientStop)
                {
                    var server = svgGradientStop.StopColor;
                    if (server is SvgDeferredPaintServer svgDeferredPaintServer)
                    {
                        server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgVisualElement);
                        if (server is null)
                        {
                            // TODO: server is sometimes null with currentColor
                        }
                    }

                    if (server is SvgColourServer stopColorSvgColourServer)
                    {
                        var stopOpacity = AdjustSvgOpacity(svgGradientStop.StopOpacity);
                        var stopColor = GetColor(stopColorSvgColourServer, opacity * stopOpacity, ignoreAttributes);
                        float offset = svgGradientStop.Offset.ToDeviceValue(UnitRenderingType.Horizontal, svgGradientServer, skBounds);
                        offset /= skBounds.Width;
                        colors.Add(stopColor);
                        colorPos.Add(offset);
                    }
                }
            }
        }

        public static void GetStops(List<SvgGradientServer> svgReferencedGradientServers, SKRect skBounds, List<SKColor> colors, List<float> colorPos, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
        {
            foreach (var svgReferencedGradientServer in svgReferencedGradientServers)
            {
                if (colors.Count == 0)
                {
                    GetStopsImpl(svgReferencedGradientServer, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes);
                    if (colors.Count > 0)
                    {
                        return;
                    }
                }
            }
        }

        private static void AdjustStopColorPos(List<float> colorPos)
        {
            float maxPos = float.MinValue;
            for (int i = 0; i < colorPos.Count; i++)
            {
                float pos = colorPos[i];
                if (pos > maxPos)
                {
                    maxPos = pos;
                }
                else if (pos < maxPos)
                {
                    colorPos[i] = maxPos;
                }
            }
        }
#if USE_COLORSPACE
        public static SKColorF[] ToSkColorF(this SKColor[] skColors)
        {
            var skColorsF = new SKColorF[skColors.Length];

            for (int i = 0; i < skColors.Length; i++)
            {
                skColorsF[i] = skColors[i];
            }

            return skColorsF;
        }
#endif
        public static SvgColourInterpolation GetColorInterpolation(SvgElement svgElement)
        {
            return svgElement.ColorInterpolation switch
            {
                SvgColourInterpolation.Auto => SvgColourInterpolation.SRGB,
                SvgColourInterpolation.SRGB => SvgColourInterpolation.SRGB,
                SvgColourInterpolation.LinearRGB => SvgColourInterpolation.LinearRGB,
                _ => SvgColourInterpolation.SRGB,
            };
        }

        public static SvgColourInterpolation GetColorInterpolationFilters(SvgElement svgElement)
        {
            return svgElement.ColorInterpolationFilters switch
            {
                SvgColourInterpolation.Auto => SvgColourInterpolation.LinearRGB,
                SvgColourInterpolation.SRGB => SvgColourInterpolation.SRGB,
                SvgColourInterpolation.LinearRGB => SvgColourInterpolation.LinearRGB,
                _ => SvgColourInterpolation.LinearRGB,
            };
        }

#if USE_COLORSPACE
        public static SKShader CreateLinearGradient(SvgLinearGradientServer svgLinearGradientServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes, SKColorSpace skColorSpace)
#else
        public static SKShader CreateLinearGradient(SvgLinearGradientServer svgLinearGradientServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
#endif
        {
            var svgReferencedGradientServers = GetLinkedGradientServer(svgLinearGradientServer, svgVisualElement);

            SvgGradientServer? firstSpreadMethod = null;
            SvgGradientServer? firstGradientTransform = null;
            SvgGradientServer? firstGradientUnits = null;
            SvgLinearGradientServer? firstX1 = null;
            SvgLinearGradientServer? firstY1 = null;
            SvgLinearGradientServer? firstX2 = null;
            SvgLinearGradientServer? firstY2 = null;

            foreach (var p in svgReferencedGradientServers)
            {
                if (firstSpreadMethod is null)
                {
                    var pSpreadMethod = p.SpreadMethod;
                    if (pSpreadMethod != SvgGradientSpreadMethod.Pad)
                    {
                        firstSpreadMethod = p;
                    }
                }
                if (firstGradientTransform is null)
                {
                    var pGradientTransform = p.GradientTransform;
                    if (pGradientTransform != null && pGradientTransform.Count > 0)
                    {
                        firstGradientTransform = p;
                    }
                }
                if (firstGradientUnits is null)
                {
                    var pGradientUnits = p.GradientUnits;
                    if (pGradientUnits != SvgCoordinateUnits.ObjectBoundingBox)
                    {
                        firstGradientUnits = p;
                    }
                }

                if (p is SvgLinearGradientServer svgLinearGradientServerHref)
                {
                    if (firstX1 is null)
                    {
                        var pX1 = svgLinearGradientServerHref.X1;
                        if (pX1 != null && pX1 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "x1", out _))
                        {
                            firstX1 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstY1 is null)
                    {
                        var pY1 = svgLinearGradientServerHref.Y1;
                        if (pY1 != null && pY1 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "y1", out _))
                        {
                            firstY1 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstX2 is null)
                    {
                        var pX2 = svgLinearGradientServerHref.X2;
                        if (pX2 != null && pX2 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "x2", out _))
                        {
                            firstX2 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstY2 is null)
                    {
                        var pY2 = svgLinearGradientServerHref.Y2;
                        if (pY2 != null && pY2 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "y2", out _))
                        {
                            firstY2 = svgLinearGradientServerHref;
                        }
                    }
                }
            }

            var svgSpreadMethod = firstSpreadMethod is null ? SvgGradientSpreadMethod.Pad : firstSpreadMethod.SpreadMethod;
            var svgGradientTransform = firstGradientTransform?.GradientTransform;
            var svgGradientUnits = firstGradientUnits is null ? SvgCoordinateUnits.ObjectBoundingBox : firstGradientUnits.GradientUnits;
            var x1Unit = firstX1 is null ? new SvgUnit(SvgUnitType.Percentage, 0f) : firstX1.X1;
            var y1Unit = firstY1 is null ? new SvgUnit(SvgUnitType.Percentage, 0f) : firstY1.Y1;
            var x2Unit = firstX2 is null ? new SvgUnit(SvgUnitType.Percentage, 100f) : firstX2.X2;
            var y2Unit = firstY2 is null ? new SvgUnit(SvgUnitType.Percentage, 0f) : firstY2.Y2;

            var normalizedX1 = x1Unit.Normalize(svgGradientUnits);
            var normalizedY1 = y1Unit.Normalize(svgGradientUnits);
            var normalizedX2 = x2Unit.Normalize(svgGradientUnits);
            var normalizedY2 = y2Unit.Normalize(svgGradientUnits);

            float x1 = normalizedX1.ToDeviceValue(UnitRenderingType.Horizontal, svgLinearGradientServer, skBounds);
            float y1 = normalizedY1.ToDeviceValue(UnitRenderingType.Vertical, svgLinearGradientServer, skBounds);
            float x2 = normalizedX2.ToDeviceValue(UnitRenderingType.Horizontal, svgLinearGradientServer, skBounds);
            float y2 = normalizedY2.ToDeviceValue(UnitRenderingType.Vertical, svgLinearGradientServer, skBounds);

            var skStart = new SKPoint(x1, y1);
            var skEnd = new SKPoint(x2, y2);
            var colors = new List<SKColor>();
            var colorPos = new List<float>();

            GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes);
            AdjustStopColorPos(colorPos);

            var shaderTileMode = svgSpreadMethod switch
            {
                SvgGradientSpreadMethod.Reflect => SKShaderTileMode.Mirror,
                SvgGradientSpreadMethod.Repeat => SKShaderTileMode.Repeat,
                _ => SKShaderTileMode.Clamp,
            };
            var skColors = colors.ToArray();
            float[] skColorPos = colorPos.ToArray();

            if (skColors.Length == 0)
            {
#if USE_COLORSPACE
                return SKShader.CreateColor(new SKColor(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
#else
                return SKShader.CreateColor(new SKColor(0xFF, 0xFF, 0xFF, 0x00));
#endif
            }
            else if (skColors.Length == 1)
            {
#if USE_COLORSPACE
                return SKShader.CreateColor(skColors[0], skColorSpace);
#else
                return SKShader.CreateColor(skColors[0]);
#endif
            }

            if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundingBoxTransform = new SKMatrix()
                {
                    ScaleX = skBounds.Width,
                    SkewY = 0f,
                    SkewX = 0f,
                    ScaleY = skBounds.Height,
                    TransX = skBounds.Left,
                    TransY = skBounds.Top,
                    Persp0 = 0,
                    Persp1 = 0,
                    Persp2 = 1
                };

                if (svgGradientTransform != null && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = ToSKMatrix(svgGradientTransform);
                    skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(gradientTransform);
                }

#if USE_COLORSPACE
                var skColorsF = ToSkColorF(skColors);
                return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, skBoundingBoxTransform);
#else
                return SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode, skBoundingBoxTransform);
#endif
            }
            else
            {
                if (svgGradientTransform != null && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = ToSKMatrix(svgGradientTransform);
#if USE_COLORSPACE
                    var skColorsF = ToSkColorF(skColors);
                    return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, gradientTransform);
#else
                    return SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode, gradientTransform);
#endif
                }
                else
                {
#if USE_COLORSPACE
                    var skColorsF = ToSkColorF(skColors);
                    return SKShader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode);
#else
                    return SKShader.CreateLinearGradient(skStart, skEnd, skColors, skColorPos, shaderTileMode);
#endif
                }
            }
        }

#if USE_COLORSPACE
        public static SKShader CreateTwoPointConicalGradient(SvgRadialGradientServer svgRadialGradientServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes, SKColorSpace skColorSpace)
#else
        public static SKShader CreateTwoPointConicalGradient(SvgRadialGradientServer svgRadialGradientServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
#endif
        {
            var svgReferencedGradientServers = GetLinkedGradientServer(svgRadialGradientServer, svgVisualElement);

            SvgGradientServer? firstSpreadMethod = null;
            SvgGradientServer? firstGradientTransform = null;
            SvgGradientServer? firstGradientUnits = null;
            SvgRadialGradientServer? firstCenterX = null;
            SvgRadialGradientServer? firstCenterY = null;
            SvgRadialGradientServer? firstRadius = null;
            SvgRadialGradientServer? firstFocalX = null;
            SvgRadialGradientServer? firstFocalY = null;

            foreach (var p in svgReferencedGradientServers)
            {
                if (firstSpreadMethod is null)
                {
                    var pSpreadMethod = p.SpreadMethod;
                    if (pSpreadMethod != SvgGradientSpreadMethod.Pad)
                    {
                        firstSpreadMethod = p;
                    }
                }
                if (firstGradientTransform is null)
                {
                    var pGradientTransform = p.GradientTransform;
                    if (pGradientTransform != null && pGradientTransform.Count > 0)
                    {
                        firstGradientTransform = p;
                    }
                }
                if (firstGradientUnits is null)
                {
                    var pGradientUnits = p.GradientUnits;
                    if (pGradientUnits != SvgCoordinateUnits.ObjectBoundingBox)
                    {
                        firstGradientUnits = p;
                    }
                }

                if (p is SvgRadialGradientServer svgRadialGradientServerHref)
                {
                    if (firstCenterX is null)
                    {
                        var pCenterX = svgRadialGradientServerHref.CenterX;
                        if (pCenterX != null && pCenterX != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "cx", out _))
                        {
                            firstCenterX = svgRadialGradientServerHref;
                        }
                    }
                    if (firstCenterY is null)
                    {
                        var pCenterY = svgRadialGradientServerHref.CenterY;
                        if (pCenterY != null && pCenterY != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "cy", out _))
                        {
                            firstCenterY = svgRadialGradientServerHref;
                        }
                    }
                    if (firstRadius is null)
                    {
                        var pRadius = svgRadialGradientServerHref.Radius;
                        if (pRadius != null && pRadius != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "r", out _))
                        {
                            firstRadius = svgRadialGradientServerHref;
                        }
                    }
                    if (firstFocalX is null)
                    {
                        var pFocalX = svgRadialGradientServerHref.FocalX;
                        if (pFocalX != null && pFocalX != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "fx", out _))
                        {
                            firstFocalX = svgRadialGradientServerHref;
                        }
                    }
                    if (firstFocalY is null)
                    {
                        var pFocalY = svgRadialGradientServerHref.FocalY;
                        if (pFocalY != null && pFocalY != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "fy", out _))
                        {
                            firstFocalY = svgRadialGradientServerHref;
                        }
                    }
                }
            }

            var svgSpreadMethod = firstSpreadMethod is null ? SvgGradientSpreadMethod.Pad : firstSpreadMethod.SpreadMethod;
            var svgGradientTransform = firstGradientTransform?.GradientTransform;
            var svgGradientUnits = firstGradientUnits is null ? SvgCoordinateUnits.ObjectBoundingBox : firstGradientUnits.GradientUnits;
            var centerXUnit = firstCenterX is null ? new SvgUnit(SvgUnitType.Percentage, 50f) : firstCenterX.CenterX;
            var centerYUnit = firstCenterY is null ? new SvgUnit(SvgUnitType.Percentage, 50f) : firstCenterY.CenterY;
            var radiusUnit = firstRadius is null ? new SvgUnit(SvgUnitType.Percentage, 50f) : firstRadius.Radius;
            var focalXUnit = firstFocalX is null ? centerXUnit : firstFocalX.FocalX;
            var focalYUnit = firstFocalY is null ? centerYUnit : firstFocalY.FocalY;

            var normalizedCenterX = centerXUnit.Normalize(svgGradientUnits);
            var normalizedCenterY = centerYUnit.Normalize(svgGradientUnits);
            var normalizedRadius = radiusUnit.Normalize(svgGradientUnits);
            var normalizedFocalX = focalXUnit.Normalize(svgGradientUnits);
            var normalizedFocalY = focalYUnit.Normalize(svgGradientUnits);

            float centerX = normalizedCenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
            float centerY = normalizedCenterY.ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);

            float startRadius = 0f;
            float endRadius = normalizedRadius.ToDeviceValue(UnitRenderingType.Other, svgRadialGradientServer, skBounds);

            float focalX = normalizedFocalX.ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
            float focalY = normalizedFocalY.ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);

            var skStart = new SKPoint(centerX, centerY);
            var skEnd = new SKPoint(focalX, focalY);

            var colors = new List<SKColor>();
            var colorPos = new List<float>();

            GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes);
            AdjustStopColorPos(colorPos);

            var shaderTileMode = svgSpreadMethod switch
            {
                SvgGradientSpreadMethod.Reflect => SKShaderTileMode.Mirror,
                SvgGradientSpreadMethod.Repeat => SKShaderTileMode.Repeat,
                _ => SKShaderTileMode.Clamp,
            };
            var skColors = colors.ToArray();
            float[] skColorPos = colorPos.ToArray();

            if (skColors.Length == 0)
            {
#if USE_COLORSPACE
                return SKShader.CreateColor(new SKColor(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
#else
                return SKShader.CreateColor(new SKColor(0xFF, 0xFF, 0xFF, 0x00));
#endif
            }
            else if (skColors.Length == 1)
            {
#if USE_COLORSPACE
                return SKShader.CreateColor(skColors[0], skColorSpace);
#else
                return SKShader.CreateColor(skColors[0]);
#endif
            }

            if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundingBoxTransform = new SKMatrix()
                {
                    ScaleX = skBounds.Width,
                    SkewY = 0f,
                    SkewX = 0f,
                    ScaleY = skBounds.Height,
                    TransX = skBounds.Left,
                    TransY = skBounds.Top,
                    Persp0 = 0,
                    Persp1 = 0,
                    Persp2 = 1
                };

                if (svgGradientTransform != null && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = ToSKMatrix(svgGradientTransform);
                    skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(gradientTransform);
                }

#if USE_COLORSPACE
                var skColorsF = ToSkColorF(skColors);
                return SKShader.CreateTwoPointConicalGradient(
                    skStart, startRadius,
                    skEnd, endRadius,
                    skColorsF, skColorSpace, skColorPos,
                    shaderTileMode,
                    skBoundingBoxTransform);
#else
                return SKShader.CreateTwoPointConicalGradient(
                    skStart, startRadius,
                    skEnd, endRadius,
                    skColors, skColorPos,
                    shaderTileMode,
                    skBoundingBoxTransform);
#endif
            }
            else
            {
                if (svgGradientTransform != null && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = ToSKMatrix(svgGradientTransform);
#if USE_COLORSPACE
                    var skColorsF = ToSkColorF(skColors);
                    return SKShader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode, gradientTransform);
#else
                    return SKShader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColors, skColorPos,
                        shaderTileMode, gradientTransform);
#endif
                }
                else
                {
#if USE_COLORSPACE
                    var skColorsF = ToSkColorF(skColors);
                    return SKShader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode);
#else
                    return SKShader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColors, skColorPos,
                        shaderTileMode);
#endif
                }
            }
        }

        public static SKPicture RecordPicture(SvgElementCollection svgElementCollection, float width, float height, SKMatrix skMatrix, float opacity, Attributes ignoreAttributes)
        {
            var skSize = new SKSize(width, height);
            var skBounds = SKRect.Create(skSize);
            using var skPictureRecorder = new SKPictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(skBounds);

            skCanvas.SetMatrix(skMatrix);

            using var skPaintOpacity = ignoreAttributes.HasFlag(Attributes.Opacity) ? null : GetOpacitySKPaint(opacity);
            if (skPaintOpacity != null)
            {
                skCanvas.SaveLayer(skPaintOpacity);
            }

            foreach (var svgElement in svgElementCollection)
            {
                using var drawable = DrawableFactory.Create(svgElement, skBounds, null, ignoreAttributes);
                if (drawable != null)
                {
                    drawable.PostProcess();
                    drawable.Draw(skCanvas, ignoreAttributes, null);
                }
            }

            if (skPaintOpacity != null)
            {
                skCanvas.Restore();
            }

            skCanvas.Restore();

            return skPictureRecorder.EndRecording();
        }

        public static SKShader? CreatePicture(SvgPatternServer svgPatternServer, SKRect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes, CompositeDisposable disposable)
        {
            var svgReferencedPatternServers = GetLinkedPatternServer(svgPatternServer, svgVisualElement);

            SvgPatternServer? firstChildren = null;
            SvgPatternServer? firstX = null;
            SvgPatternServer? firstY = null;
            SvgPatternServer? firstWidth = null;
            SvgPatternServer? firstHeight = null;
            SvgPatternServer? firstPatternUnit = null;
            SvgPatternServer? firstPatternContentUnit = null;
            SvgPatternServer? firstViewBox = null;
            SvgPatternServer? firstAspectRatio = null;

            foreach (var p in svgReferencedPatternServers)
            {
                if (firstChildren is null && p.Children.Count > 0)
                {
                    firstChildren = p;
                }

                if (firstX is null)
                {
                    var pX = p.X;
                    if (pX != null && pX != SvgUnit.None)
                    {
                        firstX = p;
                    }
                }
                if (firstY is null)
                {
                    var pY = p.Y;
                    if (pY != null && pY != SvgUnit.None)
                    {
                        firstY = p;
                    }
                }
                if (firstWidth is null)
                {
                    var pWidth = p.Width;
                    if (pWidth != null && pWidth != SvgUnit.None)
                    {
                        firstWidth = p;
                    }
                }
                if (firstHeight is null)
                {
                    var pHeight = p.Height;
                    if (pHeight != null && pHeight != SvgUnit.None)
                    {
                        firstHeight = p;
                    }
                }
                if (firstPatternUnit is null)
                {
                    if (TryGetAttribute(p, "patternUnits", out _))
                    {
                        firstPatternUnit = p;
                    }
                }
                if (firstPatternContentUnit == null)
                {
                    if (TryGetAttribute(p, "patternContentUnits", out _))
                    {
                        firstPatternContentUnit = p;
                    }
                }
                if (firstViewBox is null)
                {
                    var pViewBox = p.ViewBox;
                    if (pViewBox != null && pViewBox != SvgViewBox.Empty)
                    {
                        firstViewBox = p;
                    }
                }
                if (firstAspectRatio is null)
                {
                    var pAspectRatio = p.AspectRatio;
                    if (pAspectRatio != null && pAspectRatio.Align != SvgPreserveAspectRatio.xMidYMid)
                    {
                        firstAspectRatio = p;
                    }
                }
            }

            if (firstChildren is null || firstWidth is null || firstHeight is null)
            {
                return null;
            }

            var xUnit = firstX is null ? new SvgUnit(0f) : firstX.X;
            var yUnit = firstY is null ? new SvgUnit(0f) : firstY.Y;
            var widthUnit = firstWidth.Width;
            var heightUnit = firstHeight.Height;
            var patternUnits = firstPatternUnit is null ? SvgCoordinateUnits.ObjectBoundingBox : firstPatternUnit.PatternUnits;
            var patternContentUnits = firstPatternContentUnit is null ? SvgCoordinateUnits.UserSpaceOnUse : firstPatternContentUnit.PatternContentUnits;
            var viewBox = firstViewBox is null ? SvgViewBox.Empty : firstViewBox.ViewBox;
            var aspectRatio = firstAspectRatio is null ? new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid, false) : firstAspectRatio.AspectRatio;

            float x = xUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgPatternServer, skBounds);
            float y = yUnit.ToDeviceValue(UnitRenderingType.Vertical, svgPatternServer, skBounds);
            float width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgPatternServer, skBounds);
            float height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgPatternServer, skBounds);

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            if (patternUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                if (xUnit.Type != SvgUnitType.Percentage)
                {
                    x *= skBounds.Width;
                }

                if (yUnit.Type != SvgUnitType.Percentage)
                {
                    y *= skBounds.Height;
                }

                if (widthUnit.Type != SvgUnitType.Percentage)
                {
                    width *= skBounds.Width;
                }

                if (heightUnit.Type != SvgUnitType.Percentage)
                {
                    height *= skBounds.Height;
                }

                x += skBounds.Left;
                y += skBounds.Top;
            }

            SKRect skRectTransformed = SKRect.Create(x, y, width, height);

            var skMatrix = SKMatrix.CreateIdentity();

            var skPatternTransformMatrix = ToSKMatrix(svgPatternServer.PatternTransform);
            skMatrix = skMatrix.PreConcat(skPatternTransformMatrix);

            var translateTransform = SKMatrix.CreateTranslation(skRectTransformed.Left, skRectTransformed.Top);
            skMatrix = skMatrix.PreConcat(translateTransform);

            SKMatrix skPictureTransform = SKMatrix.CreateIdentity();
            if (!viewBox.Equals(SvgViewBox.Empty))
            {
                var viewBoxTransform = ToSKMatrix(
                    viewBox,
                    aspectRatio,
                    0f,
                    0f,
                    skRectTransformed.Width,
                    skRectTransformed.Height);
                skPictureTransform = skPictureTransform.PreConcat(viewBoxTransform);
            }
            else
            {
                if (patternContentUnits == SvgCoordinateUnits.ObjectBoundingBox)
                {
                    var skBoundsScaleTransform = SKMatrix.CreateScale(skBounds.Width, skBounds.Height);
                    skPictureTransform = skPictureTransform.PreConcat(skBoundsScaleTransform);
                }
            }

            var skPicture = RecordPicture(firstChildren.Children, skRectTransformed.Width, skRectTransformed.Height, skPictureTransform, opacity, ignoreAttributes);
            disposable.Add(skPicture);

            return SKShader.CreatePicture(skPicture, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, skMatrix, skPicture.CullRect);
        }

        public static bool SetColorOrShader(SvgVisualElement svgVisualElement, SvgPaintServer server, float opacity, SKRect skBounds, SKPaint skPaint, bool forStroke, Attributes ignoreAttributes, CompositeDisposable disposable)
        {
            var fallbackServer = SvgPaintServer.None;
            if (server is SvgDeferredPaintServer deferredServer)
            {
                server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(deferredServer, svgVisualElement);
                fallbackServer = deferredServer.FallbackServer;
            }

            if (server == SvgPaintServer.None)
            {
                return false;
            }

            switch (server)
            {
                case SvgColourServer svgColourServer:
                    {
                        var skColor = GetColor(svgColourServer, opacity, ignoreAttributes);
#if USE_COLORSPACE
                        var colorInterpolation = GetColorInterpolation(svgVisualElement);
                        var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                        var skColorSpace = isLinearRGB ? SKSvgSettings.s_srgbLinear : SKSvgSettings.s_srgb;
                        var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
#else
                        var skColorShader = SKShader.CreateColor(skColor);
#endif
                        if (skColorShader != null)
                        {
                            disposable.Add(skColorShader);
                            skPaint.Shader = skColorShader;
                            return true;
                        }
                    }
                    break;
                case SvgPatternServer svgPatternServer:
                    {
                        var skPatternShader = CreatePicture(svgPatternServer, skBounds, svgVisualElement, opacity, ignoreAttributes, disposable);
                        if (skPatternShader != null)
                        {
                            disposable.Add(skPatternShader);
                            skPaint.Shader = skPatternShader;
                            return true;
                        }
                        else
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
#if USE_COLORSPACE
                                var colorInterpolation = GetColorInterpolation(svgVisualElement);
                                var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                                var skColorSpace = isLinearRGB ? SKSvgSettings.s_srgbLinear : SKSvgSettings.s_srgb;
                                var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
#else
                                var skColorShader = SKShader.CreateColor(skColor);
#endif
                                if (skColorShader != null)
                                {
                                    disposable.Add(skColorShader);
                                    skPaint.Shader = skColorShader;
                                    return true;
                                }
                            }
                            else
                            {
                                // Do not draw element.
                                return false;
                            }
                        }
                    }
                    break;
                case SvgLinearGradientServer svgLinearGradientServer:
                    {
#if USE_COLORSPACE
                        var colorInterpolation = GetColorInterpolation(svgLinearGradientServer);
                        var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                        var skColorSpace = isLinearRGB ? SKSvgSettings.s_srgbLinear : SKSvgSettings.s_srgb;
#endif
                        if (svgLinearGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
#if USE_COLORSPACE
                                var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
#else
                                var skColorShader = SKShader.CreateColor(skColor);
#endif
                                if (skColorShader != null)
                                {
                                    disposable.Add(skColorShader);
                                    skPaint.Shader = skColorShader;
                                    return true;
                                }
                            }
                            else
                            {
                                // Do not draw element.
                                return false;
                            }
                        }
                        else
                        {
#if USE_COLORSPACE
                            var skLinearGradientShader = CreateLinearGradient(svgLinearGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace);
#else
                            var skLinearGradientShader = CreateLinearGradient(svgLinearGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes);
#endif
                            if (skLinearGradientShader != null)
                            {
                                //#if USE_COLORSPACE
                                //if (!isLinearRGB)
                                //{
                                //    var skColorFilter = SKColorFilter.CreateTable(null, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB);
                                //    disposable.Add(skColorFilter);
                                //    skPaint.ColorFilter = skColorFilter;
                                //}
                                //#endif
                                disposable.Add(skLinearGradientShader);
                                skPaint.Shader = skLinearGradientShader;
                                return true;
                            }
                            else
                            {
                                // Do not draw element.
                                return false;
                            }
                        }
                    }
                    break;
                case SvgRadialGradientServer svgRadialGradientServer:
                    {
#if USE_COLORSPACE
                        var colorInterpolation = GetColorInterpolation(svgRadialGradientServer);
                        var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                        var skColorSpace = isLinearRGB ? SKSvgSettings.s_srgbLinear : SKSvgSettings.s_srgb;
#endif
                        if (svgRadialGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
#if USE_COLORSPACE
                                var skColorShader = SKShader.CreateColor(skColor, skColorSpace);
#else
                                var skColorShader = SKShader.CreateColor(skColor);
#endif
                                if (skColorShader != null)
                                {
                                    disposable.Add(skColorShader);
                                    skPaint.Shader = skColorShader;
                                    return true;
                                }
                            }
                            else
                            {
                                // Do not draw element.
                                return false;
                            }
                        }
                        else
                        {
#if USE_COLORSPACE
                            var skRadialGradientShader = CreateTwoPointConicalGradient(svgRadialGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace);
#else
                            var skRadialGradientShader = CreateTwoPointConicalGradient(svgRadialGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes);
#endif
                            if (skRadialGradientShader != null)
                            {
                                //#if USE_COLORSPACE
                                //if (!isLinearRGB)
                                //{
                                //    var skColorFilter = SKColorFilter.CreateTable(null, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB, s_SRGBtoLinearRGB);
                                //    disposable.Add(skColorFilter);
                                //    skPaint.ColorFilter = skColorFilter;
                                //}
                                //#endif
                                disposable.Add(skRadialGradientShader);
                                skPaint.Shader = skRadialGradientShader;
                                return true;
                            }
                            else
                            {
                                // Do not draw element.
                                return false;
                            }
                        }
                    }
                    break;
                case SvgDeferredPaintServer svgDeferredPaintServer:
                    return SetColorOrShader(svgVisualElement, svgDeferredPaintServer, opacity, skBounds, skPaint, forStroke, ignoreAttributes, disposable);
                default:
                    // Do not draw element.
                    return false;
            }
            return true;
        }

        public static void SetDash(SvgVisualElement svgVisualElement, SKPaint skPaint, SKRect skBounds, CompositeDisposable disposable)
        {
            var skPathEffect = CreateDash(svgVisualElement, skBounds);
            if (skPathEffect != null)
            {
                disposable.Add(skPathEffect);
                skPaint.PathEffect = skPathEffect;
            }
        }

        public static bool IsAntialias(SvgElement svgElement)
        {
            switch (svgElement.ShapeRendering)
            {
                case SvgShapeRendering.Inherit:
                case SvgShapeRendering.Auto:
                case SvgShapeRendering.GeometricPrecision:
                default:
                    return true;
                case SvgShapeRendering.OptimizeSpeed:
                case SvgShapeRendering.CrispEdges:
                    return false;
            }
        }

        public static bool IsValidFill(SvgElement svgElement)
        {
            var fill = svgElement.Fill;
            return fill != null
                && fill != SvgPaintServer.None;
        }

        public static bool IsValidStroke(SvgElement svgElement, SKRect skBounds)
        {
            var stroke = svgElement.Stroke;
            var strokeWidth = svgElement.StrokeWidth;
            return stroke != null
                && stroke != SvgPaintServer.None
                && strokeWidth.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds) > 0f;
        }

        public static SKPaint? GetFillSKPaint(SvgVisualElement svgVisualElement, SKRect skBounds, Attributes ignoreAttributes, CompositeDisposable disposable)
        {
            var skPaint = new SKPaint()
            {
                IsAntialias = IsAntialias(svgVisualElement),
                Style = SKPaintStyle.Fill
            };

            var server = svgVisualElement.Fill;
            var opacity = AdjustSvgOpacity(svgVisualElement.FillOpacity);
            if (SetColorOrShader(svgVisualElement, server, opacity, skBounds, skPaint, forStroke: false, ignoreAttributes, disposable) == false)
            {
                return null;
            }

            disposable.Add(skPaint);
            return skPaint;
        }

        public static SKPaint? GetStrokeSKPaint(SvgVisualElement svgVisualElement, SKRect skBounds, Attributes ignoreAttributes, CompositeDisposable disposable)
        {
            var skPaint = new SKPaint()
            {
                IsAntialias = IsAntialias(svgVisualElement),
                Style = SKPaintStyle.Stroke
            };

            var server = svgVisualElement.Stroke;
            var opacity = AdjustSvgOpacity(svgVisualElement.StrokeOpacity);
            if (SetColorOrShader(svgVisualElement, server, opacity, skBounds, skPaint, forStroke: true, ignoreAttributes, disposable) == false)
            {
                return null;
            }

            switch (svgVisualElement.StrokeLineCap)
            {
                case SvgStrokeLineCap.Butt:
                    skPaint.StrokeCap = SKStrokeCap.Butt;
                    break;
                case SvgStrokeLineCap.Round:
                    skPaint.StrokeCap = SKStrokeCap.Round;
                    break;
                case SvgStrokeLineCap.Square:
                    skPaint.StrokeCap = SKStrokeCap.Square;
                    break;
            }

            switch (svgVisualElement.StrokeLineJoin)
            {
                case SvgStrokeLineJoin.Miter:
                    skPaint.StrokeJoin = SKStrokeJoin.Miter;
                    break;
                case SvgStrokeLineJoin.Round:
                    skPaint.StrokeJoin = SKStrokeJoin.Round;
                    break;
                case SvgStrokeLineJoin.Bevel:
                    skPaint.StrokeJoin = SKStrokeJoin.Bevel;
                    break;
            }

            skPaint.StrokeMiter = svgVisualElement.StrokeMiterLimit;

            skPaint.StrokeWidth = svgVisualElement.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgVisualElement, skBounds);

            var strokeDashArray = svgVisualElement.StrokeDashArray;
            if (strokeDashArray != null)
            {
                SetDash(svgVisualElement, skPaint, skBounds, disposable);
            }

            disposable.Add(skPaint);
            return skPaint;
        }

        public static SKPaint? GetOpacitySKPaint(float opacity)
        {
            if (opacity < 1f)
            {
                return new SKPaint
                {
                    IsAntialias = true,
                    Color = new SKColor(255, 255, 255, (byte)Math.Round(opacity * 255)),
                    Style = SKPaintStyle.StrokeAndFill
                };
            }
            return null;
        }

        public static SKPaint? GetOpacitySKPaint(SvgElement svgElement, CompositeDisposable disposable)
        {
            float opacity = AdjustSvgOpacity(svgElement.Opacity);
            var skPaint = GetOpacitySKPaint(opacity);
            if (skPaint != null)
            {
                disposable.Add(skPaint);
                return skPaint;
            }
            return null;
        }

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
                            var skMatrix = svgMatrix.ToSKMatrix();
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
                            float sx = (float)Math.Tan(Math.PI * svgSkew.AngleX / 180);
                            float sy = (float)Math.Tan(Math.PI * svgSkew.AngleY / 180);
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
                return SKMatrix.CreateTranslation(x, y);
            }

            float fScaleX = width / svgViewBox.Width;
            float fScaleY = height / svgViewBox.Height;
            float fMinX = -svgViewBox.MinX * fScaleX;
            float fMinY = -svgViewBox.MinY * fScaleY;

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

            var skMatrixTotal = SKMatrix.CreateIdentity();

            var skMatrixXY = SKMatrix.CreateTranslation(x, y);
            skMatrixTotal = skMatrixTotal.PreConcat(skMatrixXY);

            var skMatrixMinXY = SKMatrix.CreateTranslation(fMinX, fMinY);
            skMatrixTotal = skMatrixTotal.PreConcat(skMatrixMinXY);

            var skMatrixScale = SKMatrix.CreateScale(fScaleX, fScaleY);
            skMatrixTotal = skMatrixTotal.PreConcat(skMatrixScale);

            return skMatrixTotal;
        }
#if USE_PICTURE
        public static List<(Svg.Picture.Point Point, byte Type)> GetPathTypes(this Svg.Picture.Path path)
        {
            // System.Drawing.Drawing2D.GraphicsPath.PathTypes
            // System.Drawing.Drawing2D.PathPointType
            // byte -> PathPointType
            var pathTypes = new List<(Svg.Picture.Point Point, byte Type)>();

            if (path.Commands == null)
            {
                return pathTypes;
            }
            (Svg.Picture.Point Point, byte Type) lastPoint = (default, 0);
            foreach (var pathCommand in path.Commands)
            {
                switch (pathCommand)
                {
                    case Svg.Picture.MoveToPathCommand moveToPathCommand:
                        {
                            var point0 = new Svg.Picture.Point(moveToPathCommand.X, moveToPathCommand.Y);
                            pathTypes.Add((point0, (byte)PathPointType.Start));
                            lastPoint = (point0, (byte)PathPointType.Start);
                        }
                        break;
                    case Svg.Picture.LineToPathCommand lineToPathCommand:
                        {
                            var point1 = new Svg.Picture.Point(lineToPathCommand.X, lineToPathCommand.Y);
                            pathTypes.Add((point1, (byte)PathPointType.Line));
                            lastPoint = (point1, (byte)PathPointType.Line);
                        }
                        break;
                    case Svg.Picture.CubicToPathCommand cubicToPathCommand:
                        {
                            var point1 = new Svg.Picture.Point(cubicToPathCommand.X0, cubicToPathCommand.Y0);
                            var point2 = new Svg.Picture.Point(cubicToPathCommand.X1, cubicToPathCommand.Y1);
                            var point3 = new Svg.Picture.Point(cubicToPathCommand.X2, cubicToPathCommand.Y2);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            pathTypes.Add((point2, (byte)PathPointType.Bezier));
                            pathTypes.Add((point3, (byte)PathPointType.Bezier));
                            lastPoint = (point3, (byte)PathPointType.Bezier);
                        }
                        break;
                    case Svg.Picture.QuadToPathCommand quadToPathCommand:
                        {
                            var point1 = new Svg.Picture.Point(quadToPathCommand.X0, quadToPathCommand.Y0);
                            var point2 = new Svg.Picture.Point(quadToPathCommand.X1, quadToPathCommand.Y1);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            pathTypes.Add((point2, (byte)PathPointType.Bezier));
                            lastPoint = (point2, (byte)PathPointType.Bezier);
                        }
                        break;
                    case Svg.Picture.ArcToPathCommand arcToPathCommand:
                        {
                            var point1 = new SKPoint(arcToPathCommand.X, arcToPathCommand.Y);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            lastPoint = (point1, (byte)PathPointType.Bezier);
                        }
                        break;
                    case Svg.Picture.ClosePathCommand closePathCommand:
                        {
                            lastPoint = (lastPoint.Point, (byte)((lastPoint.Type | (byte)PathPointType.CloseSubpath)));
                            pathTypes[pathTypes.Count - 1] = lastPoint;
                        }
                        break;
                    case Svg.Picture.AddPolyPathCommand addPolyPathCommand:
                        {
                            if (addPolyPathCommand.Points != null && addPolyPathCommand.Points.Count > 0)
                            {
                                foreach (var nexPoint in addPolyPathCommand.Points)
                                {
                                    var point1 = new Svg.Picture.Point(nexPoint.X, nexPoint.Y);
                                    pathTypes.Add((point1, (byte)PathPointType.Start));
                                    lastPoint = (point1, (byte)PathPointType.Start);
                                }

                                var point = addPolyPathCommand.Points[addPolyPathCommand.Points.Count - 1];
                                lastPoint = (point, (byte)PathPointType.Line);
                            }
                        }
                        break;
                    default:
                        Debug.WriteLine($"Not implemented path point for {pathCommand?.GetType()} type.");
                        break;
                }
            }

            return pathTypes;
        }
#else
        public static List<(SKPoint Point, byte Type)> GetPathTypes(this SKPath skPath)
        {
            // System.Drawing.Drawing2D.GraphicsPath.PathTypes
            // System.Drawing.Drawing2D.PathPointType
            // byte -> PathPointType
            var pathTypes = new List<(SKPoint Point, byte Type)>();

            using (var iterator = skPath.CreateRawIterator())
            {
                var points = new SKPoint[4];
                var pathVerb = SKPathVerb.Move;
                (SKPoint Point, byte Type) lastPoint = (default, 0);
                while ((pathVerb = iterator.Next(points)) != SKPathVerb.Done)
                {
                    switch (pathVerb)
                    {
                        case SKPathVerb.Move:
                            {
                                pathTypes.Add((points[0], (byte)PathPointType.Start));
                                lastPoint = (points[0], (byte)PathPointType.Start);
                            }
                            break;
                        case SKPathVerb.Line:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Line));
                                lastPoint = (points[1], (byte)PathPointType.Line);
                            }
                            break;
                        case SKPathVerb.Cubic:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[2], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[3], (byte)PathPointType.Bezier));
                                lastPoint = (points[3], (byte)PathPointType.Bezier);
                            }
                            break;
                        case SKPathVerb.Quad:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[2], (byte)PathPointType.Bezier));
                                lastPoint = (points[2], (byte)PathPointType.Bezier);
                            }
                            break;
                        case SKPathVerb.Conic:
                            {
                                pathTypes.Add((points[1], (byte)PathPointType.Bezier));
                                pathTypes.Add((points[2], (byte)PathPointType.Bezier));
                                lastPoint = (points[2], (byte)PathPointType.Bezier);
                            }
                            break;
                        case SKPathVerb.Close:
                            {
                                lastPoint = (lastPoint.Point, (byte)((lastPoint.Type | (byte)PathPointType.CloseSubpath)));
                                pathTypes[pathTypes.Count - 1] = lastPoint;
                            }
                            break;
                    }
                }
            }

            return pathTypes;
        }
#endif
        public static SKPath? ToSKPath(this SvgPathSegmentList svgPathSegmentList, SvgFillRule svgFillRule, CompositeDisposable disposable)
        {
            if (svgPathSegmentList == null || svgPathSegmentList.Count <= 0)
            {
                return null;
            }

            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
            var skPath = new SKPath()
            {
                FillType = fillType
            };

            bool isEndFigure = false;
            bool haveFigure = false;

            for (int i = 0; i < svgPathSegmentList.Count; i++)
            {
                var svgSegment = svgPathSegmentList[i];
                var isLast = i == svgPathSegmentList.Count - 1;

                switch (svgSegment)
                {
                    case SvgMoveToSegment svgMoveToSegment:
                        {
                            if (isEndFigure && haveFigure == false)
                            {
                                return null;
                            }

                            if (isLast)
                            {
                                return skPath;
                            }
                            else
                            {
                                if (svgPathSegmentList[i + 1] is SvgMoveToSegment)
                                {
                                    return skPath;
                                }

                                if (svgPathSegmentList[i + 1] is SvgClosePathSegment)
                                {
                                    return skPath;
                                }
                            }
                            isEndFigure = true;
                            haveFigure = false;
                            float x = svgMoveToSegment.Start.X;
                            float y = svgMoveToSegment.Start.Y;
                            skPath.MoveTo(x, y);
                        }
                        break;
                    case SvgLineSegment svgLineSegment:
                        {
                            if (isEndFigure == false)
                            {
                                return null;
                            }
                            haveFigure = true;
                            float x = svgLineSegment.End.X;
                            float y = svgLineSegment.End.Y;
                            skPath.LineTo(x, y);
                        }
                        break;
                    case SvgCubicCurveSegment svgCubicCurveSegment:
                        {
                            if (isEndFigure == false)
                            {
                                return null;
                            }
                            haveFigure = true;
                            float x0 = svgCubicCurveSegment.FirstControlPoint.X;
                            float y0 = svgCubicCurveSegment.FirstControlPoint.Y;
                            float x1 = svgCubicCurveSegment.SecondControlPoint.X;
                            float y1 = svgCubicCurveSegment.SecondControlPoint.Y;
                            float x2 = svgCubicCurveSegment.End.X;
                            float y2 = svgCubicCurveSegment.End.Y;
                            skPath.CubicTo(x0, y0, x1, y1, x2, y2);
                        }
                        break;
                    case SvgQuadraticCurveSegment svgQuadraticCurveSegment:
                        {
                            if (isEndFigure == false)
                            {
                                return null;
                            }
                            haveFigure = true;
                            float x0 = svgQuadraticCurveSegment.ControlPoint.X;
                            float y0 = svgQuadraticCurveSegment.ControlPoint.Y;
                            float x1 = svgQuadraticCurveSegment.End.X;
                            float y1 = svgQuadraticCurveSegment.End.Y;
                            skPath.QuadTo(x0, y0, x1, y1);
                        }
                        break;
                    case SvgArcSegment svgArcSegment:
                        {
                            if (isEndFigure == false)
                            {
                                return null;
                            }
                            haveFigure = true;
                            float rx = svgArcSegment.RadiusX;
                            float ry = svgArcSegment.RadiusY;
                            float xAxisRotate = svgArcSegment.Angle;
                            var largeArc = svgArcSegment.Size == SvgArcSize.Small ? SKPathArcSize.Small : SKPathArcSize.Large;
                            var sweep = svgArcSegment.Sweep == SvgArcSweep.Negative ? SKPathDirection.CounterClockwise : SKPathDirection.Clockwise;
                            float x = svgArcSegment.End.X;
                            float y = svgArcSegment.End.Y;
                            skPath.ArcTo(rx, ry, xAxisRotate, largeArc, sweep, x, y);
                        }
                        break;
                    case SvgClosePathSegment _:
                        {
                            if (isEndFigure == false)
                            {
                                return null;
                            }
                            if (haveFigure == false)
                            {
                                return null;
                            }
                            isEndFigure = false;
                            haveFigure = false;
                            skPath.Close();
                        }
                        break;
                }
            }

            if (isEndFigure)
            {
                if (haveFigure == false)
                {
                    return null;
                }
            }

            disposable.Add(skPath);
            return skPath;
        }

        public static SKPath? ToSKPath(this SvgPointCollection svgPointCollection, SvgFillRule svgFillRule, bool isClosed, SKRect skOwnerBounds, CompositeDisposable disposable)
        {
            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
            var skPath = new SKPath()
            {
                FillType = fillType
            };

            var skPoints = new SKPoint[svgPointCollection.Count / 2];

            for (int i = 0; (i + 1) < svgPointCollection.Count; i += 2)
            {
                float x = svgPointCollection[i].ToDeviceValue(UnitRenderingType.Other, null, skOwnerBounds);
                float y = svgPointCollection[i + 1].ToDeviceValue(UnitRenderingType.Other, null, skOwnerBounds);
                skPoints[i / 2] = new SKPoint(x, y);
            }

            skPath.AddPoly(skPoints, isClosed);

            disposable.Add(skPath);
            return skPath;
        }

        public static SKPath? ToSKPath(this SvgRectangle svgRectangle, SvgFillRule svgFillRule, SKRect skOwnerBounds, CompositeDisposable disposable)
        {
            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
            var skPath = new SKPath()
            {
                FillType = fillType
            };

            float x = svgRectangle.X.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skOwnerBounds);
            float y = svgRectangle.Y.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skOwnerBounds);
            float width = svgRectangle.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skOwnerBounds);
            float height = svgRectangle.Height.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skOwnerBounds);
            float rx = svgRectangle.CornerRadiusX.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skOwnerBounds);
            float ry = svgRectangle.CornerRadiusY.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skOwnerBounds);

            if (width <= 0f || height <= 0f)
            {
                skPath.Dispose();
                return null;
            }

            if (rx < 0f && ry < 0f)
            {
                rx = 0f;
                ry = 0f;
            }

            if (rx == 0f || ry == 0f)
            {
                rx = 0f;
                ry = 0f;
            }

            if (rx < 0f)
            {
                rx = Math.Abs(rx);
            }

            if (ry < 0f)
            {
                ry = Math.Abs(ry);
            }

            if (rx > 0f)
            {
                float halfWidth = width / 2f;
                if (rx > halfWidth)
                {
                    rx = halfWidth;
                }
            }

            if (ry > 0f)
            {
                float halfHeight = height / 2f;
                if (ry > halfHeight)
                {
                    ry = halfHeight;
                }
            }

            bool isRound = rx > 0f && ry > 0f;
            var skRectBounds = SKRect.Create(x, y, width, height);

            if (isRound)
            {
                skPath.AddRoundRect(skRectBounds, rx, ry);
            }
            else
            {
                skPath.AddRect(skRectBounds);
            }

            disposable.Add(skPath);
            return skPath;
        }

        public static SKPath? ToSKPath(this SvgCircle svgCircle, SvgFillRule svgFillRule, SKRect skOwnerBounds, CompositeDisposable disposable)
        {
            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
            var skPath = new SKPath()
            {
                FillType = fillType
            };

            float cx = svgCircle.CenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgCircle, skOwnerBounds);
            float cy = svgCircle.CenterY.ToDeviceValue(UnitRenderingType.Vertical, svgCircle, skOwnerBounds);
            float radius = svgCircle.Radius.ToDeviceValue(UnitRenderingType.Other, svgCircle, skOwnerBounds);

            if (radius <= 0f)
            {
                skPath.Dispose();
                return null;
            }

            skPath.AddCircle(cx, cy, radius);

            disposable.Add(skPath);
            return skPath;
        }

        public static SKPath? ToSKPath(this SvgEllipse svgEllipse, SvgFillRule svgFillRule, SKRect skOwnerBounds, CompositeDisposable disposable)
        {
            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
            var skPath = new SKPath()
            {
                FillType = fillType
            };

            float cx = svgEllipse.CenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgEllipse, skOwnerBounds);
            float cy = svgEllipse.CenterY.ToDeviceValue(UnitRenderingType.Vertical, svgEllipse, skOwnerBounds);
            float rx = svgEllipse.RadiusX.ToDeviceValue(UnitRenderingType.Other, svgEllipse, skOwnerBounds);
            float ry = svgEllipse.RadiusY.ToDeviceValue(UnitRenderingType.Other, svgEllipse, skOwnerBounds);

            if (rx <= 0f || ry <= 0f)
            {
                skPath.Dispose();
                return null;
            }

            var skRectBounds = SKRect.Create(cx - rx, cy - ry, rx + rx, ry + ry);

            skPath.AddOval(skRectBounds);

            disposable.Add(skPath);
            return skPath;
        }

        public static SKPath? ToSKPath(this SvgLine svgLine, SvgFillRule svgFillRule, SKRect skOwnerBounds, CompositeDisposable disposable)
        {
            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
            var skPath = new SKPath()
            {
                FillType = fillType
            };

            float x0 = svgLine.StartX.ToDeviceValue(UnitRenderingType.Horizontal, svgLine, skOwnerBounds);
            float y0 = svgLine.StartY.ToDeviceValue(UnitRenderingType.Vertical, svgLine, skOwnerBounds);
            float x1 = svgLine.EndX.ToDeviceValue(UnitRenderingType.Horizontal, svgLine, skOwnerBounds);
            float y1 = svgLine.EndY.ToDeviceValue(UnitRenderingType.Vertical, svgLine, skOwnerBounds);

            skPath.MoveTo(x0, y0);
            skPath.LineTo(x1, y1);

            disposable.Add(skPath);
            return skPath;
        }

        public static object? GetImage(string uriString, SvgDocument svgOwnerDocument)
        {
            try
            {
                // Uri MaxLength is 65519 (https://msdn.microsoft.com/en-us/library/z6c2z492.aspx)
                // if using data URI scheme, very long URI may happen.
                var safeUriString = uriString.Length > 65519 ? uriString.Substring(0, 65519) : uriString;
                var uri = new Uri(safeUriString, UriKind.RelativeOrAbsolute);

                // handle data/uri embedded images (http://en.wikipedia.org/wiki/Data_URI_scheme)
                if (uri.IsAbsoluteUri && uri.Scheme == "data")
                {
                    return GetImageFromDataUri(uriString, svgOwnerDocument);
                }

                if (!uri.IsAbsoluteUri)
                {
                    uri = new Uri(svgOwnerDocument.BaseUri, uri);
                }

                return GetImageFromWeb(uri);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
                return null;
            }
        }

        public static object GetImageFromWeb(Uri uri)
        {
            var request = WebRequest.Create(uri);
            using var response = request.GetResponse();
            using var stream = response.GetResponseStream();

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var isSvgMimeType = response.ContentType.StartsWith(MimeTypeSvg, StringComparison.InvariantCultureIgnoreCase);
            var isSvg = uri.LocalPath.EndsWith(".svg", StringComparison.InvariantCultureIgnoreCase);
            var isSvgz = uri.LocalPath.EndsWith(".svgz", StringComparison.InvariantCultureIgnoreCase);

            if (isSvgMimeType || isSvg)
            {
                var svgDocument = LoadSvg(stream, uri);
                return svgDocument;
            }
            else if (isSvgMimeType || isSvgz)
            {
                var svgDocument = LoadSvgz(stream, uri);
                return svgDocument;
            }
            else
            {
                var skImage = SKImage.FromEncodedData(stream);
                return skImage;
            }
        }

        public static object? GetImageFromDataUri(string uriString, SvgDocument svgOwnerDocument)
        {
            var headerStartIndex = 5;
            var headerEndIndex = uriString.IndexOf(",", headerStartIndex);
            if (headerEndIndex < 0 || headerEndIndex + 1 >= uriString.Length)
            {
                throw new Exception("Invalid data URI");
            }

            var mimeType = "text/plain";
            var charset = "US-ASCII";
            var base64 = false;

            var headers = new List<string>(uriString.Substring(headerStartIndex, headerEndIndex - headerStartIndex).Split(';'));
            if (headers[0].Contains("/"))
            {
                mimeType = headers[0].Trim();
                headers.RemoveAt(0);
                charset = string.Empty;
            }

            if (headers.Count > 0 && headers[headers.Count - 1].Trim().Equals("base64", StringComparison.InvariantCultureIgnoreCase))
            {
                base64 = true;
                headers.RemoveAt(headers.Count - 1);
            }

            foreach (var param in headers)
            {
                var p = param.Split('=');
                if (p.Length < 2)
                {
                    continue;
                }

                var attribute = p[0].Trim();
                if (attribute.Equals("charset", StringComparison.InvariantCultureIgnoreCase))
                {
                    charset = p[1].Trim();
                }
            }

            var data = uriString.Substring(headerEndIndex + 1);
            if (mimeType.Equals(MimeTypeSvg, StringComparison.InvariantCultureIgnoreCase))
            {
                if (base64)
                {
                    var bytes = Convert.FromBase64String(data);

                    if (bytes.Length > 2)
                    {
                        bool isCompressed = bytes[0] == s_gZipMagicHeaderBytes[0] && bytes[1] == s_gZipMagicHeaderBytes[1];
                        if (isCompressed)
                        {
                            using var bytesStream = new MemoryStream(bytes);
                            return LoadSvgz(bytesStream, svgOwnerDocument.BaseUri);
                        }
                    }

                    var encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
                    data = encoding.GetString(bytes);
                }
                using var stream = new MemoryStream(Encoding.Default.GetBytes(data));
                return LoadSvg(stream, svgOwnerDocument.BaseUri);
            }
            else if (mimeType.StartsWith("image/") || mimeType.StartsWith("img/"))
            {
                var dataBytes = base64 ? Convert.FromBase64String(data) : Encoding.Default.GetBytes(data);
                using var stream = new MemoryStream(dataBytes);
                return SKImage.FromEncodedData(stream);
            }
            else
            {
                return null;
            }
        }

        public static SvgDocument LoadSvg(Stream stream, Uri baseUri)
        {
            var svgDocument = SvgDocument.Open<SvgDocument>(stream);
            svgDocument.BaseUri = baseUri;
            return svgDocument;
        }

        public static SvgDocument LoadSvgz(Stream stream, Uri baseUri)
        {
            using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            using var memoryStream = new MemoryStream();
            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            var svgDocument = SvgDocument.Open<SvgDocument>(memoryStream);
            svgDocument.BaseUri = baseUri;
            return svgDocument;
        }

        public static bool CanDraw(SvgVisualElement svgVisualElement, Attributes ignoreAttributes)
        {
            bool visible = svgVisualElement.Visible;
            bool ignoreDisplay = ignoreAttributes.HasFlag(Attributes.Display);
            bool display = ignoreDisplay || !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }
#if USE_PICTURE // TODO:
        private static SvgFillRule ToFillRule(SvgVisualElement svgVisualElement, SvgClipRule? svgClipPathClipRule)
        {
            var svgClipRule = (svgClipPathClipRule != null ? svgClipPathClipRule.Value : svgVisualElement.ClipRule);
            return svgClipRule == SvgClipRule.EvenOdd ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
        }

        private static SvgClipRule? GetSvgClipRule(SvgClipPath svgClipPath)
        {
            TryGetAttribute(svgClipPath, "clip-rule", out var clipRuleString);

            return clipRuleString switch
            {
                "nonzero" => SvgClipRule.NonZero,
                "evenodd" => SvgClipRule.EvenOdd,
                "inherit" => SvgClipRule.Inherit,// TODO:
                _ => null
            };
        }

        public static void GetClipPath(SvgVisualElement svgVisualElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable, Svg.Picture.ClipPath? clipPath, SvgClipRule? svgClipPathClipRule)
        {
            if (clipPath is null)
            {
                return;
            }

            if (!CanDraw(svgVisualElement, Attributes.None))
            {
                return;
            }

            switch (svgVisualElement)
            {
                case SvgPath svgPath:
                    {
                        var fillRule = ToFillRule(svgPath, svgClipPathClipRule);
                        var skPath = svgPath.PathData?.ToSKPath(fillRule, disposable);
                        if (skPath == null)
                        {
                            break;
                        }

                        var pathClip = new Svg.Picture.PathClip
                        {
                            Path = skPath,
                            Transform = ToSKMatrix(svgPath.Transforms),
                            Clip = new Svg.Picture.ClipPath()
                            {
                                Clip = new Svg.Picture.ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgPath, skPath.Bounds, uris, disposable, pathClip.Clip);
                    }
                    break;
                case SvgRectangle svgRectangle:
                    {
                        var fillRule = ToFillRule(svgRectangle, svgClipPathClipRule);
                        var skPath = svgRectangle.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath == null)
                        {
                            break;
                        }

                        var pathClip = new Svg.Picture.PathClip
                        {
                            Path = skPath,
                            Transform = ToSKMatrix(svgRectangle.Transforms),
                            Clip = new Svg.Picture.ClipPath()
                            {
                                Clip = new Svg.Picture.ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgRectangle, skPath.Bounds, uris, disposable, pathClip.Clip);
                    }
                    break;
                case SvgCircle svgCircle:
                    {
                        var fillRule = ToFillRule(svgCircle, svgClipPathClipRule);
                        var skPath = svgCircle.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath == null)
                        {
                            break;
                        }

                        var pathClip = new Svg.Picture.PathClip
                        {
                            Path = skPath,
                            Transform = ToSKMatrix(svgCircle.Transforms),
                            Clip = new Svg.Picture.ClipPath()
                            {
                                Clip = new Svg.Picture.ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgCircle, skPath.Bounds, uris, disposable, pathClip.Clip);
                    }
                    break;
                case SvgEllipse svgEllipse:
                    {
                        var fillRule = ToFillRule(svgEllipse, svgClipPathClipRule);
                        var skPath = svgEllipse.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath == null)
                        {
                            break;
                        }

                        var pathClip = new Svg.Picture.PathClip
                        {
                            Path = skPath,
                            Transform = ToSKMatrix(svgEllipse.Transforms),
                            Clip = new Svg.Picture.ClipPath()
                            {
                                Clip = new Svg.Picture.ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgEllipse, skPath.Bounds, uris, disposable, pathClip.Clip);
                    }
                    break;
                case SvgLine svgLine:
                    {
                        var fillRule = ToFillRule(svgLine, svgClipPathClipRule);
                        var skPath = svgLine.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath == null)
                        {
                            break;
                        }

                        var pathClip = new Svg.Picture.PathClip
                        {
                            Path = skPath,
                            Transform = ToSKMatrix(svgLine.Transforms),
                            Clip = new Svg.Picture.ClipPath()
                            {
                                Clip = new Svg.Picture.ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgLine, skPath.Bounds, uris, disposable, pathClip.Clip);
                    }
                    break;
                case SvgPolyline svgPolyline:
                    {
                        var fillRule = ToFillRule(svgPolyline, svgClipPathClipRule);
                        var skPath = svgPolyline.Points?.ToSKPath(fillRule, false, skBounds, disposable);
                        if (skPath == null)
                        {
                            break;
                        }

                        var pathClip = new Svg.Picture.PathClip
                        {
                            Path = skPath,
                            Transform = ToSKMatrix(svgPolyline.Transforms),
                            Clip = new Svg.Picture.ClipPath()
                            {
                                Clip = new Svg.Picture.ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgPolyline, skPath.Bounds, uris, disposable, pathClip.Clip);
                    }
                    break;
                case SvgPolygon svgPolygon:
                    {
                        var fillRule = ToFillRule(svgPolygon, svgClipPathClipRule);
                        var skPath = svgPolygon.Points?.ToSKPath(fillRule, true, skBounds, disposable);
                        if (skPath == null)
                        {
                            break;
                        }

                        var pathClip = new Svg.Picture.PathClip
                        {
                            Path = skPath,
                            Transform = ToSKMatrix(svgPolygon.Transforms),
                            Clip = new Svg.Picture.ClipPath()
                            {
                                Clip = new Svg.Picture.ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgPolygon, skPath.Bounds, uris, disposable, pathClip.Clip);
                    }
                    break;
                case SvgUse svgUse:
                    {
                        if (HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
                        {
                            break;
                        }

                        var svgReferencedVisualElement = GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
                        if (svgReferencedVisualElement == null || svgReferencedVisualElement is SvgSymbol)
                        {
                            break;
                        }

                        if (!CanDraw(svgReferencedVisualElement, Attributes.None))
                        {
                            break;
                        }

                        // TODO:
                        GetClipPath(svgReferencedVisualElement, skBounds, uris, disposable, clipPath, svgClipPathClipRule);

                        if (clipPath.Clips != null && clipPath.Clips.Count > 0)
                        {
                            // TODO:
                            var lastClip = clipPath.Clips[clipPath.Clips.Count - 1];
                            if (lastClip.Clip != null)
                            {
                                GetSvgVisualElementClipPath(svgUse, skBounds, uris, disposable, lastClip.Clip);
                            }
                        }
                    }
                    break;
                case SvgText svgText:
                    {
                        // TODO: Get path from SvgText.
                    }
                    break;
                default:
                    break;
            }
        }

        private static void GetClipPath(SvgElementCollection svgElementCollection, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable, Svg.Picture.ClipPath? clipPath, SvgClipRule? svgClipPathClipRule)
        {
            foreach (var svgElement in svgElementCollection)
            {
                if (svgElement is SvgVisualElement visualChild)
                {
                    if (!CanDraw(visualChild, Attributes.None))
                    {
                        continue;
                    }
                    GetClipPath(visualChild, skBounds, uris, disposable, clipPath, svgClipPathClipRule);
                }
            }
        }

        public static void GetClipPathClipPath(SvgClipPath svgClipPath, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable, Svg.Picture.ClipPath? clipPath)
        {
            if (clipPath == null)
            {
                return;
            }

            var svgClipPathRef = svgClipPath.GetUriElementReference<SvgClipPath>("clip-path", uris);
            if (svgClipPathRef == null || svgClipPathRef.Children == null)
            {
                return;
            }

            GetClipPath(svgClipPathRef, skBounds, uris, disposable, clipPath);

            var skMatrix = SKMatrix.CreateIdentity();

            if (svgClipPathRef.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skScaleMatrix = SKMatrix.CreateScale(skBounds.Width, skBounds.Height);
                skMatrix = skMatrix.PostConcat(skScaleMatrix);

                var skTranslateMatrix = SKMatrix.CreateTranslation(skBounds.Left, skBounds.Top);
                skMatrix = skMatrix.PostConcat(skTranslateMatrix);
            }

            var skTransformsMatrix = ToSKMatrix(svgClipPathRef.Transforms);
            skMatrix = skMatrix.PostConcat(skTransformsMatrix);

            clipPath.Transform = skMatrix; // TODO:
        }

        public static void GetClipPath(SvgClipPath svgClipPath, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable, Svg.Picture.ClipPath? clipPath)
        {
            if (clipPath == null)
            {
                return;
            }

            GetClipPathClipPath(svgClipPath, skBounds, uris, disposable, clipPath.Clip);

            var clipPathClipRule = GetSvgClipRule(svgClipPath);

            GetClipPath(svgClipPath.Children, skBounds, uris, disposable, clipPath, clipPathClipRule);

            var skMatrix = SKMatrix.CreateIdentity();

            if (svgClipPath.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skScaleMatrix = SKMatrix.CreateScale(skBounds.Width, skBounds.Height);
                skMatrix = skMatrix.PostConcat(skScaleMatrix);

                var skTranslateMatrix = SKMatrix.CreateTranslation(skBounds.Left, skBounds.Top);
                skMatrix = skMatrix.PostConcat(skTranslateMatrix);
            }

            var skTransformsMatrix = ToSKMatrix(svgClipPath.Transforms);
            skMatrix = skMatrix.PostConcat(skTransformsMatrix);

            clipPath.Transform = skMatrix; // TODO:

            if (clipPath.Clips != null && clipPath.Clips.Count == 0)
            {
                var pathClip = new Svg.Picture.PathClip
                {
                    Path = new Svg.Picture.Path(),
                    Transform = Svg.Picture.Matrix.CreateIdentity(),
                    Clip = null
                };
                clipPath.Clips.Add(pathClip);
            }
        }

        public static void GetSvgVisualElementClipPath(SvgVisualElement svgVisualElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable, Svg.Picture.ClipPath clipPath)
        {
            if (svgVisualElement == null || svgVisualElement.ClipPath == null)
            {
                return;
            }

            if (HasRecursiveReference(svgVisualElement, (e) => e.ClipPath, uris))
            {
                return;
            }

            var svgClipPath = GetReference<SvgClipPath>(svgVisualElement, svgVisualElement.ClipPath);
            if (svgClipPath == null || svgClipPath.Children == null)
            {
                return;
            }

            GetClipPath(svgClipPath, skBounds, uris, disposable, clipPath);
        }
#else
        public static SKPath? GetClipPath(SvgVisualElement svgVisualElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            if (!CanDraw(svgVisualElement, Attributes.None))
            {
                return null;
            }
            switch (svgVisualElement)
            {
                case SvgPath svgPath:
                    {
                        var fillRule = (svgPath.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgPath.PathData?.ToSKPath(fillRule, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = ToSKMatrix(svgPath.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgPath, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgRectangle svgRectangle:
                    {
                        var fillRule = (svgRectangle.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgRectangle.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = ToSKMatrix(svgRectangle.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgRectangle, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgCircle svgCircle:
                    {
                        var fillRule = (svgCircle.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgCircle.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = ToSKMatrix(svgCircle.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgCircle, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgEllipse svgEllipse:
                    {
                        var fillRule = (svgEllipse.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgEllipse.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = ToSKMatrix(svgEllipse.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgEllipse, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgLine svgLine:
                    {
                        var fillRule = (svgLine.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgLine.ToSKPath(fillRule, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = ToSKMatrix(svgLine.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgLine, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgPolyline svgPolyline:
                    {
                        var fillRule = (svgPolyline.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgPolyline.Points?.ToSKPath(fillRule, false, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = ToSKMatrix(svgPolyline.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgPolyline, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgPolygon svgPolygon:
                    {
                        var fillRule = (svgPolygon.ClipRule == SvgClipRule.EvenOdd) ? SvgFillRule.EvenOdd : SvgFillRule.NonZero;
                        var skPath = svgPolygon.Points?.ToSKPath(fillRule, true, skBounds, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = ToSKMatrix(svgPolygon.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgPolygon, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgUse svgUse:
                    {
                        if (HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
                        {
                            break;
                        }

                        var svgReferencedVisualElement = GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
                        if (svgReferencedVisualElement == null || svgReferencedVisualElement is SvgSymbol)
                        {
                            break;
                        }

                        if (!CanDraw(svgReferencedVisualElement, Attributes.None))
                        {
                            break;
                        }

                        var skPath = GetClipPath(svgReferencedVisualElement, skBounds, uris, disposable);
                        if (skPath != null)
                        {
                            var skMatrix = ToSKMatrix(svgUse.Transforms);
                            skPath.Transform(skMatrix);

                            var skPathClip = GetSvgVisualElementClipPath(svgUse, skPath.Bounds, uris, disposable);
                            if (skPathClip != null)
                            {
                                var result = skPath.Op(skPathClip, SKPathOp.Intersect);
                                disposable.Add(result);
                                return result;
                            }

                            return skPath;
                        }
                    }
                    break;
                case SvgText svgText:
                    {
                        // TODO: Get path from SvgText.
                    }
                    break;
                default:
                    break;
            }
            return null;
        }

        private static SKPath? GetClipPath(SvgElementCollection svgElementCollection, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var skPathClip = default(SKPath);

            foreach (var svgElement in svgElementCollection)
            {
                if (svgElement is SvgVisualElement visualChild)
                {
                    if (!CanDraw(visualChild, Attributes.None))
                    {
                        continue;
                    }
                    var skPath = GetClipPath(visualChild, skBounds, uris, disposable);
                    if (skPath != null)
                    {
                        if (skPathClip == null)
                        {
                            skPathClip = skPath;
                        }
                        else
                        {
                            var result = skPathClip.Op(skPath, SKPathOp.Union);
                            disposable.Add(result);
                            skPathClip = result;
                        }
                    }
                }
            }

            return skPathClip;
        }

        public static SKPath? GetClipPathClipPath(SvgClipPath svgClipPath, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var svgClipPathRef = svgClipPath.GetUriElementReference<SvgClipPath>("clip-path", uris);
            if (svgClipPathRef == null || svgClipPathRef.Children == null)
            {
                return null;
            }

            var clipPath = GetClipPath(svgClipPathRef, skBounds, uris, disposable);
            if (clipPath != null)
            {
                var skMatrix = SKMatrix.CreateIdentity();

                if (svgClipPathRef.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
                {
                    var skScaleMatrix = SKMatrix.CreateScale(skBounds.Width, skBounds.Height);
                    skMatrix = skMatrix.PostConcat(skScaleMatrix);

                    var skTranslateMatrix = SKMatrix.CreateTranslation(skBounds.Left, skBounds.Top);
                    skMatrix = skMatrix.PostConcat(skTranslateMatrix);
                }

                var skTransformsMatrix = ToSKMatrix(svgClipPathRef.Transforms);
                skMatrix = skMatrix.PostConcat(skTransformsMatrix);

                clipPath.Transform(skMatrix);
            }

            return clipPath;
        }

        public static SKPath? GetClipPath(SvgClipPath svgClipPath, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var skPathClip = default(SKPath);

            var clipPathClipPath = GetClipPathClipPath(svgClipPath, skBounds, uris, disposable);
            if (clipPathClipPath != null && !clipPathClipPath.IsEmpty)
            {
                skPathClip = clipPathClipPath;
            }

            var clipPath = GetClipPath(svgClipPath.Children, skBounds, uris, disposable);
            if (clipPath != null)
            {
                var skMatrix = SKMatrix.CreateIdentity();

                if (svgClipPath.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
                {
                    var skScaleMatrix = SKMatrix.CreateScale(skBounds.Width, skBounds.Height);
                    skMatrix = skMatrix.PostConcat(skScaleMatrix);

                    var skTranslateMatrix = SKMatrix.CreateTranslation(skBounds.Left, skBounds.Top);
                    skMatrix = skMatrix.PostConcat(skTranslateMatrix);
                }

                var skTransformsMatrix = ToSKMatrix(svgClipPath.Transforms);
                skMatrix = skMatrix.PostConcat(skTransformsMatrix);

                clipPath.Transform(skMatrix);

                if (skPathClip == null)
                {
                    skPathClip = clipPath;
                }
                else
                {
                    var result = skPathClip.Op(clipPath, SKPathOp.Intersect);
                    disposable.Add(result);
                    skPathClip = result;
                }
            }

            if (skPathClip == null)
            {
                skPathClip = new SKPath();
                disposable.Add(skPathClip);
            }

            return skPathClip;
        }

        public static SKPath? GetSvgVisualElementClipPath(SvgVisualElement svgVisualElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            if (svgVisualElement == null || svgVisualElement.ClipPath == null)
            {
                return null;
            }

            if (HasRecursiveReference(svgVisualElement, (e) => e.ClipPath, uris))
            {
                return null;
            }

            var svgClipPath = GetReference<SvgClipPath>(svgVisualElement, svgVisualElement.ClipPath);
            if (svgClipPath == null || svgClipPath.Children == null)
            {
                return null;
            }

            return GetClipPath(svgClipPath, skBounds, uris, disposable);
        }
#endif
        public static SKRect? GetClipRect(SvgVisualElement svgVisualElement, SKRect skRectBounds)
        {
            var clip = svgVisualElement.Clip;
            if (!string.IsNullOrEmpty(clip) && clip.StartsWith("rect("))
            {
                clip = clip.Trim();
                var offsets = new List<float>();
                foreach (var o in clip.Substring(5, clip.Length - 6).Split(','))
                {
                    offsets.Add(float.Parse(o.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture));
                }

                var skClipRect = SKRect.Create(
                    skRectBounds.Left + offsets[3],
                    skRectBounds.Top + offsets[0],
                    skRectBounds.Width - (offsets[3] + offsets[1]),
                    skRectBounds.Height - (offsets[2] + offsets[0]));
                return skClipRect;
            }
            return null;
        }

        public static MaskDrawable? GetSvgElementMask(SvgElement svgElement, SKRect skBounds, HashSet<Uri> uris, CompositeDisposable disposable)
        {
            var svgMaskRef = svgElement.GetUriElementReference<SvgMask>("mask", uris);
            if (svgMaskRef == null || svgMaskRef.Children == null)
            {
                return null;
            }
            var maskDrawable = MaskDrawable.Create(svgMaskRef, skBounds, null, Attributes.None);
            disposable.Add(maskDrawable);
            return maskDrawable;
        }

        public static void AddMarkers(this SvgGroup svgGroup)
        {
            Uri? marker = null;

            // TODO: The marker can not be set as presentation attribute.
            //if (svgGroup.TryGetAttribute("marker", out string markerUrl))
            //{
            //    marker = new Uri(markerUrl, UriKind.RelativeOrAbsolute);
            //}

            var groupMarkerStart = svgGroup.MarkerStart;
            var groupMarkerMid = svgGroup.MarkerMid;
            var groupMarkerEnd = svgGroup.MarkerEnd;

            if (groupMarkerStart == null && groupMarkerMid == null && groupMarkerEnd == null && marker == null)
            {
                return;
            }

            foreach (var svgElement in svgGroup.Children)
            {
                if (svgElement is SvgMarkerElement svgMarkerElement)
                {
                    if (svgMarkerElement.MarkerStart == null)
                    {
                        if (groupMarkerStart != null)
                        {
                            svgMarkerElement.MarkerStart = groupMarkerStart;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerStart = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerMid == null)
                    {
                        if (groupMarkerMid != null)
                        {
                            svgMarkerElement.MarkerMid = groupMarkerMid;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerMid = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerEnd == null)
                    {
                        if (groupMarkerEnd != null)
                        {
                            svgMarkerElement.MarkerEnd = groupMarkerEnd;
                        }
                        else if (marker != null)
                        {
                            svgMarkerElement.MarkerEnd = marker;
                        }
                    }
                }
            }
        }

        public static void CreateMarker(this SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, bool isStartMarker, SKRect skOwnerBounds, ref List<DrawableBase>? markerDrawables, CompositeDisposable disposable, Attributes ignoreAttributes = Attributes.None)
        {
            float fAngle1 = 0f;
            if (svgMarker.Orient.IsAuto)
            {
                float xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
                float yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
                fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
                if (isStartMarker && svgMarker.Orient.IsAutoStartReverse)
                {
                    fAngle1 += 180;
                }
            }

            var markerDrawable = MarkerDrawable.Create(svgMarker, pOwner, pRefPoint, fAngle1, skOwnerBounds, null, ignoreAttributes);
            if (markerDrawables == null)
            {
                markerDrawables = new List<DrawableBase>();
            }
            markerDrawables.Add(markerDrawable);
            disposable.Add(markerDrawable);
        }

        public static void CreateMarker(this SvgMarker svgMarker, SvgVisualElement pOwner, SKPoint pRefPoint, SKPoint pMarkerPoint1, SKPoint pMarkerPoint2, SKPoint pMarkerPoint3, SKRect skOwnerBounds, ref List<DrawableBase>? markerDrawables, CompositeDisposable disposable)
        {
            float xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
            float yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
            float fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            xDiff = pMarkerPoint3.X - pMarkerPoint2.X;
            yDiff = pMarkerPoint3.Y - pMarkerPoint2.Y;
            float fAngle2 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);

            var markerDrawable = MarkerDrawable.Create(svgMarker, pOwner, pRefPoint, (fAngle1 + fAngle2) / 2, skOwnerBounds, null);
            if (markerDrawables == null)
            {
                markerDrawables = new List<DrawableBase>();
            }
            markerDrawables.Add(markerDrawable);
            disposable.Add(markerDrawable);
        }

        public static void CreateMarkers(this SvgMarkerElement svgMarkerElement, SKPath skPath, SKRect skOwnerBounds, ref List<DrawableBase>? markerDrawables, CompositeDisposable disposable)
        {
            var pathTypes = skPath.GetPathTypes();
            var pathLength = pathTypes.Count;

            var markerStart = svgMarkerElement.MarkerStart;
            if (markerStart != null && pathLength > 0 && !HasRecursiveReference(svgMarkerElement, (e) => e.MarkerStart, new HashSet<Uri>()))
            {
                var marker = GetReference<SvgMarker>(svgMarkerElement, markerStart);
                if (marker != null)
                {
                    var refPoint1 = pathTypes[0].Point;
                    var index = 1;
                    while (index < pathLength && pathTypes[index].Point.X == refPoint1.X && pathTypes[index].Point.Y == refPoint1.Y)
                    {
                        ++index;
                    }
                    var refPoint2 = pathLength == 1 ? refPoint1 : pathTypes[index].Point;
                    CreateMarker(marker, svgMarkerElement, refPoint1, refPoint1, refPoint2, true, skOwnerBounds, ref markerDrawables, disposable);
                }
            }

            var markerMid = svgMarkerElement.MarkerMid;
            if (markerMid != null && pathLength > 0 && !HasRecursiveReference(svgMarkerElement, (e) => e.MarkerMid, new HashSet<Uri>()))
            {
                var marker = GetReference<SvgMarker>(svgMarkerElement, markerMid);
                if (marker != null)
                {
                    int bezierIndex = -1;
                    for (int i = 1; i <= pathLength - 2; i++)
                    {
                        // for Bezier curves, the marker shall only been shown at the last point
                        if ((pathTypes[i].Type & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Bezier)
                        {
                            bezierIndex = (bezierIndex + 1) % 3;
                        }
                        else
                        {
                            bezierIndex = -1;
                        }

                        if (bezierIndex == -1 || bezierIndex == 2)
                        {
                            CreateMarker(marker, svgMarkerElement, pathTypes[i].Point, pathTypes[i - 1].Point, pathTypes[i].Point, pathTypes[i + 1].Point, skOwnerBounds, ref markerDrawables, disposable);
                        }
                    }
                }
            }

            var markerEnd = svgMarkerElement.MarkerEnd;
            if (markerEnd != null && pathLength > 0 && !HasRecursiveReference(svgMarkerElement, (e) => e.MarkerEnd, new HashSet<Uri>()))
            {
                var marker = GetReference<SvgMarker>(svgMarkerElement, markerEnd);
                if (marker != null)
                {
                    var index = pathLength - 1;
                    var refPoint1 = pathTypes[index].Point;
                    if (pathLength > 1)
                    {
                        --index;
                        while (index > 0 && pathTypes[index].Point.X == refPoint1.X && pathTypes[index].Point.Y == refPoint1.Y)
                        {
                            --index;
                        }
                    }
                    var refPoint2 = pathLength == 1 ? refPoint1 : pathTypes[index].Point;
                    CreateMarker(marker, svgMarkerElement, refPoint1, refPoint2, pathTypes[pathLength - 1].Point, false, skOwnerBounds, ref markerDrawables, disposable);
                }
            }
        }

        public static SKBlendMode GetSKBlendMode(SvgBlendMode svgBlendMode)
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

        public static SKImageFilter? CreateBlend(SvgBlend svgBlend, SKImageFilter background, SKImageFilter? foreground = null, CropRect? cropRect = null)
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

        public static SKImageFilter? CreateColorMatrix(SvgColourMatrix svgColourMatrix, CompositeDisposable disposable, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            SKColorFilter skColorFilter;

            switch (svgColourMatrix.Type)
            {
                case SvgColourMatrixType.HueRotate:
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
                case SvgColourMatrixType.LuminanceToAlpha:
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
                case SvgColourMatrixType.Saturate:
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
                case SvgColourMatrixType.Matrix:
                    {
                        float[] matrix;
                        if (string.IsNullOrEmpty(svgColourMatrix.Values))
                        {
                            matrix = CreateIdentityColorMatrixArray();
                        }
                        else
                        {
                            var parts = svgColourMatrix.Values.Split(new char[] { ' ', '\t', '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 20)
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

        public static SKImageFilter? CreateComponentTransfer(SvgComponentTransfer svgComponentTransfer, CompositeDisposable disposable, SKImageFilter? input = null, CropRect? cropRect = null)
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

        public static SKImageFilter? CreateComposite(SvgComposite svgComposite, SKImageFilter background, SKImageFilter? foreground = null, CropRect? cropRect = null)
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

        public static SKImageFilter? CreateConvolveMatrix(SvgConvolveMatrix svgConvolveMatrix, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            GetOptionalNumbers(svgConvolveMatrix.Order, 3f, 3f, out var orderX, out var orderY);

            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                orderX *= skBounds.Width;
                orderY *= skBounds.Height;
            }

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
            float x = (float)(Math.Cos(azimuthRad) * Math.Cos(elevationRad));
            float y = (float)(Math.Sin(azimuthRad) * Math.Cos(elevationRad));
            float z = (float)Math.Sin(elevationRad);
            return new SKPoint3(x, y, z);
        }

        public static SKPoint3 GetPoint3(float x, float y, float z, SKRect skBounds, SvgCoordinateUnits primitiveUnits)
        {
            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                x *= skBounds.Width;
                y *= skBounds.Height;
                z *= CalculateOtherPercentageValue(skBounds);
            }
            return new SKPoint3(x, y, z);
        }

        public static SKImageFilter? CreateDiffuseLighting(SvgDiffuseLighting svgDiffuseLighting, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SvgVisualElement svgVisualElement, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            var lightColor = GetColor(svgVisualElement, svgDiffuseLighting.LightingColor);
            if (lightColor == null)
            {
                return null;
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
                        float specularExponentSpotLight = svgSpotLight.SpecularExponent;
                        float limitingConeAngle = svgSpotLight.LimitingConeAngle;
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

        public static SKImageFilter? CreateDisplacementMap(SvgDisplacementMap svgDisplacementMap, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter displacement, SKImageFilter? inout = null, CropRect? cropRect = null)
        {
            var xChannelSelector = GetSKDisplacementMapEffectChannelSelectorType(svgDisplacementMap.XChannelSelector);
            var yChannelSelector = GetSKDisplacementMapEffectChannelSelectorType(svgDisplacementMap.YChannelSelector);
            var scale = svgDisplacementMap.Scale;

            if (primitiveUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                scale *= CalculateOtherPercentageValue(skBounds);
            }

            return SKImageFilter.CreateDisplacementMapEffect(xChannelSelector, yChannelSelector, scale, displacement, inout, cropRect);
        }

        public static SKImageFilter? CreateFlood(SvgFlood svgFlood, SvgVisualElement svgVisualElement, SKRect skBounds, CompositeDisposable disposable, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            var floodColor = GetColor(svgVisualElement, svgFlood.FloodColor);
            if (floodColor == null)
            {
                return null;
            }

            var floodOpacity = svgFlood.FloodOpacity;
            var floodAlpha = CombineWithOpacity(floodColor.Value.Alpha, floodOpacity);
            floodColor = new SKColor(floodColor.Value.Red, floodColor.Value.Green, floodColor.Value.Blue, floodAlpha);

            if (cropRect == null)
            {
                cropRect = new CropRect(skBounds);
            }

            var cf = SKColorFilter.CreateBlendMode(floodColor.Value, SKBlendMode.Src);
            disposable.Add(cf);

            return SKImageFilter.CreateColorFilter(cf, input, cropRect);
        }

        public static SKImageFilter? CreateBlur(SvgGaussianBlur svgGaussianBlur, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter? input = null, CropRect? cropRect = null)
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
                return null;
            }

            return SKImageFilter.CreateBlur(sigmaX, sigmaY, input, cropRect);
        }

        public static SKImageFilter? CreateImage(FilterEffects.SvgImage svgImage, SKRect skBounds, CompositeDisposable disposable, CropRect? cropRect = null)
        {
            var image = GetImage(svgImage.Href, svgImage.OwnerDocument);
            var skImage = image as SKImage;
            var svgFragment = image as SvgFragment;
            if (skImage == null && svgFragment == null)
            {
                return null;
            }

            var destClip = skBounds;

            var srcRect = default(SKRect);
            var destRect = default(SKRect);

            if (skImage != null)
            {
                srcRect = SKRect.Create(0f, 0f, skImage.Width, skImage.Height);
            }

            if (svgFragment != null)
            {
                var skSize = GetDimensions(svgFragment);
                srcRect = SKRect.Create(0f, 0f, skSize.Width, skSize.Height);
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
                        xOffset = (destClip.Width - srcRect.Width * fScaleX);
                        break;
                    case SvgPreserveAspectRatio.xMinYMid:
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMidYMid:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMaxYMid:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX);
                        yOffset = (destClip.Height - srcRect.Height * fScaleY) / 2;
                        break;
                    case SvgPreserveAspectRatio.xMinYMax:
                        yOffset = (destClip.Height - srcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMidYMax:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX) / 2;
                        yOffset = (destClip.Height - srcRect.Height * fScaleY);
                        break;
                    case SvgPreserveAspectRatio.xMaxYMax:
                        xOffset = (destClip.Width - srcRect.Width * fScaleX);
                        yOffset = (destClip.Height - srcRect.Height * fScaleY);
                        break;
                }

                destRect = SKRect.Create(
                    destClip.Left + xOffset,
                    destClip.Top + yOffset,
                    srcRect.Width * fScaleX,
                    srcRect.Height * fScaleY);
            }
            else
            {
                destRect = destClip;
            }

            if (skImage != null)
            {
                disposable.Add(skImage);
                return SKImageFilter.CreateImage(skImage, srcRect, destRect, SKFilterQuality.High);
            }

            if (svgFragment != null)
            {
                var fragmentTransform = SKMatrix.CreateIdentity();
                float dx = destRect.Left;
                float dy = destRect.Top;
                float sx = destRect.Width / srcRect.Width;
                float sy = destRect.Height / srcRect.Height;
                var skTranslationMatrix = SKMatrix.CreateTranslation(dx, dy);
                var skScaleMatrix = SKMatrix.CreateScale(sx, sy);
                fragmentTransform = fragmentTransform.PreConcat(skTranslationMatrix);
                fragmentTransform = fragmentTransform.PreConcat(skScaleMatrix);

                using var fragmentDrawable = FragmentDrawable.Create(svgFragment, destRect, null, Attributes.None);
                var skPicture = fragmentDrawable.Snapshot(); // TODO:
                disposable.Add(skPicture);

                return SKImageFilter.CreatePicture(skPicture, destRect);
            }

            return null;
        }

        public static SKImageFilter? CreateMerge(SvgMerge svgMerge, Dictionary<string, SKImageFilter> results, SKImageFilter? lastResult, IFilterSource filterSource, CompositeDisposable disposable, CropRect? cropRect = null)
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

        public static SKImageFilter? CreateMorphology(SvgMorphology svgMorphology, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter? input = null, CropRect? cropRect = null)
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
                return null;
            }

            return svgMorphology.Operator switch
            {
                SvgMorphologyOperator.Dilate => SKImageFilter.CreateDilate((int)radiusX, (int)radiusY, input, cropRect),
                SvgMorphologyOperator.Erode => SKImageFilter.CreateErode((int)radiusX, (int)radiusY, input, cropRect),
                _ => null,
            };
        }

        public static SKImageFilter? CreateOffset(SvgOffset svgOffset, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SKImageFilter? input = null, CropRect? cropRect = null)
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
                }

                if (dyUnit.Type != SvgUnitType.Percentage)
                {
                    dy *= skBounds.Height;
                }
            }

            return SKImageFilter.CreateOffset(dx, dy, input, cropRect);
        }

        public static SKImageFilter? CreateSpecularLighting(SvgSpecularLighting svgSpecularLighting, SKRect skBounds, SvgCoordinateUnits primitiveUnits, SvgVisualElement svgVisualElement, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            var lightColor = GetColor(svgVisualElement, svgSpecularLighting.LightingColor);
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
                        var location = GetPoint3(svgPointLight.X, svgPointLight.Y, svgPointLight.Z, skBounds, primitiveUnits);
                        return SKImageFilter.CreatePointLitSpecular(location, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                    }
                case SvgSpotLight svgSpotLight:
                    {
                        var location = GetPoint3(svgSpotLight.X, svgSpotLight.Y, svgSpotLight.Z, skBounds, primitiveUnits);
                        var target = GetPoint3(svgSpotLight.PointsAtX, svgSpotLight.PointsAtY, svgSpotLight.PointsAtZ, skBounds, primitiveUnits);
                        float specularExponentSpotLight = svgSpotLight.SpecularExponent;
                        float limitingConeAngle = svgSpotLight.LimitingConeAngle;
                        if (float.IsNaN(limitingConeAngle) || limitingConeAngle > 90f || limitingConeAngle < -90f)
                        {
                            limitingConeAngle = 90f;
                        }
                        return SKImageFilter.CreateSpotLitSpecular(location, target, specularExponentSpotLight, limitingConeAngle, lightColor.Value, surfaceScale, specularConstant, specularExponent, input, cropRect);
                    }
            }
            return null;
        }

        public static SKImageFilter? CreateTile(SvgTile svgTile, SKRect skBounds, SKImageFilter? input = null, CropRect? cropRect = null)
        {
            var src = skBounds;
            var dst = cropRect != null ? cropRect.Rect : skBounds;
            return SKImageFilter.CreateTile(src, dst, input);
        }

        public static SKImageFilter? CreateTurbulence(SvgTurbulence svgTurbulence, SKRect skBounds, SvgCoordinateUnits primitiveUnits, CompositeDisposable disposable, CropRect? cropRect = null)
        {
            GetOptionalNumbers(svgTurbulence.BaseFrequency, 0f, 0f, out var baseFrequencyX, out var baseFrequencyY);

            if (baseFrequencyX < 0f || baseFrequencyY < 0f)
            {
                return null;
            }

            var numOctaves = svgTurbulence.NumOctaves;

            if (numOctaves < 0)
            {
                return null;
            }

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
                cropRect = new CropRect(skBounds);
            }

            return SKImageFilter.CreatePaint(skPaint, cropRect);
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
                Color = s_transparentBlack
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
                Color = s_transparentBlack
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

        public static SKImageFilter? GetFilterResult(SvgFilterPrimitive svgFilterPrimitive, SKImageFilter? skImageFilter, Dictionary<string, SKImageFilter> results, CompositeDisposable disposable)
        {
            if (skImageFilter != null)
            {
                var key = svgFilterPrimitive.Result;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    results[key] = skImageFilter;
                }
                disposable.Add(skImageFilter);
                return skImageFilter;
            }
            return null;
        }

        private static List<SvgFilter>? GetLinkedFilter(SvgVisualElement svgVisualElement, HashSet<Uri> uris)
        {
            var currentFilter = GetReference<SvgFilter>(svgVisualElement, svgVisualElement.Filter);
            if (currentFilter == null)
            {
                return null;
            }

            var svgFilters = new List<SvgFilter>();
            do
            {
                if (currentFilter != null)
                {
                    svgFilters.Add(currentFilter);
                    if (HasRecursiveReference(currentFilter, (e) => e.Href, uris))
                    {
                        return svgFilters;
                    }
                    currentFilter = GetReference<SvgFilter>(currentFilter, currentFilter.Href);
                }
            } while (currentFilter != null);

            return svgFilters;
        }

        public static SKPaint? GetFilterSKPaint(SvgVisualElement svgVisualElement, SKRect skBounds, IFilterSource filterSource, CompositeDisposable disposable, out bool isValid)
        {
            var filter = svgVisualElement.Filter;
            if (filter == null || IsNone(filter))
            {
                isValid = true;
                return null;
            }

            var svgReferencedFilters = GetLinkedFilter(svgVisualElement, new HashSet<Uri>());
            if (svgReferencedFilters == null || svgReferencedFilters.Count < 0)
            {
                isValid = false;
                return null;
            }

            var svgFirstFilter = svgReferencedFilters[0];

            SvgFilter? firstChildren = null;
            SvgFilter? firstX = null;
            SvgFilter? firstY = null;
            SvgFilter? firstWidth = null;
            SvgFilter? firstHeight = null;
            SvgFilter? firstFilterUnits = null;
            SvgFilter? firstPrimitiveUnits = null;

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
                return null;
            }

            var xUnit = firstX == null ? new SvgUnit(SvgUnitType.Percentage, -10f) : firstX.X;
            var yUnit = firstY == null ? new SvgUnit(SvgUnitType.Percentage, -10f) : firstY.Y;
            var widthUnit = firstWidth == null ? new SvgUnit(SvgUnitType.Percentage, 120f) : firstWidth.Width;
            var heightUnit = firstHeight == null ? new SvgUnit(SvgUnitType.Percentage, 120f) : firstHeight.Height;
            var filterUnits = firstFilterUnits == null ? SvgCoordinateUnits.ObjectBoundingBox : firstFilterUnits.FilterUnits;
            var primitiveUnits = firstPrimitiveUnits == null ? SvgCoordinateUnits.UserSpaceOnUse : firstPrimitiveUnits.FilterUnits;

            float x = xUnit.ToDeviceValue(UnitRenderingType.HorizontalOffset, svgFirstFilter, skBounds);
            float y = yUnit.ToDeviceValue(UnitRenderingType.VerticalOffset, svgFirstFilter, skBounds);
            float width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgFirstFilter, skBounds);
            float height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgFirstFilter, skBounds);

            var results = new Dictionary<string, SKImageFilter>();
            var lastResult = default(SKImageFilter);
            var prevoiusFilterPrimitiveRegion = SKRect.Empty;

            if (width <= 0f || height <= 0f)
            {
                isValid = false;
                return null;
            }

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

            var svgFilterPrimitives = new List<SvgFilterPrimitive>();
            foreach (var child in firstChildren.Children)
            {
                if (child is SvgFilterPrimitive svgFilterPrimitive)
                {
                    svgFilterPrimitives.Add(svgFilterPrimitive);
                }
            }

#if DEBUG
            var skFilterPrimitiveRegions = new List<(SvgFilterPrimitive primitive, SKRect region)>();
            var skImageFilterRegions = new List<(SKImageFilter filter, SvgFilterPrimitive primitive, SKRect region)>();
#endif

            int count = 0;
            foreach (var svgFilterPrimitive in svgFilterPrimitives)
            {
                count++;
                bool isFirst = count == 1;
                var skPrimitiveBounds = skFilterRegion;

                // TOOD: PrimitiveUnits
                //if (primitiveUnits == SvgCoordinateUnits.UserSpaceOnUse)
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

                var skCropRect = new CropRect(skFilterPrimitiveRegion);
#if DEBUG
                skFilterPrimitiveRegions.Add((svgFilterPrimitive, skFilterPrimitiveRegion));
#endif

                switch (svgFilterPrimitive)
                {
                    case SvgBlend svgBlend:
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
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgColourMatrix svgColourMatrix:
                        {
                            var inputKey = svgColourMatrix.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                            var skImageFilter = CreateColorMatrix(svgColourMatrix, disposable, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgComponentTransfer svgComponentTransfer:
                        {
                            var inputKey = svgComponentTransfer.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                            var skImageFilter = CreateComponentTransfer(svgComponentTransfer, disposable, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgComposite svgComposite:
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
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgConvolveMatrix svgConvolveMatrix:
                        {
                            var inputKey = svgConvolveMatrix.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                            var skImageFilter = CreateConvolveMatrix(svgConvolveMatrix, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgDiffuseLighting svgDiffuseLighting:
                        {
                            var inputKey = svgDiffuseLighting.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                            var skImageFilter = CreateDiffuseLighting(svgDiffuseLighting, skFilterPrimitiveRegion, primitiveUnits, svgVisualElement, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgDisplacementMap svgDisplacementMap:
                        {
                            var input1Key = svgDisplacementMap.Input;
                            var input1Filter = GetInputFilter(input1Key, results, lastResult, filterSource, disposable, isFirst);
                            var input2Key = svgDisplacementMap.Input2;
                            var input2Filter = GetInputFilter(input2Key, results, lastResult, filterSource, disposable, false);
                            if (input2Filter == null)
                            {
                                break;
                            }
                            var skImageFilter = CreateDisplacementMap(svgDisplacementMap, skFilterPrimitiveRegion, primitiveUnits, input2Filter, input1Filter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgFlood svgFlood:
                        {
                            var skImageFilter = CreateFlood(svgFlood, svgVisualElement, skFilterPrimitiveRegion, disposable, null, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgGaussianBlur svgGaussianBlur:
                        {
                            var inputKey = svgGaussianBlur.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                            var skImageFilter = CreateBlur(svgGaussianBlur, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case FilterEffects.SvgImage svgImage:
                        {
                            var skImageFilter = CreateImage(svgImage, skFilterPrimitiveRegion, disposable, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgMerge svgMerge:
                        {
                            var skImageFilter = CreateMerge(svgMerge, results, lastResult, filterSource, disposable, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgMorphology svgMorphology:
                        {
                            var inputKey = svgMorphology.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                            var skImageFilter = CreateMorphology(svgMorphology, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgOffset svgOffset:
                        {
                            var inputKey = svgOffset.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                            var skImageFilter = CreateOffset(svgOffset, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgSpecularLighting svgSpecularLighting:
                        {
                            var inputKey = svgSpecularLighting.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                            var skImageFilter = CreateSpecularLighting(svgSpecularLighting, skFilterPrimitiveRegion, primitiveUnits, svgVisualElement, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgTile svgTile:
                        {
                            var inputKey = svgTile.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, disposable, isFirst);
                            var skImageFilter = CreateTile(svgTile, prevoiusFilterPrimitiveRegion, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    case SvgTurbulence svgTurbulence:
                        {
                            var skImageFilter = CreateTurbulence(svgTurbulence, skFilterPrimitiveRegion, primitiveUnits, disposable, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results, disposable);
#if DEBUG
                            if (lastResult != null)
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;
                    default:
                        break;
                }

                prevoiusFilterPrimitiveRegion = skFilterPrimitiveRegion;
            }

            if (lastResult != null)
            {
                var skPaint = new SKPaint
                {
                    Style = SKPaintStyle.StrokeAndFill
                };
                skPaint.ImageFilter = lastResult;
                disposable.Add(skPaint);

                isValid = true;
                return skPaint;
            }

            isValid = false;
            return null;
        }

        public static SKFontStyleWeight ToSKFontStyleWeight(SvgFontWeight svgFontWeight)
        {
            var fontWeight = SKFontStyleWeight.Normal;

            switch (svgFontWeight)
            {
                case SvgFontWeight.Inherit:
                    // TODO: Implement SvgFontWeight.Inherit
                    break;
                case SvgFontWeight.Bolder:
                    // TODO: Implement SvgFontWeight.Bolder
                    break;
                case SvgFontWeight.Lighter:
                    // TODO: Implement SvgFontWeight.Lighter
                    break;
                case SvgFontWeight.W100:
                    fontWeight = SKFontStyleWeight.Thin;
                    break;
                case SvgFontWeight.W200:
                    fontWeight = SKFontStyleWeight.ExtraLight;
                    break;
                case SvgFontWeight.W300:
                    fontWeight = SKFontStyleWeight.Light;
                    break;
                case SvgFontWeight.W400: // SvgFontWeight.Normal:
                    fontWeight = SKFontStyleWeight.Normal;
                    break;
                case SvgFontWeight.W500:
                    fontWeight = SKFontStyleWeight.Medium;
                    break;
                case SvgFontWeight.W600:
                    fontWeight = SKFontStyleWeight.SemiBold;
                    break;
                case SvgFontWeight.W700: // SvgFontWeight.Bold:
                    fontWeight = SKFontStyleWeight.Bold;
                    break;
                case SvgFontWeight.W800:
                    fontWeight = SKFontStyleWeight.ExtraBold;
                    break;
                case SvgFontWeight.W900:
                    fontWeight = SKFontStyleWeight.Black;
                    break;
            }

            return fontWeight;
        }

        public static SKFontStyleWidth ToSKFontStyleWidth(SvgFontStretch svgFontStretch)
        {
            var fontWidth = SKFontStyleWidth.Normal;

            switch (svgFontStretch)
            {
                case SvgFontStretch.Inherit:
                    // TODO: Implement SvgFontStretch.Inherit
                    break;
                case SvgFontStretch.Normal:
                    fontWidth = SKFontStyleWidth.Normal;
                    break;
                case SvgFontStretch.Wider:
                    // TODO: Implement SvgFontStretch.Wider
                    break;
                case SvgFontStretch.Narrower:
                    // TODO: Implement SvgFontStretch.Narrower
                    break;
                case SvgFontStretch.UltraCondensed:
                    fontWidth = SKFontStyleWidth.UltraCondensed;
                    break;
                case SvgFontStretch.ExtraCondensed:
                    fontWidth = SKFontStyleWidth.ExtraCondensed;
                    break;
                case SvgFontStretch.Condensed:
                    fontWidth = SKFontStyleWidth.Condensed;
                    break;
                case SvgFontStretch.SemiCondensed:
                    fontWidth = SKFontStyleWidth.SemiCondensed;
                    break;
                case SvgFontStretch.SemiExpanded:
                    fontWidth = SKFontStyleWidth.SemiExpanded;
                    break;
                case SvgFontStretch.Expanded:
                    fontWidth = SKFontStyleWidth.Expanded;
                    break;
                case SvgFontStretch.ExtraExpanded:
                    fontWidth = SKFontStyleWidth.ExtraExpanded;
                    break;
                case SvgFontStretch.UltraExpanded:
                    fontWidth = SKFontStyleWidth.UltraExpanded;
                    break;
            }

            return fontWidth;
        }

        public static SKTextAlign ToSKTextAlign(SvgTextAnchor textAnchor)
        {
            return textAnchor switch
            {
                SvgTextAnchor.Middle => SKTextAlign.Center,
                SvgTextAnchor.End => SKTextAlign.Right,
                _ => SKTextAlign.Left,
            };
        }

        public static SKFontStyleSlant ToSKFontStyleSlant(SvgFontStyle fontStyle)
        {
            return fontStyle switch
            {
                SvgFontStyle.Oblique => SKFontStyleSlant.Oblique,
                SvgFontStyle.Italic => SKFontStyleSlant.Italic,
                _ => SKFontStyleSlant.Upright,
            };
        }

        private static void SetTypeface(SvgTextBase svgText, SKPaint skPaint, CompositeDisposable disposable)
        {
            var fontFamily = svgText.FontFamily;
            var fontWeight = ToSKFontStyleWeight(svgText.FontWeight);
            var fontWidth = ToSKFontStyleWidth(svgText.FontStretch);
            var fontStyle = ToSKFontStyleSlant(svgText.FontStyle);

#if USE_PICTURE
            // TODO:
            skPaint.Typeface = new Svg.Picture.Typeface()
            {
                FamilyName = fontFamily,
                Weight = fontWeight,
                Width = fontWidth,
                Style = fontStyle
            };
#else
            if (SKSvgSettings.s_typefaceProviders == null || SKSvgSettings.s_typefaceProviders.Count <= 0)
            {
                var skTypeface = SKTypeface.FromFamilyName(fontFamily, fontWeight, fontWidth, fontStyle);
                disposable.Add(skTypeface);
                skPaint.Typeface = skTypeface;
                return;
            }

            foreach (var typefaceProviders in SKSvgSettings.s_typefaceProviders)
            {
                var skTypeface = typefaceProviders.FromFamilyName(fontFamily, fontWeight, fontWidth, fontStyle);
                if (skTypeface != null)
                {
                    disposable.Add(skTypeface);
                    skPaint.Typeface = skTypeface;
                    break;
                }
            }
#endif
        }

        public static void SetSKPaintText(SvgTextBase svgText, SKRect skBounds, SKPaint skPaint, CompositeDisposable disposable)
        {
            skPaint.LcdRenderText = true;
            skPaint.SubpixelText = true;
            skPaint.TextEncoding = SKTextEncoding.Utf16;

            skPaint.TextAlign = ToSKTextAlign(svgText.TextAnchor);

            if (svgText.TextDecoration.HasFlag(SvgTextDecoration.Underline))
            {
                // TODO: Implement SvgTextDecoration.Underline
            }

            if (svgText.TextDecoration.HasFlag(SvgTextDecoration.Overline))
            {
                // TODO: Implement SvgTextDecoration.Overline
            }

            if (svgText.TextDecoration.HasFlag(SvgTextDecoration.LineThrough))
            {
                // TODO: Implement SvgTextDecoration.LineThrough
            }

            float fontSize;
            var fontSizeUnit = svgText.FontSize;
            if (fontSizeUnit == SvgUnit.None || fontSizeUnit == SvgUnit.Empty)
            {
                // TODO: Do not use implicit float conversion from SvgUnit.ToDeviceValue
                //fontSize = new SvgUnit(SvgUnitType.Em, 1.0f);
                // NOTE: Use default SkPaint Font_Size
                fontSize = 12f;
            }
            else
            {
                fontSize = fontSizeUnit.ToDeviceValue(UnitRenderingType.Vertical, svgText, skBounds);
            }

            skPaint.TextSize = fontSize;

            SetTypeface(svgText, skPaint, disposable);
        }
    }

    [Flags]
    internal enum Attributes
    {
        None = 0,
        Display = 1,
        Visibility = 2,
        Opacity = 4,
        Filter = 8,
        ClipPath = 16,
        Mask = 32,
        RequiredFeatures = 64,
        RequiredExtensions = 128,
        SystemLanguage = 256
    }

    internal interface IFilterSource
    {
        SKPicture? SourceGraphic();
        SKPicture? BackgroundImage();
        SKPaint? FillPaint();
        SKPaint? StrokePaint();
    }

    internal interface IPictureSource
    {
        void OnDraw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until);
        void Draw(SKCanvas canvas, Attributes ignoreAttributes, DrawableBase? until);
    }

    internal sealed class AnchorDrawable : DrawableContainer
    {
        private AnchorDrawable()
            : base()
        {
        }

        public static AnchorDrawable Create(SvgAnchor svgAnchor, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new AnchorDrawable
            {
                Element = svgAnchor,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes,
                IsDrawable = true
            };

            drawable.CreateChildren(svgAnchor, skOwnerBounds, drawable, ignoreAttributes);

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgAnchor);

            drawable.TransformedBounds = SKRect.Empty;

            drawable.CreateTransformedBounds();

            drawable.Transform = SvgExtensions.ToSKMatrix(svgAnchor.Transforms);

            drawable.Fill = null;
            drawable.Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            drawable.ClipPath = null;
            drawable.MaskDrawable = null;
            drawable.Opacity = drawable.IgnoreAttributes.HasFlag(Attributes.Opacity) ? null : SvgExtensions.GetOpacitySKPaint(svgAnchor, drawable.Disposable);
            drawable.Filter = null;

            return drawable;
        }

        public override void PostProcess()
        {
            var element = Element;
            if (element is null)
            {
                return;
            }

            var enableOpacity = !IgnoreAttributes.HasFlag(Attributes.Opacity);

            ClipPath = null;
            MaskDrawable = null;
            Opacity = enableOpacity ? SvgExtensions.GetOpacitySKPaint(element, Disposable) : null;
            Filter = null;

            foreach (var child in ChildrenDrawables)
            {
                child.PostProcess();
            }
        }
    }

    internal sealed class CircleDrawable : DrawablePath
    {
        private CircleDrawable()
            : base()
        {
        }

        public static CircleDrawable Create(SvgCircle svgCircle, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new CircleDrawable
            {
                Element = svgCircle,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgCircle, drawable.IgnoreAttributes) && drawable.HasFeatures(svgCircle, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgCircle.ToSKPath(svgCircle.FillRule, skOwnerBounds, drawable.Disposable);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgCircle);

            drawable.TransformedBounds = drawable.Path.Bounds;

            drawable.Transform = SvgExtensions.ToSKMatrix(svgCircle.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgCircle))
            {
                drawable.Fill = SvgExtensions.GetFillSKPaint(svgCircle, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgCircle, drawable.TransformedBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokeSKPaint(svgCircle, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Stroke is null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }
    }

    internal sealed class RectangleDrawable : DrawablePath
    {
        private RectangleDrawable()
            : base()
        {
        }

        public static RectangleDrawable Create(SvgRectangle svgRectangle, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new RectangleDrawable
            {
                Element = svgRectangle,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgRectangle, drawable.IgnoreAttributes) && drawable.HasFeatures(svgRectangle, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgRectangle.ToSKPath(svgRectangle.FillRule, skOwnerBounds, drawable.Disposable);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgRectangle);

            drawable.TransformedBounds = drawable.Path.Bounds;

            drawable.Transform = SvgExtensions.ToSKMatrix(svgRectangle.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgRectangle))
            {
                drawable.Fill = SvgExtensions.GetFillSKPaint(svgRectangle, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgRectangle, drawable.TransformedBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokeSKPaint(svgRectangle, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Stroke is null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }
    }

    internal sealed class GroupDrawable : DrawableContainer
    {
        private GroupDrawable()
            : base()
        {
        }

        public static GroupDrawable Create(SvgGroup svgGroup, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new GroupDrawable
            {
                Element = svgGroup,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgGroup, drawable.IgnoreAttributes) && drawable.HasFeatures(svgGroup, drawable.IgnoreAttributes);

            // NOTE: Call AddMarkers only once.
            SvgExtensions.AddMarkers(svgGroup);

            drawable.CreateChildren(svgGroup, skOwnerBounds, drawable, ignoreAttributes);

            // TODO: Check if children are explicitly set to be visible.
            //foreach (var child in drawable.ChildrenDrawables)
            //{
            //    if (child.IsDrawable)
            //    {
            //        IsDrawable = true;
            //        break;
            //    }
            //}

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgGroup);

            drawable.TransformedBounds = SKRect.Empty;

            drawable.CreateTransformedBounds();

            drawable.Transform = SvgExtensions.ToSKMatrix(svgGroup.Transforms);

            drawable.Fill = null;
            drawable.Stroke = null;

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }
    }

    internal sealed class PathDrawable : DrawablePath
    {
        private PathDrawable()
            : base()
        {
        }

        public static PathDrawable Create(SvgPath svgPath, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new PathDrawable
            {
                Element = svgPath,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgPath, drawable.IgnoreAttributes) && drawable.HasFeatures(svgPath, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgPath.PathData?.ToSKPath(svgPath.FillRule, drawable.Disposable);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgPath);

            drawable.TransformedBounds = drawable.Path.Bounds;

            drawable.Transform = SvgExtensions.ToSKMatrix(svgPath.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgPath))
            {
                drawable.Fill = SvgExtensions.GetFillSKPaint(svgPath, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgPath, drawable.TransformedBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokeSKPaint(svgPath, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Stroke is null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            SvgExtensions.CreateMarkers(svgPath, drawable.Path, skOwnerBounds, ref drawable.MarkerDrawables, drawable.Disposable);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }
    }

    internal sealed class PolylineDrawable : DrawablePath
    {
        private PolylineDrawable()
            : base()
        {
        }

        public static PolylineDrawable Create(SvgPolyline svgPolyline, SKRect skOwnerBounds, DrawableBase? parent, Attributes ignoreAttributes = Attributes.None)
        {
            var drawable = new PolylineDrawable
            {
                Element = svgPolyline,
                Parent = parent,
                IgnoreAttributes = ignoreAttributes
            };

            drawable.IsDrawable = drawable.CanDraw(svgPolyline, drawable.IgnoreAttributes) && drawable.HasFeatures(svgPolyline, drawable.IgnoreAttributes);

            if (!drawable.IsDrawable)
            {
                return drawable;
            }

            drawable.Path = svgPolyline.Points?.ToSKPath(svgPolyline.FillRule, false, skOwnerBounds, drawable.Disposable);
            if (drawable.Path is null || drawable.Path.IsEmpty)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            drawable.IsAntialias = SvgExtensions.IsAntialias(svgPolyline);

            drawable.TransformedBounds = drawable.Path.Bounds;

            drawable.Transform = SvgExtensions.ToSKMatrix(svgPolyline.Transforms);

            bool canDrawFill = true;
            bool canDrawStroke = true;

            if (SvgExtensions.IsValidFill(svgPolyline))
            {
                drawable.Fill = SvgExtensions.GetFillSKPaint(svgPolyline, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Fill is null)
                {
                    canDrawFill = false;
                }
            }

            if (SvgExtensions.IsValidStroke(svgPolyline, drawable.TransformedBounds))
            {
                drawable.Stroke = SvgExtensions.GetStrokeSKPaint(svgPolyline, drawable.TransformedBounds, ignoreAttributes, drawable.Disposable);
                if (drawable.Stroke is null)
                {
                    canDrawStroke = false;
                }
            }

            if (canDrawFill && !canDrawStroke)
            {
                drawable.IsDrawable = false;
                return drawable;
            }

            SvgExtensions.CreateMarkers(svgPolyline, drawable.Path, skOwnerBounds, ref drawable.MarkerDrawables, drawable.Disposable);

            // TODO: Transform _skBounds using _skMatrix.
            drawable.TransformedBounds = drawable.Transform.MapRect(drawable.TransformedBounds);

            return drawable;
        }
    }
}
