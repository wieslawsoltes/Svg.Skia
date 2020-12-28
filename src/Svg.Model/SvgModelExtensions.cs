using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using Svg.DataTypes;
using Svg.Document_Structure;
using Svg.FilterEffects;
using Svg.Model.Drawables;
using Svg.Model.ImageFilters;
using Svg.Model.Paint;
using Svg.Model.Path;
using Svg.Model.Path.Commands;
using Svg.Model.Picture;
using Svg.Model.Primitives;
using Svg.Model.Shaders;
using Svg.Pathing;
using Svg.Transforms;

namespace Svg.Model
{
    public static class SvgModelExtensions
    {
        private static readonly char[] _spaceTab = { ' ', '\t' };

        private static readonly char[] _comma = { ',' };

        [Flags]
        internal enum PathPointType : byte
        {
            Start = 0,
            Line = 1,
            Bezier = 3,
            Bezier3 = 3,
            PathTypeMask = 0x7,
            DashMode = 0x10,
            PathMarker = 0x20,
            CloseSubpath = 0x80
        }

        internal static HashSet<string> s_supportedFeatures = new HashSet<string>
        {
            "http://www.w3.org/TR/SVG11/feature#SVG",
            "http://www.w3.org/TR/SVG11/feature#SVGDOM",
            "http://www.w3.org/TR/SVG11/feature#SVG-static",
            "http://www.w3.org/TR/SVG11/feature#SVGDOM-static",
            "http://www.w3.org/TR/SVG11/feature#SVG-animation",
            "http://www.w3.org/TR/SVG11/feature#SVGDOM-animation",
            "http://www.w3.org/TR/SVG11/feature#SVG-dynamic",
            "http://www.w3.org/TR/SVG11/feature#SVGDOM-dynamic",
            "http://www.w3.org/TR/SVG11/feature#CoreAttribute",
            "http://www.w3.org/TR/SVG11/feature#Structure",
            "http://www.w3.org/TR/SVG11/feature#BasicStructure",
            "http://www.w3.org/TR/SVG11/feature#ContainerAttribute",
            "http://www.w3.org/TR/SVG11/feature#ConditionalProcessing",
            "http://www.w3.org/TR/SVG11/feature#Image",
            "http://www.w3.org/TR/SVG11/feature#Style",
            "http://www.w3.org/TR/SVG11/feature#ViewportAttribute",
            "http://www.w3.org/TR/SVG11/feature#Shape",
            "http://www.w3.org/TR/SVG11/feature#Text",
            "http://www.w3.org/TR/SVG11/feature#BasicText",
            "http://www.w3.org/TR/SVG11/feature#PaintAttribute",
            "http://www.w3.org/TR/SVG11/feature#BasicPaintAttribute",
            "http://www.w3.org/TR/SVG11/feature#OpacityAttribute",
            "http://www.w3.org/TR/SVG11/feature#GraphicsAttribute",
            "http://www.w3.org/TR/SVG11/feature#BasicGraphicsAttribute",
            "http://www.w3.org/TR/SVG11/feature#Marker",
            "http://www.w3.org/TR/SVG11/feature#ColorProfile",
            "http://www.w3.org/TR/SVG11/feature#Gradient",
            "http://www.w3.org/TR/SVG11/feature#Pattern",
            "http://www.w3.org/TR/SVG11/feature#Clip",
            "http://www.w3.org/TR/SVG11/feature#BasicClip",
            "http://www.w3.org/TR/SVG11/feature#Mask",
            "http://www.w3.org/TR/SVG11/feature#Filter",
            "http://www.w3.org/TR/SVG11/feature#BasicFilter",
            "http://www.w3.org/TR/SVG11/feature#DocumentEventsAttribute",
            "http://www.w3.org/TR/SVG11/feature#GraphicalEventsAttribute",
            "http://www.w3.org/TR/SVG11/feature#AnimationEventsAttribute",
            "http://www.w3.org/TR/SVG11/feature#Cursor",
            "http://www.w3.org/TR/SVG11/feature#Hyperlinking",
            "http://www.w3.org/TR/SVG11/feature#XlinkAttribute",
            "http://www.w3.org/TR/SVG11/feature#ExternalResourcesRequired",
            "http://www.w3.org/TR/SVG11/feature#View",
            "http://www.w3.org/TR/SVG11/feature#Script",
            "http://www.w3.org/TR/SVG11/feature#Animation",
            "http://www.w3.org/TR/SVG11/feature#Font",
            "http://www.w3.org/TR/SVG11/feature#BasicFont",
            "http://www.w3.org/TR/SVG11/feature#Extensibility"
        };

        internal static HashSet<string> s_supportedExtensions = new HashSet<string>
        {
        };

        internal static Color s_transparentBlack = new Color(0, 0, 0, 255);

        private const string MimeTypeSvg = "image/svg+xml";

        private static byte[] s_gZipMagicHeaderBytes => new byte[2] { 0x1f, 0x8b };

        internal const string SourceGraphic = "SourceGraphic";

        internal const string SourceAlpha = "SourceAlpha";

        internal const string BackgroundImage = "BackgroundImage";

        internal const string BackgroundAlpha = "BackgroundAlpha";

        internal const string FillPaint = "FillPaint";

        internal const string StrokePaint = "StrokePaint";

        internal static SvgFuncA s_identitySvgFuncA = new SvgFuncA
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        internal static SvgFuncR s_identitySvgFuncR = new SvgFuncR
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        internal static SvgFuncG s_identitySvgFuncG = new SvgFuncG
        {
            Type = SvgComponentTransferType.Identity,
            TableValues = new SvgNumberCollection()
        };

        internal static SvgFuncB s_identitySvgFuncB = new SvgFuncB
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

        internal static void GetOptionalNumbers(this SvgNumberCollection svgNumberCollection, float defaultValue1, float defaultValue2, out float value1, out float value2)
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

        internal static float CalculateOtherPercentageValue(this Rect skBounds)
        {
            return (float)(Math.Sqrt((skBounds.Width * skBounds.Width) + (skBounds.Width * skBounds.Height)) / Math.Sqrt(2.0));
        }

        internal static float ToDeviceValue(this SvgUnit svgUnit, UnitRenderingType renderType, SvgElement? owner, Rect skBounds)
        {
            const float cmInInch = 2.54f;
            var ppi = SvgDocument.PointsPerInch;
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

        internal static SvgUnit Normalize(this SvgUnit svgUnit, SvgCoordinateUnits svgCoordinateUnits)
        {
            return svgUnit.Type == SvgUnitType.Percentage
                && svgCoordinateUnits == SvgCoordinateUnits.ObjectBoundingBox ?
                    new SvgUnit(SvgUnitType.User, svgUnit.Value / 100) : svgUnit;
        }

        internal static T? GetReference<T>(this SvgElement svgElement, Uri? uri) where T : SvgElement
        {
            if (uri is null)
            {
                return default;
            }

            var svgElementById = svgElement.OwnerDocument?.GetElementById(uri.ToString());
            if (svgElementById is { })
            {
                return svgElementById as T;
            }

            return default;
        }

        internal static bool ElementReferencesUri<T>(this T svgElement, Func<T, Uri?> getUri, HashSet<Uri> uris, SvgElement? svgReferencedElement) where T : SvgElement
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

                if (GetReference<T>(svgElement, referencedElementUri) is { })
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

        internal static bool HasRecursiveReference<T>(this T svgElement, Func<T, Uri?> getUri, HashSet<Uri> uris) where T : SvgElement
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

        internal static Uri? GetUri(this SvgElement svgElement, string name)
        {
            if (svgElement.TryGetAttribute(name, out string uriString))
            {
                return new Uri(uriString, UriKind.RelativeOrAbsolute);
            }
            return default;
        }

        internal static bool TryGetAttribute(this SvgElement svgElement, string name, out string value)
        {
            return svgElement.TryGetAttribute(name, out value);
        }

        internal static T? GetUriElementReference<T>(this SvgElement svgOwnerElement, string name, HashSet<Uri> uris) where T : SvgElement
        {
            var uri = svgOwnerElement.GetUri(name);
            if (uri is { })
            {
                if (HasRecursiveReference(svgOwnerElement, (e) => e.GetUri(name), uris))
                {
                    return default;
                }

                var svgElement = GetReference<T>(svgOwnerElement, uri);
                if (svgElement is null)
                {
                    return default;
                }
                return svgElement;
            }
            return default;
        }

        internal static bool HasRequiredFeatures(this SvgElement svgElement)
        {
            var hasRequiredFeatures = true;

            if (TryGetAttribute(svgElement, "requiredFeatures", out var requiredFeaturesString))
            {
                if (string.IsNullOrEmpty(requiredFeaturesString))
                {
                    hasRequiredFeatures = false;
                }
                else
                {
                    var features = requiredFeaturesString.Trim().Split(_spaceTab, StringSplitOptions.RemoveEmptyEntries);
                    if (features.Length > 0)
                    {
                        foreach (var feature in features)
                        {
                            if (!s_supportedFeatures.Contains(feature))
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

        internal static bool HasRequiredExtensions(this SvgElement svgElement)
        {
            var hasRequiredExtensions = true;

            if (TryGetAttribute(svgElement, "requiredExtensions", out var requiredExtensionsString))
            {
                if (string.IsNullOrEmpty(requiredExtensionsString))
                {
                    hasRequiredExtensions = false;
                }
                else
                {
                    var extensions = requiredExtensionsString.Trim().Split(_spaceTab, StringSplitOptions.RemoveEmptyEntries);
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

        internal static bool HasSystemLanguage(this SvgElement svgElement)
        {
            var hasSystemLanguage = true;

            if (TryGetAttribute(svgElement, "systemLanguage", out var systemLanguageString))
            {
                if (string.IsNullOrEmpty(systemLanguageString))
                {
                    hasSystemLanguage = false;
                }
                else
                {
                    var languages = systemLanguageString.Trim().Split(_comma, StringSplitOptions.RemoveEmptyEntries);
                    if (languages.Length > 0)
                    {
                        hasSystemLanguage = false;
                        var systemLanguage = s_systemLanguageOverride ?? CultureInfo.InstalledUICulture;

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
                            catch
                            {
                                // ignored
                            }
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

        internal static bool IsContainerElement(this SvgElement svgElement)
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

        internal static bool IsKnownElement(this SvgElement svgElement)
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

        internal static float AdjustSvgOpacity(float opacity)
        {
            return Math.Min(Math.Max(opacity, 0), 1);
        }

        internal static byte CombineWithOpacity(byte alpha, float opacity)
        {
            return (byte)Math.Round((opacity * (alpha / 255.0)) * 255);
        }

        internal static Color GetColor(SvgColourServer svgColourServer, float opacity, Attributes ignoreAttributes)
        {
            var colour = svgColourServer.Colour;
            var alpha = ignoreAttributes.HasFlag(Attributes.Opacity) ?
                svgColourServer.Colour.A :
                CombineWithOpacity(svgColourServer.Colour.A, opacity);

            return new Color(colour.R, colour.G, colour.B, alpha);
        }

        internal static Color? GetColor(SvgVisualElement svgVisualElement, SvgPaintServer server)
        {
            if (server is SvgDeferredPaintServer svgDeferredPaintServer)
            {
                server = SvgDeferredPaintServer.TryGet<SvgPaintServer>(svgDeferredPaintServer, svgVisualElement);
            }

            if (server is SvgColourServer stopColorSvgColourServer)
            {
                return GetColor(stopColorSvgColourServer, 1f, Attributes.None);
            }

            return new Color(0x00, 0x00, 0x00, 0xFF);
        }

        internal static PathEffect? CreateDash(SvgElement svgElement, Rect skBounds)
        {
            var strokeDashArray = svgElement.StrokeDashArray;
            var strokeDashOffset = svgElement.StrokeDashOffset;
            var count = strokeDashArray.Count;

            if (strokeDashArray is { } && count > 0)
            {
                var isOdd = count % 2 != 0;
                var sum = 0f;
                float[] intervals = new float[isOdd ? count * 2 : count];
                for (var i = 0; i < count; i++)
                {
                    var dash = strokeDashArray[i].ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds);
                    if (dash < 0f)
                    {
                        return default;
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
                    return default;
                }

                var phase = strokeDashOffset is { } ? strokeDashOffset.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds) : 0f;

                return PathEffect.CreateDash(intervals, phase);
            }

            return default;
        }

        private static List<SvgPatternServer> GetLinkedPatternServer(SvgPatternServer svgPatternServer, SvgVisualElement svgVisualElement)
        {
            var svgPatternServers = new List<SvgPatternServer>();
            var currentPatternServer = svgPatternServer;
            do
            {
                svgPatternServers.Add(currentPatternServer);
                currentPatternServer = SvgDeferredPaintServer.TryGet<SvgPatternServer>(currentPatternServer.InheritGradient, svgVisualElement);
            } while (currentPatternServer is { });
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
            } while (currentGradientServer is { });
            return svgGradientServers;
        }

        private static void GetStopsImpl(SvgGradientServer svgGradientServer, Rect skBounds, List<Color> colors, List<float> colorPos, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
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
                        var offset = svgGradientStop.Offset.ToDeviceValue(UnitRenderingType.Horizontal, svgGradientServer, skBounds);
                        offset /= skBounds.Width;
                        colors.Add(stopColor);
                        colorPos.Add(offset);
                    }
                }
            }
        }

        internal static void GetStops(List<SvgGradientServer> svgReferencedGradientServers, Rect skBounds, List<Color> colors, List<float> colorPos, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes)
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
            var maxPos = float.MinValue;
            for (var i = 0; i < colorPos.Count; i++)
            {
                var pos = colorPos[i];
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

        internal static ColorF[] ToSkColorF(this Color[] skColors)
        {
            var skColorsF = new ColorF[skColors.Length];

            for (var i = 0; i < skColors.Length; i++)
            {
                skColorsF[i] = skColors[i];
            }

            return skColorsF;
        }

        internal static SvgColourInterpolation GetColorInterpolation(SvgElement svgElement)
        {
            return svgElement.ColorInterpolation switch
            {
                SvgColourInterpolation.Auto => SvgColourInterpolation.SRGB,
                SvgColourInterpolation.SRGB => SvgColourInterpolation.SRGB,
                SvgColourInterpolation.LinearRGB => SvgColourInterpolation.LinearRGB,
                _ => SvgColourInterpolation.SRGB,
            };
        }

        internal static SvgColourInterpolation GetColorInterpolationFilters(SvgElement svgElement)
        {
            return svgElement.ColorInterpolationFilters switch
            {
                SvgColourInterpolation.Auto => SvgColourInterpolation.LinearRGB,
                SvgColourInterpolation.SRGB => SvgColourInterpolation.SRGB,
                SvgColourInterpolation.LinearRGB => SvgColourInterpolation.LinearRGB,
                _ => SvgColourInterpolation.LinearRGB,
            };
        }

        internal static Shader CreateLinearGradient(SvgLinearGradientServer svgLinearGradientServer, Rect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes, ColorSpace skColorSpace)
        {
            var svgReferencedGradientServers = GetLinkedGradientServer(svgLinearGradientServer, svgVisualElement);

            SvgGradientServer? firstSpreadMethod = default;
            SvgGradientServer? firstGradientTransform = default;
            SvgGradientServer? firstGradientUnits = default;
            SvgLinearGradientServer? firstX1 = default;
            SvgLinearGradientServer? firstY1 = default;
            SvgLinearGradientServer? firstX2 = default;
            SvgLinearGradientServer? firstY2 = default;

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
                    if (pGradientTransform is { } && pGradientTransform.Count > 0)
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
                        if (pX1 is { } && pX1 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "x1", out _))
                        {
                            firstX1 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstY1 is null)
                    {
                        var pY1 = svgLinearGradientServerHref.Y1;
                        if (pY1 is { } && pY1 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "y1", out _))
                        {
                            firstY1 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstX2 is null)
                    {
                        var pX2 = svgLinearGradientServerHref.X2;
                        if (pX2 is { } && pX2 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "x2", out _))
                        {
                            firstX2 = svgLinearGradientServerHref;
                        }
                    }
                    if (firstY2 is null)
                    {
                        var pY2 = svgLinearGradientServerHref.Y2;
                        if (pY2 is { } && pY2 != SvgUnit.None && TryGetAttribute(svgLinearGradientServerHref, "y2", out _))
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

            var x1 = normalizedX1.ToDeviceValue(UnitRenderingType.Horizontal, svgLinearGradientServer, skBounds);
            var y1 = normalizedY1.ToDeviceValue(UnitRenderingType.Vertical, svgLinearGradientServer, skBounds);
            var x2 = normalizedX2.ToDeviceValue(UnitRenderingType.Horizontal, svgLinearGradientServer, skBounds);
            var y2 = normalizedY2.ToDeviceValue(UnitRenderingType.Vertical, svgLinearGradientServer, skBounds);

            var skStart = new Point(x1, y1);
            var skEnd = new Point(x2, y2);
            var colors = new List<Color>();
            var colorPos = new List<float>();

            GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes);
            AdjustStopColorPos(colorPos);

            var shaderTileMode = svgSpreadMethod switch
            {
                SvgGradientSpreadMethod.Reflect => ShaderTileMode.Mirror,
                SvgGradientSpreadMethod.Repeat => ShaderTileMode.Repeat,
                _ => ShaderTileMode.Clamp,
            };
            var skColors = colors.ToArray();
            float[] skColorPos = colorPos.ToArray();

            if (skColors.Length == 0)
            {
                return Shader.CreateColor(new Color(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
            }
            else if (skColors.Length == 1)
            {
                return Shader.CreateColor(skColors[0], skColorSpace);
            }

            if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundingBoxTransform = new Matrix
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

                if (svgGradientTransform is { } && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = ToMatrix(svgGradientTransform);
                    skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(gradientTransform);
                }

                var skColorsF = ToSkColorF(skColors);
                return Shader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, skBoundingBoxTransform);
            }
            else
            {
                if (svgGradientTransform is { } && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = ToMatrix(svgGradientTransform);
                    var skColorsF = ToSkColorF(skColors);
                    return Shader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode, gradientTransform);
                }
                else
                {
                    var skColorsF = ToSkColorF(skColors);
                    return Shader.CreateLinearGradient(skStart, skEnd, skColorsF, skColorSpace, skColorPos, shaderTileMode);
                }
            }
        }

        internal static Shader CreateTwoPointConicalGradient(SvgRadialGradientServer svgRadialGradientServer, Rect skBounds, SvgVisualElement svgVisualElement, float opacity, Attributes ignoreAttributes, ColorSpace skColorSpace)
        {
            var svgReferencedGradientServers = GetLinkedGradientServer(svgRadialGradientServer, svgVisualElement);

            SvgGradientServer? firstSpreadMethod = default;
            SvgGradientServer? firstGradientTransform = default;
            SvgGradientServer? firstGradientUnits = default;
            SvgRadialGradientServer? firstCenterX = default;
            SvgRadialGradientServer? firstCenterY = default;
            SvgRadialGradientServer? firstRadius = default;
            SvgRadialGradientServer? firstFocalX = default;
            SvgRadialGradientServer? firstFocalY = default;

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
                    if (pGradientTransform is { } && pGradientTransform.Count > 0)
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
                        if (pCenterX is { } && pCenterX != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "cx", out _))
                        {
                            firstCenterX = svgRadialGradientServerHref;
                        }
                    }
                    if (firstCenterY is null)
                    {
                        var pCenterY = svgRadialGradientServerHref.CenterY;
                        if (pCenterY is { } && pCenterY != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "cy", out _))
                        {
                            firstCenterY = svgRadialGradientServerHref;
                        }
                    }
                    if (firstRadius is null)
                    {
                        var pRadius = svgRadialGradientServerHref.Radius;
                        if (pRadius is { } && pRadius != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "r", out _))
                        {
                            firstRadius = svgRadialGradientServerHref;
                        }
                    }
                    if (firstFocalX is null)
                    {
                        var pFocalX = svgRadialGradientServerHref.FocalX;
                        if (pFocalX is { } && pFocalX != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "fx", out _))
                        {
                            firstFocalX = svgRadialGradientServerHref;
                        }
                    }
                    if (firstFocalY is null)
                    {
                        var pFocalY = svgRadialGradientServerHref.FocalY;
                        if (pFocalY is { } && pFocalY != SvgUnit.None && TryGetAttribute(svgRadialGradientServerHref, "fy", out _))
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

            var centerX = normalizedCenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
            var centerY = normalizedCenterY.ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);

            var startRadius = 0f;
            var endRadius = normalizedRadius.ToDeviceValue(UnitRenderingType.Other, svgRadialGradientServer, skBounds);

            var focalX = normalizedFocalX.ToDeviceValue(UnitRenderingType.Horizontal, svgRadialGradientServer, skBounds);
            var focalY = normalizedFocalY.ToDeviceValue(UnitRenderingType.Vertical, svgRadialGradientServer, skBounds);

            var skStart = new Point(centerX, centerY);
            var skEnd = new Point(focalX, focalY);

            var colors = new List<Color>();
            var colorPos = new List<float>();

            GetStops(svgReferencedGradientServers, skBounds, colors, colorPos, svgVisualElement, opacity, ignoreAttributes);
            AdjustStopColorPos(colorPos);

            var shaderTileMode = svgSpreadMethod switch
            {
                SvgGradientSpreadMethod.Reflect => ShaderTileMode.Mirror,
                SvgGradientSpreadMethod.Repeat => ShaderTileMode.Repeat,
                _ => ShaderTileMode.Clamp,
            };
            var skColors = colors.ToArray();
            float[] skColorPos = colorPos.ToArray();

            if (skColors.Length == 0)
            {
                return Shader.CreateColor(new Color(0xFF, 0xFF, 0xFF, 0x00), skColorSpace);
            }
            else if (skColors.Length == 1)
            {
                return Shader.CreateColor(skColors[0], skColorSpace);
            }

            if (svgGradientUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skBoundingBoxTransform = new Matrix
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

                if (svgGradientTransform is { } && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = ToMatrix(svgGradientTransform);
                    skBoundingBoxTransform = skBoundingBoxTransform.PreConcat(gradientTransform);
                }

                var skColorsF = ToSkColorF(skColors);
                return Shader.CreateTwoPointConicalGradient(
                    skStart, startRadius,
                    skEnd, endRadius,
                    skColorsF, skColorSpace, skColorPos,
                    shaderTileMode,
                    skBoundingBoxTransform);
            }
            else
            {
                if (svgGradientTransform is { } && svgGradientTransform.Count > 0)
                {
                    var gradientTransform = ToMatrix(svgGradientTransform);
                    var skColorsF = ToSkColorF(skColors);
                    return Shader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode, gradientTransform);
                }
                else
                {
                    var skColorsF = ToSkColorF(skColors);
                    return Shader.CreateTwoPointConicalGradient(
                        skStart, startRadius,
                        skEnd, endRadius,
                        skColorsF, skColorSpace, skColorPos,
                        shaderTileMode);
                }
            }
        }

        internal static Picture.Picture RecordPicture(SvgElementCollection svgElementCollection, float width, float height, Matrix skMatrix, float opacity, IAssetLoader assetLoader, Attributes ignoreAttributes)
        {
            var skSize = new Size(width, height);
            var skBounds = Rect.Create(skSize);
            using var skPictureRecorder = new PictureRecorder();
            using var skCanvas = skPictureRecorder.BeginRecording(skBounds);

            skCanvas.SetMatrix(skMatrix);

            using var skPaintOpacity = ignoreAttributes.HasFlag(Attributes.Opacity) ? null : GetOpacityPaint(opacity);
            if (skPaintOpacity is { })
            {
                skCanvas.SaveLayer(skPaintOpacity);
            }

            foreach (var svgElement in svgElementCollection)
            {
                using var drawable = DrawableFactory.Create(svgElement, skBounds, null, assetLoader, ignoreAttributes);
                if (drawable is { })
                {
                    drawable.PostProcess();
                    drawable.Draw(skCanvas, ignoreAttributes, null);
                }
            }

            if (skPaintOpacity is { })
            {
                skCanvas.Restore();
            }

            skCanvas.Restore();

            return skPictureRecorder.EndRecording();
        }

        internal static Shader? CreatePicture(SvgPatternServer svgPatternServer, Rect skBounds, SvgVisualElement svgVisualElement, float opacity, IAssetLoader assetLoader, Attributes ignoreAttributes)
        {
            var svgReferencedPatternServers = GetLinkedPatternServer(svgPatternServer, svgVisualElement);

            SvgPatternServer? firstChildren = default;
            SvgPatternServer? firstX = default;
            SvgPatternServer? firstY = default;
            SvgPatternServer? firstWidth = default;
            SvgPatternServer? firstHeight = default;
            SvgPatternServer? firstPatternUnit = default;
            SvgPatternServer? firstPatternContentUnit = default;
            SvgPatternServer? firstViewBox = default;
            SvgPatternServer? firstAspectRatio = default;

            foreach (var p in svgReferencedPatternServers)
            {
                if (firstChildren is null && p.Children.Count > 0)
                {
                    firstChildren = p;
                }

                if (firstX is null)
                {
                    var pX = p.X;
                    if (pX is { } && pX != SvgUnit.None)
                    {
                        firstX = p;
                    }
                }
                if (firstY is null)
                {
                    var pY = p.Y;
                    if (pY is { } && pY != SvgUnit.None)
                    {
                        firstY = p;
                    }
                }
                if (firstWidth is null)
                {
                    var pWidth = p.Width;
                    if (pWidth is { } && pWidth != SvgUnit.None)
                    {
                        firstWidth = p;
                    }
                }
                if (firstHeight is null)
                {
                    var pHeight = p.Height;
                    if (pHeight is { } && pHeight != SvgUnit.None)
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
                if (firstPatternContentUnit is null)
                {
                    if (TryGetAttribute(p, "patternContentUnits", out _))
                    {
                        firstPatternContentUnit = p;
                    }
                }
                if (firstViewBox is null)
                {
                    var pViewBox = p.ViewBox;
                    if (pViewBox is { } && pViewBox != SvgViewBox.Empty)
                    {
                        firstViewBox = p;
                    }
                }
                if (firstAspectRatio is null)
                {
                    var pAspectRatio = p.AspectRatio;
                    if (pAspectRatio is { } && pAspectRatio.Align != SvgPreserveAspectRatio.xMidYMid)
                    {
                        firstAspectRatio = p;
                    }
                }
            }

            if (firstChildren is null || firstWidth is null || firstHeight is null)
            {
                return default;
            }

            var xUnit = firstX is null ? new SvgUnit(0f) : firstX.X;
            var yUnit = firstY is null ? new SvgUnit(0f) : firstY.Y;
            var widthUnit = firstWidth.Width;
            var heightUnit = firstHeight.Height;
            var patternUnits = firstPatternUnit is null ? SvgCoordinateUnits.ObjectBoundingBox : firstPatternUnit.PatternUnits;
            var patternContentUnits = firstPatternContentUnit is null ? SvgCoordinateUnits.UserSpaceOnUse : firstPatternContentUnit.PatternContentUnits;
            var viewBox = firstViewBox is null ? SvgViewBox.Empty : firstViewBox.ViewBox;
            var aspectRatio = firstAspectRatio is null ? new SvgAspectRatio(SvgPreserveAspectRatio.xMidYMid, false) : firstAspectRatio.AspectRatio;

            var x = xUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgPatternServer, skBounds);
            var y = yUnit.ToDeviceValue(UnitRenderingType.Vertical, svgPatternServer, skBounds);
            var width = widthUnit.ToDeviceValue(UnitRenderingType.Horizontal, svgPatternServer, skBounds);
            var height = heightUnit.ToDeviceValue(UnitRenderingType.Vertical, svgPatternServer, skBounds);

            if (width <= 0 || height <= 0)
            {
                return default;
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

            var skRectTransformed = Rect.Create(x, y, width, height);

            var skMatrix = Matrix.CreateIdentity();

            var skPatternTransformMatrix = ToMatrix(svgPatternServer.PatternTransform);
            skMatrix = skMatrix.PreConcat(skPatternTransformMatrix);

            var translateTransform = Matrix.CreateTranslation(skRectTransformed.Left, skRectTransformed.Top);
            skMatrix = skMatrix.PreConcat(translateTransform);

            var skPictureTransform = Matrix.CreateIdentity();
            if (!viewBox.Equals(SvgViewBox.Empty))
            {
                var viewBoxTransform = ToMatrix(
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
                    var skBoundsScaleTransform = Matrix.CreateScale(skBounds.Width, skBounds.Height);
                    skPictureTransform = skPictureTransform.PreConcat(skBoundsScaleTransform);
                }
            }

            var skPicture = RecordPicture(firstChildren.Children, skRectTransformed.Width, skRectTransformed.Height, skPictureTransform, opacity, assetLoader, ignoreAttributes);

            return Shader.CreatePicture(skPicture, ShaderTileMode.Repeat, ShaderTileMode.Repeat, skMatrix, skPicture.CullRect);
        }

        internal static bool SetColorOrShader(SvgVisualElement svgVisualElement, SvgPaintServer server, float opacity, Rect skBounds, Paint.Paint skPaint, bool forStroke, IAssetLoader assetLoader, Attributes ignoreAttributes)
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
                        var colorInterpolation = GetColorInterpolation(svgVisualElement);
                        var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                        var skColorSpace = isLinearRGB ? ColorSpace.SrgbLinear : ColorSpace.Srgb;
                        var skColorShader = Shader.CreateColor(skColor, skColorSpace);
                        if (skColorShader is { })
                        {
                            skPaint.Shader = skColorShader;
                            return true;
                        }
                    }
                    break;

                case SvgPatternServer svgPatternServer:
                    {
                        var colorInterpolation = GetColorInterpolation(svgVisualElement);
                        var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                        var skColorSpace = isLinearRGB ? ColorSpace.SrgbLinear : ColorSpace.Srgb;
                        // TODO: Use skColorSpace in CreatePicture
                        var skPatternShader = CreatePicture(svgPatternServer, skBounds, svgVisualElement, opacity, assetLoader, ignoreAttributes);
                        if (skPatternShader is { })
                        {
                            skPaint.Shader = skPatternShader;
                            return true;
                        }
                        else
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
                                var skColorShader = Shader.CreateColor(skColor, skColorSpace);
                                if (skColorShader is { })
                                {
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
                        var colorInterpolation = GetColorInterpolation(svgLinearGradientServer);
                        var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                        var skColorSpace = isLinearRGB ? ColorSpace.SrgbLinear : ColorSpace.Srgb;

                        if (svgLinearGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
                                var skColorShader = Shader.CreateColor(skColor, skColorSpace);
                                if (skColorShader is { })
                                {
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
                            var skLinearGradientShader = CreateLinearGradient(svgLinearGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace);
                            if (skLinearGradientShader is { })
                            {
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
                        var colorInterpolation = GetColorInterpolation(svgRadialGradientServer);
                        var isLinearRGB = colorInterpolation == SvgColourInterpolation.LinearRGB;
                        var skColorSpace = isLinearRGB ? ColorSpace.SrgbLinear : ColorSpace.Srgb;

                        if (svgRadialGradientServer.GradientUnits == SvgCoordinateUnits.ObjectBoundingBox && (skBounds.Width == 0f || skBounds.Height == 0f))
                        {
                            if (fallbackServer is SvgColourServer svgColourServerFallback)
                            {
                                var skColor = GetColor(svgColourServerFallback, opacity, ignoreAttributes);
                                var skColorShader = Shader.CreateColor(skColor, skColorSpace);
                                if (skColorShader is { })
                                {
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
                            var skRadialGradientShader = CreateTwoPointConicalGradient(svgRadialGradientServer, skBounds, svgVisualElement, opacity, ignoreAttributes, skColorSpace);
                            if (skRadialGradientShader is { })
                            {
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
                    return SetColorOrShader(svgVisualElement, svgDeferredPaintServer, opacity, skBounds, skPaint, forStroke, assetLoader, ignoreAttributes);

                default:
                    // Do not draw element.
                    return false;
            }
            return true;
        }

        internal static void SetDash(SvgVisualElement svgVisualElement, Paint.Paint skPaint, Rect skBounds)
        {
            var skPathEffect = CreateDash(svgVisualElement, skBounds);
            if (skPathEffect is { })
            {
                skPaint.PathEffect = skPathEffect;
            }
        }

        internal static bool IsAntialias(SvgElement svgElement)
        {
            return svgElement.ShapeRendering switch
            {
                SvgShapeRendering.Inherit => true,
                SvgShapeRendering.Auto => true,
                SvgShapeRendering.GeometricPrecision => true,
                SvgShapeRendering.OptimizeSpeed => false,
                SvgShapeRendering.CrispEdges => false,
                _ => true
            };
        }

        internal static bool IsValidFill(SvgElement svgElement)
        {
            var fill = svgElement.Fill;
            return fill is { }
                && fill != SvgPaintServer.None;
        }

        internal static bool IsValidStroke(SvgElement svgElement, Rect skBounds)
        {
            var stroke = svgElement.Stroke;
            var strokeWidth = svgElement.StrokeWidth;
            return stroke is { }
                && stroke != SvgPaintServer.None
                && strokeWidth.ToDeviceValue(UnitRenderingType.Other, svgElement, skBounds) > 0f;
        }

        internal static Paint.Paint? GetFillPaint(SvgVisualElement svgVisualElement, Rect skBounds, IAssetLoader assetLoader, Attributes ignoreAttributes)
        {
            var skPaint = new Paint.Paint
            {
                IsAntialias = IsAntialias(svgVisualElement),
                Style = PaintStyle.Fill
            };

            var server = svgVisualElement.Fill;
            var opacity = AdjustSvgOpacity(svgVisualElement.FillOpacity);
            if (SetColorOrShader(svgVisualElement, server, opacity, skBounds, skPaint, forStroke: false, assetLoader: assetLoader, ignoreAttributes: ignoreAttributes) == false)
            {
                return default;
            }

            return skPaint;
        }

        internal static Paint.Paint? GetStrokePaint(SvgVisualElement svgVisualElement, Rect skBounds, IAssetLoader assetLoader, Attributes ignoreAttributes)
        {
            var skPaint = new Paint.Paint
            {
                IsAntialias = IsAntialias(svgVisualElement),
                Style = PaintStyle.Stroke
            };

            var server = svgVisualElement.Stroke;
            var opacity = AdjustSvgOpacity(svgVisualElement.StrokeOpacity);
            if (SetColorOrShader(svgVisualElement, server, opacity, skBounds, skPaint, forStroke: true, assetLoader: assetLoader, ignoreAttributes: ignoreAttributes) == false)
            {
                return default;
            }

            switch (svgVisualElement.StrokeLineCap)
            {
                case SvgStrokeLineCap.Butt:
                    skPaint.StrokeCap = StrokeCap.Butt;
                    break;

                case SvgStrokeLineCap.Round:
                    skPaint.StrokeCap = StrokeCap.Round;
                    break;

                case SvgStrokeLineCap.Square:
                    skPaint.StrokeCap = StrokeCap.Square;
                    break;
            }

            switch (svgVisualElement.StrokeLineJoin)
            {
                case SvgStrokeLineJoin.Miter:
                    skPaint.StrokeJoin = StrokeJoin.Miter;
                    break;

                case SvgStrokeLineJoin.Round:
                    skPaint.StrokeJoin = StrokeJoin.Round;
                    break;

                case SvgStrokeLineJoin.Bevel:
                    skPaint.StrokeJoin = StrokeJoin.Bevel;
                    break;
            }

            skPaint.StrokeMiter = svgVisualElement.StrokeMiterLimit;

            skPaint.StrokeWidth = svgVisualElement.StrokeWidth.ToDeviceValue(UnitRenderingType.Other, svgVisualElement, skBounds);

            var strokeDashArray = svgVisualElement.StrokeDashArray;
            if (strokeDashArray is { })
            {
                SetDash(svgVisualElement, skPaint, skBounds);
            }

            return skPaint;
        }

        internal static Paint.Paint? GetOpacityPaint(float opacity)
        {
            if (opacity < 1f)
            {
                return new Paint.Paint
                {
                    IsAntialias = true,
                    Color = new Color(255, 255, 255, (byte)Math.Round(opacity * 255)),
                    Style = PaintStyle.StrokeAndFill
                };
            }
            return default;
        }

        internal static Paint.Paint? GetOpacityPaint(SvgElement svgElement)
        {
            var opacity = AdjustSvgOpacity(svgElement.Opacity);
            var skPaint = GetOpacityPaint(opacity);
            if (skPaint is { })
            {
                return skPaint;
            }
            return default;
        }

        internal static Matrix ToMatrix(this SvgMatrix svgMatrix)
        {
            return new Matrix
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

        internal static Matrix ToMatrix(this SvgTransformCollection svgTransformCollection)
        {
            var skMatrixTotal = Matrix.CreateIdentity();

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
                            var skMatrixRotate = Matrix.CreateRotationDegrees(svgRotate.Angle, svgRotate.CenterX, svgRotate.CenterY);
                            skMatrixTotal = skMatrixTotal.PreConcat(skMatrixRotate);
                        }
                        break;

                    case SvgScale svgScale:
                        {
                            var skMatrixScale = Matrix.CreateScale(svgScale.X, svgScale.Y);
                            skMatrixTotal = skMatrixTotal.PreConcat(skMatrixScale);
                        }
                        break;

                    case SvgSkew svgSkew:
                        {
                            var sx = (float)Math.Tan(Math.PI * svgSkew.AngleX / 180);
                            var sy = (float)Math.Tan(Math.PI * svgSkew.AngleY / 180);
                            var skMatrixSkew = Matrix.CreateSkew(sx, sy);
                            skMatrixTotal = skMatrixTotal.PreConcat(skMatrixSkew);
                        }
                        break;

                    case SvgTranslate svgTranslate:
                        {
                            var skMatrixTranslate = Matrix.CreateTranslation(svgTranslate.X, svgTranslate.Y);
                            skMatrixTotal = skMatrixTotal.PreConcat(skMatrixTranslate);
                        }
                        break;
                }
            }

            return skMatrixTotal;
        }

        internal static Matrix ToMatrix(this SvgViewBox svgViewBox, SvgAspectRatio svgAspectRatio, float x, float y, float width, float height)
        {
            if (svgViewBox.Equals(SvgViewBox.Empty))
            {
                return Matrix.CreateTranslation(x, y);
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
                var fViewMidX = (svgViewBox.Width / 2) * fScaleX;
                var fViewMidY = (svgViewBox.Height / 2) * fScaleY;
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

                    default:
                        break;
                }
            }

            var skMatrixTotal = Matrix.CreateIdentity();

            var skMatrixXY = Matrix.CreateTranslation(x, y);
            skMatrixTotal = skMatrixTotal.PreConcat(skMatrixXY);

            var skMatrixMinXY = Matrix.CreateTranslation(fMinX, fMinY);
            skMatrixTotal = skMatrixTotal.PreConcat(skMatrixMinXY);

            var skMatrixScale = Matrix.CreateScale(fScaleX, fScaleY);
            skMatrixTotal = skMatrixTotal.PreConcat(skMatrixScale);

            return skMatrixTotal;
        }

        internal static List<(Point Point, byte Type)> GetPathTypes(this Path.Path path)
        {
            // System.Drawing.Drawing2D.GraphicsPath.PathTypes
            // System.Drawing.Drawing2D.PathPointType
            // byte -> PathPointType
            var pathTypes = new List<(Point Point, byte Type)>();

            if (path.Commands is null)
            {
                return pathTypes;
            }
            (Point Point, byte Type) lastPoint = (default, 0);
            foreach (var pathCommand in path.Commands)
            {
                switch (pathCommand)
                {
                    case MoveToPathCommand moveToPathCommand:
                        {
                            var point0 = new Point(moveToPathCommand.X, moveToPathCommand.Y);
                            pathTypes.Add((point0, (byte)PathPointType.Start));
                            lastPoint = (point0, (byte)PathPointType.Start);
                        }
                        break;

                    case LineToPathCommand lineToPathCommand:
                        {
                            var point1 = new Point(lineToPathCommand.X, lineToPathCommand.Y);
                            pathTypes.Add((point1, (byte)PathPointType.Line));
                            lastPoint = (point1, (byte)PathPointType.Line);
                        }
                        break;

                    case CubicToPathCommand cubicToPathCommand:
                        {
                            var point1 = new Point(cubicToPathCommand.X0, cubicToPathCommand.Y0);
                            var point2 = new Point(cubicToPathCommand.X1, cubicToPathCommand.Y1);
                            var point3 = new Point(cubicToPathCommand.X2, cubicToPathCommand.Y2);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            pathTypes.Add((point2, (byte)PathPointType.Bezier));
                            pathTypes.Add((point3, (byte)PathPointType.Bezier));
                            lastPoint = (point3, (byte)PathPointType.Bezier);
                        }
                        break;

                    case QuadToPathCommand quadToPathCommand:
                        {
                            var point1 = new Point(quadToPathCommand.X0, quadToPathCommand.Y0);
                            var point2 = new Point(quadToPathCommand.X1, quadToPathCommand.Y1);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            pathTypes.Add((point2, (byte)PathPointType.Bezier));
                            lastPoint = (point2, (byte)PathPointType.Bezier);
                        }
                        break;

                    case ArcToPathCommand arcToPathCommand:
                        {
                            var point1 = new Point(arcToPathCommand.X, arcToPathCommand.Y);
                            pathTypes.Add((point1, (byte)PathPointType.Bezier));
                            lastPoint = (point1, (byte)PathPointType.Bezier);
                        }
                        break;

                    case ClosePathCommand closePathCommand:
                        {
                            lastPoint = (lastPoint.Point, (byte)((lastPoint.Type | (byte)PathPointType.CloseSubpath)));
                            pathTypes[pathTypes.Count - 1] = lastPoint;
                        }
                        break;

                    case AddPolyPathCommand addPolyPathCommand:
                        {
                            if (addPolyPathCommand.Points is { } && addPolyPathCommand.Points.Count > 0)
                            {
                                foreach (var nexPoint in addPolyPathCommand.Points)
                                {
                                    var point1 = new Point(nexPoint.X, nexPoint.Y);
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

        internal static Path.Path? ToPath(this SvgPathSegmentList svgPathSegmentList, SvgFillRule svgFillRule)
        {
            if (svgPathSegmentList is null || svgPathSegmentList.Count <= 0)
            {
                return default;
            }

            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path.Path
            {
                FillType = fillType
            };

            var isEndFigure = false;
            var haveFigure = false;

            for (var i = 0; i < svgPathSegmentList.Count; i++)
            {
                var svgSegment = svgPathSegmentList[i];
                var isLast = i == svgPathSegmentList.Count - 1;

                switch (svgSegment)
                {
                    case SvgMoveToSegment svgMoveToSegment:
                        {
                            if (isEndFigure && haveFigure == false)
                            {
                                return default;
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
                            var x = svgMoveToSegment.Start.X;
                            var y = svgMoveToSegment.Start.Y;
                            skPath.MoveTo(x, y);
                        }
                        break;

                    case SvgLineSegment svgLineSegment:
                        {
                            if (isEndFigure == false)
                            {
                                return default;
                            }
                            haveFigure = true;
                            var x = svgLineSegment.End.X;
                            var y = svgLineSegment.End.Y;
                            skPath.LineTo(x, y);
                        }
                        break;

                    case SvgCubicCurveSegment svgCubicCurveSegment:
                        {
                            if (isEndFigure == false)
                            {
                                return default;
                            }
                            haveFigure = true;
                            var x0 = svgCubicCurveSegment.FirstControlPoint.X;
                            var y0 = svgCubicCurveSegment.FirstControlPoint.Y;
                            var x1 = svgCubicCurveSegment.SecondControlPoint.X;
                            var y1 = svgCubicCurveSegment.SecondControlPoint.Y;
                            var x2 = svgCubicCurveSegment.End.X;
                            var y2 = svgCubicCurveSegment.End.Y;
                            skPath.CubicTo(x0, y0, x1, y1, x2, y2);
                        }
                        break;

                    case SvgQuadraticCurveSegment svgQuadraticCurveSegment:
                        {
                            if (isEndFigure == false)
                            {
                                return default;
                            }
                            haveFigure = true;
                            var x0 = svgQuadraticCurveSegment.ControlPoint.X;
                            var y0 = svgQuadraticCurveSegment.ControlPoint.Y;
                            var x1 = svgQuadraticCurveSegment.End.X;
                            var y1 = svgQuadraticCurveSegment.End.Y;
                            skPath.QuadTo(x0, y0, x1, y1);
                        }
                        break;

                    case SvgArcSegment svgArcSegment:
                        {
                            if (isEndFigure == false)
                            {
                                return default;
                            }
                            haveFigure = true;
                            var rx = svgArcSegment.RadiusX;
                            var ry = svgArcSegment.RadiusY;
                            var xAxisRotate = svgArcSegment.Angle;
                            var largeArc = svgArcSegment.Size == SvgArcSize.Small ? PathArcSize.Small : PathArcSize.Large;
                            var sweep = svgArcSegment.Sweep == SvgArcSweep.Negative ? PathDirection.CounterClockwise : PathDirection.Clockwise;
                            var x = svgArcSegment.End.X;
                            var y = svgArcSegment.End.Y;
                            skPath.ArcTo(rx, ry, xAxisRotate, largeArc, sweep, x, y);
                        }
                        break;

                    case SvgClosePathSegment _:
                        {
                            if (isEndFigure == false)
                            {
                                return default;
                            }
                            if (haveFigure == false)
                            {
                                return default;
                            }
                            isEndFigure = false;
                            haveFigure = false;
                            skPath.Close();
                        }
                        break;
                }
            }

            if (isEndFigure && haveFigure == false)
            {
                return default;
            }

            return skPath;
        }

        internal static Path.Path? ToPath(this SvgPointCollection svgPointCollection, SvgFillRule svgFillRule, bool isClosed, Rect skOwnerBounds)
        {
            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path.Path
            {
                FillType = fillType
            };

            var skPoints = new Point[svgPointCollection.Count / 2];

            for (var i = 0; (i + 1) < svgPointCollection.Count; i += 2)
            {
                var x = svgPointCollection[i].ToDeviceValue(UnitRenderingType.Other, null, skOwnerBounds);
                var y = svgPointCollection[i + 1].ToDeviceValue(UnitRenderingType.Other, null, skOwnerBounds);
                skPoints[i / 2] = new Point(x, y);
            }

            skPath.AddPoly(skPoints, isClosed);

            return skPath;
        }

        internal static Path.Path? ToPath(this SvgRectangle svgRectangle, SvgFillRule svgFillRule, Rect skOwnerBounds)
        {
            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path.Path
            {
                FillType = fillType
            };

            var x = svgRectangle.X.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skOwnerBounds);
            var y = svgRectangle.Y.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skOwnerBounds);
            var width = svgRectangle.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skOwnerBounds);
            var height = svgRectangle.Height.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skOwnerBounds);
            var rx = svgRectangle.CornerRadiusX.ToDeviceValue(UnitRenderingType.Horizontal, svgRectangle, skOwnerBounds);
            var ry = svgRectangle.CornerRadiusY.ToDeviceValue(UnitRenderingType.Vertical, svgRectangle, skOwnerBounds);

            if (width <= 0f || height <= 0f)
            {
                skPath.Dispose();
                return default;
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
                var halfWidth = width / 2f;
                if (rx > halfWidth)
                {
                    rx = halfWidth;
                }
            }

            if (ry > 0f)
            {
                var halfHeight = height / 2f;
                if (ry > halfHeight)
                {
                    ry = halfHeight;
                }
            }

            var isRound = rx > 0f && ry > 0f;
            var skRectBounds = Rect.Create(x, y, width, height);

            if (isRound)
            {
                skPath.AddRoundRect(skRectBounds, rx, ry);
            }
            else
            {
                skPath.AddRect(skRectBounds);
            }

            return skPath;
        }

        internal static Path.Path? ToPath(this SvgCircle svgCircle, SvgFillRule svgFillRule, Rect skOwnerBounds)
        {
            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path.Path
            {
                FillType = fillType
            };

            var cx = svgCircle.CenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgCircle, skOwnerBounds);
            var cy = svgCircle.CenterY.ToDeviceValue(UnitRenderingType.Vertical, svgCircle, skOwnerBounds);
            var radius = svgCircle.Radius.ToDeviceValue(UnitRenderingType.Other, svgCircle, skOwnerBounds);

            if (radius <= 0f)
            {
                skPath.Dispose();
                return default;
            }

            skPath.AddCircle(cx, cy, radius);

            return skPath;
        }

        internal static Path.Path? ToPath(this SvgEllipse svgEllipse, SvgFillRule svgFillRule, Rect skOwnerBounds)
        {
            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path.Path
            {
                FillType = fillType
            };

            var cx = svgEllipse.CenterX.ToDeviceValue(UnitRenderingType.Horizontal, svgEllipse, skOwnerBounds);
            var cy = svgEllipse.CenterY.ToDeviceValue(UnitRenderingType.Vertical, svgEllipse, skOwnerBounds);
            var rx = svgEllipse.RadiusX.ToDeviceValue(UnitRenderingType.Other, svgEllipse, skOwnerBounds);
            var ry = svgEllipse.RadiusY.ToDeviceValue(UnitRenderingType.Other, svgEllipse, skOwnerBounds);

            if (rx <= 0f || ry <= 0f)
            {
                skPath.Dispose();
                return default;
            }

            var skRectBounds = Rect.Create(cx - rx, cy - ry, rx + rx, ry + ry);

            skPath.AddOval(skRectBounds);

            return skPath;
        }

        internal static Path.Path? ToPath(this SvgLine svgLine, SvgFillRule svgFillRule, Rect skOwnerBounds)
        {
            var fillType = (svgFillRule == SvgFillRule.EvenOdd) ? PathFillType.EvenOdd : PathFillType.Winding;
            var skPath = new Path.Path
            {
                FillType = fillType
            };

            var x0 = svgLine.StartX.ToDeviceValue(UnitRenderingType.Horizontal, svgLine, skOwnerBounds);
            var y0 = svgLine.StartY.ToDeviceValue(UnitRenderingType.Vertical, svgLine, skOwnerBounds);
            var x1 = svgLine.EndX.ToDeviceValue(UnitRenderingType.Horizontal, svgLine, skOwnerBounds);
            var y1 = svgLine.EndY.ToDeviceValue(UnitRenderingType.Vertical, svgLine, skOwnerBounds);

            skPath.MoveTo(x0, y0);
            skPath.LineTo(x1, y1);

            return skPath;
        }

        internal static object? GetImage(string uriString, SvgDocument svgOwnerDocument, IAssetLoader assetLoader)
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
                    return GetImageFromDataUri(uriString, svgOwnerDocument, assetLoader);
                }

                if (!uri.IsAbsoluteUri)
                {
                    uri = new Uri(svgOwnerDocument.BaseUri, uri);
                }

                return GetImageFromWeb(uri, assetLoader);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
                return default;
            }
        }

        internal static object? GetImageFromWeb(Uri uri, IAssetLoader assetLoader)
        {
            var request = WebRequest.Create(uri);
            using var response = request.GetResponse();
            using var stream = response.GetResponseStream();

            if (stream is null)
            {
                return default;
            }
            
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var isSvgMimeType = response.ContentType.StartsWith(MimeTypeSvg, StringComparison.OrdinalIgnoreCase);
            var isSvg = uri.LocalPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
            var isSvgz = uri.LocalPath.EndsWith(".svgz", StringComparison.OrdinalIgnoreCase);

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
                var skImage = assetLoader.LoadImage(stream);
                return skImage;
            }
        }

        internal static object? GetImageFromDataUri(string? uriString, SvgDocument svgOwnerDocument, IAssetLoader assetLoader)
        {
            if (uriString is null)
            {
                return default;
            }
            
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

            if (headers.Count > 0 && headers[headers.Count - 1].Trim().Equals("base64", StringComparison.OrdinalIgnoreCase))
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
                if (attribute.Equals("charset", StringComparison.OrdinalIgnoreCase))
                {
                    charset = p[1].Trim();
                }
            }

            var data = uriString.Substring(headerEndIndex + 1);
            if (mimeType.Equals(MimeTypeSvg, StringComparison.OrdinalIgnoreCase))
            {
                if (base64)
                {
                    var bytes = Convert.FromBase64String(data);
                    if (bytes.Length > 2)
                    {
                        var isCompressed = bytes[0] == s_gZipMagicHeaderBytes[0] && bytes[1] == s_gZipMagicHeaderBytes[1];
                        if (isCompressed)
                        {
                            using var bytesStream = new System.IO.MemoryStream(bytes);
                            return LoadSvgz(bytesStream, svgOwnerDocument.BaseUri);
                        }
                    }
                    var encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
                    data = encoding.GetString(bytes);
                }
                var buffer = Encoding.Default.GetBytes(data);
                using var stream = new System.IO.MemoryStream(buffer);
                return LoadSvg(stream, svgOwnerDocument.BaseUri);
            }

            if (mimeType.StartsWith("image/", StringComparison.Ordinal) || mimeType.StartsWith("img/", StringComparison.Ordinal))
            {
                if (base64)
                {
                    var bytes = Convert.FromBase64String(data);
                    if (bytes.Length > 2)
                    {
                        var isCompressed = bytes[0] == s_gZipMagicHeaderBytes[0] && bytes[1] == s_gZipMagicHeaderBytes[1];
                        if (isCompressed)
                        {
                            using var bytesStream = new System.IO.MemoryStream(bytes);
                            return LoadSvgz(bytesStream, svgOwnerDocument.BaseUri);
                        }
                    }
                    var encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
                    data = encoding.GetString(bytes);
                }
                var buffer = Encoding.Default.GetBytes(data);
                using var stream = new System.IO.MemoryStream(buffer);
                return assetLoader.LoadImage(stream);
            }

            return default;
        }

        internal static SvgDocument LoadSvg(System.IO.Stream stream, Uri baseUri)
        {
            var svgDocument = SvgDocument.Open<SvgDocument>(stream);
            svgDocument.BaseUri = baseUri;
            return svgDocument;
        }

        internal static SvgDocument LoadSvgz(System.IO.Stream stream, Uri baseUri)
        {
            using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            using var memoryStream = new System.IO.MemoryStream();
            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            var svgDocument = SvgDocument.Open<SvgDocument>(memoryStream);
            svgDocument.BaseUri = baseUri;
            return svgDocument;
        }

        internal static bool CanDraw(SvgVisualElement svgVisualElement, Attributes ignoreAttributes)
        {
            var visible = svgVisualElement.Visible;
            var ignoreDisplay = ignoreAttributes.HasFlag(Attributes.Display);
            var display = ignoreDisplay || !string.Equals(svgVisualElement.Display, "none", StringComparison.OrdinalIgnoreCase);
            return visible && display;
        }

        private static SvgFillRule ToFillRule(SvgVisualElement svgVisualElement, SvgClipRule? svgClipPathClipRule)
        {
            var svgClipRule = (svgClipPathClipRule is { } ? svgClipPathClipRule.Value : svgVisualElement.ClipRule);
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

        internal static void GetClipPath(SvgVisualElement svgVisualElement, Rect skBounds, HashSet<Uri> uris, ClipPath? clipPath, SvgClipRule? svgClipPathClipRule)
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
                        var skPath = svgPath.PathData?.ToPath(fillRule);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgPath.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgPath, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgRectangle svgRectangle:
                    {
                        var fillRule = ToFillRule(svgRectangle, svgClipPathClipRule);
                        var skPath = svgRectangle.ToPath(fillRule, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgRectangle.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgRectangle, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgCircle svgCircle:
                    {
                        var fillRule = ToFillRule(svgCircle, svgClipPathClipRule);
                        var skPath = svgCircle.ToPath(fillRule, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgCircle.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgCircle, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgEllipse svgEllipse:
                    {
                        var fillRule = ToFillRule(svgEllipse, svgClipPathClipRule);
                        var skPath = svgEllipse.ToPath(fillRule, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgEllipse.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgEllipse, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgLine svgLine:
                    {
                        var fillRule = ToFillRule(svgLine, svgClipPathClipRule);
                        var skPath = svgLine.ToPath(fillRule, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgLine.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgLine, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgPolyline svgPolyline:
                    {
                        var fillRule = ToFillRule(svgPolyline, svgClipPathClipRule);
                        var skPath = svgPolyline.Points?.ToPath(fillRule, false, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgPolyline.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgPolyline, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgPolygon svgPolygon:
                    {
                        var fillRule = ToFillRule(svgPolygon, svgClipPathClipRule);
                        var skPath = svgPolygon.Points?.ToPath(fillRule, true, skBounds);
                        if (skPath is null)
                        {
                            break;
                        }

                        var pathClip = new PathClip
                        {
                            Path = skPath,
                            Transform = ToMatrix(svgPolygon.Transforms),
                            Clip = new ClipPath
                            {
                                Clip = new ClipPath()
                            }
                        };
                        clipPath.Clips?.Add(pathClip);

                        GetSvgVisualElementClipPath(svgPolygon, skPath.Bounds, uris, pathClip.Clip);
                    }
                    break;

                case SvgUse svgUse:
                    {
                        if (HasRecursiveReference(svgUse, (e) => e.ReferencedElement, new HashSet<Uri>()))
                        {
                            break;
                        }

                        var svgReferencedVisualElement = GetReference<SvgVisualElement>(svgUse, svgUse.ReferencedElement);
                        if (svgReferencedVisualElement is null || svgReferencedVisualElement is SvgSymbol)
                        {
                            break;
                        }

                        if (!CanDraw(svgReferencedVisualElement, Attributes.None))
                        {
                            break;
                        }

                        // TODO:
                        GetClipPath(svgReferencedVisualElement, skBounds, uris, clipPath, svgClipPathClipRule);

                        if (clipPath.Clips is { } && clipPath.Clips.Count > 0)
                        {
                            // TODO:
                            var lastClip = clipPath.Clips[clipPath.Clips.Count - 1];
                            if (lastClip.Clip is { })
                            {
                                GetSvgVisualElementClipPath(svgUse, skBounds, uris, lastClip.Clip);
                            }
                        }
                    }
                    break;

                case SvgText svgText:
                    {
                        // TODO: Get path from SvgText.
                    }
                    break;
            }
        }

        private static void GetClipPath(SvgElementCollection svgElementCollection, Rect skBounds, HashSet<Uri> uris, ClipPath? clipPath, SvgClipRule? svgClipPathClipRule)
        {
            foreach (var svgElement in svgElementCollection)
            {
                if (svgElement is SvgVisualElement visualChild)
                {
                    if (!CanDraw(visualChild, Attributes.None))
                    {
                        continue;
                    }
                    GetClipPath(visualChild, skBounds, uris, clipPath, svgClipPathClipRule);
                }
            }
        }

        internal static void GetClipPathClipPath(SvgClipPath svgClipPath, Rect skBounds, HashSet<Uri> uris, ClipPath? clipPath)
        {
            if (clipPath is null)
            {
                return;
            }

            var svgClipPathRef = svgClipPath.GetUriElementReference<SvgClipPath>("clip-path", uris);
            if (svgClipPathRef?.Children is null)
            {
                return;
            }

            GetClipPath(svgClipPathRef, skBounds, uris, clipPath);

            var skMatrix = Matrix.CreateIdentity();

            if (svgClipPathRef.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skScaleMatrix = Matrix.CreateScale(skBounds.Width, skBounds.Height);
                skMatrix = skMatrix.PostConcat(skScaleMatrix);

                var skTranslateMatrix = Matrix.CreateTranslation(skBounds.Left, skBounds.Top);
                skMatrix = skMatrix.PostConcat(skTranslateMatrix);
            }

            var skTransformsMatrix = ToMatrix(svgClipPathRef.Transforms);
            skMatrix = skMatrix.PostConcat(skTransformsMatrix);

            clipPath.Transform = skMatrix; // TODO:
        }

        internal static void GetClipPath(SvgClipPath svgClipPath, Rect skBounds, HashSet<Uri> uris, ClipPath? clipPath)
        {
            if (clipPath is null)
            {
                return;
            }

            GetClipPathClipPath(svgClipPath, skBounds, uris, clipPath.Clip);

            var clipPathClipRule = GetSvgClipRule(svgClipPath);

            GetClipPath(svgClipPath.Children, skBounds, uris, clipPath, clipPathClipRule);

            var skMatrix = Matrix.CreateIdentity();

            if (svgClipPath.ClipPathUnits == SvgCoordinateUnits.ObjectBoundingBox)
            {
                var skScaleMatrix = Matrix.CreateScale(skBounds.Width, skBounds.Height);
                skMatrix = skMatrix.PostConcat(skScaleMatrix);

                var skTranslateMatrix = Matrix.CreateTranslation(skBounds.Left, skBounds.Top);
                skMatrix = skMatrix.PostConcat(skTranslateMatrix);
            }

            var skTransformsMatrix = ToMatrix(svgClipPath.Transforms);
            skMatrix = skMatrix.PostConcat(skTransformsMatrix);

            clipPath.Transform = skMatrix; // TODO:

            if (clipPath.Clips is { } && clipPath.Clips.Count == 0)
            {
                var pathClip = new PathClip
                {
                    Path = new Path.Path(),
                    Transform = Matrix.CreateIdentity(),
                    Clip = default
                };
                clipPath.Clips.Add(pathClip);
            }
        }

        internal static void GetSvgVisualElementClipPath(SvgVisualElement svgVisualElement, Rect skBounds, HashSet<Uri> uris, ClipPath clipPath)
        {
            if (svgVisualElement?.ClipPath is null)
            {
                return;
            }

            if (HasRecursiveReference(svgVisualElement, (e) => e.ClipPath, uris))
            {
                return;
            }

            var svgClipPath = GetReference<SvgClipPath>(svgVisualElement, svgVisualElement.ClipPath);
            if (svgClipPath?.Children is null)
            {
                return;
            }

            GetClipPath(svgClipPath, skBounds, uris, clipPath);
        }

        internal static Rect? GetClipRect(SvgVisualElement svgVisualElement, Rect skRectBounds)
        {
            var clip = svgVisualElement.Clip;
            if (!string.IsNullOrEmpty(clip) && clip.StartsWith("rect(", StringComparison.Ordinal))
            {
                clip = clip.Trim();
                var offsets = new List<float>();
                foreach (var o in clip.Substring(5, clip.Length - 6).Split(','))
                {
                    offsets.Add(float.Parse(o.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture));
                }

                var skClipRect = Rect.Create(
                    skRectBounds.Left + offsets[3],
                    skRectBounds.Top + offsets[0],
                    skRectBounds.Width - (offsets[3] + offsets[1]),
                    skRectBounds.Height - (offsets[2] + offsets[0]));
                return skClipRect;
            }
            return default;
        }

        internal static MaskDrawable? GetSvgElementMask(SvgElement svgElement, Rect skBounds, HashSet<Uri> uris, IAssetLoader assetLoader)
        {
            var svgMaskRef = svgElement.GetUriElementReference<SvgMask>("mask", uris);
            if (svgMaskRef?.Children is null)
            {
                return default;
            }
            var maskDrawable = MaskDrawable.Create(svgMaskRef, skBounds, null, assetLoader, Attributes.None);
            return maskDrawable;
        }

        internal static void AddMarkers(this SvgGroup svgGroup)
        {
            Uri? marker = default;

            // TODO: The marker can not be set as presentation attribute.
            //if (svgGroup.TryGetAttribute("marker", out string markerUrl))
            //{
            //    marker = new Uri(markerUrl, UriKind.RelativeOrAbsolute);
            //}

            var groupMarkerStart = svgGroup.MarkerStart;
            var groupMarkerMid = svgGroup.MarkerMid;
            var groupMarkerEnd = svgGroup.MarkerEnd;

            if (groupMarkerStart is null && groupMarkerMid is null && groupMarkerEnd is null && marker is null)
            {
                return;
            }

            foreach (var svgElement in svgGroup.Children)
            {
                if (svgElement is SvgMarkerElement svgMarkerElement)
                {
                    if (svgMarkerElement.MarkerStart is null)
                    {
                        if (groupMarkerStart is { })
                        {
                            svgMarkerElement.MarkerStart = groupMarkerStart;
                        }
                        else if (marker is { })
                        {
                            svgMarkerElement.MarkerStart = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerMid is null)
                    {
                        if (groupMarkerMid is { })
                        {
                            svgMarkerElement.MarkerMid = groupMarkerMid;
                        }
                        else if (marker is { })
                        {
                            svgMarkerElement.MarkerMid = marker;
                        }
                    }
                    if (svgMarkerElement.MarkerEnd is null)
                    {
                        if (groupMarkerEnd is { })
                        {
                            svgMarkerElement.MarkerEnd = groupMarkerEnd;
                        }
                        else if (marker is { })
                        {
                            svgMarkerElement.MarkerEnd = marker;
                        }
                    }
                }
            }
        }

        internal static void CreateMarker(this SvgMarker svgMarker, SvgVisualElement pOwner, Point pRefPoint, Point pMarkerPoint1, Point pMarkerPoint2, bool isStartMarker, Rect skOwnerBounds, IMarkerHost markerHost, IAssetLoader assetLoader, Attributes ignoreAttributes = Attributes.None)
        {
            var fAngle1 = 0f;
            if (svgMarker.Orient.IsAuto)
            {
                var xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
                var yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
                fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
                if (isStartMarker && svgMarker.Orient.IsAutoStartReverse)
                {
                    fAngle1 += 180;
                }
            }

            var markerDrawable = MarkerDrawable.Create(svgMarker, pOwner, pRefPoint, fAngle1, skOwnerBounds, null, assetLoader, ignoreAttributes);
            markerHost.AddMarker(markerDrawable);
        }

        internal static void CreateMarker(this SvgMarker svgMarker, SvgVisualElement pOwner, Point pRefPoint, Point pMarkerPoint1, Point pMarkerPoint2, Point pMarkerPoint3, Rect skOwnerBounds, IMarkerHost markerHost, IAssetLoader assetLoader)
        {
            var xDiff = pMarkerPoint2.X - pMarkerPoint1.X;
            var yDiff = pMarkerPoint2.Y - pMarkerPoint1.Y;
            var fAngle1 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);
            xDiff = pMarkerPoint3.X - pMarkerPoint2.X;
            yDiff = pMarkerPoint3.Y - pMarkerPoint2.Y;
            var fAngle2 = (float)(Math.Atan2(yDiff, xDiff) * 180.0 / Math.PI);

            var markerDrawable = MarkerDrawable.Create(svgMarker, pOwner, pRefPoint, (fAngle1 + fAngle2) / 2, skOwnerBounds, null, assetLoader);
            markerHost.AddMarker(markerDrawable);
        }

        internal static void CreateMarkers(this SvgMarkerElement svgMarkerElement, Path.Path skPath, Rect skOwnerBounds, IMarkerHost markerHost, IAssetLoader assetLoader)
        {
            var pathTypes = skPath.GetPathTypes();
            var pathLength = pathTypes.Count;

            var markerStart = svgMarkerElement.MarkerStart;
            if (markerStart is { } && pathLength > 0 && !HasRecursiveReference(svgMarkerElement, (e) => e.MarkerStart, new HashSet<Uri>()))
            {
                var marker = GetReference<SvgMarker>(svgMarkerElement, markerStart);
                if (marker is { })
                {
                    var refPoint1 = pathTypes[0].Point;
                    var index = 1;
                    while (index < pathLength && pathTypes[index].Point.X == refPoint1.X && pathTypes[index].Point.Y == refPoint1.Y)
                    {
                        ++index;
                    }
                    var refPoint2 = pathLength == 1 ? refPoint1 : pathTypes[index].Point;
                    CreateMarker(marker, svgMarkerElement, refPoint1, refPoint1, refPoint2, true, skOwnerBounds, markerHost, assetLoader);
                }
            }

            var markerMid = svgMarkerElement.MarkerMid;
            if (markerMid is { } && pathLength > 0 && !HasRecursiveReference(svgMarkerElement, (e) => e.MarkerMid, new HashSet<Uri>()))
            {
                var marker = GetReference<SvgMarker>(svgMarkerElement, markerMid);
                if (marker is { })
                {
                    var bezierIndex = -1;
                    for (var i = 1; i <= pathLength - 2; i++)
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
                            CreateMarker(marker, svgMarkerElement, pathTypes[i].Point, pathTypes[i - 1].Point, pathTypes[i].Point, pathTypes[i + 1].Point, skOwnerBounds, markerHost, assetLoader);
                        }
                    }
                }
            }

            var markerEnd = svgMarkerElement.MarkerEnd;
            if (markerEnd is { } && pathLength > 0 && !HasRecursiveReference(svgMarkerElement, (e) => e.MarkerEnd, new HashSet<Uri>()))
            {
                var marker = GetReference<SvgMarker>(svgMarkerElement, markerEnd);
                if (marker is { })
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
                    CreateMarker(marker, svgMarkerElement, refPoint1, refPoint2, pathTypes[pathLength - 1].Point, false, skOwnerBounds, markerHost, assetLoader);
                }
            }
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

        private static readonly char[] s_colorMatrixSplitChars = { ' ', '\t', '\n', '\r', ',' };

        internal static ImageFilter? CreateColorMatrix(SvgColourMatrix svgColourMatrix, ImageFilter? input = default, CropRect? cropRect = default)
        {
            ColorFilter skColorFilter;

            switch (svgColourMatrix.Type)
            {
                case SvgColourMatrixType.HueRotate:
                    {
                        var value = (string.IsNullOrEmpty(svgColourMatrix.Values) ? 0 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture));
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
                        skColorFilter = ColorFilter.CreateColorMatrix(matrix);
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
                        skColorFilter = ColorFilter.CreateColorMatrix(matrix);
                    }
                    break;

                case SvgColourMatrixType.Saturate:
                    {
                        var value = (string.IsNullOrEmpty(svgColourMatrix.Values) ? 1 : float.Parse(svgColourMatrix.Values, NumberStyles.Any, CultureInfo.InvariantCulture));
                        float[] matrix = new float[]
                        {
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
                double v2 = tableValues[Math.Min((k + 1), (n - 1))];
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
                var k = (byte)((i * n) / 255.0);
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
                var val = 255.0 * (transferFunction.Amplitude * Math.Pow((i / 255.0), exponent) + transferFunction.Offset);
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

            if ((kernelSize.Width * kernelSize.Height) != kernelMatrix.Count)
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

                using var fragmentDrawable = FragmentDrawable.Create(svgFragment, destRect, null, assetLoader, Attributes.None);
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

            var skPaint = new Paint.Paint
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

        internal static ImageFilter? GetGraphic(Picture.Picture skPicture)
        {
            var skImageFilter = ImageFilter.CreatePicture(skPicture, skPicture.CullRect);
            return skImageFilter;
        }

        internal static ImageFilter? GetAlpha(Picture.Picture skPicture)
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

        internal static ImageFilter? GetPaint(Paint.Paint skPaint)
        {
            var skImageFilter = ImageFilter.CreatePaint(skPaint);
            return skImageFilter;
        }

        internal static ImageFilter GetTransparentBlackImage()
        {
            var skPaint = new Paint.Paint
            {
                Style = PaintStyle.StrokeAndFill,
                Color = s_transparentBlack
            };
            var skImageFilter = ImageFilter.CreatePaint(skPaint);
            return skImageFilter;
        }

        internal static ImageFilter GetTransparentBlackAlpha()
        {
            var skPaint = new Paint.Paint
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

        internal static Paint.Paint? GetFilterPaint(SvgVisualElement svgVisualElement, Rect skBounds, IFilterSource filterSource, IAssetLoader assetLoader, out bool isValid)
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

            var results = new Dictionary<string, ImageFilter>();
            var lastResult = default(ImageFilter);
            var prevoiusFilterPrimitiveRegion = Rect.Empty;

            if (width <= 0f || height <= 0f)
            {
                isValid = false;
                return default;
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

            var skFilterRegion = Rect.Create(x, y, width, height);

            var svgFilterPrimitives = new List<SvgFilterPrimitive>();
            foreach (var child in firstChildren.Children)
            {
                if (child is SvgFilterPrimitive svgFilterPrimitive)
                {
                    svgFilterPrimitives.Add(svgFilterPrimitive);
                }
            }

#if DEBUG
            var skFilterPrimitiveRegions = new List<(SvgFilterPrimitive primitive, Rect region)>();
            var skImageFilterRegions = new List<(ImageFilter filter, SvgFilterPrimitive primitive, Rect region)>();
#endif

            var count = 0;
            foreach (var svgFilterPrimitive in svgFilterPrimitives)
            {
                count++;
                var isFirst = count == 1;
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

                var xChild = xUnitChild.ToDeviceValue(UnitRenderingType.HorizontalOffset, svgFilterPrimitive, skPrimitiveBounds);
                var yChild = yUnitChild.ToDeviceValue(UnitRenderingType.VerticalOffset, svgFilterPrimitive, skPrimitiveBounds);
                var widthChild = widthUnitChild.ToDeviceValue(UnitRenderingType.Horizontal, svgFilterPrimitive, skPrimitiveBounds);
                var heightChild = heightUnitChild.ToDeviceValue(UnitRenderingType.Vertical, svgFilterPrimitive, skPrimitiveBounds);

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

                var skCropRect = new CropRect(skFilterPrimitiveRegion);
#if DEBUG
                skFilterPrimitiveRegions.Add((svgFilterPrimitive, skFilterPrimitiveRegion));
#endif

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
                            var skImageFilter = CreateBlend(svgBlend, input2Filter, input1Filter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgColourMatrix svgColourMatrix:
                        {
                            var inputKey = svgColourMatrix.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            var skImageFilter = CreateColorMatrix(svgColourMatrix, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgComponentTransfer svgComponentTransfer:
                        {
                            var inputKey = svgComponentTransfer.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            var skImageFilter = CreateComponentTransfer(svgComponentTransfer, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
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
                            var skImageFilter = CreateComposite(svgComposite, input2Filter, input1Filter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgConvolveMatrix svgConvolveMatrix:
                        {
                            var inputKey = svgConvolveMatrix.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            var skImageFilter = CreateConvolveMatrix(svgConvolveMatrix, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgDiffuseLighting svgDiffuseLighting:
                        {
                            var inputKey = svgDiffuseLighting.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            var skImageFilter = CreateDiffuseLighting(svgDiffuseLighting, skFilterPrimitiveRegion, primitiveUnits, svgVisualElement, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
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
                            var skImageFilter = CreateDisplacementMap(svgDisplacementMap, skFilterPrimitiveRegion, primitiveUnits, input2Filter, input1Filter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgFlood svgFlood:
                        {
                            var skImageFilter = CreateFlood(svgFlood, svgVisualElement, skFilterPrimitiveRegion, null, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgGaussianBlur svgGaussianBlur:
                        {
                            var inputKey = svgGaussianBlur.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            var skImageFilter = CreateBlur(svgGaussianBlur, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case FilterEffects.SvgImage svgImage:
                        {
                            var skImageFilter = CreateImage(svgImage, skFilterPrimitiveRegion, assetLoader, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgMerge svgMerge:
                        {
                            var skImageFilter = CreateMerge(svgMerge, results, lastResult, filterSource, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgMorphology svgMorphology:
                        {
                            var inputKey = svgMorphology.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            var skImageFilter = CreateMorphology(svgMorphology, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgOffset svgOffset:
                        {
                            var inputKey = svgOffset.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            var skImageFilter = CreateOffset(svgOffset, skFilterPrimitiveRegion, primitiveUnits, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgSpecularLighting svgSpecularLighting:
                        {
                            var inputKey = svgSpecularLighting.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            var skImageFilter = CreateSpecularLighting(svgSpecularLighting, skFilterPrimitiveRegion, primitiveUnits, svgVisualElement, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgTile svgTile:
                        {
                            var inputKey = svgTile.Input;
                            var inputFilter = GetInputFilter(inputKey, results, lastResult, filterSource, isFirst);
                            var skImageFilter = CreateTile(svgTile, prevoiusFilterPrimitiveRegion, inputFilter, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
                            {
                                skImageFilterRegions.Add((lastResult, svgFilterPrimitive, skFilterPrimitiveRegion));
                            }
#endif
                        }
                        break;

                    case SvgTurbulence svgTurbulence:
                        {
                            var skImageFilter = CreateTurbulence(svgTurbulence, skFilterPrimitiveRegion, primitiveUnits, skCropRect);
                            lastResult = GetFilterResult(svgFilterPrimitive, skImageFilter, results);
#if DEBUG
                            if (lastResult is { })
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

            if (lastResult is { })
            {
                var skPaint = new Paint.Paint
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

        internal static FontStyleWeight ToFontStyleWeight(SvgFontWeight svgFontWeight)
        {
            var fontWeight = FontStyleWeight.Normal;

            switch (svgFontWeight)
            {
                // TODO: Implement SvgFontWeight.Inherit
                case SvgFontWeight.Inherit:
                    break;

                // TODO: Implement SvgFontWeight.Bolder
                case SvgFontWeight.Bolder:
                    break;

                // TODO: Implement SvgFontWeight.Lighter
                case SvgFontWeight.Lighter:
                    break;

                case SvgFontWeight.W100:
                    fontWeight = FontStyleWeight.Thin;
                    break;

                case SvgFontWeight.W200:
                    fontWeight = FontStyleWeight.ExtraLight;
                    break;

                case SvgFontWeight.W300:
                    fontWeight = FontStyleWeight.Light;
                    break;

                // SvgFontWeight.Normal
                case SvgFontWeight.W400:
                    fontWeight = FontStyleWeight.Normal;
                    break;

                case SvgFontWeight.W500:
                    fontWeight = FontStyleWeight.Medium;
                    break;

                case SvgFontWeight.W600:
                    fontWeight = FontStyleWeight.SemiBold;
                    break;

                // SvgFontWeight.Bold
                case SvgFontWeight.W700:
                    fontWeight = FontStyleWeight.Bold;
                    break;

                case SvgFontWeight.W800:
                    fontWeight = FontStyleWeight.ExtraBold;
                    break;

                case SvgFontWeight.W900:
                    fontWeight = FontStyleWeight.Black;
                    break;
            }

            return fontWeight;
        }

        internal static FontStyleWidth ToFontStyleWidth(SvgFontStretch svgFontStretch)
        {
            var fontWidth = FontStyleWidth.Normal;

            switch (svgFontStretch)
            {
                // TODO: Implement SvgFontStretch.Inherit
                case SvgFontStretch.Inherit:
                    break;

                case SvgFontStretch.Normal:
                    fontWidth = FontStyleWidth.Normal;
                    break;

                // TODO: Implement SvgFontStretch.Wider
                case SvgFontStretch.Wider:
                    break;

                // TODO: Implement SvgFontStretch.Narrower
                case SvgFontStretch.Narrower:
                    break;

                case SvgFontStretch.UltraCondensed:
                    fontWidth = FontStyleWidth.UltraCondensed;
                    break;

                case SvgFontStretch.ExtraCondensed:
                    fontWidth = FontStyleWidth.ExtraCondensed;
                    break;

                case SvgFontStretch.Condensed:
                    fontWidth = FontStyleWidth.Condensed;
                    break;

                case SvgFontStretch.SemiCondensed:
                    fontWidth = FontStyleWidth.SemiCondensed;
                    break;

                case SvgFontStretch.SemiExpanded:
                    fontWidth = FontStyleWidth.SemiExpanded;
                    break;

                case SvgFontStretch.Expanded:
                    fontWidth = FontStyleWidth.Expanded;
                    break;

                case SvgFontStretch.ExtraExpanded:
                    fontWidth = FontStyleWidth.ExtraExpanded;
                    break;

                case SvgFontStretch.UltraExpanded:
                    fontWidth = FontStyleWidth.UltraExpanded;
                    break;
            }

            return fontWidth;
        }

        internal static TextAlign ToTextAlign(SvgTextAnchor textAnchor)
        {
            return textAnchor switch
            {
                SvgTextAnchor.Middle => TextAlign.Center,
                SvgTextAnchor.End => TextAlign.Right,
                _ => TextAlign.Left,
            };
        }

        internal static FontStyleSlant ToFontStyleSlant(SvgFontStyle fontStyle)
        {
            return fontStyle switch
            {
                SvgFontStyle.Oblique => FontStyleSlant.Oblique,
                SvgFontStyle.Italic => FontStyleSlant.Italic,
                _ => FontStyleSlant.Upright,
            };
        }

        private static void SetTypeface(SvgTextBase svgText, Paint.Paint skPaint)
        {
            var fontFamily = svgText.FontFamily;
            var fontWeight = ToFontStyleWeight(svgText.FontWeight);
            var fontWidth = ToFontStyleWidth(svgText.FontStretch);
            var fontStyle = ToFontStyleSlant(svgText.FontStyle);

            skPaint.Typeface = new Typeface
            {
                FamilyName = fontFamily,
                Weight = fontWeight,
                Width = fontWidth,
                Style = fontStyle
            };
        }

        internal static void SetPaintText(SvgTextBase svgText, Rect skBounds, Paint.Paint skPaint)
        {
            skPaint.LcdRenderText = true;
            skPaint.SubpixelText = true;
            skPaint.TextEncoding = TextEncoding.Utf16;

            skPaint.TextAlign = ToTextAlign(svgText.TextAnchor);

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
                // fontSize = new SvgUnit(SvgUnitType.Em, 1.0f);
                // NOTE: Use default SkPaint Font_Size
                fontSize = 12f;
            }
            else
            {
                fontSize = fontSizeUnit.ToDeviceValue(UnitRenderingType.Vertical, svgText, skBounds);
            }

            skPaint.TextSize = fontSize;

            SetTypeface(svgText, skPaint);
        }

        static SvgModelExtensions()
        {
            SvgDocument.SkipGdiPlusCapabilityCheck = true;
            SvgDocument.PointsPerInch = 96;
        }

        public static CultureInfo? s_systemLanguageOverride = default;

        public static Size GetDimensions(SvgFragment svgFragment)
        {
            float w, h;
            var isWidthperc = svgFragment.Width.Type == SvgUnitType.Percentage;
            var isHeightperc = svgFragment.Height.Type == SvgUnitType.Percentage;

            var bounds = new Rect();
            if (isWidthperc || isHeightperc)
            {
                if (svgFragment.ViewBox.Width > 0 && svgFragment.ViewBox.Height > 0)
                {
                    bounds = new Rect(
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
                // NOTE: Pass bounds as Rect.Empty because percentage case is handled before.
                w = svgFragment.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, Rect.Empty);
            }
            if (isHeightperc)
            {
                h = (bounds.Height + bounds.Top) * (svgFragment.Height.Value * 0.01f);
            }
            else
            {
                // NOTE: Pass bounds as Rect.Empty because percentage case is handled before.
                h = svgFragment.Height.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, Rect.Empty);
            }

            return new Size(w, h);
        }

        public static Picture.Picture? ToModel(SvgFragment svgFragment, IAssetLoader assetLoader)
        {
            var size = GetDimensions(svgFragment);
            var bounds = Rect.Create(size);
            using var drawable = DrawableFactory.Create(svgFragment, bounds, null, assetLoader, Attributes.None);
            if (drawable is null)
            {
                return default;
            }
            drawable.PostProcess();

            if (bounds.IsEmpty)
            {
                var drawableBounds = drawable.Bounds;
                bounds = Rect.Create(
                    0f,
                    0f,
                    Math.Abs(drawableBounds.Left) + drawableBounds.Width,
                    Math.Abs(drawableBounds.Top) + drawableBounds.Height);
            }

            return drawable.Snapshot(bounds);
        }

        public static SvgDocument? OpenSvg(string path)
        {
            return SvgDocument.Open<SvgDocument>(path, null);
        }

        public static SvgDocument? OpenSvgz(string path)
        {
            using var fileStream = System.IO.File.OpenRead(path);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var memoryStream = new System.IO.MemoryStream();

            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            return Open(memoryStream);
        }

        public static SvgDocument? Open(string path)
        {
            var extension = System.IO.Path.GetExtension(path);
            return extension.ToLower() switch
            {
                ".svg" => OpenSvg(path),
                ".svgz" => OpenSvgz(path),
                _ => OpenSvg(path),
            };
        }

        public static SvgDocument? Open(System.IO.Stream stream)
        {
            return SvgDocument.Open<SvgDocument>(stream, null);
        }

        public static SvgDocument? FromSvg(string svg)
        {
            return SvgDocument.FromSvg<SvgDocument>(svg);
        }
    }
}
