using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace Svg
{
    public abstract class SvgElement
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public List<SvgElement> Children { get; set; }
        public Dictionary<string, string> Attributes { get; set; }

        public SvgElement()
        {
            Children = new List<SvgElement>();
            Attributes = new Dictionary<string, string>();
        }
    }

    public class SvgDocument : SvgFragment
    {
    }

    public class SvgElementFactory
    {
        public static SvgElement Create(string name)
        {
            return name switch
            {
                // Basic Shapes
                "circle" => new SvgCircle() { Name = name },
                "ellipse" => new SvgEllipse() { Name = name },
                "slinevg" => new SvgLine() { Name = name },
                "polygon" => new SvgPolygon() { Name = name },
                "polyline" => new SvgPolyline() { Name = name },
                "rect" => new SvgRectangle() { Name = name },
                // Clipping and Masking
                "clipPath" => new SvgClipPath() { Name = name },
                "mask" => new SvgMask() { Name = name },
                // Document Structure
                "defs" => new SvgDefinitionList() { Name = name },
                "desc" => new SvgDescription() { Name = name },
                "metadata" => new SvgDocumentMetadata() { Name = name },
                "svg" => new SvgFragment() { Name = name },
                "g" => new SvgGroup() { Name = name },
                "image" => new SvgImage() { Name = name },
                "switch" => new SvgSwitch() { Name = name },
                "symbol" => new SvgSymbol() { Name = name },
                "title" => new SvgTitle() { Name = name },
                "use" => new SvgUse() { Name = name },
                // Extensibility
                "foreignObject" => new SvgForeignObject() { Name = name },
                // Filter Effects
                "feBlend" => new FilterEffects.SvgBlend() { Name = name },
                "feColorMatrix" => new FilterEffects.SvgColourMatrix() { Name = name },
                "feComponentTransfer" => new FilterEffects.SvgComponentTransfer() { Name = name },
                "feComposite" => new FilterEffects.SvgComposite() { Name = name },
                "feConvolveMatrix" => new FilterEffects.SvgConvolveMatrix() { Name = name },
                "feDiffuseLighting" => new FilterEffects.SvgDiffuseLighting() { Name = name },
                "feDisplacementMap" => new FilterEffects.SvgDisplacementMap() { Name = name },
                "feDistantLight" => new FilterEffects.SvgDistantLight() { Name = name },
                "feFlood" => new FilterEffects.SvgFlood() { Name = name },
                "feFuncA" => new FilterEffects.SvgFuncA() { Name = name },
                "feFuncB" => new FilterEffects.SvgFuncB() { Name = name },
                "feFuncG" => new FilterEffects.SvgFuncG() { Name = name },
                "feFuncR" => new FilterEffects.SvgFuncR() { Name = name },
                "feGaussianBlur" => new FilterEffects.SvgGaussianBlur() { Name = name },
                "feImage" => new FilterEffects.SvgImage() { Name = name },
                "feMerge" => new FilterEffects.SvgMerge() { Name = name },
                "feMorphology" => new FilterEffects.SvgMorphology() { Name = name },
                "feOffset" => new FilterEffects.SvgOffset() { Name = name },
                "fePointLight" => new FilterEffects.SvgPointLight() { Name = name },
                "feSpecularLighting" => new FilterEffects.SvgSpecularLighting() { Name = name },
                "feSpotLight" => new FilterEffects.SvgSpotLight() { Name = name },
                "feTile" => new FilterEffects.SvgTile() { Name = name },
                "feTurbulence" => new FilterEffects.SvgTurbulence() { Name = name },
                // Linking
                "a" => new SvgAnchor() { Name = name },
                // Painting
                "stop" => new SvgGradientStop() { Name = name },
                "linearGradient" => new SvgLinearGradientServer() { Name = name },
                "marker" => new SvgMarker() { Name = name },
                "pattern" => new SvgPatternServer() { Name = name },
                "radialGradient" => new SvgRadialGradientServer() { Name = name },
                // Paths
                "path" => new SvgPath() { Name = name },
                // Scripting
                "script" => new SvgScript() { Name = name },
                // Text
                "font" => new SvgFont() { Name = name },
                "font-face" => new SvgFontFace() { Name = name },
                "font-face-src" => new SvgFontFaceSrc() { Name = name },
                "font-face-uri" => new SvgFontFaceUri() { Name = name },
                "glyph" => new SvgGlyph() { Name = name },
                "missing-glyph" => new SvgMissingGlyph() { Name = name },
                "text" => new SvgText() { Name = name },
                "textPath" => new SvgTextPath() { Name = name },
                "tref" => new SvgTextRef() { Name = name },
                "tspan" => new SvgTextSpan() { Name = name },
                // Unknown
                _ => new SvgUnknownElement() { Name = name },
            };
        }
    }

    public class SvgUnknownElement : SvgElement
    {
    }

    // ------------------------------------------------------------------------
    // Basic Shapes
    // ------------------------------------------------------------------------

    public abstract class SvgVisualElement : SvgElement
    {
    }

    public abstract class SvgPathBasedElement : SvgVisualElement
    {
    }

    public abstract class SvgMarkerElement : SvgPathBasedElement
    {
    }

    // "circle"
    public class SvgCircle : SvgPathBasedElement
    {
    }

    // "ellipse"
    public class SvgEllipse : SvgPathBasedElement
    {
    }

    // "line"
    public class SvgLine : SvgMarkerElement
    {
    }

    // "polygon"
    public class SvgPolygon : SvgMarkerElement
    {
    }

    // "polyline"
    public class SvgPolyline : SvgPolygon
    {
    }

    // "rect"
    public class SvgRectangle : SvgPathBasedElement
    {
    }

    // ------------------------------------------------------------------------
    // Clipping and Masking
    // ------------------------------------------------------------------------

    // "clipPath"
    public class SvgClipPath : SvgElement
    {
    }

    // "mask"
    public class SvgMask : SvgElement
    {
    }

    // ------------------------------------------------------------------------
    // Document Structure
    // ------------------------------------------------------------------------

    // "defs"
    public class SvgDefinitionList : SvgElement
    {
    }

    // "desc"
    public class SvgDescription : SvgElement
    {
    }

    // "metadata"
    public class SvgDocumentMetadata : SvgElement
    {
    }

    // "svg"
    public class SvgFragment : SvgElement
    {
    }

    // "g"
    public class SvgGroup : SvgMarkerElement
    {
    }

    // "image"
    public class SvgImage : SvgVisualElement
    {
    }

    // "switch"
    public class SvgSwitch : SvgVisualElement
    {
    }

    // "symbol"
    public class SvgSymbol : SvgVisualElement
    {
    }

    // "title"
    public class SvgTitle : SvgElement
    {
    }

    // "use"
    public class SvgUse : SvgVisualElement
    {
    }

    // ------------------------------------------------------------------------
    // Extensibility
    // ------------------------------------------------------------------------

    // "foreignObject"
    public class SvgForeignObject : SvgVisualElement
    {
    }

    // ------------------------------------------------------------------------
    // Filter Effects
    // ------------------------------------------------------------------------

    namespace FilterEffects
    {
        // "filter"
        public class SvgFilter : SvgElement
        {
        }

        public abstract class SvgFilterPrimitive : SvgElement
        {
        }

        // "feBlend"
        public class SvgBlend : SvgFilterPrimitive
        {
        }

        // "feColorMatrix"
        public class SvgColourMatrix : SvgFilterPrimitive
        {
        }

        // "feComponentTransfer"
        public class SvgComponentTransfer : SvgFilterPrimitive
        {
        }

        // "feComposite"
        public class SvgComposite : SvgFilterPrimitive
        {
        }

        // "feConvolveMatrix"
        public class SvgConvolveMatrix : SvgFilterPrimitive
        {
        }

        // "feDiffuseLighting"
        public class SvgDiffuseLighting : SvgFilterPrimitive
        {
        }

        // "feDisplacementMap"
        public class SvgDisplacementMap : SvgFilterPrimitive
        {
        }

        // "feDistantLight"
        public class SvgDistantLight : SvgElement
        {
        }

        // "feFlood"
        public class SvgFlood : SvgFilterPrimitive
        {
        }

        public abstract class SvgComponentTransferFunction : SvgElement
        {
        }

        // "feFuncA"
        public class SvgFuncA : SvgComponentTransferFunction
        {
        }

        // "feFuncB"
        public class SvgFuncB : SvgComponentTransferFunction
        {
        }

        // "feFuncG"
        public class SvgFuncG : SvgComponentTransferFunction
        {
        }

        // "feFuncR"
        public class SvgFuncR : SvgComponentTransferFunction
        {
        }

        // "feGaussianBlur"
        public class SvgGaussianBlur : SvgFilterPrimitive
        {
        }

        // "feImage"
        public class SvgImage : SvgFilterPrimitive
        {
        }

        // "feMerge"
        public class SvgMerge : SvgFilterPrimitive
        {
        }

        // "feMorphology"
        public class SvgMorphology : SvgFilterPrimitive
        {
        }

        // "feOffset"
        public class SvgOffset : SvgFilterPrimitive
        {
        }

        // "fePointLight"
        public class SvgPointLight : SvgElement
        {
        }

        // "feSpecularLighting"
        public class SvgSpecularLighting : SvgFilterPrimitive
        {
        }

        // "feSpotLight"
        public class SvgSpotLight : SvgElement
        {
        }

        // "feTile"
        public class SvgTile : SvgFilterPrimitive
        {
        }

        // "feTurbulence"
        public class SvgTurbulence : SvgFilterPrimitive
        {
        }
    }

    // Linking

    // "a"
    public class SvgAnchor : SvgElement
    {
    }

    // Painting

    public abstract class SvgPaintServer : SvgElement
    {
    }

    public class SvgColourServer : SvgPaintServer
    {
    }

    public class SvgDeferredPaintServer : SvgPaintServer
    {
    }

    public abstract class SvgGradientServer : SvgPaintServer
    {
    }

    // "stop"
    public class SvgGradientStop : SvgElement
    {
    }

    // "linearGradient"
    public class SvgLinearGradientServer : SvgGradientServer
    {
    }

    // "marker"
    public class SvgMarker : SvgPathBasedElement
    {
    }

    // "pattern"
    public class SvgPatternServer : SvgPaintServer
    {
    }

    // "radialGradient"
    public class SvgRadialGradientServer : SvgGradientServer
    {

    }

    // ------------------------------------------------------------------------
    // Paths
    // ------------------------------------------------------------------------

    // "path"
    public class SvgPath : SvgMarkerElement
    {
    }

    // Scripting

    // "script"
    public class SvgScript : SvgElement
    {
    }

    // ------------------------------------------------------------------------
    // Text
    // ------------------------------------------------------------------------

    // "font"
    public class SvgFont : SvgElement
    {
    }

    // "font-face"
    public class SvgFontFace : SvgElement
    {
    }

    // "font-face-src"
    public class SvgFontFaceSrc : SvgElement
    {
    }

    // "font-face-uri"
    public class SvgFontFaceUri : SvgElement
    {
    }

    // "glyph"
    public class SvgGlyph : SvgPathBasedElement
    {
    }

    // "missing-glyph"
    public class SvgMissingGlyph : SvgGlyph
    {
    }

    public abstract class SvgTextBase : SvgVisualElement
    {
    }

    // "text"
    public class SvgText : SvgTextBase
    {
    }

    // "textPath"
    public class SvgTextPath : SvgTextBase
    {
    }

    // "tref"
    public class SvgTextRef : SvgTextBase
    {
    }

    // "tspan"
    public class SvgTextSpan : SvgTextBase
    {
    }
}

namespace SvgXml
{
    using Svg;

    internal class Program
    {
        private static List<SvgElement> ReadNodes(XmlReader reader)
        {
            var elements = new List<SvgElement>();
            var stack = new Stack<SvgElement>();
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        {
                            var name = reader.Name;
                            var node = SvgElementFactory.Create(name);

                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    node.Attributes.Add(reader.Name, reader.Value);
                                }
                                while (reader.MoveToNextAttribute());
                                reader.MoveToElement();
                            }

                            var nodes = stack.Count > 0 ? stack.Peek().Children : elements;
                            nodes.Add(node);

                            if (!reader.IsEmptyElement)
                            {
                                stack.Push(node);
                            }
                        }
                        break;
                    case XmlNodeType.EndElement:
                        {
                            stack.Pop();
                        }
                        break;
                }
            }
            return elements;
        }

        private static void Print(SvgElement node, bool printAttributes = true, string indent = "")
        {
            Console.WriteLine($"{indent}{node.GetType().Name} [{node.Name}]");
            if (printAttributes)
            {
                foreach (var attribute in node.Attributes)
                {
                    Console.WriteLine($"{indent}  {attribute.Key}='{attribute.Value}'");
                }
            }
            foreach (var child in node.Children)
            {
                Print(child, printAttributes, indent + "  ");
            }
        }

        private static void GetFiles(DirectoryInfo directory, string pattern, List<FileInfo> paths)
        {
            var files = Directory.EnumerateFiles(directory.FullName, pattern);
            if (files != null)
            {
                foreach (var path in files)
                {
                    paths.Add(new FileInfo(path));
                }
            }
        }

        private static void Main(string[] args)
        {
            var directory = new DirectoryInfo(args[0]);
            var paths = new List<FileInfo>();
            GetFiles(directory, "*.svg", paths);
            paths.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));

            var results = new List<(FileInfo path, List<SvgElement> nodes)>();

            var sw = Stopwatch.StartNew();

            foreach (var path in paths)
            {
                try
                {
                    var settings = new XmlReaderSettings()
                    {
                        ConformanceLevel = ConformanceLevel.Fragment,
                        IgnoreWhitespace = true,
                        IgnoreComments = true
                    };
                    var reader = XmlReader.Create(path.FullName, settings);
                    var nodes = ReadNodes(reader);
                    results.Add((path, nodes));
                }
                catch (Exception)
                {
                }
            }

            sw.Stop();
            Console.WriteLine($"{sw.Elapsed.TotalMilliseconds}ms [{sw.Elapsed}], {paths.Count} files");

            foreach (var result in results)
            {
                Console.WriteLine($"{result.path.FullName}");
                var nodes = result.nodes;
                if (nodes != null)
                {
                    foreach (var node in nodes)
                    {
                        Print(node, printAttributes: false);
                    }
                }
            }
        }
    }
}
