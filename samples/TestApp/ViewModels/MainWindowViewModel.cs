using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using Svg.Skia;
using TestApp.Models;
using TestApp.Services;

namespace TestApp.ViewModels;

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

    public ICommand ExportCommand { get; }

    private List<FilePickerFileType> GetConfigurationFileTypes()
    {
        return new List<FilePickerFileType>
        {
            StorageService.Json,
            StorageService.All
        };
    }

    private static List<FilePickerFileType> GetExportFileTypes()
    {
        return new List<FilePickerFileType>
        {
            StorageService.ImagePng,
            StorageService.ImageJpg,
            StorageService.CSharp,
            StorageService.Pdf,
            StorageService.Xps,
            StorageService.All
        };
    }

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

        ResetQueryCommand = ReactiveCommand.Create(() => ItemQuery = "", resetQueryCanExecute);

        LoadConfigurationCommand = ReactiveCommand.CreateFromTask(async () => await LoadConfigurationExecute());

        SaveConfigurationCommand = ReactiveCommand.CreateFromTask(async () => await SaveConfigurationExecute());

        ClearConfigurationCommand = ReactiveCommand.Create(ClearConfigurationExecute);

        AddItemCommand = ReactiveCommand.CreateFromTask(async () => await AddItemExecute());

        ExportCommand = ReactiveCommand.CreateFromTask<Avalonia.Svg.Skia.Svg>(async svg => await ExportExecute(svg));
    }

    private async Task ExportExecute(Avalonia.Svg.Skia.Svg svg)
    {
        if (_selectedItem is null || svg.Model is null)
        {
            return;
        }

        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export svg",
            FileTypeChoices = GetExportFileTypes(),
            SuggestedFileName = Path.GetFileNameWithoutExtension(_selectedItem.Path),
            DefaultExtension = "png",
            ShowOverwritePrompt = true
        });

        if (file is not null)
        {
            try
            {
                await using var stream = await file.OpenWriteAsync();
                Export(stream, file.Name, svg, "#00FFFFFF", 1f, 1f);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }
    }

    private async Task AddItemExecute()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is null)
        {
            return;
        }

        var dlg = new OpenFileDialog
        {
            AllowMultiple = true,
            Filters = new List<FileDialogFilter>
            {
                new() {Name = "Svg Files (*.svg;*.svgz)", Extensions = new List<string> {"svg", "svgz"}},
                new() {Name = "All Files (*.*)", Extensions = new List<string> {"*"}}
            }
        };
        var result = await dlg.ShowAsync(window);
        if (result is { })
        {
            var paths = result.ToList();
            foreach (var path in paths)
            {
                AddItem(path);
            }
        }
    }

    private void ClearConfigurationExecute()
    {
        ItemQuery = null;
        SelectedItem = null;
        _items?.Clear();
    }

    private async Task SaveConfigurationExecute()
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save configuration",
            FileTypeChoices = GetConfigurationFileTypes(),
            SuggestedFileName = Path.GetFileNameWithoutExtension("TestApp.json"),
            DefaultExtension = "json",
            ShowOverwritePrompt = true
        });

        if (file is not null)
        {
            try
            {
                await using var stream = await file.OpenWriteAsync();
                SaveConfiguration(stream);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }
    }

    private async Task LoadConfigurationExecute()
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open configuration",
            FileTypeFilter = GetConfigurationFileTypes(),
            AllowMultiple = false
        });

        var file = result.FirstOrDefault();

        if (file is not null)
        {
            try
            {
                await using var stream = await file.OpenReadAsync();
                LoadConfiguration(stream);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
            }
        }
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
                {
                    AddItem(path);
                    break;
                }
                case ".json":
                {
                    using var stream = File.OpenRead(path);
                    LoadConfiguration(stream);
                    break;
                }
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

    public void LoadConfiguration(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
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

    public void SaveConfiguration(Stream stream)
    {
        var configuration = new Configuration
        {
            Paths = _items?.Select(x => x.Path).ToList(),
            Query = ItemQuery
        };

        var json = JsonSerializer.Serialize(configuration);
        using var writer = new StreamWriter(stream); 
        writer.Write(json);
    }

    public void Export(Stream stream, string name, Avalonia.Svg.Skia.Svg? svg, string backgroundColor, float scaleX, float scaleY)
    {
        if (svg?.Model is null || svg.Picture is null)
        {
            return;
        }

        if (!SkiaSharp.SKColor.TryParse(backgroundColor, out var skBackgroundColor))
        {
            return;
        }

        var extension = Path.GetExtension(name);
        switch (extension.ToLower())
        {
            case ".png":
            {
                svg.Picture?.ToImage(
                    stream, 
                    skBackgroundColor, 
                    SkiaSharp.SKEncodedImageFormat.Png, 
                    100, 
                    scaleX, 
                    scaleY, 
                    SkiaSharp.SKColorType.Rgba8888, 
                    SkiaSharp.SKAlphaType.Premul, 
                    SkiaSharp.SKColorSpace.CreateSrgb());
                break;
            }
            case ".jpg":
            case ".jpeg":
            {
                svg.Picture?.ToImage(
                    stream, 
                    skBackgroundColor, 
                    SkiaSharp.SKEncodedImageFormat.Jpeg, 
                    100, 
                    scaleX, 
                    scaleY, 
                    SkiaSharp.SKColorType.Rgba8888, 
                    SkiaSharp.SKAlphaType.Premul, 
                    SkiaSharp.SKColorSpace.CreateSrgb());
                break;
            }
            case ".pdf":
            {
                svg.Picture?.ToPdf(stream, skBackgroundColor, scaleX, scaleY);
                break;
            }
            case ".xps":
            {
                svg.Picture?.ToXps(stream, skBackgroundColor, scaleX, scaleY);
                break;
            }
        }
    }
}
