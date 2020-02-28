using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Svg;

namespace SvgXmlDiagnostics
{
    public class Item
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public SvgDocument Document { get; set; }
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<Item> Items { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Items = new ObservableCollection<Item>();
            DataContext = this;
        }

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
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
                foreach (var fileName in dlg.FileNames)
                {
                    var svgDocument = SvgDocument.Open(fileName);
                    if (svgDocument != null)
                    {
                        var item = new Item()
                        {
                            Name = Path.GetFileNameWithoutExtension(fileName),
                            Path = fileName,
                            Document = svgDocument
                        };
                        Items.Add(item);
                    }
                }
            }
        }

        private void FileExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
