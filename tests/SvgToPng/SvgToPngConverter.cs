using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace SvgToPng
{
    public class Item
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Svg { get; set; }
        public byte[] Bytes { get; set; }
        public BitmapImage Image { get; set; }
    }

    public interface IConvertProgress
    {
        Task ConvertStatusReset();
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

        public static async Task Convert(List<string> inputFiles, IList<Item> items, IConvertProgress convertProgress)
        {
            await convertProgress.ConvertStatusReset();
            await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            int count = 0;
            var launchOptions = new LaunchOptions
            {
                Headless = true
            };

            using (var browser = await Puppeteer.LaunchAsync(launchOptions))
            using (var page = await browser.NewPageAsync())
            {
                foreach (var inputFile in inputFiles)
                {
                    string inputName = Path.GetFileNameWithoutExtension(inputFile);
                    count++;
                    await convertProgress.ConvertStatusProgress(count, inputFiles.Count, inputFile);
#if NET461
                    string svg = File.ReadAllText(inputFile);
#else
                    string svg = await File.ReadAllTextAsync(inputFile);
#endif
                    byte[] bytes = await GetBytes(page, svg);
                    if (bytes != null)
                    {
                        var image = LoadImage(bytes);
                        var item = new Item()
                        {
                            Name = inputName,
                            Path = inputFile,
                            Svg = svg,
                            Bytes = bytes,
                            Image = image
                        };
                        items.Add(item);
                    }
                }
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
