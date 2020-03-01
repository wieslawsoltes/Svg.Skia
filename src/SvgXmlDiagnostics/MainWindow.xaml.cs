using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Svg;

namespace SvgXmlDiagnostics
{
    public class Item
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public SvgDocument Document { get; set; }
    }

    public class TypeNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.GetType().Name;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class MainWindow : Window
    {
        private volatile bool _isProcessing = false;
        public ObservableCollection<Item> Items { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Items = new ObservableCollection<Item>();
            DataContext = this;
        }

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing)
            {
                return;
            }
            var dlg = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Supported Files (*.svg;*.svgz)|*.svg;*svgz" +
                         "|Svg Files (*.svg)|*.svg" +
                         "|Svgz Files (*.svgz)|*.svgz" +
                         "|All Files (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                _isProcessing = true;
                var paths = dlg.FileNames.ToList();
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        foreach (var path in paths)
                        {
                            var svgDocument = SvgDocument.Open(path);
                            if (svgDocument != null)
                            {
                                svgDocument.LoadStyles();
                                var item = new Item()
                                {
                                    Name = Path.GetFileNameWithoutExtension(path),
                                    Path = path,
                                    Document = svgDocument
                                };
                                Dispatcher.Invoke(() => Items.Add(item));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        Debug.WriteLine(ex.StackTrace);
                    }
                    finally
                    {
                        _isProcessing = false;
                    }
                });
            }
        }

        private void FileExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
