using System.ComponentModel;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorAssetSectionItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public EditorAssetSectionItem(string name, int itemCount)
    {
        Name = name;
        ItemCount = itemCount;
    }

    public string Name { get; }

    public int ItemCount { get; }

    public string Label => ItemCount <= 0 ? Name : $"{Name} ({ItemCount})";

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

    public event PropertyChangedEventHandler? PropertyChanged;
}
