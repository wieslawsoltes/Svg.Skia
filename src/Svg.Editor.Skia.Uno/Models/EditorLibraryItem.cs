using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorLibraryItem : INotifyPropertyChanged
{
    private string _name;
    private string _publisher;
    private string _description;
    private string _previewLabel;
    private bool _isCurrentFile;
    private bool _isPublished;
    private bool _isEnabled;
    private bool _hasUpdate;
    private bool _isMissing;
    private int _installedVersion;
    private int _availableVersion;
    private int _componentCount;
    private int _colorCount;
    private Color _previewPrimaryColor;
    private Color _previewSecondaryColor;
    private Color _previewAccentColor;

    public EditorLibraryItem(string id, string name, string publisher, EditorLibraryCategory category)
    {
        Id = id;
        _name = name;
        _publisher = publisher;
        Category = category;
        _description = string.Empty;
        _previewLabel = name;
        _installedVersion = 1;
        _availableVersion = 1;
        _previewPrimaryColor = Color.FromArgb(255, 17, 24, 39);
        _previewSecondaryColor = Color.FromArgb(255, 247, 247, 245);
        _previewAccentColor = Color.FromArgb(255, 13, 153, 255);
    }

    public string Id { get; }

    public EditorLibraryCategory Category { get; }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value, nameof(Name), nameof(SearchText), nameof(PreviewLabel));
    }

    public string Publisher
    {
        get => _publisher;
        set => SetField(ref _publisher, value, nameof(Publisher), nameof(SearchText));
    }

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value, nameof(Description), nameof(SearchText), nameof(StatusLabel));
    }

    public string PreviewLabel
    {
        get => _previewLabel;
        set => SetField(ref _previewLabel, value, nameof(PreviewLabel));
    }

    public bool IsCurrentFile
    {
        get => _isCurrentFile;
        set => SetField(ref _isCurrentFile, value, nameof(IsCurrentFile), nameof(StatusLabel), nameof(ActionLabel), nameof(AssetsVisibility));
    }

    public bool IsPublished
    {
        get => _isPublished;
        set => SetField(ref _isPublished, value, nameof(IsPublished), nameof(StatusLabel), nameof(ActionLabel));
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value, nameof(IsEnabled), nameof(StatusLabel), nameof(ActionLabel), nameof(AssetsVisibility));
    }

    public bool HasUpdate
    {
        get => _hasUpdate;
        set => SetField(ref _hasUpdate, value, nameof(HasUpdate), nameof(StatusLabel), nameof(ActionLabel), nameof(UpdateVisibility));
    }

    public bool IsMissing
    {
        get => _isMissing;
        set => SetField(ref _isMissing, value, nameof(IsMissing), nameof(StatusLabel));
    }

    public int InstalledVersion
    {
        get => _installedVersion;
        set => SetField(ref _installedVersion, value, nameof(InstalledVersion), nameof(StatusLabel), nameof(ActionLabel));
    }

    public int AvailableVersion
    {
        get => _availableVersion;
        set => SetField(ref _availableVersion, value, nameof(AvailableVersion), nameof(StatusLabel), nameof(ActionLabel));
    }

    public int ComponentCount
    {
        get => _componentCount;
        set => SetField(ref _componentCount, value, nameof(ComponentCount), nameof(StatusLabel));
    }

    public int ColorCount
    {
        get => _colorCount;
        set => SetField(ref _colorCount, value, nameof(ColorCount), nameof(StatusLabel));
    }

    public Color PreviewPrimaryColor
    {
        get => _previewPrimaryColor;
        set => SetField(ref _previewPrimaryColor, value, nameof(PreviewPrimaryColor));
    }

    public Color PreviewSecondaryColor
    {
        get => _previewSecondaryColor;
        set => SetField(ref _previewSecondaryColor, value, nameof(PreviewSecondaryColor));
    }

    public Color PreviewAccentColor
    {
        get => _previewAccentColor;
        set => SetField(ref _previewAccentColor, value, nameof(PreviewAccentColor));
    }

    public string ActionLabel
    {
        get
        {
            if (IsCurrentFile)
            {
                return IsPublished ? "Republish" : "Publish";
            }

            return IsEnabled ? "Remove" : "Add";
        }
    }

    public string StatusLabel
    {
        get
        {
            if (IsCurrentFile)
            {
                if (IsPublished)
                {
                    var scope = ComponentCount == 1 ? "1 component" : $"{ComponentCount} components";
                    var colors = ColorCount == 1 ? "1 paint style" : $"{ColorCount} paint styles";
                    return $"Published version {AvailableVersion} · {scope} · {colors}";
                }

                return "Publish local components and paint styles as a reusable library.";
            }

            if (IsMissing)
            {
                return $"Missing from this file · installed v{InstalledVersion}";
            }

            if (HasUpdate && IsEnabled)
            {
                return $"Update available · v{InstalledVersion} -> v{AvailableVersion}";
            }

            if (IsEnabled)
            {
                return $"Added to this file · v{InstalledVersion}";
            }

            return Description;
        }
    }

    public string SearchText => $"{Name} {Publisher} {Description} {StatusLabel}";

    public string ComponentsLabel => ComponentCount == 1 ? "1 component" : $"{ComponentCount} components";

    public string ColorsLabel => ColorCount == 1 ? "1 paint style" : $"{ColorCount} paint styles";

    public string AssetsCountLabel => ColorCount > 0 ? $"{ComponentsLabel} · {ColorsLabel}" : ComponentsLabel;

    public Visibility AssetsVisibility => !IsCurrentFile && (IsEnabled || IsMissing)
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility UpdateVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;

    public FigmaIconKind CategoryIconKind => Category == EditorLibraryCategory.Team
        ? FigmaIconKind.Team
        : FigmaIconKind.Library;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, params string[] propertyNames)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        foreach (var propertyName in propertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
