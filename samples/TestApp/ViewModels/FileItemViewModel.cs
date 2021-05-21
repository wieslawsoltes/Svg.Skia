using ReactiveUI;

namespace TestApp.ViewModels
{
    public class FileItemViewModel : ViewModelBase
    {
        private string? _name;
        private string? _path;

        public FileItemViewModel(string? name, string? path)
        {
            _name = name;
            _path = path;
        }

        public string? Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string? Path
        {
            get => _path;
            set => this.RaiseAndSetIfChanged(ref _path, value);
        }
    }
}
