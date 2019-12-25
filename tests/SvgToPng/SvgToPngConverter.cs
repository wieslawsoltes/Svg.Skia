using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using SkiaSharp;
using Svg.Skia;

namespace SvgToPng
{
    public class Item
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public SKSvg Svg { get; set; }
        public BitmapImage Image { get; set; }
    }

    public interface IConvertProgress
    {
        Task ConvertStatusReset();
        Task ConvertStatus(string message);
        Task ConvertStatusProgress(int count, int total, string inputFile);
        Task ConvertStatusDone();
    }

    public interface ISaveProgress
    {
        Task SaveStatusReset();
        Task SaveStatusProgress(int count, int total, string inputFile, string outputFile);
        Task SaveStatusDone();
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

        public static async Task Convert(List<string> inputFiles, IList<Item> items, string referencePath, IConvertProgress convertProgress)
        {
            await convertProgress.ConvertStatusReset();
            int count = 0;
            var fullReferencePath = string.IsNullOrWhiteSpace(referencePath) ? default : Path.GetFullPath(referencePath);

            // Items

            await convertProgress.ConvertStatus("Loading items...");

            foreach (var inputFile in inputFiles)
            {
                var inputName = Path.GetFileNameWithoutExtension(inputFile);
                var item = new Item()
                {
                    Name = inputName,
                    Path = inputFile
                };
                items.Add(item);
            }

            await convertProgress.ConvertStatus("Converting svg using Svg.Skia...");

            count = 0;
            foreach (var item in items)
            {
                count++;
                await convertProgress.ConvertStatusProgress(count, inputFiles.Count, item.Path);
                await Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Directory.SetCurrentDirectory(Path.GetDirectoryName(item.Path));

                        item.Svg = new SKSvg();
                        item.Svg.Load(item.Path);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to load svg file: {item.Path}");
                        Debug.WriteLine(ex.Message);
                        Debug.WriteLine(ex.StackTrace);
                    }

                    if (!string.IsNullOrWhiteSpace(fullReferencePath))
                    {
                        var referenceImagePath = Path.Combine(fullReferencePath, item.Name + ".png");
                        if (File.Exists(referenceImagePath))
                        {
                            try
                            {
                                var image = new BitmapImage(new Uri(referenceImagePath));
                                image.Freeze();
                                item.Image = image;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to load reference image: {referenceImagePath}");
                                Debug.WriteLine(ex.Message);
                                Debug.WriteLine(ex.StackTrace);
                            }
                        } 
                    }
                });
            }

            await convertProgress.ConvertStatusDone();
        }

        public static async Task Save(string outputPath, IList<Item> items, ISaveProgress saveProgress)
        {
            int count = 0;
            await saveProgress.SaveStatusReset();

            foreach (var item in items)
            {
                string inputFile = item.Path;
                string inputName = Path.GetFileNameWithoutExtension(inputFile);
                string outputFile = Path.Combine(outputPath, inputName + ".png");
                count++;
                await saveProgress.SaveStatusProgress(count, items.Count, inputFile, outputFile);
                item.Svg.Save(outputFile, SKColors.Transparent, SKEncodedImageFormat.Png, 100, 1f, 1f);
            }

            await saveProgress.SaveStatusDone();
        }
    }
}
