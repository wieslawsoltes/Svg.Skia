using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SkiaSharp;
using Svg.Skia;
//using SkiaSharp.Views.Desktop;
using SvgToPng.ViewModels;

namespace SvgToPng
{
    public partial class MainWindow : Window
    {
        public MainWindowViewModel VM { get; set; }

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            Drawable.s_systemLanguageOverride = CultureInfo.CreateSpecificCulture("en-US");
#endif
            var vm = MainWindowViewModel.Load<MainWindowViewModel>("VM.json");
            if (vm != null)
            {
                VM = vm;
                VM.ItemsViewFilter = ItemsViewFilter;
                VM.CreateItemsView();
            }
            else
            {
                VM = new MainWindowViewModel()
                {
                    Items = new ObservableCollection<Item>(),
                    ReferencePaths = new ObservableCollection<string>(),
                    ItemsViewFilter = ItemsViewFilter,
                    ShowFailed = true,
                    ShowPassed = true
                };
                VM.CreateItemsView();
#if DEBUG
                VM.ReferencePaths = new ObservableCollection<string>(new string[]
                {
                @"c:\DOWNLOADS\GitHub\Svg.Skia\externals\SVG\Tests\W3CTestSuite\png\",
                @"c:\DOWNLOADS\GitHub-Forks\resvg-test-suite\png\",
                @"e:\Dropbox\Draw2D\SVG\vs2017-png\",
                @"e:\Dropbox\Draw2D\SVG\W3CTestSuite-png\"
                });
                VM.ReferencePath = VM.ReferencePaths[0];
                VM.OutputPath = Path.Combine(Directory.GetCurrentDirectory(), "png");
#endif
            }

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            TextItemsFilter.TextChanged += TextItemsFilter_TextChanged;
            CheckShowPassed.Click += CheckShowPassed_Click;
            CheckShowFailed.Click += CheckShowFailed_Click;

            items.SelectionChanged += Items_SelectionChanged;
            items.MouseDoubleClick += Items_MouseDoubleClick;
            items.KeyDown += Items_KeyDown;

            skElementSvg.PaintSurface += OnPaintCanvasSvg;
            skElementPng.PaintSurface += OnPaintCanvasPng;
            skElementDiff.PaintSurface += OnPaintCanvasDiff;
#if DEBUG
            skElementSvg.MouseMove += Svg_MouseMove;
#endif
            DataContext = this.VM;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            VM.ItemsView.Refresh();
        }

        private void Svg_MouseMove(object sender, MouseEventArgs e)
        {
            var point = e.GetPosition(skElementSvg);

            if (items.SelectedItem is Item item && item.Drawable != null)
            {
                var skPoint = new SKPoint((float)point.X, (float)point.Y);
                var drawable = item.Drawable.HitTest(skPoint);
                if (drawable != null)
                {
                    Debug.WriteLine($"{point} {drawable.GetType().Name}");
#if false
                    if (drawable is DrawablePath drawablePath)
                    {
                        var sb = new StringBuilder();
                        CSharpCodeGen.Generate(drawablePath.Path, sb);
                        CSharpCodeGen.Generate(drawablePath.Transform, sb);
                        Debug.WriteLine(sb.ToString());
                    }
#endif
                }
            }
        }

        private bool ItemsViewFilter(object obj)
        {
            var name = TextItemsFilter.Text;
            var showPassed = CheckShowPassed.IsChecked == true;
            var showFailed = CheckShowFailed.IsChecked == true;
            var isEmpty = string.IsNullOrWhiteSpace(name);
            if (obj is Item item)
            {
                if (showPassed == false)
                {
                    if (item.Passed == true)
                    {
                        return false;
                    }
                }
                if (showFailed == false)
                {
                    if (item.Passed == false)
                    {
                        return false;
                    }
                }
                if (!isEmpty)
                {
                    var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
                    return compareInfo.IndexOf(item.Name, name, CompareOptions.IgnoreCase) >= 0;
                }
            }
            return true;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            MainWindowViewModel.Save("VM.json", VM);
            VM.ClearItems();
        }

        private void TextItemsFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            VM.ItemsView.Refresh();
        }

        private void CheckShowFailed_Click(object sender, RoutedEventArgs e)
        {
            VM.ItemsView.Refresh();
        }

        private void CheckShowPassed_Click(object sender, RoutedEventArgs e)
        {
            VM.ItemsView.Refresh();
        }

        private void Items_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (items.SelectedItem is Item item)
            {
                Update(item);
            }
            Invalidate();
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

        private void Items_KeyDown(object sender, KeyEventArgs e)
        {
            if (items.SelectedItem is Item item)
            {
                if (e.Key == Key.Delete)
                {
                    VM.RemoveItem(item);
                    Invalidate();
                }
                else if (e.Key == Key.Insert)
                {
                    VM.ResetItem(item);
                    Update(item);
                    Invalidate();
                }
            }
        }

        private void HandleDrop(string[] paths, string referencePath, string outputPath)
        {
            var inputFiles = MainWindowViewModel.GetFilesDrop(paths).ToList();
            if (inputFiles.Count > 0)
            {
                VM.AddItems(inputFiles, VM.Items, referencePath, outputPath);
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

        private void ButtonOutputPath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog()
            {
                SelectedPath = TextOutputPath.Text
            };
            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                var path = dlg.SelectedPath;
                if (path != null)
                {
                    VM.OutputPath = path;
                    TextOutputPath.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                }
            }
        }

        private void ButtonReferencePath_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog()
            {
                SelectedPath = TextReferencePath.Text
            };
            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                var path = dlg.SelectedPath;
                if (path != null)
                {
                    VM.ReferencePaths.Add(path);
                    VM.ReferencePath = path;
                    TextReferencePath.GetBindingExpression(ComboBox.SelectedItemProperty).UpdateTarget();
                }
            }
        }

        private void ButtonClearFilter_Click(object sender, RoutedEventArgs e)
        {
            VM.ItemsFilter = "";
            TextItemsFilter.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            VM.ClearItems();
            Invalidate();
        }

        private void ButtonAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Supported Files (*.svg;*.svgz)" +
                         "|*.svg;*.svgz|Svg Files (*.svg)|*.svg;" +
                         "|Svgz Files (*.svgz)|*.svgz" +
                         "|All Files (*.*)|*.*",
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

        private async void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            string outputPath = TextOutputPath.Text;
            var outputFormats = new List<string>();

            if (CheckFormatPng.IsChecked == true)
            {
                outputFormats.Add("png");
            }

            if (CheckFormatJpg.IsChecked == true)
            {
                outputFormats.Add("jpg");
            }

            if (CheckFormatWebp.IsChecked == true)
            {
                outputFormats.Add("webp");
            }

            if (CheckFormatPdf.IsChecked == true)
            {
                outputFormats.Add("pdf");
            }

            if (CheckFormatXps.IsChecked == true)
            {
                outputFormats.Add("xps");
            }

            if (outputFormats.Count <= 0)
            {
                return;
            }

            var textBackground = TextOutputBackground.Text;
            var textScaleX = TextOutputScaleX.Text;
            var textScaleY = TextOutputScaleY.Text;

            if (SKColor.TryParse(textBackground, out var skBackgroundColor) == false)
            {
                return;
            }

            if (float.TryParse(textScaleX, out var scaleX) == false)
            {
                return;
            }

            if (float.TryParse(textScaleY, out var scaleY) == false)
            {
                return;
            }

            var items = new List<Item>();

            foreach (var obj in VM.ItemsView)
            {
                if (obj is Item item)
                {
                    items.Add(item);
                }
            }

            if (items.Count <= 0)
            {
                return;
            }

            await Task.Factory.StartNew(() =>
            {
                VM.ExportItems(items, outputPath, outputFormats, skBackgroundColor, scaleX, scaleY);
            });
        }

        private void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Items Files (*.json)|*.json;|" +
                         "All Files (*.*)|*.*",
                DefaultExt = "json",
                FilterIndex = 0
            };
            if (dlg.ShowDialog() == true)
            {
                var path = dlg.FileName;
                if (path != null)
                {
                    VM.ClearItems();
                    VM.LoadItems(path);
                    VM.CreateItemsView();
                    DataContext = null;
                    DataContext = VM;
                }
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog()
            {
                Filter = "Items Files (*.json)|*.json;" +
                         "|All Files (*.*)|*.*",
                FileName = "Items",
                DefaultExt = "json",
                FilterIndex = 0
            };
            if (dlg.ShowDialog() == true)
            {
                var path = dlg.FileName;
                if (path != null)
                {
                    VM.SaveItems(path);
                }
            }
        }

        private void ItemExport_Click(object sender, RoutedEventArgs e)
        {
            if (items.SelectedIndex >= 0 && items.Items.GetItemAt(items.SelectedIndex) is Item item)
            {
                var dlg = new Microsoft.Win32.SaveFileDialog()
                {
                    Filter = "Png Files (*.png)|*.png;" +
                             "|Jpg Files (*.jpg)|*.jpg;" +
                             "|Jpeg Files (*.jpeg)|*.jpeg;" +
                             "|Webp Files (*.webp)|*.webp;" +
                             "|Pdf Files (*.pdf)|*.pdf;" +
                             "|Xps Files (*.xps)|*.xps;" +
                             "|Svg Files (*.svg)|*.svg;" +
                             "|All Files (*.*)|*.*",
                    FileName = item.Name,
                    DefaultExt = "png",
                    FilterIndex = 0
                };
                if (dlg.ShowDialog() == true && dlg.FileName != null)
                {
                    var textBackground = TextOutputBackground.Text;
                    var textScaleX = TextOutputScaleX.Text;
                    var textScaleY = TextOutputScaleY.Text;

                    if (SKColor.TryParse(textBackground, out var skBackgroundColor) == false)
                    {
                        return;
                    }

                    if (float.TryParse(textScaleX, out var scaleX) == false)
                    {
                        return;
                    }

                    if (float.TryParse(textScaleY, out var scaleY) == false)
                    {
                        return;
                    }

                    VM.ExportItem(item.SvgPath, dlg.FileName, skBackgroundColor, scaleX, scaleY);
                }
            }
        }

        private void ItemReload_Click(object sender, RoutedEventArgs e)
        {
            if (items.SelectedIndex >= 0 && items.Items.GetItemAt(items.SelectedIndex) is Item item)
            {
                VM.ResetItem(item);
                Update(item);
                Invalidate();
            }
        }

        private void Item_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (items.SelectedIndex >= 0 && items.Items.GetItemAt(items.SelectedIndex) is Item item)
            {
                VM.RemoveItem(item);
                Invalidate();
            }
        }

        private void ItemOpenNotepad_Click(object sender, RoutedEventArgs e)
        {
            if (items.SelectedIndex >= 0 && items.Items.GetItemAt(items.SelectedIndex) is Item item)
            {
                Process.Start("notepad", item.SvgPath);
            }
        }

        private void ItemOpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (items.SelectedIndex >= 0 && items.Items.GetItemAt(items.SelectedIndex) is Item item)
            {
                Process.Start("explorer", item.SvgPath);
            }
        }

        private void Update(Item item)
        {
            TextOpenTime.Text = "";
            TextToPictureTime.Text = "";
            TextDrawTime.Text = "";
            VM.UpdateItem(item, (text) => TextOpenTime.Text = text, (text) => TextToPictureTime.Text = text);
        }

        private void Invalidate()
        {
            skElementSvg.InvalidateVisual();
            skElementPng.InvalidateVisual();
            skElementDiff.InvalidateVisual();
        }

        private void OnPaintCanvasSvg(object sender, SKPaintSurfaceEventArgs e)
        {
            OnPaintSurfaceSvg(e.Surface.Canvas, e.Info.Width, e.Info.Height);
        }

        private void OnPaintCanvasPng(object sender, SKPaintSurfaceEventArgs e)
        {
            OnPaintSurfacePng(e.Surface.Canvas, e.Info.Width, e.Info.Height);
        }

        private void OnPaintCanvasDiff(object sender, SKPaintSurfaceEventArgs e)
        {
            OnPaintSurfaceDiff(e.Surface.Canvas, e.Info.Width, e.Info.Height);
        }

        private void OnPaintSurfaceSvg(SKCanvas canvas, int width, int height)
        {
            if (items.SelectedItem is Item item)
            {
                var stopwatch = Stopwatch.StartNew();

                canvas.Clear(SKColors.White);

                if (item.Picture != null)
                {
                    float pwidth = item.Picture.CullRect.Width;
                    float pheight = item.Picture.CullRect.Height;
                    if (pwidth > 0f && pheight > 0f)
                    {
                        skElementSvg.Width = pwidth;
                        skElementSvg.Height = pheight;
                        canvas.DrawPicture(item.Picture);
                    }
                }

                stopwatch.Stop();
                TextDrawTime.Text = $"{Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3)}ms";
                Debug.WriteLine($"Draw: {Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3)}ms");
            }
            else
            {
                canvas.Clear(SKColors.White);
            }
        }

        private void OnPaintSurfacePng(SKCanvas canvas, int width, int height)
        {
            canvas.Clear(SKColors.White);

            if (items.SelectedItem is Item item && item.ReferencePng != null && CheckDrawReference.IsChecked == true)
            {
                float pwidth = item.ReferencePng.Width;
                float pheight = item.ReferencePng.Height;
                if (pwidth > 0f && pheight > 0f)
                {
                    skElementPng.Width = pwidth;
                    skElementPng.Height = pheight;
                    canvas.DrawBitmap(item.ReferencePng, 0f, 0f);
                }
            }
        }

        private void OnPaintSurfaceDiff(SKCanvas canvas, int width, int height)
        {
            canvas.Clear(SKColors.White);

            if (items.SelectedItem is Item item && item.PixelDiff != null && CheckDrawDiff.IsChecked == true)
            {
                float pwidth = item.PixelDiff.Width;
                float pheight = item.PixelDiff.Height;
                if (pwidth > 0f && pheight > 0f)
                {
                    skElementDiff.Width = pwidth;
                    skElementDiff.Height = pheight;
                    canvas.DrawBitmap(item.PixelDiff, 0f, 0f);
                }
            }
        }
    }
}
