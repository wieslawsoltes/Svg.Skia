using System;
using System.Collections.Generic;
using System.Globalization;

namespace Svg.Model;

public static class SvgService
{
    public static CultureInfo? s_systemLanguageOverride = default;

    private static readonly char[] s_spaceTab = { ' ', '\t' };

    private static readonly char[] s_comma = { ',' };

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

    internal static HashSet<string> s_supportedExtensions = new();

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
        return ElementReferencesUri(svgElement, getUri, uris, svgReferencedElement);
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
        if (uri is null)
        {
            return default;
        }

        if (HasRecursiveReference(svgOwnerElement, (e) => e.GetUri(name), uris))
        {
            return default;
        }

        return GetReference<T>(svgOwnerElement, uri) ?? default;
    }

    internal static bool HasRequiredFeatures(this SvgElement svgElement)
    {
        if (!TryGetAttribute(svgElement, "requiredFeatures", out var requiredFeaturesString))
        {
            return true;
        }
            
        if (string.IsNullOrEmpty(requiredFeaturesString))
        {
            return false;
        }

        var features = requiredFeaturesString.Trim().Split(s_spaceTab, StringSplitOptions.RemoveEmptyEntries);
        if (features.Length <= 0)
        {
            return false;
        }

        var hasRequiredFeatures = true;
        foreach (var feature in features)
        {
            if (!s_supportedFeatures.Contains(feature))
            {
                hasRequiredFeatures = false;
                break;
            }
        }

        return hasRequiredFeatures;
    }

    internal static bool HasRequiredExtensions(this SvgElement svgElement)
    {
        if (!TryGetAttribute(svgElement, "requiredExtensions", out var requiredExtensionsString))
        {
            return true;
        }
            
        if (string.IsNullOrEmpty(requiredExtensionsString))
        {
            return false;
        }

        var extensions = requiredExtensionsString.Trim().Split(s_spaceTab, StringSplitOptions.RemoveEmptyEntries);
        if (extensions.Length <= 0)
        {
            return false;
        }

        var hasRequiredExtensions = true;
        foreach (var extension in extensions)
        {
            if (!s_supportedExtensions.Contains(extension))
            {
                hasRequiredExtensions = false;
                break;
            }
        }

        return hasRequiredExtensions;
    }

    internal static bool HasSystemLanguage(this SvgElement svgElement)
    {
        if (!TryGetAttribute(svgElement, "systemLanguage", out var systemLanguageString))
        {
            return true;
        }

        if (string.IsNullOrEmpty(systemLanguageString))
        {
            return false;
        }

        var languages = systemLanguageString.Trim().Split(s_comma, StringSplitOptions.RemoveEmptyEntries);
        if (languages.Length <= 0)
        {
            return false;
        }

        var hasSystemLanguage = false;
        var systemLanguage = s_systemLanguageOverride ?? CultureInfo.InstalledUICulture;

        foreach (var language in languages)
        {
            try
            {
                var languageCultureInfo = CultureInfo.CreateSpecificCulture(language.Trim());
                if (systemLanguage.Equals(languageCultureInfo) 
                    || systemLanguage.TwoLetterISOLanguageName == languageCultureInfo.TwoLetterISOLanguageName)
                {
                    hasSystemLanguage = true;
                }
            }
            catch
            {
                // ignored
            }
        }

        return hasSystemLanguage;
    }

    internal static bool IsContainerElement(this SvgElement svgElement)
    {
        return svgElement switch
        {
            SvgAnchor _ => true,
            SvgDefinitionList _ => true,
            SvgMissingGlyph _ => true,
            SvgGlyph _ => true,
            SvgGroup _ => true,
            SvgMarker _ => true,
            SvgMask _ => true,
            SvgPatternServer _ => true,
            SvgFragment _ => true,
            SvgSwitch _ => true,
            SvgSymbol _ => true,
            _ => false
        };
    }

    internal static bool IsKnownElement(this SvgElement svgElement)
    {
        return svgElement switch
        {
            SvgAnchor _ => true,
            SvgCircle _ => true,
            SvgEllipse _ => true,
            SvgFragment _ => true,
            SvgGroup _ => true,
            SvgImage _ => true,
            SvgLine _ => true,
            SvgPath _ => true,
            SvgPolyline _ => true,
            SvgPolygon _ => true,
            SvgRectangle _ => true,
            SvgSwitch _ => true,
            SvgText _ => true,
            SvgUse _ => true,
            _ => false
        };
    }

    internal static double DegreeToRadian(this double degrees)
    {
        return Math.PI * degrees / 180.0;
    }

    internal static double RadianToDegree(this double radians)
    {
        return radians * (180.0 / Math.PI);
    }
}
