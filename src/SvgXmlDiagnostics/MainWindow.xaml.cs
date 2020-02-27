using System.Windows;
using Svg;

namespace SvgXmlDiagnostics
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = "Supported Files (*.svg;*.svgz)|*.svg;*svgz|Svg Files (*.svg)|*.svg|Svgz Files (*.svgz)|*.svgz|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                var svgDocument = SvgDocument.Open(dlg.FileName);
                if (svgDocument != null)
                {
                    DocumentTree.Items.Add(svgDocument);
                }
            }
        }

        private void FileExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
