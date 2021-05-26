using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

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
    }
}
