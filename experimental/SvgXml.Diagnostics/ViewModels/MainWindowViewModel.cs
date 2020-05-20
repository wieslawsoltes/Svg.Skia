using System.Collections.ObjectModel;
using SvgXml.Diagnostics.Models;

namespace SvgXml.Diagnostics.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<Item> Items { get; set; }

        public MainWindowViewModel()
        {
            Items = new ObservableCollection<Item>();
        }
    }
}
