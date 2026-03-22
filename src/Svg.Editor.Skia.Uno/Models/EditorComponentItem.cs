using System.ComponentModel;
using Microsoft.UI.Xaml;
using Svg;
using Svg.Editor.Svg.Models;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorComponentItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _sourceName = "Local";
    private string _sourceSubtitle = "Main component";
    private bool _isUpdateAvailable;
    private string _assetKey = string.Empty;
    private string _libraryId = string.Empty;
    private string _documentSymbolId = string.Empty;
    private string _sectionName = "Components";
    private string _searchKeywords = string.Empty;

    public EditorComponentItem(SymbolEntry symbolEntry)
    {
        SymbolEntry = symbolEntry;
        _documentSymbolId = symbolEntry.Symbol.ID ?? string.Empty;
        _assetKey = _documentSymbolId;
    }

    public SymbolEntry SymbolEntry { get; }

    public SvgSymbol Symbol => SymbolEntry.Symbol;

    public string Name => SymbolEntry.Name;

    public string Subtitle => _sourceSubtitle;

    public string AssetKey
    {
        get => _assetKey;
        set
        {
            if (_assetKey == value)
            {
                return;
            }

            _assetKey = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AssetKey)));
        }
    }

    public string LibraryId
    {
        get => _libraryId;
        set
        {
            if (_libraryId == value)
            {
                return;
            }

            _libraryId = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LibraryId)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLibraryAsset)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
        }
    }

    public string DocumentSymbolId
    {
        get => _documentSymbolId;
        set
        {
            if (_documentSymbolId == value)
            {
                return;
            }

            _documentSymbolId = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DocumentSymbolId)));
        }
    }

    public string SectionName
    {
        get => _sectionName;
        set
        {
            if (_sectionName == value)
            {
                return;
            }

            _sectionName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SectionName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
        }
    }

    public string SearchKeywords
    {
        get => _searchKeywords;
        set
        {
            if (_searchKeywords == value)
            {
                return;
            }

            _searchKeywords = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchKeywords)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
        }
    }

    public bool IsLibraryAsset => !string.IsNullOrWhiteSpace(LibraryId);

    public string SearchText => $"{Name} {SourceName} {SourceSubtitle} {SectionName} {SearchKeywords}";

    public string SourceName
    {
        get => _sourceName;
        set
        {
            if (_sourceName == value)
            {
                return;
            }

            _sourceName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
        }
    }

    public string SourceSubtitle
    {
        get => _sourceSubtitle;
        set
        {
            if (_sourceSubtitle == value)
            {
                return;
            }

            _sourceSubtitle = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceSubtitle)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
        }
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set
        {
            if (_isUpdateAvailable == value)
            {
                return;
            }

            _isUpdateAvailable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAvailable)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateVisibility)));
        }
    }

    public Visibility UpdateVisibility => _isUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;

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
