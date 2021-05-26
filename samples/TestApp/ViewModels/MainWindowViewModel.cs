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
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using TestApp.Models;

namespace TestApp.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private FileItemViewModel? _selectedItem;
        private ObservableCollection<FileItemViewModel>? _items;
        private string? _itemQuery;
        private ReadOnlyObservableCollection<FileItemViewModel>? _filteredItems;

        public FileItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
        }

        public ObservableCollection<FileItemViewModel>? Items
        {
            get => _items;
            set => this.RaiseAndSetIfChanged(ref _items, value);
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
                Items?.Clear();
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
                var extension = Path.GetExtension(path);
                switch (extension.ToLower())
                {
                    case ".svg":
                    case ".svgz":
                        Items?.Add(new FileItemViewModel(Path.GetFileName(path), path));
                        break;
                    case ".json":
                        LoadConfiguration(path);
                        break;
                }
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
                Items?.Clear();

                foreach (var path in configuration.Paths)
                {
                    Items?.Add(new FileItemViewModel(Path.GetFileName(path), path));
                }
            }

            if (configuration?.Query is { })
            {
                ItemQuery = configuration.Query;
            }
            else
            {
                ItemQuery = null;
            }
        }

        public void SaveConfiguration(string configurationPath)
        {
            var configuration = new Configuration()
            {
                Paths = Items?.Select(x => x.Path).ToList(),
                Query = ItemQuery
            };

            var json = JsonSerializer.Serialize<Configuration>(configuration);
            File.WriteAllText(configurationPath, json);
        }

    }
}
