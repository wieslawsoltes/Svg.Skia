using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml;
using ShimSkiaSharp;
using Svg.Model.Drawables;

namespace Svg.Model;

public static class SvgService
{
    public static CultureInfo? s_systemLanguageOverride = default;

    private static readonly char[] s_spaceTab = { ' ', '\t' };

    private static readonly char[] s_comma = { ',' };

    private const string MimeTypeSvg = "image/svg+xml";

    private static byte[] GZipMagicHeaderBytes => new byte[] {0x1f, 0x8b};

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

    static SvgService()
    {
        // SvgDocument.SkipGdiPlusCapabilityCheck = true;
        SvgDocument.PointsPerInch = 96;
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
    
        internal static Uri GetImageUri(string uriString, SvgDocument svgOwnerDocument)
    {
        // Uri MaxLength is 65519 (https://msdn.microsoft.com/en-us/library/z6c2z492.aspx)
        // if using data URI scheme, very long URI may happen.
        var safeUriString = uriString.Length > 65519 ? uriString.Substring(0, 65519) : uriString;
        var uri = new Uri(safeUriString, UriKind.RelativeOrAbsolute);

        // handle data/uri embedded images (http://en.wikipedia.org/wiki/Data_URI_scheme)
        if (uri.IsAbsoluteUri && uri.Scheme == "data")
        {
            return uri;
        }

        if (!uri.IsAbsoluteUri)
        {
            uri = new Uri(svgOwnerDocument.BaseUri, uri);
        }

        return uri;
    }

    internal static object? GetImage(string uriString, SvgDocument svgOwnerDocument, IAssetLoader assetLoader)
    {
        try
        {
            var uri = GetImageUri(uriString, svgOwnerDocument);
            if (uri.IsAbsoluteUri && uri.Scheme == "data")
            {
                return GetImageFromDataUri(uriString, svgOwnerDocument, assetLoader);
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
#pragma warning disable 618, SYSLIB0014
        var request = WebRequest.Create(uri);
#pragma warning restore 618, SYSLIB0014
        using var response = request.GetResponse();
        using var stream = response.GetResponseStream();

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
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
            return LoadSvg(stream, uri);
        }

        if (isSvgMimeType || isSvgz)
        {
            return LoadSvgz(stream, uri);
        }

        return assetLoader.LoadImage(stream);
    }

    internal static object? GetImageFromDataUri(string? uriString, SvgDocument svgOwnerDocument, IAssetLoader assetLoader)
    {
        if (uriString is null)
        {
            return default;
        }

        var headerStartIndex = 5;
        var headerEndIndex = uriString.IndexOf(",", headerStartIndex, StringComparison.Ordinal);
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

        if (headers.Count > 0 &&
            headers[headers.Count - 1].Trim().Equals("base64", StringComparison.OrdinalIgnoreCase))
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
                    var isCompressed = bytes[0] == GZipMagicHeaderBytes[0] && bytes[1] == GZipMagicHeaderBytes[1];
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

        if (mimeType.StartsWith("image/", StringComparison.Ordinal) ||
            mimeType.StartsWith("img/", StringComparison.Ordinal))
        {
            if (base64)
            {
                var bytes = Convert.FromBase64String(data);
                if (bytes.Length > 2)
                {
                    var isCompressed = bytes[0] == GZipMagicHeaderBytes[0] && bytes[1] == GZipMagicHeaderBytes[1];
                    if (isCompressed)
                    {
                        using var bytesStream = new System.IO.MemoryStream(bytes);
                        return LoadSvgz(bytesStream, svgOwnerDocument.BaseUri);
                    }
                }

                using var stream = new System.IO.MemoryStream(bytes);
                return assetLoader.LoadImage(stream);
            }
            else
            {
                var bytes = Encoding.Default.GetBytes(data);
                using var stream = new System.IO.MemoryStream(bytes);
                return assetLoader.LoadImage(stream);
            }
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

    public static SKDrawable? ToDrawable(SvgFragment svgFragment, IAssetLoader assetLoader, HashSet<Uri>? references, out SKRect? bounds, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var size = TransformsService.GetDimensions(svgFragment);
        var fragmentBounds = SKRect.Create(size);
        var drawable = DrawableFactory.Create(svgFragment, fragmentBounds, null, assetLoader, references, ignoreAttributes);
        if (drawable is null)
        {
            bounds = default;
            return default;
        }

        if (fragmentBounds.IsEmpty || fragmentBounds.Width <= 0 || fragmentBounds.Height <= 0)
        {
            var drawableBounds = drawable.Bounds;

            var width = fragmentBounds.Width <= 0
                ? Math.Abs(drawableBounds.Left) + drawableBounds.Width
                : fragmentBounds.Width;

            var height = fragmentBounds.Height <= 0
                ? Math.Abs(drawableBounds.Top) + drawableBounds.Height
                : fragmentBounds.Height;

            fragmentBounds = SKRect.Create(0f, 0f, width, height);
        }

        drawable.PostProcess(fragmentBounds, SKMatrix.Identity);

        bounds = fragmentBounds;
        return drawable;
    }

    public static SKPicture? ToModel(SvgFragment svgFragment, IAssetLoader assetLoader, out SKDrawable? skDrawable, out SKRect? skBounds, DrawAttributes ignoreAttributes = DrawAttributes.None)
    {
        var references = new HashSet<Uri>
        {
            svgFragment is SvgDocument svgDocument ? svgDocument.BaseUri : svgFragment.OwnerDocument.BaseUri
        };
        var drawable = ToDrawable(svgFragment, assetLoader, references, out var bounds, ignoreAttributes);
        if (drawable is null || bounds is null)
        {
            skDrawable = default;
            skBounds = default;
            return default;
        }

        var picture = drawable.Snapshot(bounds.Value);
        skDrawable = drawable;
        skBounds = bounds;
        return picture;
    }

    public static SvgDocument? OpenSvg(string path, SvgParameters? parameters = null)
    {
        return SvgDocument.Open<SvgDocument>(path, new SvgOptions(parameters?.Entities, parameters?.Css));
    }

    public static SvgDocument? OpenSvgz(string path, SvgParameters? parameters = null)
    {
        using var fileStream = System.IO.File.OpenRead(path);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var memoryStream = new System.IO.MemoryStream();

        gzipStream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        return Open(memoryStream, parameters);
    }

    public static SvgDocument? Open(string path, SvgParameters? parameters = null)
    {
        var extension = System.IO.Path.GetExtension(path);
        return extension.ToLower() switch
        {
            ".svg" => OpenSvg(path, parameters),
            ".svgz" => OpenSvgz(path, parameters),
            _ => OpenSvg(path, parameters),
        };
    }

    public static SvgDocument? Open(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        return SvgDocument.Open<SvgDocument>(stream, new SvgOptions(parameters?.Entities, parameters?.Css));
    }

    public static SvgDocument? FromSvg(string svg)
    {
        return SvgDocument.FromSvg<SvgDocument>(svg);
    }

    public static SvgDocument? Open(XmlReader reader)
    {
        return SvgDocument.Open<SvgDocument>(reader);
    }
}
