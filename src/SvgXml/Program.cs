using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Svg;

namespace SvgXml
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Action<string> write = Console.WriteLine;

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

            foreach (var path in paths)
            {
                try
                {
                    var document = SvgDocument.Open(path.FullName);
                    if (document != null)
                    {
                        results.Add((path, document));
                    }
                }
#if true
                catch (Exception)
                {
                }
#else
                catch (Exception ex)
                {
                    write($"{path.FullName}");
                    write(ex.Message);
                    write(ex.StackTrace);
                }
#endif
            }

            sw.Stop();
            write($"# {sw.Elapsed.TotalMilliseconds}ms [{sw.Elapsed}], {paths.Count} files");
#if true
            foreach (var result in results)
            {
                write($"# {result.path.FullName}");
                var document = result.document;
                if (document != null)
                {
                    document.Print(write, printAttributes: true);
                }
            }
#endif
        }
    }
}
