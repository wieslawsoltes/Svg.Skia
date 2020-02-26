using System.IO;
using System.Xml;
using Xml;

namespace Svg
{
    public class SvgDocument : SvgFragment
    {
        public static IElementFactory s_elementFactory = new SvgElementFactory();

        public static SvgDocument? Open(XmlReader reader)
        {
            var element = XmlLoader.Open(reader, s_elementFactory);
            if (element is SvgDocument svgDocument)
            {
                return svgDocument;
            }
            return null;
        }

        public static SvgDocument? Open(Stream stream)
        {
            var element = XmlLoader.Open(stream, s_elementFactory);
            if (element is SvgDocument svgDocument)
            {
                return svgDocument;
            }
            return null;
        }

        public static SvgDocument? Open(string path)
        {
            var element = XmlLoader.Open(path, s_elementFactory);
            if (element is SvgDocument svgDocument)
            {
                return svgDocument;
            }
            return null;
        }
    }
}
