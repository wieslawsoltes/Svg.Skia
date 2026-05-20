using System;
using System.Collections.Generic;
using System.IO;

namespace Svg
{
    /// <summary>
    /// Shared SVG 2 static-subset external resource policy helpers.
    /// </summary>
    public static class SvgExternalResourceResolver
    {
        /// <summary>
        /// Gets the external resource policy after applying processing-mode restrictions.
        /// </summary>
        public static SvgExternalResourcePolicy GetEffectiveExternalResourcePolicy(SvgDocumentLoadOptions loadOptions)
        {
            var policy = loadOptions?.ExternalResources ?? SvgExternalResourcePolicy.Enabled;
            var processingMode = loadOptions?.ProcessingMode ?? SvgProcessingMode.Static;
            return GetEffectiveExternalResourcePolicy(processingMode, policy);
        }

        /// <summary>
        /// Gets the external resource policy after applying processing-mode restrictions.
        /// </summary>
        public static SvgExternalResourcePolicy GetEffectiveExternalResourcePolicy(
            SvgProcessingMode processingMode,
            SvgExternalResourcePolicy policy)
        {
            if (processingMode == SvgProcessingMode.SecureStatic ||
                processingMode == SvgProcessingMode.SecureAnimated)
            {
                if (policy == SvgExternalResourcePolicy.Enabled ||
                    policy == SvgExternalResourcePolicy.SameOrigin)
                {
                    return SvgExternalResourcePolicy.SameDocumentAndDataOnly;
                }
            }

            return policy;
        }

        /// <summary>
        /// Resolves a resource reference against the owning element's base URI.
        /// </summary>
        public static Uri ResolveResourceUri(SvgElement ownerElement, Uri uri)
        {
            if (uri.IsAbsoluteUri)
            {
                return uri;
            }

            var uriString = uri.OriginalString;
            if (string.IsNullOrEmpty(uriString) || uriString[0] == '#')
            {
                return uri;
            }

            return TryGetBaseUri(ownerElement, out var baseUri)
                ? ResolveRelativeUri(baseUri, uriString)
                : uri;
        }

        /// <summary>
        /// Attempts to parse and resolve a resource reference.
        /// </summary>
        public static bool TryResolveResourceUri(SvgElement ownerElement, string reference, out Uri uri)
        {
            uri = null;
            var href = UnwrapUrlReference(reference);
            if (string.IsNullOrWhiteSpace(href) ||
                !Uri.TryCreate(href.Trim(), UriKind.RelativeOrAbsolute, out var parsedUri))
            {
                return false;
            }

            uri = ResolveResourceUri(ownerElement, parsedUri);
            return true;
        }

        /// <summary>
        /// Returns whether the owner element may load the supplied resource URI.
        /// </summary>
        public static bool AllowsExternalResource(SvgElement ownerElement, Uri uri)
        {
            if (ownerElement is null)
            {
                throw new ArgumentNullException("ownerElement");
            }

            if (uri is null)
            {
                throw new ArgumentNullException("uri");
            }

            var document = ownerElement as SvgDocument ?? ownerElement.OwnerDocument;
            var loadOptions = document?.LoadOptions ?? new SvgDocumentLoadOptions();
            return AllowsExternalResource(ownerElement, uri, GetEffectiveExternalResourcePolicy(loadOptions));
        }

        /// <summary>
        /// Returns whether the owner element may load the supplied resource URI under a policy.
        /// </summary>
        public static bool AllowsExternalResource(
            SvgElement ownerElement,
            Uri uri,
            SvgExternalResourcePolicy externalResourcePolicy)
        {
            if (ownerElement is null)
            {
                throw new ArgumentNullException("ownerElement");
            }

            if (uri is null)
            {
                throw new ArgumentNullException("uri");
            }

            switch (externalResourcePolicy)
            {
                case SvgExternalResourcePolicy.Enabled:
                    return true;
                case SvgExternalResourcePolicy.SameOrigin:
                    return IsDataUri(uri) ||
                           IsSameDocumentResource(ownerElement, uri) ||
                           IsSameOriginResource(ownerElement, uri);
                case SvgExternalResourcePolicy.SameDocumentAndDataOnly:
                    return IsDataUri(uri) ||
                           IsSameDocumentResource(ownerElement, uri);
                case SvgExternalResourcePolicy.Disabled:
                    return IsSameDocumentResource(ownerElement, uri);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns whether a stylesheet URI may be loaded under the supplied SVG 2 resource policy.
        /// </summary>
        public static bool AllowsStylesheetResource(
            Uri stylesheetUri,
            Uri policyBaseUri,
            SvgDocumentLoadOptions loadOptions,
            SvgExternalResourcePolicy minimumPolicyForData)
        {
            switch (GetEffectiveExternalResourcePolicy(loadOptions))
            {
                case SvgExternalResourcePolicy.Disabled:
                    return false;
                case SvgExternalResourcePolicy.SameDocumentAndDataOnly:
                    return IsDataUri(stylesheetUri) &&
                           minimumPolicyForData == SvgExternalResourcePolicy.SameDocumentAndDataOnly;
                case SvgExternalResourcePolicy.SameOrigin:
                    return IsDataUri(stylesheetUri) ||
                           IsSameOriginResource(stylesheetUri, policyBaseUri);
                case SvgExternalResourcePolicy.Enabled:
                    return true;
                default:
                    return false;
            }
        }

        private static string UnwrapUrlReference(string reference)
        {
            if (reference is null)
            {
                return string.Empty;
            }

            var value = reference.Trim();
            if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase) &&
                value.EndsWith(")", StringComparison.Ordinal))
            {
                value = value.Substring(4, value.Length - 5).Trim();
                if (value.Length >= 2 &&
                    ((value[0] == '"' && value[value.Length - 1] == '"') ||
                     (value[0] == '\'' && value[value.Length - 1] == '\'')))
                {
                    value = value.Substring(1, value.Length - 2).Trim();
                }
            }

            return value;
        }

        private static Uri ResolveRelativeUri(Uri baseUri, string uriString)
        {
            var fragmentIndex = uriString.IndexOf('#');
            if (fragmentIndex < 0)
            {
                return new Uri(baseUri, uriString);
            }

            var path = uriString.Substring(0, fragmentIndex);
            var fragment = uriString.Substring(fragmentIndex);
            if (string.IsNullOrEmpty(path))
            {
                return new Uri(baseUri.AbsoluteUri + fragment);
            }

            var resolvedPath = new Uri(baseUri, path);
            return new Uri(resolvedPath.AbsoluteUri + fragment);
        }

        private static bool TryGetBaseUri(SvgElement ownerElement, out Uri baseUri)
        {
            baseUri = null;
            if (ownerElement is null)
            {
                return false;
            }

            var baseUriFragments = new Stack<string>();
            for (var current = ownerElement; current is not null; current = current.Parent)
            {
                if (TryGetXmlBase(current, out var baseUriFragment) &&
                    !string.IsNullOrWhiteSpace(baseUriFragment))
                {
                    baseUriFragments.Push(baseUriFragment.Trim());
                }
            }

            var resolvedBaseUri = (ownerElement as SvgDocument)?.BaseUri ?? ownerElement.OwnerDocument?.BaseUri;
            while (baseUriFragments.Count > 0)
            {
                var nextBaseUri = new Uri(baseUriFragments.Pop(), UriKind.RelativeOrAbsolute);
                if (!nextBaseUri.IsAbsoluteUri)
                {
                    if (resolvedBaseUri is null)
                    {
                        return false;
                    }

                    nextBaseUri = new Uri(resolvedBaseUri, nextBaseUri);
                }

                resolvedBaseUri = nextBaseUri;
            }

            if (resolvedBaseUri is null)
            {
                return false;
            }

            baseUri = resolvedBaseUri;
            return true;
        }

        private static bool TryGetXmlBase(SvgElement element, out string value)
        {
            if (element.TryGetAttribute("base", out var baseValue) && baseValue is not null)
            {
                value = baseValue;
                return true;
            }

            if (element.CustomAttributes.TryGetValue(SvgNamespaces.XmlNamespace + ":base", out var xmlBaseValue) &&
                xmlBaseValue is not null)
            {
                value = xmlBaseValue;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static bool IsDataUri(Uri uri)
        {
            return uri.IsAbsoluteUri &&
                   string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameDocumentResource(SvgElement ownerElement, Uri uri)
        {
            if (!uri.IsAbsoluteUri)
            {
                var uriString = uri.OriginalString;
                return string.IsNullOrEmpty(uriString) || uriString[0] == '#';
            }

            var document = ownerElement as SvgDocument ?? ownerElement.OwnerDocument;
            if (document?.BaseUri is not { IsAbsoluteUri: true } baseUri)
            {
                return false;
            }

            return HaveSameDocumentUri(uri, baseUri);
        }

        private static bool IsSameOriginResource(SvgElement ownerElement, Uri uri)
        {
            if (!uri.IsAbsoluteUri)
            {
                return IsSameDocumentResource(ownerElement, uri);
            }

            var document = ownerElement as SvgDocument ?? ownerElement.OwnerDocument;
            return IsSameOriginResource(uri, document?.BaseUri);
        }

        private static bool IsSameOriginResource(Uri resourceUri, Uri baseUri)
        {
            if (baseUri is null || !baseUri.IsAbsoluteUri || !resourceUri.IsAbsoluteUri)
            {
                return false;
            }

            if (resourceUri.IsFile || baseUri.IsFile)
            {
                return IsFileResourceUnderBaseDirectory(resourceUri, baseUri);
            }

            return string.Equals(resourceUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(resourceUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) &&
                   resourceUri.Port == baseUri.Port;
        }

        private static bool HaveSameDocumentUri(Uri left, Uri right)
        {
            var leftDocumentUri = GetDocumentUri(left);
            var rightDocumentUri = GetDocumentUri(right);

            return string.Equals(
                leftDocumentUri.AbsoluteUri,
                rightDocumentUri.AbsoluteUri,
                StringComparison.OrdinalIgnoreCase);
        }

        private static Uri GetDocumentUri(Uri uri)
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

        private static bool IsFileResourceUnderBaseDirectory(Uri resourceUri, Uri baseUri)
        {
            if (!resourceUri.IsFile || !baseUri.IsFile)
            {
                return false;
            }

            var basePath = Path.GetFullPath(baseUri.LocalPath);
            var baseDirectory = Directory.Exists(basePath)
                ? basePath
                : Path.GetDirectoryName(basePath);
            if (string.IsNullOrEmpty(baseDirectory))
            {
                return false;
            }

            var resourcePath = Path.GetFullPath(resourceUri.LocalPath);
            var normalizedBaseDirectory = EnsureTrailingDirectorySeparator(baseDirectory);
            return resourcePath.StartsWith(normalizedBaseDirectory, GetPathComparison());
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static StringComparison GetPathComparison()
        {
            return Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }
    }
}
