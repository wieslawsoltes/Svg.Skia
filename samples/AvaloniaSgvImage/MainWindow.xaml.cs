using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Svg.Skia.Avalonia;

namespace AvaloniaSgvImage
{
    public class MainWindow : Window
    {
        private readonly Image _svgSourceImage;
        private readonly Image _svgResourceImage;
        private readonly DockPanel _svgSourceDockPanel;
        private readonly DockPanel _svgResourceDockPanel;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            _svgSourceImage = this.FindControl<Image>("svgSourceImage");
            _svgResourceImage = this.FindControl<Image>("svgResourceImage");
            _svgSourceDockPanel = this.FindControl<DockPanel>("svgSourceDockPanel");
            _svgResourceDockPanel = this.FindControl<DockPanel>("svgResourceDockPanel");

            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
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


        private void DragOver(object sender, DragEventArgs e)
        {
            e.DragEffects = e.DragEffects & (DragDropEffects.Copy | DragDropEffects.Link);

            if (!e.Data.Contains(DataFormats.FileNames))
            {
                e.DragEffects = DragDropEffects.None;
            }
        }

        private void Drop(object sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.FileNames))
            {
                var fileName = e.Data.GetFileNames()?.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    if (sender == _svgSourceDockPanel)
                    {
                        var svg = new SvgSkia();
                        var picture = svg.Load(fileName);
                        if (picture != null)
                        {
                            _svgSourceImage.Source = new SvgImage()
                            {
                                Source = svg
                            };
                        }
                    }

                    if (sender == _svgResourceDockPanel)
                    {
                        var svg = new SvgSkia();
                        var picture = svg.Load(fileName);
                        if (picture != null)
                        {
                            _svgResourceImage.Source = new SvgImage()
                            {
                                Source = svg
                            };
                        }
                    }
                }
            }
        }

    }
}
