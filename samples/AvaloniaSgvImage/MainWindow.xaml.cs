using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace AvaloniaSgvImage
{
    public class MainWindow : Window
    {
        private readonly Image _svgSourceImage;
        private readonly Image _svgResourceImage;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            _svgSourceImage = this.FindControl<Image>("svgSourceImage");
            _svgResourceImage = this.FindControl<Image>("svgResourceImage");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void SvgSourceStretchChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_svgSourceImage != null)
            {
                var comboxBox = (ComboBox)sender;
                _svgSourceImage.Stretch = (Stretch)comboxBox.SelectedIndex;
            }
        }

        public void SvgResourceStretchChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_svgResourceImage != null)
            {
                var comboxBox = (ComboBox)sender;
                _svgResourceImage.Stretch = (Stretch)comboxBox.SelectedIndex;
            }
        }
    }
}
