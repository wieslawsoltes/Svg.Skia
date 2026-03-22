namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorActionPaletteItem
{
    public required string Title { get; init; }

    public string Subtitle { get; init; } = string.Empty;

    public string ShortcutText { get; init; } = string.Empty;

    public string Keywords { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public bool IsEnabled { get; init; } = true;

    public bool IsToggle { get; init; }

    public bool IsChecked { get; init; }

    public bool IsSuggested { get; init; }

    public bool IsCommonSetting { get; init; }

    public FigmaIconKind IconKind { get; init; } = FigmaIconKind.Search;

    public EditorActionPaletteTab Tab { get; init; } = EditorActionPaletteTab.All;

    public EditorActionPaletteItemKind Kind { get; init; } = EditorActionPaletteItemKind.Command;

    public EditorMainMenuCommand? Command { get; init; }

    public EditorComponentItem? Component { get; init; }

    public EditorLibraryItem? Library { get; init; }

    public string SearchText =>
        $"{Title} {Subtitle} {Keywords} {ShortcutText} {Component?.SearchText ?? string.Empty} {Library?.SearchText ?? string.Empty}";

    public Visibility SubtitleVisibility => string.IsNullOrWhiteSpace(Subtitle) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ShortcutVisibility => string.IsNullOrWhiteSpace(ShortcutText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility IconVisibility => IsToggle ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ToggleVisibility => IsToggle ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CheckedIndicatorVisibility => IsToggle && IsChecked ? Visibility.Visible : Visibility.Collapsed;

    public bool Matches(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return SearchText.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public bool BelongsToTab(EditorActionPaletteTab selectedTab)
    {
        return selectedTab == EditorActionPaletteTab.All || Tab == selectedTab;
    }
}
