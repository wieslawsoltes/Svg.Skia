using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Windows.Media.Imaging;
using SkiaSharp;
using Svg.Skia;

namespace SvgToPng
{
    [DataContract]
    public class Item
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string SvgPath { get; set; }

        [DataMember]
        public string ReferencePngPath { get; set; }

        [DataMember]
        public string OutputPngPath { get; set; }

        [IgnoreDataMember]
        public SKSvg Svg { get; set; }

        [IgnoreDataMember]
        public BitmapImage Image { get; set; }
    }

    public static class SvgToPngConverter
    {
        public static IEnumerable<string> GetFiles(string inputPath)
        {
            foreach (var file in Directory.EnumerateFiles(inputPath, "*.svg"))
            {
                yield return file;
            }

            foreach (var file in Directory.EnumerateFiles(inputPath, "*.svgz"))
            {
                yield return file;
            }

            foreach (var directory in Directory.EnumerateDirectories(inputPath))
            {
                foreach (var file in GetFiles(directory))
                {
                    yield return file;
                }
            }
        }

        public static IEnumerable<string> GetFilesDrop(string[] paths)
        {
            if (paths != null && paths.Length > 0)
            {
                foreach (var path in paths)
                {
                    if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                    {
                        foreach (var file in GetFiles(path))
                        {
                            yield return file;
                        }
                    }
                    else
                    {
                        var extension = Path.GetExtension(path).ToLower();
                        if (extension == ".svg" || extension == ".svgz")
                        {
                            yield return path;
                        }
                    }
                }
            }
        }

        public static void Load(Item item)
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            try
            {
                if (File.Exists(item.SvgPath))
                {
                    Directory.SetCurrentDirectory(Path.GetDirectoryName(item.SvgPath));
                    item.Svg = new SKSvg();
                    item.Svg.Load(item.SvgPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load svg file: {item.SvgPath}");
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }

            try
            {
                if (File.Exists(item.ReferencePngPath))
                {
                    var bi = new BitmapImage(new Uri(item.ReferencePngPath));
                    bi.Freeze();
                    item.Image = bi;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load reference png: {item.ReferencePngPath}");
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }

            Directory.SetCurrentDirectory(currentDirectory);
        }

        public static void Add(List<string> paths, IList<Item> items, string referencePath, string outputPath)
        {
            var fullReferencePath = string.IsNullOrWhiteSpace(referencePath) ? default : Path.GetFullPath(referencePath);

            foreach (var path in paths)
            {
                string inputName = Path.GetFileNameWithoutExtension(path);
                string referencePng = string.Empty;
                string outputPng = Path.Combine(outputPath, inputName + ".png");

                if (!string.IsNullOrWhiteSpace(fullReferencePath))
                {
                    referencePng = Path.Combine(fullReferencePath, inputName + ".png");
                }

                var item = new Item()
                {
                    Name = inputName,
                    SvgPath = path,
                    ReferencePngPath = referencePng,
                    OutputPngPath = outputPng
                };

                items.Add(item);
            }
        }

        public static void Save(IList<Item> items)
        {
            foreach (var item in items)
            {
                item.Svg.Save(
                    item.OutputPngPath, 
                    SKColors.Transparent, 
                    SKEncodedImageFormat.Png, 100, 
                    1f, 1f);
            }
        }
    }
}
