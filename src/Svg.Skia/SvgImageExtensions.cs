// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using SkiaSharp;

namespace Svg.Skia
{
    public static class SvgImageExtensions
    {
        private const string MimeTypeSvg = "image/svg+xml";
        private static byte[] s_gZipMagicHeaderBytes = { 0x1f, 0x8b };

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
            // should work with http: and file: protocol urls
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
                using (var stream = new MemoryStream(Encoding.Default.GetBytes(data)))
                {
                    return LoadSvg(stream, svgOwnerDocument.BaseUri);
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
    }
}
