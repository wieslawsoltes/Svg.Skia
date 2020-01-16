// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SvgImageUtil
    {
        private const string MimeTypeSvg = "image/svg+xml";

        public static object? GetImage(SvgImage svgImage, string uriString)
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
                    return GetImageFromDataUri(svgImage, uriString);
                }

                if (!uri.IsAbsoluteUri)
                {
                    uri = new Uri(svgImage.OwnerDocument.BaseUri, uri);
                }

                return GetImageFromWeb(uri);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static object GetImageFromWeb(Uri uri)
        {
            // should work with http: and file: protocol urls
            var httpRequest = WebRequest.Create(uri);

            using (var webResponse = httpRequest.GetResponse())
            {
                using (var stream = webResponse.GetResponseStream())
                {
                    if (stream.CanSeek)
                        stream.Position = 0;

                    if (webResponse.ContentType.StartsWith(MimeTypeSvg, StringComparison.InvariantCultureIgnoreCase) ||
                        uri.LocalPath.EndsWith(".svg", StringComparison.InvariantCultureIgnoreCase))
                        return LoadSvg(stream, uri);
                    else
                        return SKImage.FromEncodedData(stream);
                }
            }
        }

        public static object? GetImageFromDataUri(SvgImage svgImage, string uriString)
        {
            var headerStartIndex = 5;
            var headerEndIndex = uriString.IndexOf(",", headerStartIndex);
            if (headerEndIndex < 0 || headerEndIndex + 1 >= uriString.Length)
                throw new Exception("Invalid data URI");

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
                    continue;

                var attribute = p[0].Trim();
                if (attribute.Equals("charset", StringComparison.InvariantCultureIgnoreCase))
                    charset = p[1].Trim();
            }

            var data = uriString.Substring(headerEndIndex + 1);
            if (mimeType.Equals(MimeTypeSvg, StringComparison.InvariantCultureIgnoreCase))
            {
                if (base64)
                {
                    var encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
                    data = encoding.GetString(Convert.FromBase64String(data));
                }
                using (var stream = new MemoryStream(Encoding.Default.GetBytes(data)))
                {
                    return LoadSvg(stream, svgImage.OwnerDocument.BaseUri);
                }
            }
            // support nonstandard "img" spelling of mimetype
            else if (mimeType.StartsWith("image/") || mimeType.StartsWith("img/"))
            {
                var dataBytes = base64 ? Convert.FromBase64String(data) : Encoding.Default.GetBytes(data);
                using (var stream = new MemoryStream(dataBytes))
                {
                    return SKImage.FromEncodedData(stream);
                }
            }
            else
                return null;
        }

        public static SvgDocument LoadSvg(Stream stream, Uri baseUri)
        {
            var document = SvgDocument.Open<SvgDocument>(stream);
            document.BaseUri = baseUri;
            return document;
        }
    }
}
