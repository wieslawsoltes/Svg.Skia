#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Svg.Pathing;

namespace Svg;

internal sealed class SvgComputedStyleCache
{
    private readonly Dictionary<SvgElement, SvgComputedStyleSnapshot> _snapshots =
        new(SvgElementReferenceComparer.Instance);

    public SvgComputedStyleSnapshot GetOrCreate(SvgElement element)
    {
        if (!_snapshots.TryGetValue(element, out var snapshot))
        {
            snapshot = new SvgComputedStyleSnapshot(this, element);
            _snapshots[element] = snapshot;
        }

        return snapshot;
    }

    private sealed class SvgElementReferenceComparer : IEqualityComparer<SvgElement>
    {
        public static readonly SvgElementReferenceComparer Instance = new();

        public bool Equals(SvgElement? x, SvgElement? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(SvgElement obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}

internal sealed class SvgComputedStyleSnapshot
{
    private readonly SvgComputedStyleCache _cache;
    private readonly SvgElement _element;
    private Dictionary<string, string?>? _properties;

    public SvgComputedStyleSnapshot(SvgComputedStyleCache cache, SvgElement element)
    {
        _cache = cache;
        _element = element;
    }

    public bool TryGetPropertyValue(string propertyName, out string value)
    {
        var metadata = SvgComputedStyleMetadata.For(propertyName);
        if (TryGetPropertyValue(metadata, out var computedValue))
        {
            value = computedValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public bool TryGetPaintOrder(out SvgPaintOrder paintOrder)
    {
        paintOrder = SvgPaintOrder.Normal;
        if (!TryGetPropertyValue(SvgComputedStyleMetadata.PaintOrder, out var value))
        {
            return false;
        }

        return SvgComputedStyleMetadata.TryParsePaintOrder(value, out paintOrder);
    }

    public bool TryGetWhiteSpace(out SvgWhiteSpace whiteSpace)
    {
        whiteSpace = SvgWhiteSpace.Normal;
        if (!TryGetPropertyValue(SvgComputedStyleMetadata.WhiteSpace, out var value))
        {
            return false;
        }

        return SvgComputedStyleMetadata.TryParseWhiteSpace(value, out whiteSpace);
    }

    public string TextOverflow => GetPropertyValueOrDefault(SvgComputedStyleMetadata.TextOverflow, "clip");

    public string? InlineSize => GetPropertyValueOrNull(SvgComputedStyleMetadata.InlineSize);

    public string? ShapeInside => GetPropertyValueOrNull(SvgComputedStyleMetadata.ShapeInside);

    public string? ShapeSubtract => GetPropertyValueOrNull(SvgComputedStyleMetadata.ShapeSubtract);

    public string? ClipPath => GetPropertyValueOrNull(SvgComputedStyleMetadata.ClipPath);

    public string? Filter => GetPropertyValueOrNull(SvgComputedStyleMetadata.Filter);

    public string? Mask => GetPropertyValueOrNull(SvgComputedStyleMetadata.Mask);

    public string? MarkerStart => GetPropertyValueOrNull(SvgComputedStyleMetadata.MarkerStart);

    public string? MarkerMid => GetPropertyValueOrNull(SvgComputedStyleMetadata.MarkerMid);

    public string? MarkerEnd => GetPropertyValueOrNull(SvgComputedStyleMetadata.MarkerEnd);

    public bool TryGetIsolation(out SvgIsolation isolation)
    {
        isolation = SvgIsolation.Auto;
        if (!TryGetPropertyValue(SvgComputedStyleMetadata.Isolation, out var value))
        {
            return false;
        }

        return SvgComputedStyleMetadata.TryParseIsolation(value, out isolation);
    }

    public bool TryGetMixBlendMode(out SvgMixBlendMode mixBlendMode)
    {
        mixBlendMode = SvgMixBlendMode.Normal;
        if (!TryGetPropertyValue(SvgComputedStyleMetadata.MixBlendMode, out var value))
        {
            return false;
        }

        return SvgComputedStyleMetadata.TryParseMixBlendMode(value, out mixBlendMode);
    }

    private string GetPropertyValueOrDefault(SvgComputedStyleMetadata metadata, string defaultValue)
    {
        return TryGetPropertyValue(metadata, out var value) ? value : defaultValue;
    }

    private string? GetPropertyValueOrNull(SvgComputedStyleMetadata metadata)
    {
        return TryGetPropertyValue(metadata, out var value) ? value : null;
    }

    private bool TryGetPropertyValue(SvgComputedStyleMetadata metadata, out string value)
    {
        _properties ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!_properties.TryGetValue(metadata.Name, out var computedValue))
        {
            computedValue = ResolveProperty(metadata);
            _properties[metadata.Name] = computedValue;
        }

        if (computedValue is not null)
        {
            value = computedValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private string? ResolveProperty(SvgComputedStyleMetadata metadata)
    {
        if (_element.TryGetOwnCascadedStyleValue(metadata.Name, out var rawValue))
        {
            var normalizedValue = Normalize(rawValue);
            if (normalizedValue.Length > 0)
            {
                if (string.Equals(normalizedValue, "inherit", StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveInheritedProperty(metadata);
                }

                if (string.Equals(normalizedValue, "initial", StringComparison.OrdinalIgnoreCase))
                {
                    return metadata.InitialValue;
                }

                if (string.Equals(normalizedValue, "unset", StringComparison.OrdinalIgnoreCase))
                {
                    return metadata.Inherited ? ResolveInheritedProperty(metadata) : metadata.InitialValue;
                }

                if (metadata.IsValid(normalizedValue))
                {
                    return normalizedValue;
                }
            }
        }

        if (metadata.ShorthandName is not null &&
            _element.TryGetOwnCascadedStyleValue(metadata.ShorthandName, out var rawShorthandValue))
        {
            var normalizedShorthandValue = Normalize(rawShorthandValue);
            if (normalizedShorthandValue.Length > 0)
            {
                if (string.Equals(normalizedShorthandValue, "inherit", StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveInheritedProperty(metadata);
                }

                if (string.Equals(normalizedShorthandValue, "initial", StringComparison.OrdinalIgnoreCase))
                {
                    return metadata.InitialValue;
                }

                if (string.Equals(normalizedShorthandValue, "unset", StringComparison.OrdinalIgnoreCase))
                {
                    return metadata.Inherited ? ResolveInheritedProperty(metadata) : metadata.InitialValue;
                }

                if (metadata.IsValid(normalizedShorthandValue))
                {
                    return normalizedShorthandValue;
                }
            }
        }

        return metadata.Inherited ? ResolveInheritedProperty(metadata) : metadata.InitialValue;
    }

    private string? ResolveInheritedProperty(SvgComputedStyleMetadata metadata)
    {
        return _element.Parent is not null &&
               _cache.GetOrCreate(_element.Parent).TryGetPropertyValue(metadata, out var parentValue)
            ? parentValue
            : metadata.InitialValue;
    }

    private static string Normalize(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

}

internal sealed class SvgComputedStyleMetadata
{
    private const string MarkerShorthandName = "marker";

    public static readonly SvgComputedStyleMetadata PaintOrder = new(
        "paint-order",
        inherited: true,
        initialValue: "normal",
        IsValidPaintOrder);

    public static readonly SvgComputedStyleMetadata WhiteSpace = new(
        "white-space",
        inherited: true,
        initialValue: "normal",
        IsValidWhiteSpace);

    public static readonly SvgComputedStyleMetadata TextOverflow = new(
        "text-overflow",
        inherited: false,
        initialValue: "clip",
        IsValidTextOverflow);

    public static readonly SvgComputedStyleMetadata InlineSize = new(
        "inline-size",
        inherited: false,
        initialValue: null,
        IsValidInlineSize);

    public static readonly SvgComputedStyleMetadata ShapeInside = new(
        "shape-inside",
        inherited: false,
        initialValue: null,
        static value => !string.IsNullOrWhiteSpace(value));

    public static readonly SvgComputedStyleMetadata ShapeSubtract = new(
        "shape-subtract",
        inherited: false,
        initialValue: null,
        static value => !string.IsNullOrWhiteSpace(value));

    public static readonly SvgComputedStyleMetadata GeometryUnit = new(
        string.Empty,
        inherited: false,
        initialValue: null,
        IsValidSvgUnit);

    public static readonly SvgComputedStyleMetadata PathData = new(
        "d",
        inherited: false,
        initialValue: null,
        IsValidPathData);

    public static readonly SvgComputedStyleMetadata ClipPath = new(
        "clip-path",
        inherited: false,
        initialValue: "none",
        IsValidReferenceOrNone);

    public static readonly SvgComputedStyleMetadata Filter = new(
        "filter",
        inherited: false,
        initialValue: "none",
        IsValidReferenceOrNone);

    public static readonly SvgComputedStyleMetadata Mask = new(
        "mask",
        inherited: false,
        initialValue: "none",
        IsValidReferenceOrNone);

    public static readonly SvgComputedStyleMetadata Marker = new(
        MarkerShorthandName,
        inherited: true,
        initialValue: "none",
        IsValidMarkerReference);

    public static readonly SvgComputedStyleMetadata MarkerStart = new(
        "marker-start",
        inherited: true,
        initialValue: "none",
        IsValidMarkerReference,
        MarkerShorthandName);

    public static readonly SvgComputedStyleMetadata MarkerMid = new(
        "marker-mid",
        inherited: true,
        initialValue: "none",
        IsValidMarkerReference,
        MarkerShorthandName);

    public static readonly SvgComputedStyleMetadata MarkerEnd = new(
        "marker-end",
        inherited: true,
        initialValue: "none",
        IsValidMarkerReference,
        MarkerShorthandName);

    public static readonly SvgComputedStyleMetadata MaskType = new(
        "mask-type",
        inherited: false,
        initialValue: "luminance",
        static value => IsCssIdentifier(value, "luminance", "alpha"));

    public static readonly SvgComputedStyleMetadata ColorInterpolation = new(
        "color-interpolation",
        inherited: true,
        initialValue: "sRGB",
        IsValidColorInterpolation);

    public static readonly SvgComputedStyleMetadata ColorInterpolationFilters = new(
        "color-interpolation-filters",
        inherited: true,
        initialValue: "linearRGB",
        IsValidColorInterpolation);

    public static readonly SvgComputedStyleMetadata Isolation = new(
        "isolation",
        inherited: false,
        initialValue: "auto",
        static value => IsCssIdentifier(value, "auto", "isolate"));

    public static readonly SvgComputedStyleMetadata MixBlendMode = new(
        "mix-blend-mode",
        inherited: false,
        initialValue: "normal",
        IsValidMixBlendMode);

    private SvgComputedStyleMetadata(
        string name,
        bool inherited,
        string? initialValue,
        Func<string, bool> validator,
        string? shorthandName = null,
        bool knownProperty = true)
    {
        Name = name;
        Inherited = inherited;
        InitialValue = initialValue;
        ShorthandName = shorthandName;
        IsKnownProperty = knownProperty;
        _validator = validator;
    }

    private readonly Func<string, bool> _validator;

    public string Name { get; }

    public bool Inherited { get; }

    public string? InitialValue { get; }

    public string? ShorthandName { get; }

    public bool IsKnownProperty { get; }

    public static SvgComputedStyleMetadata For(string propertyName)
    {
        return propertyName.ToLowerInvariant() switch
        {
            "paint-order" => PaintOrder,
            "white-space" => WhiteSpace,
            "text-overflow" => TextOverflow,
            "inline-size" => InlineSize,
            "shape-inside" => ShapeInside,
            "shape-subtract" => ShapeSubtract,
            "d" => PathData,
            "x" or
            "y" or
            "x1" or
            "y1" or
            "x2" or
            "y2" or
            "cx" or
            "cy" or
            "r" or
            "width" or
            "height" => new SvgComputedStyleMetadata(
                propertyName,
                GeometryUnit.Inherited,
                GeometryUnit.InitialValue,
                IsValidSvgUnit),
            "rx" or
            "ry" => new SvgComputedStyleMetadata(
                propertyName,
                GeometryUnit.Inherited,
                GeometryUnit.InitialValue,
                IsValidSvgUnitOrAuto),
            "clip-path" => ClipPath,
            "filter" => Filter,
            "mask" => Mask,
            "marker" => Marker,
            "marker-start" => MarkerStart,
            "marker-mid" => MarkerMid,
            "marker-end" => MarkerEnd,
            "mask-type" => MaskType,
            "color-interpolation" => ColorInterpolation,
            "color-interpolation-filters" => ColorInterpolationFilters,
            "isolation" => Isolation,
            "mix-blend-mode" => MixBlendMode,
            _ => new SvgComputedStyleMetadata(
                propertyName,
                inherited: false,
                initialValue: null,
                static value => !string.IsNullOrWhiteSpace(value),
                knownProperty: false)
        };
    }

    public static bool ShouldIgnoreInvalidDeclaration(string propertyName, string value)
    {
        var metadata = For(propertyName);
        if (!metadata.IsKnownProperty ||
            ContainsVarFunction(value) ||
            IsCssWideKeyword(value))
        {
            return false;
        }

        var normalizedValue = value.Trim();
        return normalizedValue.Length == 0 || !metadata.IsValid(normalizedValue);
    }

    public bool IsValid(string value)
    {
        return _validator(value);
    }

    private static bool IsValidTextOverflow(string value)
    {
        if (IsCssIdentifier(value, "clip", "ellipsis"))
        {
            return true;
        }

        return IsQuotedString(value);
    }

    private static bool IsValidInlineSize(string value)
    {
        return IsCssIdentifier(value, "auto") ||
               IsValidSvgUnit(value) ||
               IsKnownLengthFunction(value);
    }

    private static bool IsValidSvgUnit(string value)
    {
        try
        {
            _ = SvgUnitConverter.Parse(value.AsSpan());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidSvgUnitOrAuto(string value)
    {
        return IsCssIdentifier(value, "auto") || IsValidSvgUnit(value);
    }

    private static bool IsValidPathData(string value)
    {
        if (IsCssIdentifier(value, "none"))
        {
            return true;
        }

        var normalized = NormalizeCssPathData(value);
        if (normalized.Length == 0)
        {
            return false;
        }

        try
        {
            return SvgPathBuilder.Parse(normalized).Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidReferenceOrNone(string value)
    {
        return IsCssIdentifier(value, "none") || IsValidUrlReference(value);
    }

    private static bool IsValidPaintOrder(string value)
    {
        return TryParsePaintOrder(value, out _);
    }

    private static bool IsValidWhiteSpace(string value)
    {
        return TryParseWhiteSpace(value, out _);
    }

    private static bool IsValidMarkerReference(string value)
    {
        if (IsCssIdentifier(value, "none"))
        {
            return true;
        }

        if (IsValidUrlReference(value))
        {
            return true;
        }

        if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !ContainsCssWhitespace(value) &&
               Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out _);
    }

    private static bool IsValidUrlReference(string value)
    {
        if (!value.StartsWith("url(", StringComparison.OrdinalIgnoreCase) ||
            !value.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        var inner = value.Substring(4, value.Length - 5).Trim();
        if (IsQuotedString(inner))
        {
            inner = inner.Substring(1, inner.Length - 2).Trim();
        }

        return inner.Length > 0 && Uri.TryCreate(inner, UriKind.RelativeOrAbsolute, out _);
    }

    private static bool IsValidColorInterpolation(string value)
    {
        return IsCssIdentifier(value, "auto", "sRGB", "linearRGB");
    }

    private static bool IsValidMixBlendMode(string value)
    {
        return IsCssIdentifier(
            value,
            "normal",
            "multiply",
            "screen",
            "overlay",
            "darken",
            "lighten",
            "color-dodge",
            "color-burn",
            "hard-light",
            "soft-light",
            "difference",
            "exclusion",
            "hue",
            "saturation",
            "color",
            "luminosity");
    }

    private static bool IsCssIdentifier(string value, params string[] expected)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            if (string.Equals(value, expected[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsVarFunction(string value)
    {
        return value.IndexOf("var(", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsCssWideKeyword(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Equals("initial", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("inherit", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("unset", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("revert", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("revert-layer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownLengthFunction(string value)
    {
        return value.StartsWith("calc(", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("min(", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("max(", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("clamp(", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsQuotedString(string value)
    {
        return value.Length >= 2 &&
               ((value[0] == '\'' && value[value.Length - 1] == '\'') ||
                (value[0] == '"' && value[value.Length - 1] == '"'));
    }

    private static bool ContainsCssWhitespace(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeCssPathData(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("path(", StringComparison.OrdinalIgnoreCase) ||
            !trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var inner = trimmed.Substring(5, trimmed.Length - 6).Trim();
        return IsQuotedString(inner)
            ? inner.Substring(1, inner.Length - 2)
            : inner;
    }

    internal static bool TryParsePaintOrder(string value, out SvgPaintOrder paintOrder)
    {
        paintOrder = SvgPaintOrder.Normal;
        var normalized = value.Trim();
        if (string.Equals(normalized, "normal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.Length == 0)
        {
            return false;
        }

        var tokens = normalized.Split([' ', '\t', '\r', '\n', '\f'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || tokens.Length > 3)
        {
            return false;
        }

        var order = new List<string>(3);
        foreach (var token in tokens)
        {
            var phase = token.Trim().ToLowerInvariant();
            if (phase != "fill" && phase != "stroke" && phase != "markers")
            {
                return false;
            }

            if (order.Contains(phase))
            {
                return false;
            }

            order.Add(phase);
        }

        AddMissing(order, "fill");
        AddMissing(order, "stroke");
        AddMissing(order, "markers");

        switch (string.Join(" ", order.ToArray()))
        {
            case "fill stroke markers":
                paintOrder = SvgPaintOrder.FillStrokeMarkers;
                return true;
            case "fill markers stroke":
                paintOrder = SvgPaintOrder.FillMarkersStroke;
                return true;
            case "stroke fill markers":
                paintOrder = SvgPaintOrder.StrokeFillMarkers;
                return true;
            case "stroke markers fill":
                paintOrder = SvgPaintOrder.StrokeMarkersFill;
                return true;
            case "markers fill stroke":
                paintOrder = SvgPaintOrder.MarkersFillStroke;
                return true;
            case "markers stroke fill":
                paintOrder = SvgPaintOrder.MarkersStrokeFill;
                return true;
            default:
                return false;
        }
    }

    internal static bool TryParseWhiteSpace(string value, out SvgWhiteSpace whiteSpace)
    {
        switch (value.Trim())
        {
            case var token when string.Equals(token, "normal", StringComparison.OrdinalIgnoreCase):
                whiteSpace = SvgWhiteSpace.Normal;
                return true;
            case var token when string.Equals(token, "pre", StringComparison.OrdinalIgnoreCase):
                whiteSpace = SvgWhiteSpace.Pre;
                return true;
            case var token when string.Equals(token, "nowrap", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(token, "no-wrap", StringComparison.OrdinalIgnoreCase):
                whiteSpace = SvgWhiteSpace.NoWrap;
                return true;
            case var token when string.Equals(token, "pre-wrap", StringComparison.OrdinalIgnoreCase):
                whiteSpace = SvgWhiteSpace.PreWrap;
                return true;
            case var token when string.Equals(token, "break-spaces", StringComparison.OrdinalIgnoreCase):
                whiteSpace = SvgWhiteSpace.BreakSpaces;
                return true;
            case var token when string.Equals(token, "pre-line", StringComparison.OrdinalIgnoreCase):
                whiteSpace = SvgWhiteSpace.PreLine;
                return true;
            default:
                whiteSpace = SvgWhiteSpace.Normal;
                return false;
        }
    }

    internal static bool TryParseIsolation(string value, out SvgIsolation isolation)
    {
        switch (value.Trim())
        {
            case var token when string.Equals(token, "auto", StringComparison.OrdinalIgnoreCase):
                isolation = SvgIsolation.Auto;
                return true;
            case var token when string.Equals(token, "isolate", StringComparison.OrdinalIgnoreCase):
                isolation = SvgIsolation.Isolate;
                return true;
            default:
                isolation = SvgIsolation.Auto;
                return false;
        }
    }

    internal static bool TryParseMixBlendMode(string value, out SvgMixBlendMode mixBlendMode)
    {
        switch (value.Trim())
        {
            case var token when string.Equals(token, "normal", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Normal;
                return true;
            case var token when string.Equals(token, "multiply", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Multiply;
                return true;
            case var token when string.Equals(token, "screen", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Screen;
                return true;
            case var token when string.Equals(token, "overlay", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Overlay;
                return true;
            case var token when string.Equals(token, "darken", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Darken;
                return true;
            case var token when string.Equals(token, "lighten", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Lighten;
                return true;
            case var token when string.Equals(token, "color-dodge", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.ColorDodge;
                return true;
            case var token when string.Equals(token, "color-burn", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.ColorBurn;
                return true;
            case var token when string.Equals(token, "hard-light", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.HardLight;
                return true;
            case var token when string.Equals(token, "soft-light", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.SoftLight;
                return true;
            case var token when string.Equals(token, "difference", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Difference;
                return true;
            case var token when string.Equals(token, "exclusion", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Exclusion;
                return true;
            case var token when string.Equals(token, "hue", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Hue;
                return true;
            case var token when string.Equals(token, "saturation", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Saturation;
                return true;
            case var token when string.Equals(token, "color", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Color;
                return true;
            case var token when string.Equals(token, "luminosity", StringComparison.OrdinalIgnoreCase):
                mixBlendMode = SvgMixBlendMode.Luminosity;
                return true;
            default:
                mixBlendMode = SvgMixBlendMode.Normal;
                return false;
        }
    }

    internal static string ToCssText(SvgPaintOrder paintOrder)
    {
        return paintOrder switch
        {
            SvgPaintOrder.Normal => "normal",
            SvgPaintOrder.FillStrokeMarkers => "fill stroke markers",
            SvgPaintOrder.FillMarkersStroke => "fill markers stroke",
            SvgPaintOrder.StrokeFillMarkers => "stroke fill markers",
            SvgPaintOrder.StrokeMarkersFill => "stroke markers fill",
            SvgPaintOrder.MarkersFillStroke => "markers fill stroke",
            SvgPaintOrder.MarkersStrokeFill => "markers stroke fill",
            _ => "normal"
        };
    }

    internal static string ToCssText(SvgWhiteSpace whiteSpace)
    {
        return whiteSpace switch
        {
            SvgWhiteSpace.Pre => "pre",
            SvgWhiteSpace.NoWrap => "nowrap",
            SvgWhiteSpace.PreWrap => "pre-wrap",
            SvgWhiteSpace.BreakSpaces => "break-spaces",
            SvgWhiteSpace.PreLine => "pre-line",
            _ => "normal"
        };
    }

    internal static string ToCssText(SvgIsolation isolation)
    {
        return isolation == SvgIsolation.Isolate ? "isolate" : "auto";
    }

    internal static string ToCssText(SvgMixBlendMode mixBlendMode)
    {
        return mixBlendMode switch
        {
            SvgMixBlendMode.Multiply => "multiply",
            SvgMixBlendMode.Screen => "screen",
            SvgMixBlendMode.Overlay => "overlay",
            SvgMixBlendMode.Darken => "darken",
            SvgMixBlendMode.Lighten => "lighten",
            SvgMixBlendMode.ColorDodge => "color-dodge",
            SvgMixBlendMode.ColorBurn => "color-burn",
            SvgMixBlendMode.HardLight => "hard-light",
            SvgMixBlendMode.SoftLight => "soft-light",
            SvgMixBlendMode.Difference => "difference",
            SvgMixBlendMode.Exclusion => "exclusion",
            SvgMixBlendMode.Hue => "hue",
            SvgMixBlendMode.Saturation => "saturation",
            SvgMixBlendMode.Color => "color",
            SvgMixBlendMode.Luminosity => "luminosity",
            _ => "normal"
        };
    }

    private static void AddMissing(List<string> order, string phase)
    {
        if (!order.Contains(phase))
        {
            order.Add(phase);
        }
    }
}

public abstract partial class SvgElement
{
    internal SvgComputedStyleSnapshot ComputedStyle =>
        OwnerDocument is not null
            ? OwnerDocument.GetComputedStyle(this)
            : new SvgComputedStyleSnapshot(new SvgComputedStyleCache(), this);

    internal bool TryGetOwnCascadedStyleValue(string propertyName, out string value)
    {
        if (_styles.TryGetValue(propertyName, out var rules) && rules.Count > 0)
        {
            value = rules.Last().Value;
            return true;
        }

        foreach (var style in _styles)
        {
            if (string.Equals(style.Key, propertyName, StringComparison.OrdinalIgnoreCase) &&
                style.Value.Count > 0)
            {
                value = style.Value.Last().Value;
                return true;
            }
        }

        if (Attributes.TryGetValue(propertyName, out var attributeValue) &&
            attributeValue is not null)
        {
            value = ConvertAttributeValueToStyleText(attributeValue);
            return true;
        }

        if (CustomAttributes.TryGetValue(propertyName, out var customValue) &&
            customValue is not null)
        {
            value = customValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string ConvertAttributeValueToStyleText(object attributeValue)
    {
        switch (attributeValue)
        {
            case SvgPaintOrder paintOrder:
                return SvgComputedStyleMetadata.ToCssText(paintOrder);
            case SvgWhiteSpace whiteSpace:
                return SvgComputedStyleMetadata.ToCssText(whiteSpace);
            case SvgIsolation isolation:
                return SvgComputedStyleMetadata.ToCssText(isolation);
            case SvgMixBlendMode mixBlendMode:
                return SvgComputedStyleMetadata.ToCssText(mixBlendMode);
            default:
                return attributeValue.ToString() ?? string.Empty;
        }
    }
}

public partial class SvgDocument
{
    private SvgComputedStyleCache? _computedStyleCache;

    internal SvgComputedStyleSnapshot GetComputedStyle(SvgElement element)
    {
        _computedStyleCache ??= new SvgComputedStyleCache();
        return _computedStyleCache.GetOrCreate(element);
    }

    internal void InvalidateComputedStyleCache()
    {
        _computedStyleCache = null;
    }
}
