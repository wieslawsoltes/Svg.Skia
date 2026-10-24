using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Svg.Editor.Skia;

public enum SvgPanelDock { Left, RightTop, RightBottom, Bottom, Floating, Hidden }
public enum SvgWorkspacePreset { Essentials, Drawing, Typography }

/// <summary>Serializable UI state, deliberately independent of document state and history.</summary>
public sealed class SvgWorkspacePanelPlacement
{
    internal SvgWorkspacePanelPlacement(string id, SvgPanelDock dock) { Id = id; Dock = dock; }
    public string Id { get; }
    public SvgPanelDock Dock { get; internal set; }
    public double X { get; internal set; } = 120;
    public double Y { get; internal set; } = 100;
    public double Width { get; internal set; } = 320;
    public double Height { get; internal set; } = 440;
}

public readonly struct SvgWorkspaceMetrics
{
    internal SvgWorkspaceMetrics(double left, double right, double bottom, bool compact)
    { LeftWidth = left; RightWidth = right; BottomHeight = bottom; Compact = compact; }
    public double LeftWidth { get; }
    public double RightWidth { get; }
    public double BottomHeight { get; }
    public bool Compact { get; }
}

public sealed class SvgWorkspaceLayout
{
    public static IReadOnlyList<string> PanelIds { get; } = Array.AsReadOnly(new[]
    { "properties", "transform", "appearance", "stroke", "align", "geometry", "layers", "resources", "typography", "effects", "attributes" });
    private static readonly SvgPanelDock[] DockGroups = { SvgPanelDock.Left, SvgPanelDock.RightTop, SvgPanelDock.RightBottom, SvgPanelDock.Bottom };
    private readonly List<SvgWorkspacePanelPlacement> _panels = new();
    private readonly Dictionary<SvgPanelDock, string> _active = new();
    public IReadOnlyList<SvgWorkspacePanelPlacement> Panels => _panels.AsReadOnly();
    public double LeftWidth { get; private set; } = 248;
    public double RightWidth { get; private set; } = 312;
    public double BottomHeight { get; private set; } = 220;
    public double RightTopRatio { get; private set; } = 0.60;
    public bool TouchDensity { get; set; }
    public bool ShowRulers { get; set; } = true;
    public bool PanelsHidden { get; set; }

    public SvgWorkspaceLayout() => Reset(SvgWorkspacePreset.Essentials);
    public void Reset(SvgWorkspacePreset preset)
    {
        if (!Enum.IsDefined(typeof(SvgWorkspacePreset), preset)) throw new ArgumentOutOfRangeException(nameof(preset));
        _panels.Clear(); _active.Clear(); PanelsHidden = false;
        LeftWidth = 248; RightWidth = 312; BottomHeight = 220; RightTopRatio = 0.60;
        foreach (var id in PanelIds)
        {
            var dock = id is "layers" or "resources" ? SvgPanelDock.RightBottom : SvgPanelDock.RightTop;
            if (id == "attributes") dock = SvgPanelDock.Hidden;
            _panels.Add(new SvgWorkspacePanelPlacement(id, dock));
        }
        _active[SvgPanelDock.RightTop] = preset == SvgWorkspacePreset.Typography ? "typography" : "properties";
        _active[SvgPanelDock.RightBottom] = "layers";
        if (preset == SvgWorkspacePreset.Drawing)
        {
            Move("layers", SvgPanelDock.Left); Move("resources", SvgPanelDock.Left);
            _active[SvgPanelDock.Left] = "layers";
            Move("geometry", SvgPanelDock.RightBottom); Move("align", SvgPanelDock.RightBottom);
            _active[SvgPanelDock.RightBottom] = "geometry";
        }
        else if (preset == SvgWorkspacePreset.Typography)
        {
            Move("layers", SvgPanelDock.Left);
            _active[SvgPanelDock.Left] = "layers";
            _active[SvgPanelDock.RightBottom] = "resources";
        }
    }
    public SvgWorkspacePanelPlacement Find(string id)
        => _panels.FirstOrDefault(panel => panel.Id == id) ?? throw new ArgumentException("Unknown workspace panel.", nameof(id));
    public void Move(string id, SvgPanelDock destination, int index = int.MaxValue)
    {
        if (!Enum.IsDefined(typeof(SvgPanelDock), destination)) throw new ArgumentOutOfRangeException(nameof(destination));
        var panel = Find(id);
        var previous = panel.Dock;
        _panels.Remove(panel);
        var siblings = _panels.Where(item => item.Dock == destination).ToArray();
        var insertion = index < 0 ? 0 : Math.Min(index, siblings.Length);
        panel.Dock = destination;
        if (insertion < siblings.Length) _panels.Insert(_panels.IndexOf(siblings[insertion]), panel);
        else _panels.Add(panel);
        if (DockGroups.Contains(destination)) _active[destination] = id;
        if (previous != destination && _active.TryGetValue(previous, out var active) && active == id) _active.Remove(previous);
    }
    public string? Active(SvgPanelDock dock)
    {
        if (_active.TryGetValue(dock, out var id) && _panels.Any(panel => panel.Dock == dock && panel.Id == id)) return id;
        return _panels.FirstOrDefault(panel => panel.Dock == dock)?.Id;
    }
    public void Activate(string id)
    {
        var panel = Find(id);
        if (panel.Dock == SvgPanelDock.Hidden) Move(id, SvgPanelDock.RightTop);
        if (DockGroups.Contains(panel.Dock)) _active[panel.Dock] = id;
    }
    public void Resize(SvgPanelDock dock, double extent)
    {
        if (!Finite(extent)) throw new ArgumentOutOfRangeException(nameof(extent));
        switch (dock)
        {
            case SvgPanelDock.Left: LeftWidth = Clamp(extent, 220, 600); break;
            case SvgPanelDock.RightTop: case SvgPanelDock.RightBottom: RightWidth = Clamp(extent, 260, 640); break;
            case SvgPanelDock.Bottom: BottomHeight = Clamp(extent, 140, 560); break;
            default: throw new ArgumentException("Resize a docked group.", nameof(dock));
        }
    }
    public void SetRightSplit(double ratio)
    {
        if (!Finite(ratio)) throw new ArgumentOutOfRangeException(nameof(ratio));
        RightTopRatio = Clamp(ratio, 0.25, 0.8);
    }
    public void SetFloatingBounds(string id, double x, double y, double width, double height, double viewportWidth, double viewportHeight)
    {
        if (new[] { x, y, width, height, viewportWidth, viewportHeight }.Any(value => !Finite(value))) throw new ArgumentOutOfRangeException(nameof(width));
        var panel = Find(id);
        var availableWidth = Math.Max(1, viewportWidth); var availableHeight = Math.Max(1, viewportHeight);
        panel.Width = Math.Min(availableWidth, Clamp(width, 240, 800));
        panel.Height = Math.Min(availableHeight, Clamp(height, 160, 1000));
        panel.X = Clamp(x, 0, Math.Max(0, availableWidth - panel.Width));
        panel.Y = Clamp(y, 0, Math.Max(0, availableHeight - panel.Height));
    }
    public SvgWorkspaceMetrics Measure(double width, double height, bool focusCanvas = false)
    {
        if (!Finite(width) || !Finite(height) || width < 0 || height < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (PanelsHidden || focusCanvas) return new SvgWorkspaceMetrics(0, 0, 0, width < 1060);
        var right = width >= 900 && _panels.Any(panel => panel.Dock is SvgPanelDock.RightTop or SvgPanelDock.RightBottom)
            ? Math.Min(RightWidth, Math.Max(0, width - 530)) : 0;
        var left = width >= 1320 && _panels.Any(panel => panel.Dock == SvgPanelDock.Left)
            ? Math.Min(LeftWidth, Math.Max(0, width - right - 640)) : 0;
        var bottom = height >= 620 && width >= 900 && _panels.Any(panel => panel.Dock == SvgPanelDock.Bottom)
            ? Math.Min(BottomHeight, height * 0.42) : 0;
        return new SvgWorkspaceMetrics(left, right, bottom, width < 1060);
    }
    public string Serialize()
    {
        var root = new XElement("workspace", new XAttribute("version", 1),
            new XAttribute("left", LeftWidth), new XAttribute("right", RightWidth), new XAttribute("bottom", BottomHeight),
            new XAttribute("split", RightTopRatio), new XAttribute("touch", TouchDensity), new XAttribute("rulers", ShowRulers));
        foreach (var panel in _panels)
            root.Add(new XElement("panel", new XAttribute("id", panel.Id), new XAttribute("dock", panel.Dock),
                new XAttribute("x", panel.X), new XAttribute("y", panel.Y), new XAttribute("width", panel.Width), new XAttribute("height", panel.Height)));
        // Persist effective selection in deterministic group order, including fallback
        // selection. Hidden/floating panels do not share a single active tab.
        foreach (var dock in DockGroups)
            if (Active(dock) is { } id) root.Add(new XElement("active", new XAttribute("dock", dock), new XAttribute("id", id)));
        return root.ToString(SaveOptions.DisableFormatting);
    }
    public static SvgWorkspaceLayout Parse(string? xml)
    {
        var result = new SvgWorkspaceLayout();
        if (string.IsNullOrWhiteSpace(xml)) return result;
        if (xml!.Length > 32768) throw new ArgumentException("Workspace settings exceed the size limit.", nameof(xml));
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32768 });
        var root = XElement.Load(reader);
        if (root.Name != "workspace" || (string?)root.Attribute("version") != "1") return result;
        double Read(XElement element, string name, double fallback)
            => double.TryParse((string?)element.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && Finite(value) ? value : fallback;
        result.Resize(SvgPanelDock.Left, Read(root, "left", 248)); result.Resize(SvgPanelDock.RightTop, Read(root, "right", 312));
        result.Resize(SvgPanelDock.Bottom, Read(root, "bottom", 220)); result.SetRightSplit(Read(root, "split", 0.60));
        result.TouchDensity = (string?)root.Attribute("touch") == "true"; result.ShowRulers = (string?)root.Attribute("rulers") != "false";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in root.Elements("panel"))
        {
            var id = (string?)item.Attribute("id") ?? string.Empty;
            if (!PanelIds.Contains(id) || !seen.Add(id) || !Enum.TryParse((string?)item.Attribute("dock"), out SvgPanelDock dock)
                || !Enum.IsDefined(typeof(SvgPanelDock), dock)) continue;
            result.Move(id, dock);
            result.SetFloatingBounds(id, Read(item, "x", 120), Read(item, "y", 100), Read(item, "width", 320), Read(item, "height", 440), 16384, 16384);
        }
        result._active.Clear();
        foreach (var active in root.Elements("active"))
        {
            var id = (string?)active.Attribute("id") ?? string.Empty;
            if (PanelIds.Contains(id) && Enum.TryParse((string?)active.Attribute("dock"), out SvgPanelDock dock)
                && DockGroups.Contains(dock) && result.Find(id).Dock == dock) result._active[dock] = id;
        }
        return result;
    }
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
}
