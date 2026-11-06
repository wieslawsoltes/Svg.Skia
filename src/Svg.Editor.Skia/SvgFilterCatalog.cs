using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;

namespace Svg.Editor.Skia;

/// <summary>SVG Filter Effects authoring metadata, independent of any UI framework.</summary>
public static class SvgFilterCatalog
{
    public static readonly XNamespace Namespace = "http://www.w3.org/2000/svg";
    public static readonly XNamespace XLinkNamespace = "http://www.w3.org/1999/xlink";

    public static IReadOnlyList<string> Primitives { get; } = Array.AsReadOnly(new[]
    {
        "feBlend", "feColorMatrix", "feComponentTransfer", "feComposite", "feConvolveMatrix",
        "feDiffuseLighting", "feDisplacementMap", "feDropShadow", "feFlood", "feGaussianBlur",
        "feImage", "feMerge", "feMorphology", "feOffset", "feSpecularLighting", "feTile", "feTurbulence"
    });

    public static IReadOnlyList<string> Sources { get; } = Array.AsReadOnly(new[]
    {
        "SourceGraphic", "SourceAlpha", "BackgroundImage", "BackgroundAlpha", "FillPaint", "StrokePaint"
    });

    private static readonly IReadOnlyDictionary<string, string> Fields = new ReadOnlyDictionary<string, string>(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["filter"] = "x y width height filterUnits primitiveUnits color-interpolation-filters",
            ["feBlend"] = "mode",
            ["feColorMatrix"] = "type values",
            ["feComponentTransfer"] = "",
            ["feComposite"] = "operator k1 k2 k3 k4",
            ["feConvolveMatrix"] = "order kernelMatrix divisor bias targetX targetY edgeMode kernelUnitLength preserveAlpha",
            ["feDiffuseLighting"] = "surfaceScale diffuseConstant kernelUnitLength lighting-color",
            ["feDisplacementMap"] = "scale xChannelSelector yChannelSelector",
            ["feDropShadow"] = "dx dy stdDeviation flood-color flood-opacity",
            ["feFlood"] = "flood-color flood-opacity",
            ["feGaussianBlur"] = "stdDeviation edgeMode",
            ["feImage"] = "href xlink:href preserveAspectRatio",
            ["feMerge"] = "",
            ["feMergeNode"] = "in",
            ["feMorphology"] = "operator radius",
            ["feOffset"] = "dx dy",
            ["feSpecularLighting"] = "surfaceScale specularConstant specularExponent kernelUnitLength lighting-color",
            ["feTile"] = "",
            ["feTurbulence"] = "baseFrequency numOctaves seed stitchTiles type",
            ["feFuncR"] = "type tableValues slope intercept amplitude exponent offset",
            ["feFuncG"] = "type tableValues slope intercept amplitude exponent offset",
            ["feFuncB"] = "type tableValues slope intercept amplitude exponent offset",
            ["feFuncA"] = "type tableValues slope intercept amplitude exponent offset",
            ["feDistantLight"] = "azimuth elevation",
            ["fePointLight"] = "x y z",
            ["feSpotLight"] = "x y z pointsAtX pointsAtY pointsAtZ specularExponent limitingConeAngle"
        });

    public static IReadOnlyList<string> Attributes(string elementName)
    {
        if (!Fields.TryGetValue(elementName, out var fields))
            return Array.Empty<string>();
        var names = fields.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (Primitives.Contains(elementName))
        {
            names.InsertRange(0, new[] { "result", "x", "y", "width", "height", "color-interpolation-filters" });
            if (HasInput(elementName))
                names.Insert(1, "in");
            if (HasSecondInput(elementName))
                names.Insert(2, "in2");
        }
        return names.AsReadOnly();
    }

    public static bool HasInput(string name)
        => Primitives.Contains(name) && name is not ("feFlood" or "feImage" or "feTurbulence" or "feMerge");

    public static bool HasSecondInput(string name)
        => name is "feBlend" or "feComposite" or "feDisplacementMap";

    public static IReadOnlyList<string> Children(string name) => name switch
    {
        "feMerge" => new[] { "feMergeNode" },
        "feComponentTransfer" => new[] { "feFuncR", "feFuncG", "feFuncB", "feFuncA" },
        "feDiffuseLighting" or "feSpecularLighting" => new[] { "feDistantLight", "fePointLight", "feSpotLight" },
        _ => Array.Empty<string>()
    };

    public static IReadOnlyList<string> Choices(string elementName, string attribute) => attribute switch
    {
        "filterUnits" or "primitiveUnits" => new[] { "objectBoundingBox", "userSpaceOnUse" },
        "color-interpolation-filters" => new[] { "linearRGB", "sRGB", "auto" },
        "mode" => new[] { "normal", "multiply", "screen", "darken", "lighten", "overlay", "color-dodge", "color-burn", "hard-light", "soft-light", "difference", "exclusion", "hue", "saturation", "color", "luminosity" },
        "operator" when elementName == "feMorphology" => new[] { "erode", "dilate" },
        "operator" => new[] { "over", "in", "out", "atop", "xor", "arithmetic", "lighter" },
        "type" when elementName == "feColorMatrix" => new[] { "matrix", "saturate", "hueRotate", "luminanceToAlpha" },
        "type" when elementName == "feTurbulence" => new[] { "turbulence", "fractalNoise" },
        "type" when elementName.StartsWith("feFunc", StringComparison.Ordinal) => new[] { "identity", "table", "discrete", "linear", "gamma" },
        "edgeMode" => new[] { "duplicate", "wrap", "none" },
        "xChannelSelector" or "yChannelSelector" => new[] { "R", "G", "B", "A" },
        "stitchTiles" => new[] { "stitch", "noStitch" },
        "preserveAlpha" => new[] { "true", "false" },
        _ => Array.Empty<string>()
    };

    public static XName AttributeName(string name)
        => name == "xlink:href" ? XLinkNamespace + "href" : XName.Get(name);

    public static XElement Create(string name)
    {
        if (!Fields.ContainsKey(name))
            throw new ArgumentException("Unknown SVG filter element.", nameof(name));
        var element = new XElement(Namespace + name);
        void Set(string key, string value) => element.SetAttributeValue(key, value);
        switch (name)
        {
            case "filter":
                Set("x", "-20%"); Set("y", "-20%"); Set("width", "140%"); Set("height", "140%");
                Set("color-interpolation-filters", "sRGB");
                break;
            case "feGaussianBlur": Set("stdDeviation", "3"); break;
            case "feOffset": Set("dx", "8"); Set("dy", "8"); break;
            case "feBlend": Set("mode", "multiply"); Set("in2", "SourceGraphic"); break;
            case "feColorMatrix": Set("type", "saturate"); Set("values", "0"); break;
            case "feComposite": Set("operator", "over"); Set("in2", "SourceGraphic"); break;
            case "feConvolveMatrix": Set("order", "3"); Set("kernelMatrix", "0 -1 0 -1 5 -1 0 -1 0"); break;
            case "feDisplacementMap": Set("scale", "20"); Set("in2", "SourceGraphic"); Set("xChannelSelector", "R"); Set("yChannelSelector", "G"); break;
            case "feMorphology": Set("operator", "dilate"); Set("radius", "2"); break;
            case "feTurbulence": Set("baseFrequency", "0.03"); Set("numOctaves", "3"); Set("seed", "1"); Set("type", "fractalNoise"); break;
            case "feFlood": Set("flood-color", "#7061DC"); Set("flood-opacity", "1"); break;
            case "feDropShadow": Set("dx", "6"); Set("dy", "6"); Set("stdDeviation", "3"); Set("flood-opacity", "0.5"); break;
            case "feMerge": element.Add(new XElement(Namespace + "feMergeNode", new XAttribute("in", "SourceGraphic"))); break;
            case "feMergeNode": Set("in", "SourceGraphic"); break;
            case "feComponentTransfer":
                foreach (var channel in Children(name)) element.Add(Create(channel));
                break;
            case "feDiffuseLighting":
            case "feSpecularLighting":
                Set("surfaceScale", "2"); Set("lighting-color", "white"); element.Add(Create("feDistantLight"));
                break;
            case "feDistantLight": Set("azimuth", "225"); Set("elevation", "45"); break;
            case "fePointLight": Set("x", "50"); Set("y", "50"); Set("z", "100"); break;
            case "feSpotLight": Set("x", "50"); Set("y", "50"); Set("z", "100"); Set("pointsAtX", "50"); Set("pointsAtY", "50"); Set("pointsAtZ", "0"); break;
            case "feFuncR": case "feFuncG": case "feFuncB": case "feFuncA": Set("type", "identity"); break;
        }
        return element;
    }
}
