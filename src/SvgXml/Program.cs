using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Svg;

namespace SvgXml
{
    internal class Program
    {
        private static Element? Open(XmlReader reader)
        {
            var elements = new List<Element>();
            var stack = new Stack<Element>();
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        {
                            var name = reader.LocalName;
                            var element = SvgElementFactory.Create(name);

                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    element.Attributes.Add(reader.Name, reader.Value);
                                }
                                while (reader.MoveToNextAttribute());
                                reader.MoveToElement();
                            }

                            var nodes = stack.Count > 0 ? stack.Peek().Children : elements;
                            nodes.Add(element);

                            if (!reader.IsEmptyElement)
                            {
                                stack.Push(element);
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
            if (elements.Count == 1)
            {
                return elements[0];
            }
            return null;
        }

        private static Element? Open(Stream stream)
        {
            var settings = new XmlReaderSettings()
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                IgnoreWhitespace = true,
                IgnoreComments = true
            };
            var reader = XmlReader.Create(stream, settings);
            var element = Open(reader);
            return element;
        }

        private static Element? Open(string path)
        {
            using var stream = File.OpenRead(path);
            return Open(stream);
        }

        private static void PrintElement(Element element, bool printAttributes = true, string indent = "")
        {
            Console.WriteLine($"{indent}{element.GetType().Name} [{element.Name}]");
            if (printAttributes)
            {
                foreach (var attribute in element.Attributes)
                {
                    Console.WriteLine($"{indent}  {attribute.Key}='{attribute.Value}'");
                }
            }
            foreach (var child in element.Children)
            {
                PrintElement(child, printAttributes, indent + "  ");
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
            if (args.Length != 1)
            {
                Console.WriteLine($"Usage: {nameof(SvgXml)} <directory>");
                return;
            }

            var directory = new DirectoryInfo(args[0]);
            var paths = new List<FileInfo>();
            GetFiles(directory, "*.svg", paths);
            paths.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));

            var sw = Stopwatch.StartNew();

            var results = new List<(FileInfo path, Element element)>();

            foreach (var path in paths)
            {
                try
                {
                    var element = Open(path.FullName);
                    if (element != null)
                    {
                        results.Add((path, element));
                    }
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
                var element = result.element;
                if (element != null)
                {
                    PrintElement(element, printAttributes: true);
                }
            }
        }
    }
}
