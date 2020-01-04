using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows.Data;
using Newtonsoft.Json;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using Svg.Skia;

namespace SvgToPng.ViewModels
{
    [DataContract]
    public class MainWindowViewModel
    {
        [DataMember]
        public ObservableCollection<Item> Items { get; set; }

        [IgnoreDataMember]
        public ICollectionView ItemsView { get; set; }

        [DataMember]
        public ObservableCollection<string> ReferencePaths { get; set; }

        [IgnoreDataMember]
        public Predicate<object> ItemsFilter { get; set; }

        public MainWindowViewModel()
        {
        }

        public void CreateItemsView()
        {
            ItemsView = CollectionViewSource.GetDefaultView(Items);
            ItemsView.Filter = ItemsFilter;
        }

        public void LoadItems()
        {
            if (File.Exists("Items.json"))
            {
                var jsonSerializerSettings = new JsonSerializerSettings()
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore
                };
                var json = File.ReadAllText("Items.json");
                Items = JsonConvert.DeserializeObject<ObservableCollection<Item>>(json, jsonSerializerSettings);
            }
        }

        public void SaveItems()
        {
            var jsonSerializerSettings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };
            string json = JsonConvert.SerializeObject(Items, jsonSerializerSettings);
            File.WriteAllText("Items.json", json);
        }

        public void ClearItems()
        {
            var items = Items.ToList();

            Items.Clear();

            foreach (var item in items)
            {
                item.Svg?.Dispose();
                item.ReferencePng?.Dispose();
                item.PixelDiff?.Dispose();
            }
        }

        public void UpdateItem(Item item)
        {
            if (item.Svg?.Picture == null)
            {
                var currentDirectory = Directory.GetCurrentDirectory();

                try
                {
                    if (File.Exists(item.SvgPath))
                    {
                        Directory.SetCurrentDirectory(Path.GetDirectoryName(item.SvgPath));
                        item.Svg = new SKSvg();

                        var stopwatch = Stopwatch.StartNew();

                        item.Svg.Load(item.SvgPath);

                        stopwatch.Stop();
                        Debug.WriteLine($"Load: {stopwatch.Elapsed.TotalMilliseconds}ms");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load svg file: {item.SvgPath}");
                    Debug.WriteLine(ex.Message);
                    Debug.WriteLine(ex.StackTrace);
                }

                Directory.SetCurrentDirectory(currentDirectory);
            }

#if true
            if (item.ReferencePng == null)
            {
                try
                {
                    if (File.Exists(item.ReferencePngPath))
                    {
                        var referencePng = SKBitmap.Decode(item.ReferencePngPath);
                        item.ReferencePng = referencePng;

                        float scaleX = referencePng.Width / item.Svg.Picture.CullRect.Width;
                        float scaleY = referencePng.Height / item.Svg.Picture.CullRect.Height;

                        using (var svgBitmap = item.Svg.Picture.ToBitmap(SKColors.Transparent, scaleX, scaleY))
                        {
                            if (svgBitmap.Width == referencePng.Width && svgBitmap.Height == referencePng.Height)
                            {
                                var pixelDiff = PixelDiff(referencePng, svgBitmap);
                                item.PixelDiff = pixelDiff;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load reference png: {item.ReferencePngPath}");
                    Debug.WriteLine(ex.Message);
                    Debug.WriteLine(ex.StackTrace);
                }
            } 
#endif
        }

        public void AddItems(List<string> paths, IList<Item> items, string referencePath, string outputPath)
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

        public void SaveItemsAsPng(IList<Item> items)
        {
            foreach (var item in items)
            {
                UpdateItem(item);

                if (item.Svg?.Picture != null)
                {
                    item.Svg.Save(item.OutputPngPath, SKColors.Transparent, SKEncodedImageFormat.Png, 100, 1f, 1f);
                }
            }
        }

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

        unsafe public static SKBitmap PixelDiff(SKBitmap a, SKBitmap b)
        {
            SKBitmap output = new SKBitmap(a.Width, a.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            byte* aPtr = (byte*)a.GetPixels().ToPointer();
            byte* bPtr = (byte*)b.GetPixels().ToPointer();
            byte* outputPtr = (byte*)output.GetPixels().ToPointer();
            int len = a.RowBytes * a.Height;
            for (int i = 0; i < len; i++)
            {
                // For alpha use the average of both images (otherwise pixels with the same alpha won't be visible)
                if ((i + 1) % 4 == 0)
                    *outputPtr = (byte)((*aPtr + *bPtr) / 2);
                else
                    *outputPtr = (byte)~(*aPtr ^ *bPtr);

                outputPtr++;
                aPtr++;
                bPtr++;
            }
            return output;
        }
    }
}
