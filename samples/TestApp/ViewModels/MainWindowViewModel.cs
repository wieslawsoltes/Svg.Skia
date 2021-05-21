using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;

namespace TestApp.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private FileItemViewModel? _selectedItem;
        private IList<FileItemViewModel>? _items;

        public FileItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
        }

        public IList<FileItemViewModel>? Items
        {
            get => _items;
            set => this.RaiseAndSetIfChanged(ref _items, value);
        }

        public MainWindowViewModel()
        {
            Items = new ObservableCollection<FileItemViewModel>();
        }
    }
}
