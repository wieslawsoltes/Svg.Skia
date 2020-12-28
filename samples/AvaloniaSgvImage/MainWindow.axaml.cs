using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using SS = Svg.Skia;
using Avalonia.Svg.Skia;

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

            //VisualRoot.Renderer.DrawDirtyRects = true;
            //VisualRoot.Renderer.DrawFps = true;

            _svgSourceImage = this.FindControl<Image>("svgSourceImage");
            _svgResourceImage = this.FindControl<Image>("svgResourceImage");
            _svgSourceDockPanel = this.FindControl<DockPanel>("svgSourceDockPanel");
            _svgResourceDockPanel = this.FindControl<DockPanel>("svgResourceDockPanel");

            _svgSourceDockPanel.AddHandler(DragDrop.DropEvent, Drop);
            _svgSourceDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);

            _svgResourceDockPanel.AddHandler(DragDrop.DropEvent, Drop);
            _svgResourceDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);
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
                        var svg = new SvgSource();
#if USE_PICTURE
                        var document = SS.SKSvg.Open(fileName);
                        if (document != null)
                        {
                            var picture = SS.SKSvg.ToModel(document);
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
                            _svgSourceImage.Source = new SvgImage
                            {
                                Source = svg
                            };
                        }
#endif
                    }

                    if (sender == _svgResourceDockPanel)
                    {
#if USE_PICTURE
                        var svg = new SvgSource();
                        var document = SS.SKSvg.Open(fileName);
                        if (document != null)
                        {
                            var picture = SS.SKSvg.ToModel(document);
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
                            _svgResourceImage.Source = new SvgImage
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
