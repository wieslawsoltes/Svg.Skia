using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorDevCodeSnippet : INotifyPropertyChanged
{
    private bool _isSelected;

    public EditorDevCodeSnippet(string id, string title, string language, string content, string summary = "")
    {
        Id = id;
        Title = title;
        Language = language;
        Content = content;
        Summary = summary;
    }

    public string Id { get; }

    public string Title { get; }

    public string Language { get; }

    public string Content { get; }

    public string Summary { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public Visibility SummaryVisibility => string.IsNullOrWhiteSpace(Summary)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public event PropertyChangedEventHandler? PropertyChanged;
}
