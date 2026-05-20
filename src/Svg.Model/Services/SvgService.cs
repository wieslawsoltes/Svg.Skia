// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml;
using ShimSkiaSharp;
using Svg;

namespace Svg.Model.Services;

public static class SvgService
{
    public static CultureInfo? s_systemLanguageOverride = default;

    private static readonly char[] s_spaceTab = { ' ', '\t' };

    private static readonly char[] s_comma = { ',' };

    private const string MimeTypeSvg = "image/svg+xml";

    private static byte[] GZipMagicHeaderBytes => new byte[] { 0x1f, 0x8b };

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

        var uriString = uri.OriginalString;
        if (uri.IsAbsoluteUri)
        {
            if (string.IsNullOrEmpty(uri.Fragment))
            {
                return default;
            }
        }
        else if (uriString.IndexOf('#') < 0)
        {
            return default;
        }

        var resourceUri = GetReferenceUri(uri, svgElement);
        if (!AllowsExternalResource(svgElement, resourceUri))
        {
            Trace.TraceWarning("Trying to resolve element reference from '{0}', but the document resource policy blocks it.", resourceUri);
            return default;
        }

        if (!IsSameDocumentResource(svgElement, resourceUri))
        {
            return GetExternalSvgReference<T>(resourceUri, svgElement);
        }

        var svgElementById = GetSameDocumentReference(svgElement, resourceUri);
        if (svgElementById is { })
        {
            return svgElementById as T;
        }

        return default;
    }

    private static SvgElement? GetSameDocumentReference(SvgElement svgElement, Uri uri)
    {
        var svgDocument = svgElement as SvgDocument ?? svgElement.OwnerDocument;
        if (!uri.IsAbsoluteUri)
        {
            return svgDocument?.GetElementById(uri);
        }

        var fragment = uri.Fragment;
        return string.IsNullOrWhiteSpace(fragment)
            ? null
            : svgDocument?.IdManager.GetElementById(fragment[0] == '#' ? fragment.Substring(1) : fragment);
    }

    private static T? GetExternalSvgReference<T>(Uri uri, SvgElement svgOwnerElement) where T : SvgElement
    {
        if (!uri.IsAbsoluteUri || string.IsNullOrWhiteSpace(uri.Fragment))
        {
            return default;
        }

        var documentUri = GetImageDocumentUri(uri);
        if (!SvgDocument.ResolveExternalElements.AllowsResolving(documentUri))
        {
            Trace.TraceWarning("Trying to resolve element reference from '{0}', but resolving external resources of that type is disabled.", documentUri);
            return default;
        }

        var svgDocument = LoadExternalSvgReferenceDocument(documentUri, GetEffectiveDocumentLoadOptions(svgOwnerElement));
        var fragment = uri.Fragment;
        if (fragment.Length > 0 && fragment[0] == '#')
        {
            fragment = fragment.Substring(1);
        }

        return svgDocument?.GetElementById(fragment) as T;
    }

    private static SvgDocument? LoadExternalSvgReferenceDocument(Uri documentUri, SvgDocumentLoadOptions? loadOptions)
    {
        try
        {
            if (documentUri.IsFile)
            {
                using var fileStream = System.IO.File.OpenRead(documentUri.LocalPath);
                return documentUri.LocalPath.EndsWith(".svgz", StringComparison.OrdinalIgnoreCase)
                    ? LoadSvgz(fileStream, documentUri, loadOptions, loadLinkedStylesheets: false)
                    : LoadSvg(fileStream, documentUri, loadOptions, loadLinkedStylesheets: false);
            }

#pragma warning disable 618, SYSLIB0014
            var request = WebRequest.Create(documentUri);
#pragma warning restore 618, SYSLIB0014
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

            var contentType = response.ContentType ?? string.Empty;
            var isSvgMimeType = contentType.StartsWith(MimeTypeSvg, StringComparison.OrdinalIgnoreCase);
            var isSvg = documentUri.LocalPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
            var isSvgz = documentUri.LocalPath.EndsWith(".svgz", StringComparison.OrdinalIgnoreCase);

            if (isSvgMimeType || isSvg)
            {
                return LoadSvg(stream, documentUri, loadOptions, loadLinkedStylesheets: false);
            }

            if (isSvgz)
            {
                return LoadSvgz(stream, documentUri, loadOptions, loadLinkedStylesheets: false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);
        }

        return default;
    }

    private static Uri GetReferenceUri(Uri uri, SvgElement svgOwnerElement)
    {
        return SvgExternalResourceResolver.ResolveResourceUri(svgOwnerElement, uri);
    }

    internal static Uri? GetEffectiveReferenceUri(SvgElement svgElement, Uri? fallback)
    {
        if (svgElement.TryGetEffectiveHrefString(out var hrefText))
        {
            var trimmedHrefText = hrefText?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedHrefText))
            {
                return null;
            }

            return Uri.TryCreate(trimmedHrefText, UriKind.RelativeOrAbsolute, out var href)
                ? href
                : null;
        }

        return fallback;
    }

    internal static Uri? GetEffectiveReferenceUri(SvgElement svgElement, string? fallback)
    {
        var hrefText = GetEffectiveHrefString(svgElement, fallback);
        var trimmedHrefText = hrefText?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedHrefText))
        {
            return null;
        }

        return Uri.TryCreate(trimmedHrefText, UriKind.RelativeOrAbsolute, out var href)
            ? href
            : null;
    }

    internal static string? GetEffectiveHrefString(SvgElement svgElement, string? fallback)
    {
        return svgElement.TryGetEffectiveHrefString(out var href)
            ? href
            : fallback;
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
        // Chrome ignores requiredFeatures, and the W3C PNG baselines for this
        // slice are stale. Match current browser behavior for rendering parity.
        return true;
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

        var systemLanguage = s_systemLanguageOverride ?? CultureInfo.InstalledUICulture;
        var systemLanguageTag = GetSystemLanguageTag(systemLanguage);
        if (string.IsNullOrWhiteSpace(systemLanguageTag))
        {
            return false;
        }

        foreach (var language in languages)
        {
            if (MatchesSystemLanguage(systemLanguageTag!, language.Trim()))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetSystemLanguageTag(CultureInfo culture)
    {
        var languageTag = culture.Name;
        return string.IsNullOrWhiteSpace(languageTag)
            ? null
            : languageTag.Replace('_', '-');
    }

    private static bool MatchesSystemLanguage(string systemLanguageTag, string requestedLanguage)
    {
        if (string.IsNullOrWhiteSpace(requestedLanguage))
        {
            return false;
        }

        requestedLanguage = requestedLanguage.Replace('_', '-');

        if (systemLanguageTag.Equals(requestedLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return systemLanguageTag.Length > requestedLanguage.Length &&
               systemLanguageTag.StartsWith(requestedLanguage, StringComparison.OrdinalIgnoreCase) &&
               systemLanguageTag[requestedLanguage.Length] == '-';
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
        return GetImageUri(uriString, (SvgElement)svgOwnerDocument);
    }

    internal static Uri GetImageUri(string uriString, SvgElement svgOwnerElement)
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

        return SvgExternalResourceResolver.ResolveResourceUri(svgOwnerElement, uri);
    }

    internal static Uri GetImageDocumentUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || string.IsNullOrEmpty(uri.Fragment))
        {
            return uri;
        }

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty
        };

        return builder.Uri;
    }

    internal static HashSet<Uri>? ExtendImageReferences(HashSet<Uri>? references, SvgFragment? fragment)
    {
        var document = fragment as SvgDocument ?? fragment?.OwnerDocument;
        var nextReferences = references is null ? null : new HashSet<Uri>(references);

        if (document?.BaseUri is not { } baseUri)
        {
            return nextReferences;
        }

        nextReferences ??= new HashSet<Uri>();
        nextReferences.Add(GetImageDocumentUri(baseUri));
        return nextReferences;
    }

    internal static object? GetImage(string uriString, SvgDocument svgOwnerDocument, ISvgAssetLoader assetLoader)
    {
        return GetImage(uriString, (SvgElement)svgOwnerDocument, assetLoader);
    }

    internal static object? GetImage(string uriString, SvgElement svgOwnerElement, ISvgAssetLoader assetLoader)
    {
        try
        {
            var uri = GetImageUri(uriString, svgOwnerElement);
            if (!AllowsExternalResource(svgOwnerElement, uri))
            {
                Trace.TraceWarning("Trying to resolve image from '{0}', but the document resource policy blocks it.", uri);
                return default;
            }

            if (uri.IsAbsoluteUri && uri.Scheme == "data")
            {
                return GetImageFromDataUri(uriString, svgOwnerElement, assetLoader);
            }

            if (!uri.IsAbsoluteUri)
            {
                return default;
            }

            if (!SvgDocument.ResolveExternalImages.AllowsResolving(uri))
            {
                Trace.TraceWarning("Trying to resolve image from '{0}', but resolving external resources of that type is disabled.", uri);
                return default;
            }

            return GetImageFromWeb(uri, svgOwnerElement, assetLoader);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);
            return default;
        }
    }

    internal static object? GetImageFromWeb(Uri uri, ISvgAssetLoader assetLoader)
    {
        return GetImageFromWeb(uri, svgOwnerElement: null, assetLoader);
    }

    private static object? GetImageFromWeb(Uri uri, SvgElement? svgOwnerElement, ISvgAssetLoader assetLoader)
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
            return LoadSvg(stream, uri, GetEffectiveDocumentLoadOptions(svgOwnerElement));
        }

        if (isSvgMimeType || isSvgz)
        {
            return LoadSvgz(stream, uri, GetEffectiveDocumentLoadOptions(svgOwnerElement));
        }

        return LoadImage(assetLoader, stream, uri, svgOwnerElement);
    }

    internal static object? GetImageFromDataUri(string? uriString, SvgElement svgOwnerElement, ISvgAssetLoader assetLoader)
    {
        if (uriString is null)
        {
            return default;
        }

        var imageBaseUri = GetImageUri(uriString, svgOwnerElement);

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
                        return LoadSvgz(bytesStream, imageBaseUri, GetEffectiveDocumentLoadOptions(svgOwnerElement));
                    }
                }

                var encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
                data = encoding.GetString(bytes);
            }

            var buffer = Encoding.Default.GetBytes(data);
            using var stream = new System.IO.MemoryStream(buffer);
            return LoadSvg(stream, imageBaseUri, GetEffectiveDocumentLoadOptions(svgOwnerElement));
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
                        return LoadSvgz(bytesStream, svgOwnerElement.OwnerDocument.BaseUri, GetEffectiveDocumentLoadOptions(svgOwnerElement));
                    }
                }

                using var stream = new System.IO.MemoryStream(bytes);
                return LoadImage(assetLoader, stream, imageBaseUri, svgOwnerElement);
            }
            else
            {
                var bytes = Encoding.Default.GetBytes(data);
                using var stream = new System.IO.MemoryStream(bytes);
                return LoadImage(assetLoader, stream, imageBaseUri, svgOwnerElement);
            }
        }

        return default;
    }

    private static SKImage LoadImage(
        ISvgAssetLoader assetLoader,
        System.IO.Stream stream,
        Uri resourceUri,
        SvgElement? svgOwnerElement)
    {
        if (assetLoader is ISvgImageAssetLoader imageAssetLoader)
        {
            return imageAssetLoader.LoadImage(
                stream,
                new SvgImageLoadContext(
                    resourceUri,
                    GetCrossOrigin(svgOwnerElement),
                    svgOwnerElement));
        }

        return assetLoader.LoadImage(stream);
    }

    private static string? GetCrossOrigin(SvgElement? svgOwnerElement)
    {
        return svgOwnerElement is { } &&
               svgOwnerElement.TryGetAttribute("crossorigin", out var crossOrigin) &&
               !string.IsNullOrWhiteSpace(crossOrigin)
            ? crossOrigin
            : null;
    }

    internal static SvgDocument LoadSvg(System.IO.Stream stream, Uri baseUri)
    {
        return LoadSvg(stream, baseUri, loadOptions: null);
    }

    private static SvgDocument LoadSvg(System.IO.Stream stream, Uri baseUri, SvgDocumentLoadOptions? loadOptions, bool loadLinkedStylesheets = true)
    {
        return SvgDocumentCompatibilityLoader.Open<SvgDocument>(
            stream,
            new SvgOptions(),
            baseUri,
            loadOptions,
            captureCompatibilityStyleState: false,
            loadLinkedStylesheets);
    }

    internal static SvgDocument LoadSvgz(System.IO.Stream stream, Uri baseUri)
    {
        return LoadSvgz(stream, baseUri, loadOptions: null);
    }

    private static SvgDocument LoadSvgz(System.IO.Stream stream, Uri baseUri, SvgDocumentLoadOptions? loadOptions, bool loadLinkedStylesheets = true)
    {
        using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
        using var memoryStream = new System.IO.MemoryStream();
        gzipStream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        return SvgDocumentCompatibilityLoader.Open<SvgDocument>(
            memoryStream,
            new SvgOptions(),
            baseUri,
            loadOptions,
            captureCompatibilityStyleState: false,
            loadLinkedStylesheets);
    }

    private static SvgDocument? ApplyParameters(SvgDocument? svgDocument, SvgParameters? parameters)
    {
        if (svgDocument is null)
        {
            return null;
        }

        svgDocument.LoadOptions = CloneDocumentLoadOptions(parameters?.LoadOptions);

        if (parameters?.CurrentColor is { } currentColor && CanApplyCurrentColorParameter(svgDocument))
        {
            svgDocument.Color = new SvgColourServer(currentColor);
        }

        return svgDocument;
    }

    public static SvgDocumentLoadOptions GetDocumentLoadOptions(SvgDocument svgDocument)
    {
        if (svgDocument is null)
        {
            throw new ArgumentNullException(nameof(svgDocument));
        }

        return GetEffectiveDocumentLoadOptions(svgDocument).Clone();
    }

    internal static bool AllowsExternalResource(SvgElement svgOwnerElement, Uri uri)
    {
        if (svgOwnerElement is null)
        {
            throw new ArgumentNullException(nameof(svgOwnerElement));
        }

        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        return AllowsExternalResource(
            svgOwnerElement,
            uri,
            SvgExternalResourceResolver.GetEffectiveExternalResourcePolicy(GetEffectiveDocumentLoadOptions(svgOwnerElement)));
    }

    private static bool AllowsExternalResource(
        SvgElement svgOwnerElement,
        Uri uri,
        SvgExternalResourcePolicy externalResourcePolicy)
    {
        return SvgExternalResourceResolver.AllowsExternalResource(svgOwnerElement, uri, externalResourcePolicy);
    }

    private static SvgDocumentLoadOptions GetEffectiveDocumentLoadOptions(SvgElement? svgElement)
    {
        var svgDocument = svgElement as SvgDocument ?? svgElement?.OwnerDocument;
        return GetEffectiveDocumentLoadOptions(svgDocument);
    }

    private static SvgDocumentLoadOptions GetEffectiveDocumentLoadOptions(SvgDocument? svgDocument)
    {
        return svgDocument?.LoadOptions ?? new SvgDocumentLoadOptions();
    }

    private static SvgDocumentLoadOptions CloneDocumentLoadOptions(SvgDocumentLoadOptions? loadOptions)
    {
        return loadOptions?.Clone() ?? new SvgDocumentLoadOptions();
    }

    private static bool IsDataUri(Uri uri)
    {
        return uri.IsAbsoluteUri &&
               string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameDocumentResource(SvgElement svgOwnerElement, Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            var uriString = uri.OriginalString;
            return string.IsNullOrEmpty(uriString) || uriString[0] == '#';
        }

        var svgOwnerDocument = svgOwnerElement as SvgDocument ?? svgOwnerElement.OwnerDocument;
        if (svgOwnerDocument?.BaseUri is not { IsAbsoluteUri: true } baseUri)
        {
            return false;
        }

        return HaveSameDocumentUri(uri, baseUri);
    }

    private static bool IsSameOriginResource(SvgElement svgOwnerElement, Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            return IsSameDocumentResource(svgOwnerElement, uri);
        }

        var svgOwnerDocument = svgOwnerElement as SvgDocument ?? svgOwnerElement.OwnerDocument;
        if (svgOwnerDocument?.BaseUri is not { IsAbsoluteUri: true } baseUri)
        {
            return false;
        }

        if (uri.IsFile || baseUri.IsFile)
        {
            return IsFileResourceUnderBaseDirectory(uri, baseUri);
        }

        return string.Equals(uri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) &&
               uri.Port == baseUri.Port;
    }

    private static bool HaveSameDocumentUri(Uri left, Uri right)
    {
        var leftDocumentUri = GetImageDocumentUri(left);
        var rightDocumentUri = GetImageDocumentUri(right);

        return string.Equals(
            leftDocumentUri.AbsoluteUri,
            rightDocumentUri.AbsoluteUri,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFileResourceUnderBaseDirectory(Uri resourceUri, Uri baseUri)
    {
        if (!resourceUri.IsFile || !baseUri.IsFile)
        {
            return false;
        }

        var basePath = System.IO.Path.GetFullPath(baseUri.LocalPath);
        var baseDirectory = System.IO.Directory.Exists(basePath)
            ? basePath
            : System.IO.Path.GetDirectoryName(basePath);
        if (string.IsNullOrEmpty(baseDirectory))
        {
            return false;
        }

        var resourcePath = System.IO.Path.GetFullPath(resourceUri.LocalPath);
        var normalizedBaseDirectory = EnsureTrailingDirectorySeparator(baseDirectory);
        return resourcePath.StartsWith(normalizedBaseDirectory, GetPathComparison());
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        if (path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
            path.EndsWith(System.IO.Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path;
        }

        return path + System.IO.Path.DirectorySeparatorChar;
    }

    private static StringComparison GetPathComparison()
    {
        return System.IO.Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static bool CanApplyCurrentColorParameter(SvgDocument svgDocument)
    {
        if (!svgDocument.TryGetAttribute("color", out var color))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(color) ||
               string.Equals(color, "inherit", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(color, "currentColor", StringComparison.OrdinalIgnoreCase);
    }

    public static SKSize GetDimensions(SvgFragment svgFragment, SKRect skViewport = default)
    {
        float w, h;
        var isWidthperc = svgFragment.Width.Type == SvgUnitType.Percentage;
        var isHeightperc = svgFragment.Height.Type == SvgUnitType.Percentage;

        var percentViewport = skViewport;

        if (svgFragment is SvgDocument)
        {
            if (percentViewport.IsEmpty && svgFragment.ViewBox.Width > 0 && svgFragment.ViewBox.Height > 0)
            {
                percentViewport = SKRect.Create(
                    svgFragment.ViewBox.MinX,
                    svgFragment.ViewBox.MinY,
                    svgFragment.ViewBox.Width,
                    svgFragment.ViewBox.Height);
            }
        }
        else if (percentViewport.IsEmpty && svgFragment.ViewBox.Width > 0 && svgFragment.ViewBox.Height > 0)
        {
            percentViewport = SKRect.Create(
                svgFragment.ViewBox.MinX,
                svgFragment.ViewBox.MinY,
                svgFragment.ViewBox.Width,
                svgFragment.ViewBox.Height);
        }

        if (isWidthperc && svgFragment is SvgDocument)
        {
            var bounds = percentViewport;
            w = bounds.Width * (svgFragment.Width.Value * 0.01f);
        }
        else
        {
            w = svgFragment.Width.ToDeviceValue(UnitRenderingType.Horizontal, svgFragment, percentViewport);
        }

        if (isHeightperc && svgFragment is SvgDocument)
        {
            var bounds = percentViewport;
            h = bounds.Height * (svgFragment.Height.Value * 0.01f);
        }
        else
        {
            h = svgFragment.Height.ToDeviceValue(UnitRenderingType.Vertical, svgFragment, percentViewport);
        }

        if (float.IsNaN(w) || float.IsInfinity(w))
        {
            w = 0f;
        }

        if (float.IsNaN(h) || float.IsInfinity(h))
        {
            h = 0f;
        }

        return new SKSize((float)Math.Round(w), (float)Math.Round(h));
    }

    public static SvgDocument? OpenSvg(string path, SvgParameters? parameters = null)
    {
        return OpenSvg(path, parameters, captureCompatibilityStyleState: false);
    }

    public static SvgDocument? OpenSvg(string path, SvgParameters? parameters, bool captureCompatibilityStyleState)
    {
        return ApplyParameters(
            SvgDocumentCompatibilityLoader.Open<SvgDocument>(
                path,
                new SvgOptions(parameters?.Entities, parameters?.Css),
                parameters?.LoadOptions,
                captureCompatibilityStyleState),
            parameters);
    }

    public static SvgDocument? OpenSvgz(string path, SvgParameters? parameters = null)
    {
        return OpenSvgz(path, parameters, captureCompatibilityStyleState: false);
    }

    public static SvgDocument? OpenSvgz(string path, SvgParameters? parameters, bool captureCompatibilityStyleState)
    {
        using var fileStream = System.IO.File.OpenRead(path);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var memoryStream = new System.IO.MemoryStream();

        gzipStream.CopyTo(memoryStream);
        memoryStream.Position = 0;

        return ApplyParameters(
            SvgDocumentCompatibilityLoader.Open<SvgDocument>(
                memoryStream,
                new SvgOptions(parameters?.Entities, parameters?.Css),
                new Uri(System.IO.Path.GetFullPath(path), UriKind.Absolute),
                parameters?.LoadOptions,
                captureCompatibilityStyleState),
            parameters);
    }

    public static SvgDocument? Open(string path, SvgParameters? parameters = null)
    {
        return Open(path, parameters, captureCompatibilityStyleState: false);
    }

    public static SvgDocument? Open(string path, SvgParameters? parameters, bool captureCompatibilityStyleState)
    {
        var extension = System.IO.Path.GetExtension(path);
        return extension.ToLower() switch
        {
            ".svg" => OpenSvg(path, parameters, captureCompatibilityStyleState),
            ".svgz" => OpenSvgz(path, parameters, captureCompatibilityStyleState),
            ".xml" => IsVectorDrawablePath(path) ? OpenVectorDrawable(path, parameters) : OpenSvg(path, parameters, captureCompatibilityStyleState),
            _ => OpenSvg(path, parameters, captureCompatibilityStyleState),
        };
    }

    public static SvgDocument? OpenVectorDrawable(string path, SvgParameters? parameters = null)
    {
        using var fileStream = System.IO.File.OpenRead(path);
        var svgDocument = OpenVectorDrawable(fileStream, parameters);
        if (svgDocument is { })
        {
            svgDocument.BaseUri = new Uri(System.IO.Path.GetFullPath(path));
        }

        return svgDocument;
    }

    public static SvgDocument? OpenVectorDrawable(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        return ApplyParameters(VectorDrawableConverter.Open(stream), parameters);
    }

    public static SvgDocument? FromVectorDrawable(string xml)
    {
        return VectorDrawableConverter.FromXml(xml);
    }

    public static SvgDocument? OpenVectorDrawable(XmlReader reader)
    {
        return VectorDrawableConverter.Open(reader);
    }

    public static SvgDocument? Open(System.IO.Stream stream, SvgParameters? parameters = null)
    {
        return Open(stream, parameters, captureCompatibilityStyleState: false);
    }

    public static SvgDocument? Open(System.IO.Stream stream, SvgParameters? parameters, bool captureCompatibilityStyleState)
    {
        return ApplyParameters(
            SvgDocumentCompatibilityLoader.Open<SvgDocument>(
                stream,
                new SvgOptions(parameters?.Entities, parameters?.Css),
                parameters?.LoadOptions,
                captureCompatibilityStyleState),
            parameters);
    }

    public static SvgDocument? FromSvg(string svg)
    {
        return FromSvg(svg, parameters: null, captureCompatibilityStyleState: false);
    }

    public static SvgDocument? FromSvg(string svg, SvgParameters? parameters)
    {
        return FromSvg(svg, parameters, captureCompatibilityStyleState: false);
    }

    public static SvgDocument? FromSvg(string svg, bool captureCompatibilityStyleState)
    {
        return FromSvg(svg, parameters: null, captureCompatibilityStyleState);
    }

    public static SvgDocument? FromSvg(string svg, SvgParameters? parameters, bool captureCompatibilityStyleState)
    {
        if (string.IsNullOrEmpty(svg))
        {
            throw new ArgumentNullException(nameof(svg));
        }

        using var memoryStream = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(svg));
        return Open(memoryStream, parameters, captureCompatibilityStyleState);
    }

    public static SvgDocument? Open(XmlReader reader)
    {
        return Open(reader, captureCompatibilityStyleState: false);
    }

    public static SvgDocument? Open(XmlReader reader, bool captureCompatibilityStyleState)
    {
        return SvgDocumentCompatibilityLoader.Open<SvgDocument>(reader, captureCompatibilityStyleState);
    }

    private static bool IsVectorDrawablePath(string path)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };

        using var fileStream = System.IO.File.OpenRead(path);
        using var xmlReader = XmlReader.Create(fileStream, settings);
        return VectorDrawableConverter.IsVectorDrawable(xmlReader);
    }
}
