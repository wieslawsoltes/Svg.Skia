using System;
using System.Collections.ObjectModel;
using System.Linq;
using Svg;
using Svg.Model.Drawables;

namespace AvalonDraw.Services;

public class LayerService
{
    public class LayerEntry
    {
        public SvgGroup Group { get; }
        private string _name;
        private bool _locked;
        private bool _visible;
        public ObservableCollection<LayerEntry> Sublayers { get; } = new();
        public DrawableBase? Drawable { get; set; }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                Group.CustomAttributes["data-name"] = value;
            }
        }

        public bool Locked
        {
            get => _locked;
            set
            {
                _locked = value;
                Group.CustomAttributes["data-lock"] = value ? "true" : "false";
            }
        }

        public bool Visible
        {
            get => _visible;
            set
            {
                _visible = value;
                Group.CustomAttributes["data-visible"] = value ? "true" : "false";
                Group.Visibility = value ? "visible" : "hidden";
                Group.Display = value ? "inline" : "none";
            }
        }

        public LayerEntry(SvgGroup group, string name)
        {
            Group = group;
            _name = name;
            group.CustomAttributes.TryGetValue("data-lock", out var locked);
            _locked = string.Equals(locked, "true", StringComparison.OrdinalIgnoreCase);
            group.CustomAttributes.TryGetValue("data-visible", out var vis);
            _visible = vis is null || string.Equals(vis, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    public ObservableCollection<LayerEntry> Layers { get; } = new();

    public void Load(SvgDocument? document, DrawableBase? root = null)
    {
        Layers.Clear();
        if (document is null)
            return;
        int index = 1;
        foreach (var g in document.Children.OfType<SvgGroup>())
        {
            if (IsLayerGroup(g))
                Layers.Add(CreateEntry(g, root, ref index));
        }
    }

    public LayerEntry AddLayer(SvgDocument document, string? name = null)
    {
        var group = new SvgGroup();
        group.CustomAttributes["data-layer"] = "true";
        group.CustomAttributes["data-name"] = name ?? $"Layer {Layers.Count + 1}";
        group.CustomAttributes["data-visible"] = "true";
        group.CustomAttributes["data-lock"] = "false";
        document.Children.Add(group);
        var entry = new LayerEntry(group, group.CustomAttributes["data-name"]);
        Layers.Add(entry);
        return entry;
    }

    public void RemoveLayer(LayerEntry layer, SvgDocument document)
    {
        document.Children.Remove(layer.Group);
        Layers.Remove(layer);
    }

    public void MoveUp(LayerEntry layer, SvgDocument document)
    {
        var idx = document.Children.IndexOf(layer.Group);
        if (idx > 0)
        {
            document.Children.RemoveAt(idx);
            document.Children.Insert(idx - 1, layer.Group);
            var lidx = Layers.IndexOf(layer);
            Layers.Move(lidx, lidx - 1);
        }
    }

    public void MoveDown(LayerEntry layer, SvgDocument document)
    {
        var idx = document.Children.IndexOf(layer.Group);
        if (idx >= 0 && idx < document.Children.Count - 1)
        {
            document.Children.RemoveAt(idx);
            document.Children.Insert(idx + 1, layer.Group);
            var lidx = Layers.IndexOf(layer);
            Layers.Move(lidx, lidx + 1);
        }
    }

    private static bool IsLayerGroup(SvgGroup group)
        => group.CustomAttributes.TryGetValue("data-layer", out var flag) &&
           string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);

    private static LayerEntry CreateEntry(SvgGroup group, DrawableBase? root, ref int index)
    {
        group.CustomAttributes.TryGetValue("data-name", out var name);
        var entry = new LayerEntry(group, string.IsNullOrEmpty(name) ? $"Layer {index++}" : name);
        if (root is not null)
            entry.Drawable = FindDrawable(root, group);
        foreach (var child in group.Children.OfType<SvgGroup>())
            if (IsLayerGroup(child))
                entry.Sublayers.Add(CreateEntry(child, root, ref index));
        return entry;
    }

    private static DrawableBase? FindDrawable(DrawableBase drawable, SvgElement element)
    {
        if (drawable.Element == element)
            return drawable;
        if (drawable is DrawableContainer container)
        {
            foreach (var child in container.ChildrenDrawables)
            {
                var found = FindDrawable(child, element);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }
}
