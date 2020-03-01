using System.IO;
using System.IO.Compression;
using System.Xml;
using Xml;

namespace Svg
{
    public class SvgDocument : SvgFragment
    {
        public static IElementFactory s_elementFactory = new SvgElementFactory();

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
    }
}
