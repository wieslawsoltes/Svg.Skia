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
            writeLine($"{indent}{element.GetType().Name} [{element.Name}] [parent={element.Parent?.Name}]");
            if (printAttributes)
            {
#if true
                if (element is ISvgAttributePrinter attributePrinter)
                {
                    attributePrinter.Print(indent + "  ");
                }
#else
                foreach (var attribute in element.Attributes)
                {
                    writeLine($"{indent}  {attribute.Key}='{attribute.Value}'");
                }
#endif
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
                    var element = XmlLoader.Open(path.FullName, elementFactory);
                    if (element != null)
                    {
                        results.Add((path, element));
                    }
                }
#if true
                catch (Exception)
                {
                }
#else
                catch (Exception ex)
                {
                    Console.WriteLine($"{path.FullName}");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
#endif
            }

            sw.Stop();
            Console.WriteLine($"{sw.Elapsed.TotalMilliseconds}ms [{sw.Elapsed}], {paths.Count} files");
#if true
            foreach (var result in results)
            {
                Console.WriteLine($"{result.path.FullName}");
                var element = result.element;
                if (element != null)
                {
                    PrintElement(element, Console.WriteLine, printAttributes: true);
                }
            }
#endif
        }
    }
}
