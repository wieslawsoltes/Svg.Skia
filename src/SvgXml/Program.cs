using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Svg;
using Xml;

namespace SvgXml
{
    internal class Program
    {
        private static void PrintElement(Element element, Action<string> writeLine, bool printAttributes = true, string indent = "")
        {
            writeLine($"{indent}{element.GetType().Name} [{element.Name}]");
            if (printAttributes)
            {
                foreach (var attribute in element.Attributes)
                {
                    writeLine($"{indent}  {attribute.Key}='{attribute.Value}'");
                }
            }
            foreach (var child in element.Children)
            {
                PrintElement(child, writeLine, printAttributes, indent + "  ");
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
            var elementFactory = new SvgElementFactory();

            foreach (var path in paths)
            {
                try
                {
                    var element = Element.Open(path.FullName, elementFactory);
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
                    PrintElement(element, Console.WriteLine, printAttributes: true);
                }
            }
        }
    }
}
