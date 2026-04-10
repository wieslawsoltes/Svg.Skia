using System;
using System.Windows.Input;
using TestApp.Services;

namespace TestApp.ViewModels;

public sealed class FileItemViewModel : ViewModelBase
{
    private string _name;
    private string _path;

    public FileItemViewModel(string name, string path, Action<FileItemViewModel> remove)
    {
        _name = name;
        _path = path;
        RemoveCommand = new RelayCommand(() => remove(this));
        OpenInExplorerCommand = new RelayCommand(() => PathLauncher.OpenInExplorer(_path));
        OpenInNotepadCommand = new RelayCommand(() => PathLauncher.OpenInTextEditor(_path));
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public ICommand RemoveCommand { get; }

    public ICommand OpenInExplorerCommand { get; }

    public ICommand OpenInNotepadCommand { get; }
}
