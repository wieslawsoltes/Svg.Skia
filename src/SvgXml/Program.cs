using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace SvgXml
{
    public abstract class SvgElement
    {
        public string Name { get; set; }
        public string Text { get; set; }
        public List<SvgElement> Children { get; set; }
        public Dictionary<string, string> Attributes;

        public SvgElement(string name)
        {
            Name = name;
            Children = new List<SvgElement>();
            Attributes = new Dictionary<string, string>();
        }
    }

    public class SvgUnknown : SvgElement
    {
        public SvgUnknown(string name) : base(name)
        {

        }
    }

    public class SvgFragment : SvgElement
    {
        public SvgFragment(string name) : base(name)
        {

        }
    }

    public class SvgGroup : SvgElement
    {
        public SvgGroup(string name) : base(name)
        {

        }
    }

    public class SvgPath : SvgElement
    {
        public SvgPath(string name) : base(name)
        {

        }
    }

    public class SvgRectangle : SvgElement
    {
        public SvgRectangle(string name) : base(name)
        {

        }
    }

    public class SvgText : SvgElement
    {
        public SvgText(string name) : base(name)
        {

        }
    }

    class Program
    {
        static SvgElement Create(string name)
        {
            switch (name)
            {
                case "svg":
                    return new SvgFragment(name);
                case "g":
                    return new SvgGroup(name);
                case "path":
                    return new SvgPath(name);
                case "rect":
                    return new SvgRectangle(name);
                case "text":
                    return new SvgText(name);
                default:
                    return new SvgUnknown(name);
            }
        }

        static List<SvgElement> ReadNodes(XmlReader reader)
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
                            var node = Create(name);

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

        static void Print(SvgElement node, bool printAttributes = true, string indent = "")
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

        static void GetFiles(DirectoryInfo directory, string pattern, List<FileInfo> paths)
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

        static void Main(string[] args)
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
                catch (Exception ex)
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
