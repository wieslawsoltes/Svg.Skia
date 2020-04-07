using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Svg;
using SvgXml.Diagnostics.Models;
using SvgXml.Diagnostics.ViewModels;

namespace SvgXml.Diagnostics.Views
{
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

    public class MainWindow : Window
    {
        private volatile bool _isProcessing = false;

        public MainWindow()
        {
            InitializeComponent();
            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async Task Open(string[] paths)
        {
            foreach (var path in paths)
            {
                var svgDocument = SvgDocument.Open(path);
                if (svgDocument != null)
                {
                    svgDocument.LoadIds();
                    svgDocument.LoadStyles();
                    svgDocument.FlushStyles(true);
                    var item = new Item()
                    {
                        Name = Path.GetFileNameWithoutExtension(path),
                        Path = path,
                        Document = svgDocument
                    };
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (DataContext is MainWindowViewModel mainWindowViewModel)
                        {
                            mainWindowViewModel.Items.Add(item);
                        }
                    });
                }
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

        private async void Drop(object sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.FileNames))
            {
                var paths = e.Data.GetFileNames().ToArray();
                _isProcessing = true;
                await Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        await Open(paths);
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

        private async void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_isProcessing)
            {
                return;
            }

            var dlg = new OpenFileDialog();
            dlg.AllowMultiple = true;
            dlg.Filters.Add(new FileDialogFilter() { Name = "Supported Files", Extensions = new List<string> { "svg", "svgz" } });
            dlg.Filters.Add(new FileDialogFilter() { Name = "Svg Files", Extensions = new List<string> { "svg" } });
            dlg.Filters.Add(new FileDialogFilter() { Name = "Svgz Files", Extensions = new List<string> { "svgz" } });
            dlg.Filters.Add(new FileDialogFilter() { Name = "All Files", Extensions = new List<string> { "*" } });

            var paths = await dlg.ShowAsync(this);

            if (paths == null || paths.Length <= 0)
            {
                return;
            }

            _isProcessing = true;
            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    await Open(paths);
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

        private void FileExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
