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
using Svg.Skia;

namespace SvgToPng.ViewModels
{
    [DataContract]
    public class MainWindowViewModel
    {
        [DataMember]
        public ObservableCollection<Item> Items { get; set; }

        [DataMember]
        public string OutputPath { get; set; }

        [DataMember]
        public string ReferencePath { get; set; }

        [DataMember]
        public ObservableCollection<string> ReferencePaths { get; set; }

        [IgnoreDataMember]
        public ICollectionView ItemsView { get; set; }

        [IgnoreDataMember]
        public Predicate<object> ItemsViewFilter { get; set; }

        [DataMember]
        public string ItemsFilter { get; set; }

        [DataMember]
        public bool ShowPassed { get; set; }

        [DataMember]
        public bool ShowFailed { get; set; }

        public MainWindowViewModel()
        {
        }

        public void CreateItemsView()
        {
            ItemsView = CollectionViewSource.GetDefaultView(Items);
            ItemsView.Filter = ItemsViewFilter;
        }

        public void LoadItems(string path)
        {
            var items = Load<ObservableCollection<Item>>(path);
            if (items != null)
            {
                Items = items;
            }
        }

        public void SaveItems(string path)
        {
            Save(path, Items);
        }

        public void ClearItems()
        {
            var items = Items.ToList();

            Items.Clear();

            foreach (var item in items)
            {
                item.Dispose();
            }
        }

        public void RemoveItem(Item item)
        {
            Items.Remove(item);
            item.Dispose();
        }

        public void ResetItem(Item item)
        {
            item.Reset();
        }

        private void LoadSvg(Item item, Action<string> statusOpen, Action<string> statusToPicture)
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            try
            {
                if (!File.Exists(item.SvgPath))
                {
                    return;
                }

                Directory.SetCurrentDirectory(Path.GetDirectoryName(item.SvgPath));

                var stopwatchOpen = Stopwatch.StartNew();
                item.Svg = SKSvg.Open(item.SvgPath);
                stopwatchOpen.Stop();
                statusOpen?.Invoke($"{Math.Round(stopwatchOpen.Elapsed.TotalMilliseconds, 3)}ms");
                Debug.WriteLine($"Open: {Math.Round(stopwatchOpen.Elapsed.TotalMilliseconds, 3)}ms");

                if (item.Svg != null)
                {
                    var stopwatchToPicture = Stopwatch.StartNew();
                    item.Picture = SKSvg.ToPicture(item.Svg, out var drawable);
                    item.Drawable = drawable;
                    stopwatchToPicture.Stop();
                    statusToPicture?.Invoke($"{Math.Round(stopwatchToPicture.Elapsed.TotalMilliseconds, 3)}ms");
                    Debug.WriteLine($"ToPicture: {Math.Round(stopwatchToPicture.Elapsed.TotalMilliseconds, 3)}ms");
                }
                else
                {
                    statusToPicture?.Invoke($"");
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

        private void LoadPng(Item item)
        {
            try
            {
                if (!File.Exists(item.ReferencePngPath))
                {
                    return;
                }

                using var codec = SKCodec.Create(new SKFileStream(item.ReferencePngPath));
#if USE_COLORSPACE
                var skImageInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKSvg.s_colorType, SKSvg.s_alphaType, SvgPaintingExtensions.Srgb);
#else
                var skImageInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKSvg.s_colorType, SKSvg.s_alphaType);
#endif
                var skReferenceBitmap = new SKBitmap(skImageInfo);
                codec.GetPixels(skReferenceBitmap.Info, skReferenceBitmap.GetPixels());
                if (skReferenceBitmap == null)
                {
                    return;
                }

                item.ReferencePng = skReferenceBitmap;

                float scaleX = skReferenceBitmap.Width / item.Picture.CullRect.Width;
                float scaleY = skReferenceBitmap.Height / item.Picture.CullRect.Height;
#if USE_COLORSPACE
                using var svgBitmap = item.Picture.ToBitmap(SKColors.Transparent, scaleX, scaleY, SKSvg.s_colorType, SKSvg.s_alphaType, SvgPaintingExtensions.Srgb);
#else
                using var svgBitmap = item.Picture.ToBitmap(SKColors.Transparent, scaleX, scaleY, SKSvg.s_colorType, SKSvg.s_alphaType);
#endif
                if (svgBitmap.Width == skReferenceBitmap.Width && svgBitmap.Height == skReferenceBitmap.Height)
                {
                    var pixelDiff = PixelDiff(skReferenceBitmap, svgBitmap);
                    item.PixelDiff = pixelDiff;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load reference png: {item.ReferencePngPath}");
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }

        public void UpdateItem(Item item, Action<string> statusOpen, Action<string> statusToPicture)
        {
            if (item.Svg == null)
            {
                LoadSvg(item, statusOpen, statusToPicture);
            }

            if (item.ReferencePng == null && item.Picture != null)
            {
                LoadPng(item);
            }
        }

        public void AddItems(List<string> paths, IList<Item> items, string referencePath, string outputPath)
        {
            var fullReferencePath = string.IsNullOrWhiteSpace(referencePath) ? default : Path.GetFullPath(referencePath);

            foreach (var path in paths)
            {
                string inputName = Path.GetFileNameWithoutExtension(path);
                string referencePng = string.Empty;

                if (!string.IsNullOrWhiteSpace(fullReferencePath))
                {
                    referencePng = Path.Combine(fullReferencePath, inputName + ".png");
                }

                var item = new Item()
                {
                    Name = inputName,
                    SvgPath = path,
                    ReferencePngPath = referencePng
                };

                items.Add(item);
            }
        }

        public void ExportItem(string svgPath, string outputPath, SKColor background, float scaleX, float scaleY)
        {
            if (!File.Exists(svgPath))
            {
                return;
            }

            var currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(Path.GetDirectoryName(svgPath));

            var extension = Path.GetExtension(outputPath);

            if (string.Compare(extension, ".pdf", StringComparison.OrdinalIgnoreCase) == 0)
            {
                var svg = SKSvg.Open(svgPath);
                using var picture = SKSvg.ToPicture(svg);
                if (picture != null)
                {
                    picture.ToPdf(outputPath, background, scaleX, scaleY);
                }

            }
            else if (string.Compare(extension, ".xps", StringComparison.OrdinalIgnoreCase) == 0)
            {
                var svg = SKSvg.Open(svgPath);
                using var picture = SKSvg.ToPicture(svg);
                if (picture != null)
                {
                    picture.ToXps(outputPath, background, scaleX, scaleY);
                }
            }
            else if (string.Compare(extension, ".svg", StringComparison.OrdinalIgnoreCase) == 0)
            {
                var svg = SKSvg.Open(svgPath);
                using var picture = SKSvg.ToPicture(svg);
                if (picture != null)
                {
                    picture.ToSvg(outputPath, background, scaleX, scaleY);
                }
            }
            else if (string.Compare(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) == 0)
            {
                var svg = SKSvg.Open(svgPath);
                using var picture = SKSvg.ToPicture(svg);
                if (picture != null)
                {
                    using var stream = File.OpenWrite(outputPath);
#if USE_COLORSPACE
                    picture.ToImage(stream, background, SKEncodedImageFormat.Jpeg, 100, scaleX, scaleY, SKSvg.s_colorType, SKSvg.s_alphaType, SvgPaintingExtensions.Srgb);
#else
                    picture.ToImage(stream, background, SKEncodedImageFormat.Jpeg, 100, scaleX, scaleY, SKSvg.s_colorType, SKSvg.s_alphaType);
#endif
                }
            }
            else if (string.Compare(extension, ".jpg", StringComparison.OrdinalIgnoreCase) == 0)
            {
                var svg = SKSvg.Open(svgPath);
                using var picture = SKSvg.ToPicture(svg);
                if (picture != null)
                {
                    using var stream = File.OpenWrite(outputPath);
#if USE_COLORSPACE
                    picture.ToImage(stream, background, SKEncodedImageFormat.Jpeg, 100, scaleX, scaleY, SKSvg.s_colorType, SKSvg.s_alphaType, SvgPaintingExtensions.Srgb);
#else
                    picture.ToImage(stream, background, SKEncodedImageFormat.Jpeg, 100, scaleX, scaleY, SKSvg.s_colorType, SKSvg.s_alphaType);
#endif
                }
            }
            else if (string.Compare(extension, ".png", StringComparison.OrdinalIgnoreCase) == 0)
            {
                var svg = SKSvg.Open(svgPath);
                using var picture = SKSvg.ToPicture(svg);
                if (picture != null)
                {
                    using var stream = File.OpenWrite(outputPath);
#if USE_COLORSPACE
                    picture.ToImage(stream, background, SKEncodedImageFormat.Png, 100, scaleX, scaleY, SKSvg.s_colorType, SKSvg.s_alphaType, SvgPaintingExtensions.Srgb);
#else
                    picture.ToImage(stream, background, SKEncodedImageFormat.Png, 100, scaleX, scaleY, SKSvg.s_colorType, SKSvg.s_alphaType);
#endif
                }
            }
            else if (string.Compare(extension, ".webp", StringComparison.OrdinalIgnoreCase) == 0)
            {
                var svg = SKSvg.Open(svgPath);
                using var picture = SKSvg.ToPicture(svg);
                if (picture != null)
                {
                    using var stream = File.OpenWrite(outputPath);
#if USE_COLORSPACE
                    picture.ToImage(stream, background, SKEncodedImageFormat.Webp, 100, scaleX, scaleY, SKSvg.s_colorType, SKSvg.s_alphaType, SvgPaintingExtensions.Srgb);
#else
                    picture.ToImage(stream, background, SKEncodedImageFormat.Webp, 100, scaleX, scaleY, SKSvg.s_colorType, SKSvg.s_alphaType);
#endif
                }
            }

            Directory.SetCurrentDirectory(currentDirectory);
        }

        public void ExportItems(IList<Item> items, string outputPath, List<string> outputFormats, SKColor background, float scaleX, float scaleY)
        {
            foreach (var item in items)
            {
                foreach (var format in outputFormats)
                {
                    string path = Path.Combine(outputPath, item.Name + "." + format);
                    ExportItem(item.SvgPath, path, background, scaleX, scaleY);
                }
            }
        }

        private static JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static T Load<T>(string path)
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<T>(json, s_jsonSettings);
            }
            return default;
        }

        public static void Save<T>(string path, T value)
        {
            string json = JsonConvert.SerializeObject(value, s_jsonSettings);
            File.WriteAllText(path, json);
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

        unsafe public static SKBitmap PixelDiff(SKBitmap referenceBitmap, SKBitmap svgBitmap)
        {
#if USE_COLORSPACE
            var skImageInfo = new SKImageInfo(referenceBitmap.Width, referenceBitmap.Height, SKSvg.s_colorType, SKSvg.s_alphaType, SvgPaintingExtensions.Srgb);
#else
            var skImageInfo = new SKImageInfo(referenceBitmap.Width, referenceBitmap.Height, SKSvg.s_colorType, SKSvg.s_alphaType);
#endif
            var output = new SKBitmap(skImageInfo);
            byte* aPtr = (byte*)referenceBitmap.GetPixels().ToPointer();
            byte* bPtr = (byte*)svgBitmap.GetPixels().ToPointer();
            byte* outputPtr = (byte*)output.GetPixels().ToPointer();
            int len = referenceBitmap.RowBytes * referenceBitmap.Height;
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
