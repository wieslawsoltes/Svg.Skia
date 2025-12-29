using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using ShimSkiaSharp;
using SS = Svg.Skia;

namespace AvaloniaSvgSkiaSample;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        svgSvgDockPanel.AddHandler(DragDrop.DropEvent, Drop);
        svgSvgDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);

        svgExtensionDockPanel.AddHandler(DragDrop.DropEvent, Drop);
        svgExtensionDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);

        svgSourceDockPanel.AddHandler(DragDrop.DropEvent, Drop);
        svgSourceDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);

        svgResourceDockPanel.AddHandler(DragDrop.DropEvent, Drop);
        svgResourceDockPanel.AddHandler(DragDrop.DragOverEvent, DragOver);

        stringTextBox.Text =
            """
            <svg width="100" height="100">
               <circle cx="50" cy="50" r="40" stroke="green" stroke-width="4" fill="yellow" />
            </svg>
            """;
        
        var svgImage = svgCloningOriginal.Source as SvgImage;
        var clone = new SvgImage { Source = svgImage?.Source?.Clone() };

        foreach (var cmd in clone.Source?.Svg?.Model?.Commands?.OfType<DrawPathCanvasCommand>() ?? [])
        {
            var paint = cmd.Paint;
            if (paint?.Color is not null)
            {
                paint.Color = ToGrayscale(paint.Color.Value);
            }

            if (paint?.Shader is ColorShader shader)
            {
                paint.Shader = SKShader.CreateColor(ToGrayscale(paint.Color.Value), shader.ColorSpace);
            }
        }

        clone.Source?.ReLoad(null);
        svgCloningClone.Source = clone;
        svgCloningClone.InvalidateVisual();
    }

    private static SKColor ToGrayscale(SKColor color)
    {
        var luminance = (byte)(0.2126f * color.Red + 0.7152f * color.Green + 0.0722f * color.Blue);
        return new SKColor(luminance, luminance, luminance, color.Alpha);
    }

    public void SvgSvgStretchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (svgSvg is { })
        {
            var comboBox = (ComboBox)sender;
            svgSvg.Stretch = (Stretch)comboBox.SelectedIndex;
        }
    }

    public void SvgExtensionStretchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (svgExtensionImage is { })
        {
            var comboBox = (ComboBox)sender;
            svgExtensionImage.Stretch = (Stretch)comboBox.SelectedIndex;
        }
    }

    public void SvgSourceStretchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (svgSourceImage is { })
        {
            var comboBox = (ComboBox)sender;
            svgSourceImage.Stretch = (Stretch)comboBox.SelectedIndex;
        }
    }

    public void SvgResourceStretchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (svgResourceImage is { })
        {
            var comboBox = (ComboBox)sender;
            svgResourceImage.Stretch = (Stretch)comboBox.SelectedIndex;
        }
    }

    public void SvgStringStretchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (svgString is { })
        {
            var comboBox = (ComboBox)sender;
            svgString.Stretch = (Stretch)comboBox.SelectedIndex;
        }
    }

    private void DragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = e.DragEffects & (DragDropEffects.Copy | DragDropEffects.Link);

        if (!e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void Drop(object sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var fileName = e.Data.GetFileNames()?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                if (sender == svgSvgDockPanel)
                {
                    svgSvg.Path = fileName;
                }
                else if (sender == svgExtensionDockPanel)
                {
                    var svg = SvgSource.Load(fileName);
                    if (svg is { })
                    {
                        svgExtensionImage.Source = new SvgImage
                        {
                            Source = svg
                        };
                    }
                }
                else if (sender == svgSourceDockPanel)
                {
                    var svg = SvgSource.Load(fileName);
                    if (svg is { })
                    {
                        svgSourceImage.Source = new SvgImage
                        {
                            Source = svg
                        };
                    }
                }
                else if (sender == svgResourceDockPanel)
                {
                    var svg = SvgSource.Load(fileName);
                    if (svg is { })
                    {
                        svgResourceImage.Source = new SvgImage
                        {
                            Source = svg
                        };
                    }
                }
                else if (sender == stringTextBox || sender == svgString)
                {
                    var source = File.ReadAllText(fileName);
                    stringTextBox.Text = source;
                }
            }
        }
    }
}
