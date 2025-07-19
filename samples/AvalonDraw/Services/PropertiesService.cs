using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Svg;

namespace AvalonDraw.Services;

public class PropertiesService
{
    public ObservableCollection<PropertyEntry> Properties { get; } = new();
    public ObservableCollection<PropertyEntry> FilteredProperties { get; } = new();
    public ObservableCollection<string> Ids { get; } = new();
    public ObservableCollection<AppearanceLayer> AppearanceLayers { get; } = new();
    private string _filter = string.Empty;

    public event Action<PropertyEntry>? EntryChanged;

    public void LoadProperties(SvgElement element)
    {
        foreach (var e in Properties)
            e.PropertyChanged -= OnEntryChanged;
        Properties.Clear();

        var props = element.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<SvgAttributeAttribute>() != null);
        foreach (var prop in props)
        {
            var converter = TypeDescriptor.GetConverter(prop.PropertyType);
            var value = prop.GetValue(element);
            string? str = null;
            if (value is not null)
            {
                try
                {
                    str = converter.ConvertToInvariantString(value);
                }
                catch
                {
                    str = value.ToString();
                }
            }
            var entry = new PropertyEntry(prop.GetCustomAttribute<SvgAttributeAttribute>()!.Name, prop, str);
            if (IsUriProperty(prop))
                entry.Suggestions = Ids;
            entry.PropertyChanged += OnEntryChanged;
            Properties.Add(entry);
        }

        if (element.TryGetAttribute("class", out var cls))
        {
            var entry = PropertyEntry.CreateAttribute("class", cls, (target, value) =>
            {
                if (target is SvgElement el)
                    el.CustomAttributes["class"] = value ?? string.Empty;
            });
            entry.PropertyChanged += OnEntryChanged;
            Properties.Add(entry);
        }

        if (element.TryGetAttribute("style", out var style))
        {
            var entry = PropertyEntry.CreateAttribute("style", style, (target, value) =>
            {
                if (target is SvgElement el)
                    el.CustomAttributes["style"] = value ?? string.Empty;
            });
            entry.PropertyChanged += OnEntryChanged;
            Properties.Add(entry);
        }

        if (element is SvgTextBase txt)
        {
            var prop = element.GetType().GetProperty(nameof(SvgTextBase.Text));
            if (prop is { })
            {
                var entry = new PropertyEntry("Text", prop, txt.Text);
                entry.PropertyChanged += OnEntryChanged;
                Properties.Add(entry);
            }
            AddTypographicAttribute(element, "tracking");
            AddTypographicAttribute(element, "kerning");
            AddTypographicAttribute(element, "font-feature-settings");
        }

        if (element is SvgLinearGradientServer linGrad)
        {
            var gEntry = new GradientStopsEntry(linGrad);
            gEntry.PropertyChanged += OnEntryChanged;
            Properties.Add(gEntry);
        }
        else if (element is SvgRadialGradientServer radGrad)
        {
            var gEntry = new GradientStopsEntry(radGrad);
            gEntry.PropertyChanged += OnEntryChanged;
            Properties.Add(gEntry);
        }

        if (element is SvgVisualElement vis &&
            vis.CustomAttributes.TryGetValue("stroke-profile", out var prof))
        {
            var sEntry = new StrokeProfileEntry(prof);
            sEntry.PropertyChanged += OnEntryChanged;
            Properties.Add(sEntry);
        }

        LoadAppearanceLayers(element);

        ApplyFilter(_filter);
    }

    private void LoadAppearanceLayers(SvgElement element)
    {
        AppearanceLayers.Clear();
        if (element is SvgVisualElement ve)
        {
            var converter = TypeDescriptor.GetConverter(typeof(SvgPaintServer));
            var layer = new AppearanceLayer();
            if (ve.Fill is { })
            {
                try
                {
                    layer.Fill = converter.ConvertToInvariantString(ve.Fill);
                }
                catch
                {
                    layer.Fill = ve.Fill.ToString();
                }
            }
            if (ve.Stroke is { })
            {
                try
                {
                    layer.Stroke = converter.ConvertToInvariantString(ve.Stroke);
                }
                catch
                {
                    layer.Stroke = ve.Stroke.ToString();
                }
            }
            layer.StrokeWidth = ve.StrokeWidth.Value;
            layer.Effect = AppearanceEffect.None;
            AppearanceLayers.Add(layer);
        }
    }

    public void ApplyFilter(string filter)
    {
        _filter = filter;
        FilteredProperties.Clear();
        foreach (var entry in Properties)
        {
            if (string.IsNullOrEmpty(_filter) || entry.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                FilteredProperties.Add(entry);
        }
    }

    public void UpdateIdList(SvgDocument? document)
    {
        Ids.Clear();
        if (document is null)
            return;
        foreach (var el in document.Descendants().OfType<SvgElement>())
            if (!string.IsNullOrEmpty(el.ID))
                Ids.Add($"url(#{el.ID})");
    }

    public void ApplyAll(SvgElement element)
    {
        foreach (var entry in Properties)
            entry.Apply(element);

        ApplyAppearanceLayers(element);
    }

    private void ApplyAppearanceLayers(SvgElement element)
    {
        if (AppearanceLayers.Count == 0 || element is not SvgVisualElement ve)
            return;

        var layer = AppearanceLayers[0];
        var converter = TypeDescriptor.GetConverter(typeof(SvgPaintServer));
        if (layer.Fill is { })
        {
            try
            {
                ve.Fill = (SvgPaintServer?)converter.ConvertFromInvariantString(layer.Fill);
            }
            catch
            {
            }
        }
        if (layer.Stroke is { })
        {
            try
            {
                ve.Stroke = (SvgPaintServer?)converter.ConvertFromInvariantString(layer.Stroke);
            }
            catch
            {
            }
        }
        ve.StrokeWidth = layer.StrokeWidth;
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is PropertyEntry entry)
            EntryChanged?.Invoke(entry);
    }

    private static bool IsUriProperty(PropertyInfo prop)
    {
        if (prop.PropertyType == typeof(Uri) || typeof(SvgPaintServer).IsAssignableFrom(prop.PropertyType))
            return true;
        var tc = prop.GetCustomAttribute<TypeConverterAttribute>();
        if (tc != null && tc.ConverterTypeName == typeof(UriTypeConverter).FullName)
            return true;
        if (prop.Name.Contains("Href", StringComparison.OrdinalIgnoreCase))
            return true;
        var svgAttr = prop.GetCustomAttribute<SvgAttributeAttribute>();
        if (svgAttr != null && svgAttr.Name.Contains("href", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private void AddTypographicAttribute(SvgElement element, string name)
    {
        if (Properties.Any(p => p.Name == name))
            return;
        element.TryGetAttribute(name, out var value);
        var entry = PropertyEntry.CreateAttribute(name, value, (target, val) =>
        {
            if (target is SvgElement el)
                el.CustomAttributes[name] = val ?? string.Empty;
        });
        entry.PropertyChanged += OnEntryChanged;
        Properties.Add(entry);
    }
}
