using Svg;

namespace Svg.Editor.Skia.Uno.Models;

public sealed class SampleLibraryDefinition
{
    public SampleLibraryDefinition(
        EditorLibraryItem item,
        IEnumerable<SampleLibraryAssetDefinition> assets,
        IEnumerable<ColorSwatchItem> swatches,
        IEnumerable<EditorTextStyleItem>? textStyles = null,
        IEnumerable<EditorEffectStyleItem>? effectStyles = null,
        IEnumerable<EditorLayoutGuideStyleItem>? layoutGuideStyles = null)
    {
        Item = item;
        Assets = assets.ToList();
        Swatches = swatches.ToList();
        TextStyles = textStyles?.ToList() ?? [];
        EffectStyles = effectStyles?.ToList() ?? [];
        LayoutGuideStyles = layoutGuideStyles?.ToList() ?? [];
    }

    public EditorLibraryItem Item { get; }

    public List<SampleLibraryAssetDefinition> Assets { get; }

    public List<ColorSwatchItem> Swatches { get; }

    public List<EditorTextStyleItem> TextStyles { get; }

    public List<EditorEffectStyleItem> EffectStyles { get; }

    public List<EditorLayoutGuideStyleItem> LayoutGuideStyles { get; }
}
