using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Linq;
using Svg;
using Xml;

namespace SvgXml.Test
{
    internal class Program
    {
        internal class ElementInfo
        {
            public string? Name { get; set; }
            public Type? Type { get; set; }
            public (PropertyInfo Property, AttributeAttribute? Attribute)[]? Attributes { get; set; }
        }

        private static PropertyInfo[] GetPublicProperties(Type type)
        {
            var propertyInfos = new List<PropertyInfo>();
            var considered = new List<Type>();
            var queue = new Queue<Type>();
            considered.Add(type);
            queue.Enqueue(type);
            while (queue.Count > 0)
            {
                var subType = queue.Dequeue();
                foreach (var subInterface in subType.GetInterfaces())
                {
                    if (considered.Contains(subInterface))
                    {
                        continue;
                    }
                    considered.Add(subInterface);
                    queue.Enqueue(subInterface);
                }
                var typeProperties = subType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance);
                var newPropertyInfos = typeProperties.Where(x => !propertyInfos.Contains(x));
                propertyInfos.InsertRange(0, newPropertyInfos);
            }
            return propertyInfos.ToArray();
        }

        private static void PrintAttributeUsage(Action<string> write)
        {
            var elements = typeof(SvgDocument).Assembly
                .GetExportedTypes()
                .Where(x => x.GetCustomAttributes(typeof(ElementAttribute), true).Length > 0 && x.IsSubclassOf(typeof(Element)))
                .Select(x => new ElementInfo
                {
                    Name = ((ElementAttribute)x.GetCustomAttributes(typeof(ElementAttribute), true)[0]).Name,
                    Type = x,
                    Attributes = GetPublicProperties(x).Select(x => (x, (AttributeAttribute?)x.GetCustomAttributes(typeof(AttributeAttribute), true).FirstOrDefault())).ToArray()
                })
                .OrderBy(x => x.Name);
#if true
            foreach (var element in elements)
            {
                write($"{element.Name} [{element.Type?.Name}]");
                if (element.Attributes != null)
                {
                    foreach (var attribute in element.Attributes)
                    {
                        if (attribute.Attribute != null)
                        {
                            write($"  {attribute.Attribute.Name} [{attribute.Property.Name}]");
                        }
                    }
                }
            }
#else
            foreach (var element in elements)
            {
                write($"public interface I{element.Type?.Name}");
                write($"{{");
                if (element.Attributes != null)
                {
                    foreach (var attribute in element.Attributes)
                    {
                        if (attribute.Attribute != null)
                        {
                            write($"    {attribute.Property.Name} {{ get; set; }}");
                        }
                    }
                }
                write($"}}");
            }
#endif
        }

        private static void Main(string[] args)
        {
            Action<string> write = Console.WriteLine;

            if (args.Length == 0)
            {
                PrintAttributeUsage(write);
                return;
            }

            if (args.Length != 1)
            {
                write($"Usage: {nameof(SvgXml)} <directory>");
                return;
            }

            var directory = new DirectoryInfo(args[0]);
            var paths = new List<FileInfo>();
            var files = Directory.EnumerateFiles(directory.FullName, "*.svg");
            if (files != null)
            {
                foreach (var path in files)
                {
                    paths.Add(new FileInfo(path));
                }
            }
            paths.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));

            var sw = Stopwatch.StartNew();

            var results = new List<(FileInfo path, SvgDocument document)>();
            var elementFactory = new SvgElementFactory();
            var errors = new List<(FileInfo path, Exception ex)>();

            foreach (var path in paths)
            {
                try
                {
                    var document = SvgDocument.Open(path.FullName);
                    if (document != null)
                    {
                        document.LoadStyles();
                        results.Add((path, document));
                    }
                }
                catch (Exception ex)
                {
                    errors.Add((path, ex));
                }
            }

            sw.Stop();
            write($"# {sw.Elapsed.TotalMilliseconds}ms [{sw.Elapsed}], {paths.Count} files");
#if true
            void Print(Exception ex)
            {
                write(ex.Message);
                if (ex.StackTrace != null)
                {
                    write(ex.StackTrace);
                }
                if (ex.InnerException != null)
                {
                    Print(ex.InnerException);
                }
            }
            foreach (var (path, ex) in errors)
            {
                write($"{path.FullName}");
                Print(ex);
            }
#endif
#if true
            foreach (var result in results)
            {
                write($"# {result.path.FullName}");
                var document = result.document;
                if (document != null)
                {
                    SvgElement.Print(document, write, printAttributes: true);
                }
            }
#endif
        }
    }
}
