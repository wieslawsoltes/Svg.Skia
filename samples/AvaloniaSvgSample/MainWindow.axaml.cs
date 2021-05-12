using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using SS = Svg.Skia;
using Avalonia.Svg;

namespace AvaloniaSvgSample
{
    public class MainWindow : Window
    {
        private Image _svgExtensionImage;
        private Image _svgSourceImage;
        private Image _svgResourceImage;
        private DockPanel _svgExtensionDockPanel;
        private DockPanel _svgSourceDockPanel;
        private DockPanel _svgResourceDockPanel;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            _svgExtensionImage = this.FindControl<Image>("svgExtensionImage");
            _svgSourceImage = this.FindControl<Image>("svgSourceImage");
            _svgResourceImage = this.FindControl<Image>("svgResourceImage");

            _svgExtensionDockPanel = this.FindControl<DockPanel>("svgExtensionDockPanel");
            _svgSourceDockPanel = this.FindControl<DockPanel>("svgSourceDockPanel");
            _svgResourceDockPanel = this.FindControl<DockPanel>("svgResourceDockPanel");

            _svgSourceDockPanel.AddHandler(DragDrop.DropEvent, Drop);
            _svgSourceDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);

            _svgResourceDockPanel.AddHandler(DragDrop.DropEvent, Drop);
            _svgResourceDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);
        }

        public void SvgExtensionStretchChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_svgExtensionImage is { })
            {
                var comboBox = (ComboBox)sender;
                _svgExtensionImage.Stretch = (Stretch)comboBox.SelectedIndex;
            }
        }

        public void SvgSourceStretchChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_svgSourceImage is { })
            {
                var comboBox = (ComboBox)sender;
                _svgSourceImage.Stretch = (Stretch)comboBox.SelectedIndex;
            }
        }

        public void SvgResourceStretchChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_svgResourceImage is { })
            {
                var comboBox = (ComboBox)sender;
                _svgResourceImage.Stretch = (Stretch)comboBox.SelectedIndex;
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
                    if (sender == _svgExtensionDockPanel)
                    {
                        var svg = new SvgSource();
                        var picture = svg.Load(fileName);
                        if (picture is { })
                        {
                            _svgExtensionImage.Source = new SvgImage
                            {
                                Source = svg
                            };
                        }
                    }
                    else if (sender == _svgSourceDockPanel)
                    {
                        var svg = new SvgSource();
                        var picture = svg.Load(fileName);
                        if (picture is { })
                        {
                            _svgSourceImage.Source = new SvgImage
                            {
                                Source = svg
                            };
                        }
                    }
                    else if (sender == _svgResourceDockPanel)
                    {
                        var svg = new SvgSource();
                        var picture = svg.Load(fileName);
                        if (picture is { })
                        {
                            _svgResourceImage.Source = new SvgImage
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
