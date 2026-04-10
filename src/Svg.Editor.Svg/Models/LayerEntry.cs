using System;
using System.Collections.ObjectModel;
using Svg;
using Svg.Skia;

namespace Svg.Editor.Svg.Models;

public class LayerEntry
{
    public SvgGroup Group { get; }
    private string _name;
    private bool _locked;
    private bool _visible;

    public ObservableCollection<LayerEntry> Sublayers { get; } = new();
    public SvgSceneNode? SceneNode { get; set; }

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
        group.CustomAttributes.TryGetValue("data-visible", out var visible);
        _visible = visible is null || string.Equals(visible, "true", StringComparison.OrdinalIgnoreCase);
    }
}
