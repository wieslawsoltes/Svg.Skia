using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using TestApp.ViewModels;

namespace TestApp;

public class ViewLocator : IDataTemplate
{
    public IControl Build(object? data)
    {
        var name = data?.GetType().FullName?.Replace("ViewModel", "View");
        var type = name is null ? null : Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }
        else
        {
            return new TextBlock { Text = "Not Found: " + name };
        }
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
