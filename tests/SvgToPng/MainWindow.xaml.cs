using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace SvgToPng
{
    public partial class MainWindow : Window, IConvertProgress, ISaveProgress
    {
        public ObservableCollection<Item> Items { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            Items = new ObservableCollection<Item>();
            DataContext = this;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            items.SelectionChanged += Items_SelectionChanged;
            canvas.PaintSurface += Canvas_PaintSurface;
            canvas.InvalidateVisual();
        }

        private void Items_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            canvas.InvalidateVisual();
        }

        private void Canvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.White);

            if (items.SelectedItem is Item item)
            {
                if (item.Picture != null)
                {
                    canvas.DrawPicture(item.Picture);
                }
            }
        }

        public async Task HandleDrop(string[] paths)
        {
            var inputFiles = SvgToPngConverter.GetFilesDrop(paths).ToList();
            if (inputFiles.Count > 0)
            {
                await SvgToPngConverter.Convert(inputFiles, Items, this);
            }
        }

        public async Task ConvertStatusReset()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TextProgress.Text = $"";
                TextInputFile.Text = $"";
                TextOutputFile.Text = $"";
            });
        }

        public async Task ConvertStatusProgress(int count, int total, string inputFile)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TextProgress.Text = $"Conterting file {count}/{total}";
                TextInputFile.Text = $"{inputFile}";
            });
        }

        public async Task ConvertStatusDone()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TextProgress.Text = $"Done";
                TextInputFile.Text = $"";
                TextOutputFile.Text = $"";
            });
        }

        public async Task SaveStatusReset()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TextProgress.Text = $"";
                TextInputFile.Text = $"";
                TextOutputFile.Text = $"";
            });
        }

        public async Task SaveStatusProgress(int count, int total, string inputFile, string outputFile)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TextProgress.Text = $"Saving file {count}/{total}";
                TextInputFile.Text = $"{inputFile}";
                TextOutputFile.Text = $"{outputFile}";
            });
        }

        public async Task SaveStatusDone()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TextProgress.Text = $"Done";
                TextInputFile.Text = $"";
                TextOutputFile.Text = $"";
            });
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (paths != null && paths.Length > 0)
                {
                    await HandleDrop(paths);
                }
            }
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            Items.Clear();
        }

        private async void ButtonSavePng_Click(object sender, RoutedEventArgs e)
        {
            string outputPath = TextOutputPath.Text;
            await SvgToPngConverter.Save(outputPath, Items, this);
        }
    }
}
