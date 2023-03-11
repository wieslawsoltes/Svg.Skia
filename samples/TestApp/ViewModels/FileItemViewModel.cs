using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer", _path);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", _path);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", _path); 
            }
        });

        OpenInNotepadCommand = ReactiveCommand.Create(() =>
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("notepad", _path);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", _path);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", "-t " + _path); 
            }
        });

        RemoveCommand = ReactiveCommand.Create(() =>
        {
            remove(this);
        });
    }
}
