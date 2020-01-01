using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using Newtonsoft.Json;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
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
        public SKBitmap ReferencePng { get; set; }

        [IgnoreDataMember]
        public SKBitmap PixelDiff { get; set; }
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<Item> Items { get; set; }
        public ICollectionView ItemsView { get; set; }
        public ObservableCollection<string> ReferencePaths { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Items = new ObservableCollection<Item>();
            ReferencePaths = new ObservableCollection<string>();
#if DEBUG
            TextOutputPath.Text = Path.Combine(Directory.GetCurrentDirectory(), "png");
            ReferencePaths = new ObservableCollection<string>(new string[]
            {
                @"c:\DOWNLOADS\GitHub\Svg.Skia\externals\SVG\Tests\W3CTestSuite\png\",
                @"c:\DOWNLOADS\GitHub-Forks\resvg-test-suite\png\",
                @"e:\Dropbox\Draw2D\SVG\vs2017-png\",
                @"e:\Dropbox\Draw2D\SVG\W3CTestSuite-png\"
            });
#endif
            LoadItems();
            CreateItemsView();

            this.Closing += MainWindow_Closing;
            this.TextItemsFilter.TextChanged += TextItemsFilter_TextChanged;

            this.items.SelectionChanged += Items_SelectionChanged;
            this.items.MouseDoubleClick += Items_MouseDoubleClick;

            this.skElementSvg.PaintSurface += OnPaintCanvasSvg;
            this.skElementPng.PaintSurface += OnPaintCanvasPng;
            this.skElementDiff.PaintSurface += OnPaintCanvasDiff;

            this.glHostSvg.Initialized += OnGLControlHostSvg;
            this.glHostPng.Initialized += OnGLControlHostPng;
            this.glHostDiff.Initialized += OnGLControlHostDiff;

            DataContext = this;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            SaveItems();
        }

        private void TextItemsFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ItemsView.Refresh();
        }

        private void Items_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (items.SelectedItem is Item item)
            {
                UpdateItem(item);
            }

            skElementSvg.InvalidateVisual();
            skElementPng.InvalidateVisual();
            skElementDiff.InvalidateVisual();

            glHostSvg.Child?.Invalidate();
            glHostPng.Child?.Invalidate();
            glHostDiff.Child?.Invalidate();
        }

        private void Items_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (items.SelectedItem is Item item)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Process.Start("notepad", item.SvgPath);
                }
                if (e.RightButton == MouseButtonState.Pressed)
                {
                    Process.Start("explorer", item.SvgPath);
                }
            }
        }

        private void HandleDrop(string[] paths, string referencePath, string outputPath)
        {
            var inputFiles = GetFilesDrop(paths).ToList();
            if (inputFiles.Count > 0)
            {
                AddItems(inputFiles, Items, referencePath, outputPath);
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (paths != null && paths.Length > 0)
                {
                    HandleDrop(paths, TextReferencePath.Text, TextOutputPath.Text);
                }
            }
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
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

        private void ButtonAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Supported Files (*.svg;*.svgz)|*.svg;*.svgz|Svg Files (*.svg)|*.svg;|Svgz Files (*.svgz)|*.svgz|All Files (*.*)|*.*",
                Multiselect = true,
                FilterIndex = 0
            };
            if (dlg.ShowDialog() == true)
            {
                var paths = dlg.FileNames;
                if (paths != null && paths.Length > 0)
                {
                    HandleDrop(paths, TextReferencePath.Text, TextOutputPath.Text);
                }
            }
        }

        private async void ButtonSavePng_Click(object sender, RoutedEventArgs e)
        {
            string outputPath = TextOutputPath.Text;
            await Task.Factory.StartNew(() =>
            {
                SaveItemsAsPng(Items);
            });
        }

        private void OnGLControlHostSvg(object sender, EventArgs e)
        {
            var glControl = new SKGLControl();
            glControl.PaintSurface += OnPaintGLSvg;
            glControl.Dock = System.Windows.Forms.DockStyle.None;
            var host = (WindowsFormsHost)sender;
            host.Child = glControl;
        }

        private void OnPaintGLSvg(object sender, SKPaintGLSurfaceEventArgs e)
        {
            OnPaintSurfaceSvg(e.Surface.Canvas, e.BackendRenderTarget.Width, e.BackendRenderTarget.Height);
        }

        private void OnPaintCanvasSvg(object sender, SKPaintSurfaceEventArgs e)
        {
            OnPaintSurfaceSvg(e.Surface.Canvas, e.Info.Width, e.Info.Height);
        }

        private void OnPaintSurfaceSvg(SKCanvas canvas, int width, int height)
        {
            canvas.Clear(SKColors.White);

            if (items.SelectedItem is Item item && item.Svg?.Picture != null)
            {
                float pwidth = item.Svg.Picture.CullRect.Width;
                float pheight = item.Svg.Picture.CullRect.Height;
                if (pwidth > 0f && pheight > 0f)
                {
                    skElementSvg.Width = pwidth;
                    skElementSvg.Height = pheight;
                    //glHostSvg.Width = pwidth;
                    //glHostSvg.Height = pheight;
                    canvas.DrawPicture(item.Svg.Picture);
                }
            }
        }

        private void OnGLControlHostPng(object sender, EventArgs e)
        {
            var glControl = new SKGLControl();
            glControl.PaintSurface += OnPaintGLPng;
            glControl.Dock = System.Windows.Forms.DockStyle.None;
            var host = (WindowsFormsHost)sender;
            host.Child = glControl;
        }

        private void OnPaintGLPng(object sender, SKPaintGLSurfaceEventArgs e)
        {
            OnPaintSurfacePng(e.Surface.Canvas, e.BackendRenderTarget.Width, e.BackendRenderTarget.Height);
        }

        private void OnPaintCanvasPng(object sender, SKPaintSurfaceEventArgs e)
        {
            OnPaintSurfacePng(e.Surface.Canvas, e.Info.Width, e.Info.Height);
        }

        private void OnPaintSurfacePng(SKCanvas canvas, int width, int height)
        {
            canvas.Clear(SKColors.White);

            if (items.SelectedItem is Item item && item.ReferencePng != null)
            {
                float pwidth = item.ReferencePng.Width;
                float pheight = item.ReferencePng.Height;
                if (pwidth > 0f && pheight > 0f)
                {
                    skElementPng.Width = pwidth;
                    skElementPng.Height = pheight;
                    //glHostPng.Width = pwidth;
                    //glHostPng.Height = pheight;
                    canvas.DrawBitmap(item.ReferencePng, 0f, 0f);
                }
            }
        }

        private void OnGLControlHostDiff(object sender, EventArgs e)
        {
            var glControl = new SKGLControl();
            glControl.PaintSurface += OnPaintGLDiff;
            glControl.Dock = System.Windows.Forms.DockStyle.None;
            var host = (WindowsFormsHost)sender;
            host.Child = glControl;
        }

        private void OnPaintGLDiff(object sender, SKPaintGLSurfaceEventArgs e)
        {
            OnPaintSurfaceDiff(e.Surface.Canvas, e.BackendRenderTarget.Width, e.BackendRenderTarget.Height);
        }

        private void OnPaintCanvasDiff(object sender, SKPaintSurfaceEventArgs e)
        {
            OnPaintSurfaceDiff(e.Surface.Canvas, e.Info.Width, e.Info.Height);
        }

        private void OnPaintSurfaceDiff(SKCanvas canvas, int width, int height)
        {
            canvas.Clear(SKColors.White);

            if (items.SelectedItem is Item item && item.PixelDiff != null)
            {
                float pwidth = item.PixelDiff.Width;
                float pheight = item.PixelDiff.Height;
                if (pwidth > 0f && pheight > 0f)
                {
                    skElementDiff.Width = pwidth;
                    skElementDiff.Height = pheight;
                    //glHostDiff.Width = pwidth;
                    //glHostDiff.Height = pheight;
                    canvas.DrawBitmap(item.PixelDiff, 0f, 0f);
                }
            }
        }

        private void LoadItems()
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

        private void SaveItems()
        {
            var jsonSerializerSettings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };
            string json = JsonConvert.SerializeObject(Items, jsonSerializerSettings);
            File.WriteAllText("Items.json", json);
        }

        private void CreateItemsView()
        {
            ItemsView = CollectionViewSource.GetDefaultView(Items);

            var compareInfo = CultureInfo.InvariantCulture.CompareInfo;

            ItemsView.Filter = (o) =>
            {
                var name = TextItemsFilter.Text;
                var isEmpty = string.IsNullOrWhiteSpace(name);
                if (o is Item item && !isEmpty)
                {
                    return compareInfo.IndexOf(item.Name, name, CompareOptions.IgnoreCase) >= 0;
                }
                return true;
            };
        }

        private static IEnumerable<string> GetFiles(string inputPath)
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

        private static IEnumerable<string> GetFilesDrop(string[] paths)
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

        unsafe private static SKBitmap PixelDiff(SKBitmap a, SKBitmap b)
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
            /*
            for (int row = 0; row < a.Height; row++)
            {
                for (int col = 0; col < a.Width; col++)
                {
                    // red
                    *outputPtr = (byte)~(*aPtr ^ *bPtr);
                    outputPtr++;aPtr++;bPtr++;
                    // green
                    *outputPtr = (byte)~(*aPtr ^ *bPtr);
                    outputPtr++;aPtr++;bPtr++;
                    // blue
                    *outputPtr = (byte)~(*aPtr ^ *bPtr);
                    outputPtr++;aPtr++;bPtr++;
                    // alpha
                    *outputPtr = (byte)((*aPtr + *bPtr) / 2); 
                    outputPtr++;aPtr++;bPtr++;
                }
            }
            */
            return output;
        }

        private static void UpdateItem(Item item)
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
                        item.Svg.Load(item.SvgPath);
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

            if (item.ReferencePng == null)
            {
                try
                {
                    if (File.Exists(item.ReferencePngPath))
                    {
                        var referencePng = SKBitmap.Decode(item.ReferencePngPath);
                        item.ReferencePng = referencePng;

                        using (var svgBitmap = item.Svg.Picture.ToBitmap(SKColors.Transparent, 1f, 1f))
                        {
                            if (svgBitmap.Width == referencePng.Width 
                                && svgBitmap.Height == referencePng.Height)
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
        }

        private static void AddItems(List<string> paths, IList<Item> items, string referencePath, string outputPath)
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

        private static void SaveItemsAsPng(IList<Item> items)
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
    }
}
