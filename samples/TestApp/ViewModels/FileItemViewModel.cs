using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using ReactiveUI;

namespace TestApp.ViewModels;

public partial class FileItemViewModel : ViewModelBase
{
    [Reactive]
    public partial string Name { get; set; }
    
    [Reactive]
    public partial string Path { get; set; }
    
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
