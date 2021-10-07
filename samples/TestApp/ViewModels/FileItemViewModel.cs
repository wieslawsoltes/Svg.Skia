using System;
using System.Diagnostics;
using System.Windows.Input;
using ReactiveUI;

namespace TestApp.ViewModels;

public class FileItemViewModel : ViewModelBase
{
    private string _name;
    private string _path;

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Path
    {
        get => _path;
        set => this.RaiseAndSetIfChanged(ref _path, value);
    }

    public ICommand RemoveCommand { get; }

    public ICommand OpenInExplorerCommand { get; }

    public ICommand OpenInNotepadCommand { get; }

    public FileItemViewModel(string name, string path, Action<FileItemViewModel> remove)
    {
        _name = name;
        _path = path;

        OpenInExplorerCommand = ReactiveCommand.Create(() =>
        {
            Process.Start("explorer", _path);
        });

        OpenInNotepadCommand = ReactiveCommand.Create(() =>
        {
            Process.Start("notepad", _path);
        });

        RemoveCommand = ReactiveCommand.Create(() =>
        {
            remove(this);
        });
    }
}