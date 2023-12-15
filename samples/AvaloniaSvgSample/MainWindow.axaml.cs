/*
 * Svg.Skia SVG rendering library.
 * Copyright (C) 2023  Wiesław Šoltés
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Svg;

namespace AvaloniaSvgSample;

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
                    svgExtensionImage.Source = new SvgImage
                    {
                        Source = SvgSource.Load(fileName, null)
                    };
                }
                else if (sender == svgSourceDockPanel)
                {
                    svgSourceImage.Source = new SvgImage
                    {
                        Source = SvgSource.Load(fileName, null)
                    };
                }
                else if (sender == svgResourceDockPanel)
                {
                    svgResourceImage.Source = new SvgImage
                    {
                        Source = SvgSource.Load(fileName, null)
                    };
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
