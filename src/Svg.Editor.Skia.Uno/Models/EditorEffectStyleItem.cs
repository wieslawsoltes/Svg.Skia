namespace Svg.Editor.Skia.Uno.Models;

public sealed class EditorEffectStyleItem
{
    public EditorEffectStyleItem(
        string styleId,
        string name,
        string? description,
        IEnumerable<EditorEffectItem> effects)
    {
        StyleId = styleId;
        Name = string.IsNullOrWhiteSpace(name) ? "Effect style" : name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Effects = effects.Select(CloneEffect).ToList();
    }

    public string StyleId { get; }

    public string Name { get; }

    public string Description { get; }

    public List<EditorEffectItem> Effects { get; }

    public string Summary
    {
        get
        {
            var active = Effects.Where(static item => item.IsEnabled).ToList();
            if (active.Count == 0)
            {
                return "No active effects";
            }

            return string.Join(" · ", active.Select(static item => item.DisplayName));
        }
    }

    public EditorEffectStyleItem Clone()
    {
        return new EditorEffectStyleItem(StyleId, Name, Description, Effects);
    }

    public static EditorEffectItem CloneEffect(EditorEffectItem item)
    {
        return new EditorEffectItem
        {
            Kind = item.Kind,
            IsEnabled = item.IsEnabled,
            OffsetX = item.OffsetX,
            OffsetY = item.OffsetY,
            Blur = item.Blur,
            Spread = item.Spread,
            Scale = item.Scale,
            Amount = item.Amount,
            Distortion = item.Distortion,
            Saturation = item.Saturation,
            Color = item.Color
        };
    }
}
