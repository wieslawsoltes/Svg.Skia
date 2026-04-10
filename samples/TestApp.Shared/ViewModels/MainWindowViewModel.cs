using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using SkiaSharp;
using TestApp.Models;
using TestApp.Services;

namespace TestApp.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ITestAppStorageService _storageService;
    private readonly ObservableCollection<FileItemViewModel> _items = new();
    private readonly ObservableCollection<FileItemViewModel> _filteredItems = new();
    private readonly ReadOnlyObservableCollection<FileItemViewModel> _readOnlyFilteredItems;
    private readonly RelayCommand _resetQueryCommand;
    private readonly AsyncRelayCommand _loadConfigurationCommand;
    private readonly AsyncRelayCommand _saveConfigurationCommand;
    private readonly RelayCommand _clearConfigurationCommand;
    private readonly AsyncRelayCommand _addItemCommand;
    private FileItemViewModel? _selectedItem;
    private string? _itemQuery;

    public MainWindowViewModel()
        : this(NullTestAppStorageService.Instance)
    {
    }

    public MainWindowViewModel(ITestAppStorageService storageService)
    {
        _storageService = storageService;
        _readOnlyFilteredItems = new ReadOnlyObservableCollection<FileItemViewModel>(_filteredItems);
        SvgView = new SvgInteractionViewModel();

        _resetQueryCommand = new RelayCommand(() => ItemQuery = string.Empty, () => !string.IsNullOrWhiteSpace(ItemQuery));
        _loadConfigurationCommand = new AsyncRelayCommand(LoadConfigurationExecuteAsync);
        _saveConfigurationCommand = new AsyncRelayCommand(SaveConfigurationExecuteAsync, () => _items.Count > 0);
        _clearConfigurationCommand = new RelayCommand(ClearConfiguration);
        _addItemCommand = new AsyncRelayCommand(AddItemExecuteAsync);

        RefreshFilteredItems();
    }

    public FileItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public string? ItemQuery
    {
        get => _itemQuery;
        set
        {
            if (SetProperty(ref _itemQuery, value))
            {
                RefreshFilteredItems();
                _resetQueryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ReadOnlyObservableCollection<FileItemViewModel> FilteredItems => _readOnlyFilteredItems;

    public SvgInteractionViewModel SvgView { get; }

    public ICommand ResetQueryCommand => _resetQueryCommand;

    public ICommand LoadConfigurationCommand => _loadConfigurationCommand;

    public ICommand SaveConfigurationCommand => _saveConfigurationCommand;

    public ICommand ClearConfigurationCommand => _clearConfigurationCommand;

    public ICommand AddItemCommand => _addItemCommand;

    public void Drop(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                continue;
            }

            if (Directory.Exists(path))
            {
                Drop(Directory.EnumerateFiles(path, "*.svg", new EnumerationOptions { RecurseSubdirectories = true }));
                Drop(Directory.EnumerateFiles(path, "*.svgz", new EnumerationOptions { RecurseSubdirectories = true }));
                continue;
            }

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".svg":
                case ".svgz":
                    AddItem(path);
                    break;
                case ".json":
                    using (var stream = File.OpenRead(path))
                    {
                        LoadConfiguration(stream);
                    }
                    break;
            }
        }
    }

    public void LoadConfiguration(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var configuration = JsonSerializer.Deserialize<Configuration>(json);

        SelectedItem = null;
        _items.Clear();

        if (configuration?.Paths is not null)
        {
            foreach (var path in configuration.Paths)
            {
                AddItem(path);
            }
        }

        ItemQuery = configuration?.Query;
        SvgView.NotifySelectionChanged();
        RefreshFilteredItems();
    }

    public void SaveConfiguration(Stream stream)
    {
        var configuration = new Configuration
        {
            Paths = _items.Select(x => x.Path).ToList(),
            Query = ItemQuery
        };

        using var writer = new StreamWriter(stream);
        writer.Write(JsonSerializer.Serialize(configuration));
    }

    public async Task ExportAsync(SKPicture? picture)
    {
        if (SelectedItem is null || picture is null)
        {
            return;
        }

        var exportTarget = await _storageService.OpenExportWriteStreamAsync(Path.GetFileNameWithoutExtension(SelectedItem.Path));
        if (exportTarget is null)
        {
            return;
        }

        await using var stream = exportTarget.Stream;
        await TestAppExportService.ExportAsync(stream, exportTarget.Name, picture);
    }

    private async Task LoadConfigurationExecuteAsync()
    {
        await using var stream = await _storageService.OpenConfigurationReadStreamAsync();
        if (stream is not null)
        {
            LoadConfiguration(stream);
        }
    }

    private async Task SaveConfigurationExecuteAsync()
    {
        await using var stream = await _storageService.OpenConfigurationWriteStreamAsync("TestApp.json");
        if (stream is not null)
        {
            SaveConfiguration(stream);
        }
    }

    private async Task AddItemExecuteAsync()
    {
        var paths = await _storageService.PickSvgPathsAsync();
        foreach (var path in paths)
        {
            AddItem(path);
        }
    }

    private void ClearConfiguration()
    {
        ItemQuery = null;
        SelectedItem = null;
        _items.Clear();
        RefreshFilteredItems();
        SvgView.NotifySelectionChanged();
        _saveConfigurationCommand.NotifyCanExecuteChanged();
    }

    private void AddItem(string path)
    {
        if (_items.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var item = new FileItemViewModel(Path.GetFileName(path), path, RemoveItem);
        _items.Add(item);
        RefreshFilteredItems();
        _saveConfigurationCommand.NotifyCanExecuteChanged();
    }

    private void RemoveItem(FileItemViewModel item)
    {
        if (SelectedItem == item)
        {
            SelectedItem = null;
        }

        _items.Remove(item);
        RefreshFilteredItems();
        _saveConfigurationCommand.NotifyCanExecuteChanged();
    }

    private void RefreshFilteredItems()
    {
        var filtered = string.IsNullOrWhiteSpace(ItemQuery)
            ? _items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            : _items.Where(x => x.Name.Contains(ItemQuery, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);

        _filteredItems.Clear();
        foreach (var item in filtered)
        {
            _filteredItems.Add(item);
        }
    }
}
