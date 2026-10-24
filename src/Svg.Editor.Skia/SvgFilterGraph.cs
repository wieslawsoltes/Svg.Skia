using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Svg.Editor.Skia;

public sealed class SvgFilterConnection
{
    internal SvgFilterConnection(int source, int target, int child, string port, string value)
    { Source = source; Target = target; Child = child; Port = port; Value = value; }
    /// <summary>Primitive index; -1 for a standard source, -2 for an unresolved reference.</summary>
    public int Source { get; }
    public int Target { get; }
    public int Child { get; }
    public string Port { get; }
    public string Value { get; }
}

/// <summary>
/// Detached SVG filter draft. Opening a graph never rewrites its source. Structural edits
/// make implicit edges explicit and preserve nearest-preceding result bindings. Unknown
/// attributes, namespaces, child metadata and extension elements survive round trips.
/// </summary>
public sealed class SvgFilterGraph
{
    public const int MaximumPrimitives = 128;
    public const int MaximumCharacters = 1048576;
    private XElement _root;
    public SvgFilterGraph() : this(SvgFilterCatalog.Create("filter")) { }
    public SvgFilterGraph(XElement filter)
    {
        if (filter is null) throw new ArgumentNullException(nameof(filter));
        if (filter.Name != SvgFilterCatalog.Namespace + "filter")
            throw new ArgumentException("Expected an SVG filter element.", nameof(filter));
        _root = new XElement(filter);
        CheckLimits(_root);
    }
    public static SvgFilterGraph Parse(string xml)
    {
        if (xml is null) throw new ArgumentNullException(nameof(xml));
        if (xml.Length > MaximumCharacters) throw new ArgumentException("Filter source is too large.", nameof(xml));
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumCharacters });
        return new SvgFilterGraph(XElement.Load(reader, LoadOptions.PreserveWhitespace));
    }
    public XElement ToElement() => new(_root);
    public string GetXml() => _root.ToString(SaveOptions.DisableFormatting);
    public IReadOnlyList<XElement> Nodes => PrimitiveElements(_root).Select(node => new XElement(node)).ToArray();
    public IReadOnlyList<SvgFilterConnection> Connections => ReadConnections(_root);

    public int Add(string name)
    {
        if (!SvgFilterCatalog.Primitives.Contains(name)) throw new ArgumentException("Choose a filter primitive.", nameof(name));
        var draft = new XElement(_root);
        FreezeConnections(draft);
        var nodes = PrimitiveElements(draft);
        var added = SvgFilterCatalog.Create(name);
        added.SetAttributeValue("result", NextResult(draft));
        if (SvgFilterCatalog.HasInput(name))
            added.SetAttributeValue("in", nodes.Length == 0 ? "SourceGraphic" : (string?)nodes[nodes.Length - 1].Attribute("result"));
        draft.Add(added);
        CheckLimits(draft);
        _root = draft;
        return nodes.Length;
    }

    /// <summary>Edits may be temporarily invalid while a user types. Validate before rendering or applying.</summary>
    public void SetAttribute(int node, string name, string? value, int child = -1)
    {
        var element = node < 0 ? _root : At(_root, node);
        if (child >= 0) element = element.Elements().ElementAt(child);
        var attribute = SvgFilterCatalog.AttributeName(name);
        if (attribute.LocalName == "result" && child < 0 && node >= 0)
        {
            RenameResult(node, value ?? string.Empty);
            return;
        }
        if (value?.Length > 65536) throw new ArgumentException("The attribute is too long.", nameof(value));
        element.SetAttributeValue(attribute, string.IsNullOrWhiteSpace(value) ? null : value);
    }

    public void RenameResult(int index, string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        name = name.Trim();
        if (name.Length == 0 || name.Any(char.IsWhiteSpace) || SvgFilterCatalog.Sources.Contains(name))
            throw new ArgumentException("Choose a nonempty result name that is not a standard input.", nameof(name));
        var nodes = PrimitiveElements(_root);
        var original = At(_root, index);
        if ((string?)original.Attribute("result") == name) return;
        if (nodes.Where((_, i) => i != index).Any(node => (string?)node.Attribute("result") == name))
            throw new ArgumentException("Another primitive already uses that result name.", nameof(name));
        var connections = ReadConnections(_root);
        original.SetAttributeValue("result", name);
        foreach (var edge in connections.Where(edge => edge.Source == index))
            PortElement(nodes[edge.Target], edge.Child).SetAttributeValue(edge.Port, name);
    }

    public int Duplicate(int index)
    {
        var draft = new XElement(_root);
        FreezeConnections(draft);
        var source = At(draft, index);
        // XML IDs and result labels have different scopes. Keep XML identity usable
        // for animation/href references while reserving new IDs against the whole draft.
        var copy = SvgXmlIdRemapper.Clone(source, draft.DescendantsAndSelf().Attributes("id").Select(id => id.Value));
        copy.SetAttributeValue("result", NextResult(draft));
        source.AddAfterSelf(copy);
        CheckLimits(draft);
        _root = draft;
        return index + 1;
    }

    public void Move(int index, int destination)
    {
        var draft = new XElement(_root);
        var count = PrimitiveElements(draft).Length;
        if (destination < 0 || destination >= count) throw new ArgumentOutOfRangeException(nameof(destination));
        if (index == destination) return;
        FreezeConnections(draft);
        var source = At(draft, index);
        var target = At(draft, destination);
        source.Remove();
        if (destination > index) target.AddAfterSelf(source); else target.AddBeforeSelf(source);
        if (ReadConnections(draft).Any(edge => edge.Source == -2))
            throw new InvalidOperationException("This order would put an input after the primitive that consumes it.");
        _root = draft;
    }

    public void Remove(int index, bool bypass = false)
    {
        var draft = new XElement(_root);
        FreezeConnections(draft);
        var nodes = PrimitiveElements(draft);
        var source = At(draft, index);
        var connections = ReadConnections(draft);
        var consumers = connections.Where(edge => edge.Source == index).ToArray();
        if (consumers.Length > 0 && !bypass)
            throw new InvalidOperationException("Other primitives use this result. Choose Bypass and remove or reconnect them first.");
        var replacement = connections.FirstOrDefault(edge => edge.Target == index)?.Value ?? "SourceGraphic";
        foreach (var edge in consumers)
            PortElement(nodes[edge.Target], edge.Child).SetAttributeValue(edge.Port, replacement);
        source.Remove();
        _root = draft;
    }

    public void AddChild(int index, string name)
    {
        var draft = new XElement(_root);
        var node = At(draft, index);
        if (!SvgFilterCatalog.Children(node.Name.LocalName).Contains(name))
            throw new ArgumentException("This child is not supported by the selected primitive.", nameof(name));
        if (name.StartsWith("feFunc", StringComparison.Ordinal) && node.Elements(SvgFilterCatalog.Namespace + name).Any())
            throw new InvalidOperationException("That transfer channel already exists.");
        if (node.Name.LocalName is "feDiffuseLighting" or "feSpecularLighting")
            node.Elements().Where(child => SvgFilterCatalog.Children(node.Name.LocalName).Contains(child.Name.LocalName)).Remove();
        node.Add(SvgFilterCatalog.Create(name));
        CheckLimits(draft);
        _root = draft;
    }
    public void RemoveChild(int index, int child)
        => At(_root, index).Elements().ElementAt(child).Remove();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        try { CheckLimits(_root); } catch (ArgumentException exception) { errors.Add(exception.Message); }
        foreach (var duplicate in _root.DescendantsAndSelf().Attributes("id").GroupBy(id => id.Value).Where(group => group.Count() > 1))
            errors.Add($"The filter contains a duplicate XML ID: '{duplicate.Key}'.");
        foreach (var edge in Connections.Where(edge => edge.Source == -2))
            errors.Add($"Primitive {edge.Target + 1}: '{edge.Value}' is not a preceding result or standard input.");
        foreach (var node in PrimitiveElements(_root))
        {
            if (SvgFilterCatalog.Sources.Contains((string?)node.Attribute("result") ?? string.Empty))
                errors.Add($"{node.Name.LocalName}: a result cannot shadow a standard input.");
            foreach (var element in node.DescendantsAndSelf().Where(e => e.Name.Namespace == SvgFilterCatalog.Namespace))
            {
                var supported = SvgFilterCatalog.Attributes(element.Name.LocalName);
                // Animation 'values' and metadata attributes are not filter-number lists.
                // Preserve their original semantics rather than validating by name alone.
                if (supported.Count == 0) continue;
                foreach (var name in NumericFields.Where(supported.Contains))
                {
                    if (element.Attribute(name) is not { } attribute) continue;
                    if (Numbers(attribute.Value) is not { Length: > 0 })
                        errors.Add($"{element.Name.LocalName}: {name} requires finite numbers.");
                }
                foreach (var name in new[] { "stdDeviation", "radius", "baseFrequency", "kernelUnitLength" }.Where(supported.Contains))
                    if (element.Attribute(name) is { } attribute && Numbers(attribute.Value) is { } numbers
                        && (numbers.Length is < 1 or > 2 || numbers.Any(n => n < 0)))
                        errors.Add($"{element.Name.LocalName}: {name} requires one or two nonnegative numbers.");
                if (element.Name.LocalName == "feTurbulence" && element.Attribute("numOctaves") is { } octaves &&
                    (!int.TryParse(octaves.Value, out var count) || count < 1 || count > 16))
                    errors.Add("Turbulence authoring supports 1–16 octaves.");
                if (element.Name.LocalName == "feConvolveMatrix")
                {
                    var order = Numbers((string?)element.Attribute("order") ?? "3");
                    if (order is null || order.Length is < 1 or > 2 || order.Any(n => n < 1 || n > 63 || n != Math.Floor(n)))
                        errors.Add("Convolution order requires one or two whole numbers from 1 to 63.");
                    else if (Numbers((string?)element.Attribute("kernelMatrix") ?? "") is not { } kernel ||
                        kernel.Length != order[0] * order[order.Length - 1])
                        errors.Add("The kernel size must match the convolution order.");
                }
                if (element.Name.LocalName == "feColorMatrix" && ((string?)element.Attribute("type") ?? "matrix") == "matrix"
                    && element.Attribute("values") is { } matrix && Numbers(matrix.Value)?.Length != 20)
                    errors.Add("A color matrix requires exactly 20 numbers.");
            }
        }
        return errors.AsReadOnly();
    }
    public void ThrowIfInvalid()
    {
        var errors = Validate();
        if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
    }

    private static readonly string[] NumericFields =
    {
        "stdDeviation", "radius", "dx", "dy", "scale", "baseFrequency", "numOctaves", "seed", "k1", "k2", "k3", "k4",
        "order", "kernelMatrix", "divisor", "bias", "targetX", "targetY", "kernelUnitLength", "surfaceScale",
        "diffuseConstant", "specularConstant", "specularExponent", "azimuth", "elevation", "z", "pointsAtX", "pointsAtY",
        "pointsAtZ", "limitingConeAngle", "tableValues", "slope", "intercept", "amplitude", "exponent", "offset", "values", "flood-opacity"
    };
    private static double[]? Numbers(string text)
    {
        var parts = text.Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var values = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) ||
                double.IsNaN(values[i]) || double.IsInfinity(values[i])) return null;
        return values;
    }
    private static XElement[] PrimitiveElements(XElement root)
        => root.Elements().Where(node => node.Name.Namespace == SvgFilterCatalog.Namespace
            && node.Name.LocalName.StartsWith("fe", StringComparison.Ordinal)).ToArray();
    private static XElement At(XElement root, int index)
    {
        var nodes = PrimitiveElements(root);
        if (index < 0 || index >= nodes.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return nodes[index];
    }
    private static XElement PortElement(XElement node, int child)
        => child < 0 ? node : node.Elements().ElementAt(child);
    private static string NextResult(XElement root)
    {
        var used = new HashSet<string>(PrimitiveElements(root).Select(node => (string?)node.Attribute("result") ?? ""), StringComparer.Ordinal);
        for (var index = 1; ; index++)
        { var result = "effect" + index.ToString(CultureInfo.InvariantCulture); if (!used.Contains(result)) return result; }
    }
    private static List<SvgFilterConnection> ReadConnections(XElement root)
    {
        var connections = new List<SvgFilterConnection>();
        var named = new Dictionary<string, int>(StringComparer.Ordinal);
        var nodes = PrimitiveElements(root);
        for (var index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index];
            void Read(XElement element, int child, string port)
            {
                var value = (string?)element.Attribute(port);
                var implicitInput = string.IsNullOrWhiteSpace(value);
                var source = implicitInput ? index - 1 : SvgFilterCatalog.Sources.Contains(value!) ? -1
                    : named.TryGetValue(value!, out var target) ? target : -2;
                value = implicitInput ? index == 0 ? "SourceGraphic" : (string?)nodes[index - 1].Attribute("result") ?? "(previous)" : value;
                connections.Add(new SvgFilterConnection(source, index, child, port, value!));
            }
            if (SvgFilterCatalog.HasInput(node.Name.LocalName) || node.Attribute("in") is not null) Read(node, -1, "in");
            if (SvgFilterCatalog.HasSecondInput(node.Name.LocalName) || node.Attribute("in2") is not null) Read(node, -1, "in2");
            var children = node.Elements().ToArray();
            for (var child = 0; child < children.Length; child++)
                if (children[child].Name == SvgFilterCatalog.Namespace + "feMergeNode") Read(children[child], child, "in");
            if ((string?)node.Attribute("result") is { Length: > 0 } result) named[result] = index;
        }
        return connections;
    }
    private static void FreezeConnections(XElement root)
    {
        var edges = ReadConnections(root);
        if (edges.Any(edge => edge.Source == -2)) throw new InvalidOperationException("Repair unresolved inputs before changing graph structure.");
        var nodes = PrimitiveElements(root);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var result = (string?)node.Attribute("result");
            if (string.IsNullOrWhiteSpace(result) || !used.Add(result!))
            { result = NextResult(root); node.SetAttributeValue("result", result); used.Add(result); }
        }
        foreach (var edge in edges)
            PortElement(nodes[edge.Target], edge.Child).SetAttributeValue(edge.Port,
                edge.Source < 0 ? edge.Value : (string?)nodes[edge.Source].Attribute("result"));
    }
    private static void CheckLimits(XElement root)
    {
        if (PrimitiveElements(root).Length > MaximumPrimitives || root.Descendants().Take(1025).Count() > 1024
            || root.ToString(SaveOptions.DisableFormatting).Length > MaximumCharacters)
            throw new ArgumentException("A filter supports at most 128 primitives, 1,024 child elements and 1 MiB of XML.");
    }
}
