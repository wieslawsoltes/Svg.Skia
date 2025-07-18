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
        }

        if (element is SvgGradientServer grad)
        {
            var gEntry = new GradientStopsEntry(grad);
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

        ApplyFilter(_filter);
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
}
