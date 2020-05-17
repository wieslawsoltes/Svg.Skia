using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Svg.Skia;
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
                    if (e.Source == _svgSourceDockPanel)
                    {
                        var svg = new SvgSource();
#if USE_MODEL
                        var document = SKSvg.Open(fileName);
                        if (document != null)
                        {
                            var picture = SKSvg.ToModel(document);
                            if (picture != null)
                            {
                                svg.Picture = picture;
                                _svgSourceImage.Source = new SvgImage()
                                {
                                    Source = svg
                                };
                            }
                        }
#else
                        var picture = svg.Load(fileName);
                        if (picture != null)
                        {
                            _svgSourceImage.Source = new SvgImage()
                            {
                                Source = svg
                            };
                        }
#endif
                    }

                    if (e.Source == _svgResourceDockPanel)
                    {
#if USE_MODEL
                        var svg = new SvgSource();
                        var document = SKSvg.Open(fileName);
                        if (document != null)
                        {
                            var picture = SKSvg.ToModel(document);
                            if (picture != null)
                            {
                                svg.Picture = picture;
                                _svgResourceImage.Source = new SvgImage()
                                {
                                    Source = svg
                                };
                            }
                        }
#else
                        var svg = new SvgSource();
                        var picture = svg.Load(fileName);
                        if (picture != null)
                        {
                            _svgResourceImage.Source = new SvgImage()
                            {
                                Source = svg
                            };
                        }
#endif
                        }
                }
            }
        }

    }
}
