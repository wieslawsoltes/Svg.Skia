using System;
using System.Collections.Generic;
using System.Globalization;
using Svg.Document_Structure;

namespace Svg.Model
{
    public static partial class SvgModelExtensions
    {
        public static CultureInfo? s_systemLanguageOverride = default;

        private static readonly char[] _spaceTab = {' ', '\t'};

        private static readonly char[] _comma = {','};

        internal static HashSet<string> s_supportedFeatures = new()
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

        internal static HashSet<string> s_supportedExtensions = new() { };

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

        internal static bool ElementReferencesUri<T>(this T svgElement, Func<T, Uri?> getUri, HashSet<Uri> uris,
            SvgElement? svgReferencedElement) where T : SvgElement
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

        internal static bool HasRecursiveReference<T>(this T svgElement, Func<T, Uri?> getUri, HashSet<Uri> uris)
            where T : SvgElement
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

        internal static T? GetUriElementReference<T>(this SvgElement svgOwnerElement, string name, HashSet<Uri> uris)
            where T : SvgElement
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
                    var features = requiredFeaturesString.Trim()
                        .Split(_spaceTab, StringSplitOptions.RemoveEmptyEntries);
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
                    var extensions = requiredExtensionsString.Trim()
                        .Split(_spaceTab, StringSplitOptions.RemoveEmptyEntries);
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
                                if (systemLanguage.Equals(languageCultureInfo) ||
                                    systemLanguage.TwoLetterISOLanguageName ==
                                    languageCultureInfo.TwoLetterISOLanguageName)
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
    }
}
