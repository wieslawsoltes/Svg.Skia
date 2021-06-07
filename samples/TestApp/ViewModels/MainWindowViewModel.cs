using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using Svg.CodeGen.Skia;
using Svg.Skia;
using TestApp.Models;

namespace TestApp.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly ObservableCollection<FileItemViewModel>? _items;
        private FileItemViewModel? _selectedItem;
        private string? _itemQuery;
        private ReadOnlyObservableCollection<FileItemViewModel>? _filteredItems;

        public FileItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
        }

        public string? ItemQuery
        {
            get => _itemQuery;
            set => this.RaiseAndSetIfChanged(ref _itemQuery, value);
        }

        public ReadOnlyObservableCollection<FileItemViewModel>? FilteredItems
        {
            get => _filteredItems;
            set => this.RaiseAndSetIfChanged(ref _filteredItems, value);
        }

        public ICommand ResetQueryCommand { get; }

        public ICommand LoadConfigurationCommand { get; }

        public ICommand SaveConfigurationCommand { get; }

        public ICommand ClearConfigurationCommand { get; }
        
        public ICommand AddItemCommand { get; }

        public ICommand CopyAsCSharpCommand { get; }

        public ICommand ExportCommand { get; }

        public MainWindowViewModel()
        {
            _items = new ObservableCollection<FileItemViewModel>();

            var queryFilter = this.WhenValueChanged(t => t.ItemQuery)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Select(ItemQueryFilter)
                .DistinctUntilChanged();

            _items
                .ToObservableChangeSet()
                .Filter(queryFilter)
                .Sort(SortExpressionComparer<FileItemViewModel>.Ascending(x => x.Name))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out _filteredItems)
                .AsObservableList();

            var resetQueryCanExecute = this.WhenAnyValue(x => x.ItemQuery)
                .Select(x => !string.IsNullOrWhiteSpace(x))
                .ObserveOn(RxApp.MainThreadScheduler);

            ResetQueryCommand = ReactiveCommand.Create(
                () => ItemQuery = "", 
                resetQueryCanExecute);

            LoadConfigurationCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var dlg = new OpenFileDialog();
                dlg.Filters.Add(new FileDialogFilter() { Name = "Configuration (*.json)", Extensions = new List<string> {"json"} });
                dlg.Filters.Add(new FileDialogFilter() { Name = "All Files (*.*)", Extensions = new List<string> {"*"} });
                var result = await dlg.ShowAsync((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
                if (result is { })
                {
                    var path = result.FirstOrDefault();
                    if (path is { })
                    {
                        LoadConfiguration(path);
                    }
                }
            });

            SaveConfigurationCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var dlg = new SaveFileDialog();
                dlg.Filters.Add(new FileDialogFilter() { Name = "Configuration (*.json)", Extensions = new List<string> {"json"} });
                dlg.Filters.Add(new FileDialogFilter() { Name = "All Files (*.*)", Extensions = new List<string> {"*"} });
                var result = await dlg.ShowAsync((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
                if (result is { })
                {
                    SaveConfiguration(result);
                }
            });

            ClearConfigurationCommand = ReactiveCommand.Create(() =>
            {
                ItemQuery = null;
                SelectedItem = null;
                _items?.Clear();
            });

            AddItemCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var dlg = new OpenFileDialog {AllowMultiple = true};
                dlg.Filters.Add(new FileDialogFilter() { Name = "Svg Files (*.svg;*.svgz)", Extensions = new List<string> {"svg", "svgz"} });
                dlg.Filters.Add(new FileDialogFilter() { Name = "All Files (*.*)", Extensions = new List<string> {"*"} });
                var result = await dlg.ShowAsync((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
                if (result is { })
                {
                    var paths = result.ToList();
                    foreach (var path in paths)
                    {
                        AddItem(path);
                    }
                }
            });

            CopyAsCSharpCommand = ReactiveCommand.CreateFromTask<Avalonia.Svg.Skia.Svg>(async svg =>
            {
                if (_selectedItem is null || svg?.Model is null)
                {
                    return;
                }

                var code = SkiaCSharpCodeGen.Generate(svg.Model, "Svg", CreateClassName(_selectedItem.Path));

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        Application.Current.Clipboard.SetTextAsync(code);
                    }
                    catch
                    {
                        // ignored
                    }
                });
            });

            ExportCommand = ReactiveCommand.CreateFromTask<Avalonia.Svg.Skia.Svg>(async svg =>
            {
                if (_selectedItem is null || svg?.Model is null)
                {
                    return;
                }

                var dlg = new SaveFileDialog();
                dlg.Filters.Add(new FileDialogFilter() { Name = "Png Files (*.png)", Extensions = new List<string> {"png"} });
                dlg.Filters.Add(new FileDialogFilter() { Name = "Jpeg Files (*.jpeg)", Extensions = new List<string> {"jpeg"} });
                dlg.Filters.Add(new FileDialogFilter() { Name = "C# Files (*.cs)", Extensions = new List<string> {"cs"} });
                dlg.Filters.Add(new FileDialogFilter() { Name = "Pdf Files (*.pdf)", Extensions = new List<string> {"pdf"} });
                dlg.Filters.Add(new FileDialogFilter() { Name = "Xps Files (*.xps)", Extensions = new List<string> {"xps"} });
                dlg.Filters.Add(new FileDialogFilter() { Name = "All Files (*.*)", Extensions = new List<string> {"*"} });
                dlg.InitialFileName = Path.GetFileNameWithoutExtension(_selectedItem.Path);
                var result = await dlg.ShowAsync((Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
                if (result is { })
                {
                    Export(result, svg, "#00FFFFFF", 1f, 1f);
                }
            });
        }

        private Func<FileItemViewModel, bool> ItemQueryFilter(string? searchQuery)
        {
            return item =>
            {
                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    return item.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
                }

                return true;
            };
        }

        public void Drop(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                {
                    var svgPaths = Directory.EnumerateFiles(path, "*.svg", new EnumerationOptions {RecurseSubdirectories = true});
                    var svgzPaths = Directory.EnumerateFiles(path, "*.svgz", new EnumerationOptions {RecurseSubdirectories = true});
                    Drop(svgPaths);
                    Drop(svgzPaths);
                    continue;
                }

                var extension = Path.GetExtension(path);
                switch (extension.ToLower())
                {
                    case ".svg":
                    case ".svgz":
                        AddItem(path);
                        break;
                    case ".json":
                        LoadConfiguration(path);
                        break;
                }
            }
        }

        private void AddItem(string path)
        {
            if (_items is { })
            {
                var item = new FileItemViewModel(Path.GetFileName(path), path, x => _items.Remove(x));
                _items.Add(item);
            }
        }

        public void LoadConfiguration(string configurationPath)
        {
            if (!File.Exists(configurationPath))
            {
                return;
            }

            var json = File.ReadAllText(configurationPath);
            var configuration = JsonSerializer.Deserialize<Configuration>(json);

            if (configuration?.Paths is { })
            {
                SelectedItem = null;
                _items?.Clear();

                foreach (var path in configuration.Paths)
                {
                    AddItem(path);
                }
            }

            ItemQuery = configuration?.Query;
        }

        public void SaveConfiguration(string configurationPath)
        {
            var configuration = new Configuration()
            {
                Paths = _items?.Select(x => x.Path).ToList(),
                Query = ItemQuery
            };

            var json = JsonSerializer.Serialize(configuration);
            File.WriteAllText(configurationPath, json);
        }

        public void Export(string path, Avalonia.Svg.Skia.Svg? svg, string backgroundColor, float scaleX, float scaleY)
        {
            if (svg?.Model is null || svg?.Picture is null)
            {
                return;
            }

            if (!SkiaSharp.SKColor.TryParse(backgroundColor, out var skBackgroundColor))
            {
                return;
            }

            var extension = Path.GetExtension(path);
            switch (extension.ToLower())
            {
                case ".png":
                {
                    using var stream = File.OpenWrite(path);
                    svg.Picture?.ToImage(stream, skBackgroundColor, SkiaSharp.SKEncodedImageFormat.Png, 100, scaleX, scaleY, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul, SKSvgSettings.s_srgb);
                }
                    break;
                case ".jpg":
                case ".jpeg":
                {
                    using var stream = File.OpenWrite(path);
                    svg.Picture?.ToImage(stream, skBackgroundColor, SkiaSharp.SKEncodedImageFormat.Jpeg, 100, scaleX, scaleY, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul, SKSvgSettings.s_srgb);
                }
                    break;
                case ".pdf":
                {
                    svg.Picture?.ToPdf(path, skBackgroundColor, scaleX, scaleY);
                }
                    break;
                case ".xps":
                {
                    svg.Picture?.ToXps(path, skBackgroundColor, scaleX, scaleY);
                }
                    break;
                case ".cs":
                {
                    var code = SkiaCSharpCodeGen.Generate(svg.Model, "Svg", CreateClassName(path));
                    File.WriteAllText(path, code);
                }
                    break;
            }
        }

        private static string CreateClassName(string filename)
        {
            string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filename);
            string className = fileNameWithoutExtension.Replace("-", "_");
            return $"Svg_{className}";
        }
    }
}
