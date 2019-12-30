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
using System.Windows.Media.Imaging;
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
        public BitmapImage Image { get; set; }
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

            this.Closing += (sender, e) => SaveItems();
            this.TextItemsFilter.TextChanged += (sender, e) => ItemsView.Refresh();

            items.SelectionChanged += (sender, e) =>
            {
                if (items.SelectedItem is Item item)
                {
                    UpdateItem(item);
                }
                skelement.InvalidateVisual();
                glhost.Child?.Invalidate();
            };

            items.MouseDoubleClick += (sender, e) =>
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
            };

            CreateItemsView();

            DataContext = this;
        }

        private void OnGLControlHost(object sender, EventArgs e)
        {
            var glControl = new SKGLControl();
            glControl.PaintSurface += OnPaintGL;
            glControl.Dock = System.Windows.Forms.DockStyle.None;
            var host = (WindowsFormsHost)sender;
            host.Child = glControl;
        }

        private void OnPaintGL(object sender, SKPaintGLSurfaceEventArgs e)
        {
            OnPaintSurface(e.Surface.Canvas, e.BackendRenderTarget.Width, e.BackendRenderTarget.Height);
        }

        private void OnPaintCanvas(object sender, SKPaintSurfaceEventArgs e)
        {
            OnPaintSurface(e.Surface.Canvas, e.Info.Width, e.Info.Height);
        }

        private void OnPaintSurface(SKCanvas canvas, int width, int height)
        {
            canvas.Clear(SKColors.White);

            if (items.SelectedItem is Item item && item.Svg?.Picture != null)
            {
                float pwidth = item.Svg.Picture.CullRect.Width;
                float pheight = item.Svg.Picture.CullRect.Height;
                if (pwidth > 0f && pheight > 0f)
                {
                    skelement.Width = pwidth;
                    skelement.Height = pheight;
                    //glhost.Width = pwidth;
                    //glhost.Height = pheight;
                    canvas.DrawPicture(item.Svg.Picture);
                }
            }
        }

        private void HandleDrop(string[] paths, string referencePath, string outputPath)
        {
            var inputFiles = GetFilesDrop(paths).ToList();
            if (inputFiles.Count > 0)
            {
                Add(inputFiles, Items, referencePath, outputPath);
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
                item.Image = null;
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
                Save(Items);
            });
        }

        private void UpdateItem(Item item)
        {
            if (item.Svg?.Picture == null)
            {
                Load(item);
            }

            if (item.Image != null)
            {
                referenceImage.Source = item.Image;
            }
            else
            {
                referenceImage.Source = null;
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

        private static void Load(Item item)
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

        private static void Add(List<string> paths, IList<Item> items, string referencePath, string outputPath)
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

        private static void Save(IList<Item> items)
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
