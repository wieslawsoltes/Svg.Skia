using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;
using Svg.Model.Drawables;
using Svg.Model.Primitives;

namespace Svg.Model
{
    public static partial class SvgModelExtensions
    {
        private const string MimeTypeSvg = "image/svg+xml";

        private static byte[] s_gZipMagicHeaderBytes => new byte[2] { 0x1f, 0x8b };

        static SvgModelExtensions()
        {
            SvgDocument.SkipGdiPlusCapabilityCheck = true;
            SvgDocument.PointsPerInch = 96;
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

        public static Drawable? ToDrawable(SvgFragment svgFragment, IAssetLoader assetLoader, out Rect? bounds)
        {
            var size = GetDimensions(svgFragment);
            var fragmentBounds = Rect.Create(size);
            var drawable = DrawableFactory.Create(svgFragment, fragmentBounds, null, assetLoader);
            if (drawable is null)
            {
                bounds = default;
                return default;
            }
            drawable.PostProcess();

            if (fragmentBounds.IsEmpty)
            {
                var drawableBounds = drawable.Bounds;
                fragmentBounds = Rect.Create(
                    0f,
                    0f,
                    Math.Abs(drawableBounds.Left) + drawableBounds.Width,
                    Math.Abs(drawableBounds.Top) + drawableBounds.Height);
            }

            bounds = fragmentBounds;
            return drawable;
        }

        public static Picture? ToModel(SvgFragment svgFragment, IAssetLoader assetLoader)
        {
            var drawable = ToDrawable(svgFragment, assetLoader, out var bounds);
            if (drawable is null || bounds is null)
            {
                return default;
            }
            var picture = drawable.Snapshot(bounds.Value);
            return picture;
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
