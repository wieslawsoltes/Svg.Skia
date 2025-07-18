using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Svg;

namespace AvalonDraw.Services;

public class AppearanceService
{
    public class StyleEntry
    {
        public string Name { get; set; } = string.Empty;
        public string? Fill { get; set; }
        public string? Stroke { get; set; }
        public float Opacity { get; set; } = 1f;

        public override string ToString() => Name;

        public static StyleEntry FromElement(string name, SvgVisualElement element)
        {
            var converter = TypeDescriptor.GetConverter(typeof(SvgPaintServer));
            var entry = new StyleEntry { Name = name, Opacity = element.Opacity };
            if (element.Fill is { })
            {
                try
                {
                    entry.Fill = converter.ConvertToInvariantString(element.Fill);
                }
                catch
                {
                    entry.Fill = element.Fill.ToString();
                }
            }
            if (element.Stroke is { })
            {
                try
                {
                    entry.Stroke = converter.ConvertToInvariantString(element.Stroke);
                }
                catch
                {
                    entry.Stroke = element.Stroke.ToString();
                }
            }
            return entry;
        }

        public void Apply(SvgVisualElement element)
        {
            var converter = TypeDescriptor.GetConverter(typeof(SvgPaintServer));
            if (Fill is { })
            {
                try
                {
                    element.Fill = (SvgPaintServer?)converter.ConvertFromInvariantString(Fill);
                }
                catch
                {
                }
            }
            if (Stroke is { })
            {
                try
                {
                    element.Stroke = (SvgPaintServer?)converter.ConvertFromInvariantString(Stroke);
                }
                catch
                {
                }
            }
            element.Opacity = Opacity;
        }
    }

    public ObservableCollection<StyleEntry> Styles { get; } = new();

    private const string Prefix = "data-style-";

    public void Load(SvgDocument? document)
    {
        Styles.Clear();
        if (document is null)
            return;
        foreach (var pair in document.CustomAttributes)
        {
            if (pair.Key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                var name = pair.Key.Substring(Prefix.Length);
                var entry = ParseStyle(name, pair.Value);
                if (entry is { })
                    Styles.Add(entry);
            }
        }
    }

    private static StyleEntry? ParseStyle(string name, string data)
    {
        var entry = new StyleEntry { Name = name, Opacity = 1f };
        foreach (var part in data.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var items = part.Split(':');
            if (items.Length != 2)
                continue;
            var key = items[0];
            var val = items[1];
            switch (key)
            {
                case "fill":
                    entry.Fill = val;
                    break;
                case "stroke":
                    entry.Stroke = val;
                    break;
                case "opacity":
                    if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var o))
                        entry.Opacity = o;
                    break;
            }
        }
        return entry;
    }

    private static string SerializeStyle(StyleEntry style)
    {
        var parts = new List<string>();
        if (style.Fill is { })
            parts.Add($"fill:{style.Fill}");
        if (style.Stroke is { })
            parts.Add($"stroke:{style.Stroke}");
        parts.Add($"opacity:{style.Opacity.ToString(CultureInfo.InvariantCulture)}");
        return string.Join(';', parts);
    }

    public void AddOrUpdateStyle(SvgDocument document, StyleEntry style)
    {
        var data = SerializeStyle(style);
        document.CustomAttributes[Prefix + style.Name] = data;
        var existing = Styles.FirstOrDefault(s => s.Name == style.Name);
        if (existing is { })
        {
            existing.Fill = style.Fill;
            existing.Stroke = style.Stroke;
            existing.Opacity = style.Opacity;
        }
        else
        {
            Styles.Add(style);
        }
    }

    public void RemoveStyle(SvgDocument document, StyleEntry style)
    {
        document.CustomAttributes.Remove(Prefix + style.Name);
        Styles.Remove(style);
    }
}
