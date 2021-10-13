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
using Svg.CodeGen.Skia;
using Svg.Model;
using Svg.Skia;

namespace SvgToPng.ViewModels;

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
        if (items is { })
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

            Directory.SetCurrentDirectory(System.IO.Path.GetDirectoryName(item.SvgPath));

            var stopwatchOpen = Stopwatch.StartNew();
            item.Document = SvgExtensions.Open(item.SvgPath);
            stopwatchOpen.Stop();
            statusOpen?.Invoke($"{Math.Round(stopwatchOpen.Elapsed.TotalMilliseconds, 3)}ms");
            Debug.WriteLine($"Open: {Math.Round(stopwatchOpen.Elapsed.TotalMilliseconds, 3)}ms");

            if (item.Document is { })
            {
                var stopwatchToPicture = Stopwatch.StartNew();

                var references = new HashSet<Uri> {item.Document.BaseUri};
                item.Drawable = SvgExtensions.ToDrawable(item.Document, new SkiaAssetLoader(), references, out var bounds);
                if (item.Drawable is { } && bounds is { })
                {
                    item.Picture = item.Drawable.Snapshot(bounds.Value);

                    item.SkiaPicture = item.Picture?.ToSKPicture();

                    if (item.Picture?.Commands is { })
                    {
                        item.Code = SkiaCSharpCodeGen.Generate(item.Picture, "Svg", CreateClassName(item.SvgPath));

                        string CreateClassName(string path)
                        {
                            string name = System.IO.Path.GetFileNameWithoutExtension(path);
                            string className = name.Replace("-", "_");
                            return $"Svg_{className}";
                        }
                    }
                }

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

            using var codec = SkiaSharp.SKCodec.Create(new SkiaSharp.SKFileStream(item.ReferencePngPath));
            var skImageInfo = new SkiaSharp.SKImageInfo(codec.Info.Width, codec.Info.Height, SKSvgSettings.s_colorType, SKSvgSettings.s_alphaType, SKSvgSettings.s_srgb);
            var skReferenceBitmap = new SkiaSharp.SKBitmap(skImageInfo);
            codec.GetPixels(skReferenceBitmap.Info, skReferenceBitmap.GetPixels());
            if (skReferenceBitmap == null)
            {
                return;
            }

            item.ReferencePng = skReferenceBitmap;

            float scaleX = skReferenceBitmap.Width / item.SkiaPicture.CullRect.Width;
            float scaleY = skReferenceBitmap.Height / item.SkiaPicture.CullRect.Height;
            using var svgBitmap = item.SkiaPicture.ToBitmap(SkiaSharp.SKColors.Transparent, scaleX, scaleY, SKSvgSettings.s_colorType, SKSvgSettings.s_alphaType, SKSvgSettings.s_srgb);
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
        if (item.Document == null)
        {
            LoadSvg(item, statusOpen, statusToPicture);
        }

        if (item.ReferencePng == null && item.SkiaPicture is { })
        {
            LoadPng(item);
        }
    }

    public void AddItems(List<string> paths, IList<Item> items, string referencePath, string outputPath)
    {
        var fullReferencePath = string.IsNullOrWhiteSpace(referencePath) ? default : System.IO.Path.GetFullPath(referencePath);

        foreach (var path in paths)
        {
            string inputName = System.IO.Path.GetFileNameWithoutExtension(path);
            string referencePng = string.Empty;

            if (!string.IsNullOrWhiteSpace(fullReferencePath))
            {
                referencePng = System.IO.Path.Combine(fullReferencePath, inputName + ".png");
            }

            var item = new Item
            {
                Name = inputName,
                SvgPath = path,
                ReferencePngPath = referencePng
            };

            items.Add(item);
        }
    }

    public void ExportItem(string svgPath, string outputPath, SkiaSharp.SKColor background, float scaleX, float scaleY)
    {
        if (!File.Exists(svgPath))
        {
            return;
        }

        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(System.IO.Path.GetDirectoryName(svgPath));

        var extension = System.IO.Path.GetExtension(outputPath);

        if (string.Compare(extension, ".pdf", StringComparison.OrdinalIgnoreCase) == 0)
        {
            var svg = SvgExtensions.Open(svgPath);
            using var picture = SKSvg.ToPicture(svg);
            if (picture is { })
            {
                picture.ToPdf(outputPath, background, scaleX, scaleY);
            }

        }
        else if (string.Compare(extension, ".xps", StringComparison.OrdinalIgnoreCase) == 0)
        {
            var svg = SvgExtensions.Open(svgPath);
            using var picture = SKSvg.ToPicture(svg);
            if (picture is { })
            {
                picture.ToXps(outputPath, background, scaleX, scaleY);
            }
        }
        else if (string.Compare(extension, ".svg", StringComparison.OrdinalIgnoreCase) == 0)
        {
            var svg = SvgExtensions.Open(svgPath);
            using var picture = SKSvg.ToPicture(svg);
            if (picture is { })
            {
                picture.ToSvg(outputPath, background, scaleX, scaleY);
            }
        }
        else if (string.Compare(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) == 0)
        {
            var svg = SvgExtensions.Open(svgPath);
            using var picture = SKSvg.ToPicture(svg);
            if (picture is { })
            {
                using var stream = File.OpenWrite(outputPath);
                picture.ToImage(stream, background, SkiaSharp.SKEncodedImageFormat.Jpeg, 100, scaleX, scaleY, SKSvgSettings.s_colorType, SKSvgSettings.s_alphaType, SKSvgSettings.s_srgb);
            }
        }
        else if (string.Compare(extension, ".jpg", StringComparison.OrdinalIgnoreCase) == 0)
        {
            var svg = SvgExtensions.Open(svgPath);
            using var picture = SKSvg.ToPicture(svg);
            if (picture is { })
            {
                using var stream = File.OpenWrite(outputPath);
                picture.ToImage(stream, background, SkiaSharp.SKEncodedImageFormat.Jpeg, 100, scaleX, scaleY, SKSvgSettings.s_colorType, SKSvgSettings.s_alphaType, SKSvgSettings.s_srgb);
            }
        }
        else if (string.Compare(extension, ".png", StringComparison.OrdinalIgnoreCase) == 0)
        {
            var svg = SvgExtensions.Open(svgPath);
            using var picture = SKSvg.ToPicture(svg);
            if (picture is { })
            {
                using var stream = File.OpenWrite(outputPath);
                picture.ToImage(stream, background, SkiaSharp.SKEncodedImageFormat.Png, 100, scaleX, scaleY, SKSvgSettings.s_colorType, SKSvgSettings.s_alphaType, SKSvgSettings.s_srgb);
            }
        }
        else if (string.Compare(extension, ".webp", StringComparison.OrdinalIgnoreCase) == 0)
        {
            var svg = SvgExtensions.Open(svgPath);
            using var picture = SKSvg.ToPicture(svg);
            if (picture is { })
            {
                using var stream = File.OpenWrite(outputPath);
                picture.ToImage(stream, background, SkiaSharp.SKEncodedImageFormat.Webp, 100, scaleX, scaleY, SKSvgSettings.s_colorType, SKSvgSettings.s_alphaType, SKSvgSettings.s_srgb);
            }
        }

        Directory.SetCurrentDirectory(currentDirectory);
    }

    public void ExportItems(IList<Item> items, string outputPath, List<string> outputFormats, SkiaSharp.SKColor background, float scaleX, float scaleY)
    {
        foreach (var item in items)
        {
            foreach (var format in outputFormats)
            {
                string path = System.IO.Path.Combine(outputPath, item.Name + "." + format);
                ExportItem(item.SvgPath, path, background, scaleX, scaleY);
            }
        }
    }

    private static JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
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
        if (paths is { } && paths.Length > 0)
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
                    var extension = System.IO.Path.GetExtension(path).ToLower();
                    if (extension == ".svg" || extension == ".svgz")
                    {
                        yield return path;
                    }
                }
            }
        }
    }

    unsafe public static SkiaSharp.SKBitmap PixelDiff(SkiaSharp.SKBitmap referenceBitmap, SkiaSharp.SKBitmap svgBitmap)
    {
        var skImageInfo = new SkiaSharp.SKImageInfo(referenceBitmap.Width, referenceBitmap.Height, SKSvgSettings.s_colorType, SKSvgSettings.s_alphaType, SKSvgSettings.s_srgb);
        var output = new SkiaSharp.SKBitmap(skImageInfo);
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