using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using SkiaSharp;
using Svg.Skia;

namespace SvgToPng
{
    public class Item
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Svg { get; set; }
        public byte[] Bytes { get; set; }
        public BitmapImage Image { get; set; }
        public SKSvg Skia { get; set; }
        public SKPicture Picture { get; set; }
    }

    public interface IConvertProgress
    {
        Task ConvertStatusReset();
        Task ConvertStatus(string message);
        Task ConvertStatusProgress(int count, int total, string inputFile);
    }

    public interface ISaveProgress
    {
        Task SaveStatusReset();
        Task SaveStatusProgress(int count, int total, string inputFile, string outputFile);
        Task SaveStatusDone();
    }

    public static class SvgToPngConverter
    {
        public static BitmapImage LoadImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                return null;
            }

            var image = new BitmapImage();

            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }

            image.Freeze();
            return image;
        }

        public static BitmapImage LoadImage(string path)
        {
            var image = new BitmapImage(new Uri(path));
            image.Freeze();
            return image;
        }

        public static async Task<byte[]> GetBytes(Page page, string svg, bool clipPage = false)
        {
            await page.SetContentAsync(svg);

            var elements = await page.QuerySelectorAllAsync("svg");
            var svgElement = elements.FirstOrDefault();
            if (svgElement == null)
            {
                return null;
            }
            var boundingBox = await svgElement.BoundingBoxAsync();
            decimal x = boundingBox.X;
            decimal y = boundingBox.Y;
            decimal width = boundingBox.Width;
            decimal height = boundingBox.Height;

            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = (int)width,
                Height = (int)height
            });

            var options = new ScreenshotOptions()
            {
                BurstMode = false,
                OmitBackground = true,
                Quality = null,
                Type = ScreenshotType.Png
            };

            if (clipPage == true)
            {
                var clip = new Clip()
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height
                };

                options.Clip = clip;
                options.FullPage = false;
            }
            else
            {
                options.Clip = null;
                options.FullPage = true;
            }

            var bytes = await page.ScreenshotDataAsync(options);
            return bytes;
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

        public static async Task Convert(List<string> inputFiles, IList<Item> items, string referencePath, IConvertProgress convertProgress)
        {
            await convertProgress.ConvertStatusReset();
            int count = 0;

            // Items

            await convertProgress.ConvertStatus("Loading svg...");

            await Task.Factory.StartNew(() =>
            {
                foreach (var inputFile in inputFiles)
                {
                    string inputName = Path.GetFileNameWithoutExtension(inputFile);
                    string svg;
                    string extension = System.IO.Path.GetExtension(inputFile);
                    switch (extension.ToLower())
                    {
                        default:
                        case ".svg":
                            {
#if NET461
                                svg = File.ReadAllText(inputFile);
#else
                                svg = await File.ReadAllTextAsync(inputFile);
#endif
                            }
                            break;
                        case ".svgz":
                            {
                                using (var fileStream = File.OpenRead(inputFile))
                                using (var gzipStream = new GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress))
                                using (var sr = new StreamReader(gzipStream))
                                {
                                    svg = sr.ReadToEnd();
                                }
                            }
                            break;
                    }

                    var item = new Item()
                    {
                        Name = inputName,
                        Path = inputFile,
                        Svg = svg
                    };
                    items.Add(item);
                }
            });

            // Svg.Skia
#if true
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
                        var skia = new SKSvg();
                        var picture = skia.FromSvg(item.Svg);
                        item.Skia = skia;
                        item.Picture = picture;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        Debug.WriteLine(ex.StackTrace);
                    }
                });
            }
#endif
            // Reference Png
#if true
            if (!string.IsNullOrEmpty(referencePath) && Directory.Exists(referencePath))
            {
                await convertProgress.ConvertStatus("Loading references...");

                count = 0;
                foreach (var item in items)
                {
                    count++;
                    var referenceImagePath = Path.Combine(referencePath, item.Name + ".png");
                    if (File.Exists(referenceImagePath))
                    {
                        await convertProgress.ConvertStatusProgress(count, inputFiles.Count, referenceImagePath);
                        await Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                var image = LoadImage(referenceImagePath);
                                item.Bytes = null;
                                item.Image = image;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                                Debug.WriteLine(ex.StackTrace);
                            }
                        });
                    }
                }
            }
#endif
            // Google Chrome
#if false
            await convertProgress.ConvertStatus("Converting svg using chrome...");

            count = 0;
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            var launchOptions = new LaunchOptions
            {
                Headless = true
            };
            using (var browser = await Puppeteer.LaunchAsync(launchOptions))
            using (var page = await browser.NewPageAsync())
            {
                foreach (var item in items)
                {
                    try
                    {
                        count++;
                        await convertProgress.ConvertStatusProgress(count, inputFiles.Count, item.Path);
                        var bytes = await GetBytes(page, item.Svg);
                        var image = LoadImage(bytes);
                        item.Bytes = bytes;
                        item.Image = image;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        Debug.WriteLine(ex.StackTrace);
                    }
                }
            }
#endif
            await convertProgress.ConvertStatus("Done");
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
#if NET461
                File.WriteAllBytes(outputFile, item.Bytes);
#else
                await File.WriteAllBytesAsync(outputFile, item.Bytes);
#endif
            }

            await saveProgress.SaveStatusDone();
        }
    }
}
