using System.IO;
using System.IO.Compression;
using System.Xml;
using Xml;

namespace Svg
{
    public class SvgDocument : SvgFragment
    {
        public static IElementFactory s_elementFactory = new SvgElementFactory();


        public static void GetElements<T>(Element element, List<T> elements)
        {
            foreach (var child in element.Children)
            {
                if (child is T t)
                {
                    elements.Add(t);
                }
                else
                {
                    GetElements(child, elements);
                }
            }
        }

        public static SvgDocument? Open(XmlReader reader)
        {
            var element = XmlLoader.Read(reader, s_elementFactory);
            if (element is SvgDocument svgDocument)
            {
                return svgDocument;
            }
            return null;
        }

        public static SvgDocument? Open(Stream stream)
        {
            using var reader = XmlReader.Create(stream, XmlLoader.s_settings);
            return Open(reader);
        }

        public static SvgDocument? OpenSvg(string path)
        {
            using var stream = File.OpenRead(path);
            return Open(stream);
        }

        public static SvgDocument? OpenSvgz(string path)
        {
            using var fileStream = File.OpenRead(path);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var memoryStream = new MemoryStream();
            gzipStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            return Open(memoryStream);
        }

        public static SvgDocument? Open(string path)
        {
            var extension = Path.GetExtension(path);
            return extension.ToLower() switch
            {
                ".svg" => OpenSvg(path),
                ".svgz" => OpenSvgz(path),
                _ => OpenSvg(path),
            };
        }

        public static SvgDocument? FromSvg(string svg)
        {
            using var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream);
            streamWriter.Write(svg);
            streamWriter.Flush();
            memoryStream.Position = 0;
            return Open(memoryStream);
        }

        private static bool IsStyleAttribute(string name)
        {
            switch (name)
            {
                case "alignment-baseline":
                case "baseline-shift":
                case "clip":
                case "clip-path":
                case "clip-rule":
                case "color":
                case "color-interpolation":
                case "color-interpolation-filters":
                case "color-profile":
                case "color-rendering":
                case "cursor":
                case "direction":
                case "display":
                case "dominant-baseline":
                case "enable-background":
                case "fill":
                case "fill-opacity":
                case "fill-rule":
                case "filter":
                case "flood-color":
                case "flood-opacity":
                case "font": // NOTE: css only
                case "font-family":
                case "font-size":
                case "font-size-adjust":
                case "font-stretch":
                case "font-style":
                case "font-variant":
                case "font-weight":
                case "glyph-orientation-horizontal":
                case "glyph-orientation-vertical":
                case "image-rendering":
                case "kerning":
                case "letter-spacing":
                case "lighting-color":
                case "marker":
                case "marker-end":
                case "marker-mid":
                case "marker-start":
                case "mask": // NOTE: css only
                case "opacity":
                case "overflow":
                case "pointer-events":
                case "shape-rendering":
                case "stop-color":
                case "stop-opacity":
                case "stroke":
                case "stroke-dasharray":
                case "stroke-dashoffset":
                case "stroke-linecap":
                case "stroke-linejoin":
                case "stroke-miterlimit":
                case "stroke-opacity":
                case "stroke-width":
                case "text-anchor":
                case "text-decoration":
                case "text-rendering":
                case "text-transform": // NOTE: css only
                case "unicode-bidi":
                case "visibility":
                case "word-spacing":
                case "writing-mode":
                    return true;
                default:
                    return false;
            }
        }
    }
}
