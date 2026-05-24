#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Svg.Pathing;

namespace Svg;

[Flags]
internal enum SvgCascadedStyleFeatureFlags
{
    None = 0,
    MarkerReference = 1,
    MixBlendMode = 2,
    Isolation = 4
}

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
        var hasWhiteSpaceCollapse = TryGetPropertyValue(SvgComputedStyleMetadata.WhiteSpaceCollapse, out var whiteSpaceCollapse);
        var hasTextWrapMode = TryGetPropertyValue(SvgComputedStyleMetadata.TextWrapMode, out var textWrapMode);
        var hasWhiteSpaceTrim = TryGetPropertyValue(SvgComputedStyleMetadata.WhiteSpaceTrim, out var whiteSpaceTrim);
        if ((hasWhiteSpaceCollapse &&
             !whiteSpaceCollapse.Equals(SvgComputedStyleMetadata.WhiteSpaceCollapse.InitialValue, StringComparison.OrdinalIgnoreCase)) ||
            (hasTextWrapMode &&
             !textWrapMode.Equals(SvgComputedStyleMetadata.TextWrapMode.InitialValue, StringComparison.OrdinalIgnoreCase)) ||
            (hasWhiteSpaceTrim &&
             !whiteSpaceTrim.Equals(SvgComputedStyleMetadata.WhiteSpaceTrim.InitialValue, StringComparison.OrdinalIgnoreCase)))
        {
            if (SvgComputedStyleMetadata.TryParseWhiteSpaceLonghands(
                hasWhiteSpaceCollapse ? whiteSpaceCollapse : SvgComputedStyleMetadata.WhiteSpaceCollapse.InitialValue!,
                hasTextWrapMode ? textWrapMode : SvgComputedStyleMetadata.TextWrapMode.InitialValue!,
                hasWhiteSpaceTrim ? whiteSpaceTrim : SvgComputedStyleMetadata.WhiteSpaceTrim.InitialValue!,
                out whiteSpace))
            {
                return true;
            }
        }

        if (TryGetPropertyValue(SvgComputedStyleMetadata.WhiteSpace, out var value))
        {
            return SvgComputedStyleMetadata.TryParseWhiteSpace(value, out whiteSpace);
        }

        if (!hasWhiteSpaceCollapse && !hasTextWrapMode && !hasWhiteSpaceTrim)
        {
            return false;
        }

        return SvgComputedStyleMetadata.TryParseWhiteSpaceLonghands(
            hasWhiteSpaceCollapse ? whiteSpaceCollapse : SvgComputedStyleMetadata.WhiteSpaceCollapse.InitialValue!,
            hasTextWrapMode ? textWrapMode : SvgComputedStyleMetadata.TextWrapMode.InitialValue!,
            hasWhiteSpaceTrim ? whiteSpaceTrim : SvgComputedStyleMetadata.WhiteSpaceTrim.InitialValue!,
            out whiteSpace);
    }

    public string TextOverflow => GetPropertyValueOrDefault(SvgComputedStyleMetadata.TextOverflow, "clip");

    public string WhiteSpaceCollapse => GetPropertyValueOrDefault(SvgComputedStyleMetadata.WhiteSpaceCollapse, "collapse");

    public string TextWrapMode => GetPropertyValueOrDefault(SvgComputedStyleMetadata.TextWrapMode, "wrap");

    public string WhiteSpaceTrim => GetPropertyValueOrDefault(SvgComputedStyleMetadata.WhiteSpaceTrim, "none");

    public string OverflowWrap => GetPropertyValueOrDefault(SvgComputedStyleMetadata.OverflowWrap, "normal");

    public string WordBreak => GetPropertyValueOrDefault(SvgComputedStyleMetadata.WordBreak, "normal");

    public string LineBreak => GetPropertyValueOrDefault(SvgComputedStyleMetadata.LineBreak, "auto");

    public string LineHeight => GetPropertyValueOrDefault(SvgComputedStyleMetadata.LineHeight, "normal");

    public string FontFeatureSettings => GetPropertyValueOrDefault(SvgComputedStyleMetadata.FontFeatureSettings, "normal");

    public string FontKerning => GetPropertyValueOrDefault(SvgComputedStyleMetadata.FontKerning, "auto");

    public string FontVariantLigatures => GetPropertyValueOrDefault(SvgComputedStyleMetadata.FontVariantLigatures, "normal");

    public string Direction => GetPropertyValueOrDefault(SvgComputedStyleMetadata.Direction, "ltr");

    public string UnicodeBidi => GetPropertyValueOrDefault(SvgComputedStyleMetadata.UnicodeBidi, "normal");

    public string? InlineSize => GetPropertyValueOrNull(SvgComputedStyleMetadata.InlineSize);

    public string? ShapeInside => GetPropertyValueOrNull(SvgComputedStyleMetadata.ShapeInside);

    public string? ShapeSubtract => GetPropertyValueOrNull(SvgComputedStyleMetadata.ShapeSubtract);

    public string? ShapePadding => GetPropertyValueOrNull(SvgComputedStyleMetadata.ShapePadding);

    public string? ShapeMargin => GetPropertyValueOrNull(SvgComputedStyleMetadata.ShapeMargin);

    public string? ShapeImageThreshold => GetPropertyValueOrNull(SvgComputedStyleMetadata.ShapeImageThreshold);

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

                if (TryResolveShorthandLonghand(metadata, normalizedShorthandValue, out var longhandValue))
                {
                    return longhandValue;
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

    private static bool TryResolveShorthandLonghand(
        SvgComputedStyleMetadata metadata,
        string shorthandValue,
        out string value)
    {
        value = string.Empty;
        if (metadata.ShorthandName is null ||
            !metadata.ShorthandName.Equals(SvgComputedStyleMetadata.WhiteSpace.Name, StringComparison.OrdinalIgnoreCase) ||
            !SvgComputedStyleMetadata.TryParseWhiteSpaceShorthandLonghands(
                shorthandValue,
                out var whiteSpaceCollapse,
                out var textWrapMode,
                out var whiteSpaceTrim))
        {
            return false;
        }

        if (metadata.Name.Equals(SvgComputedStyleMetadata.WhiteSpaceCollapse.Name, StringComparison.OrdinalIgnoreCase))
        {
            value = whiteSpaceCollapse;
            return true;
        }

        if (metadata.Name.Equals(SvgComputedStyleMetadata.TextWrapMode.Name, StringComparison.OrdinalIgnoreCase))
        {
            value = textWrapMode;
            return true;
        }

        if (metadata.Name.Equals(SvgComputedStyleMetadata.WhiteSpaceTrim.Name, StringComparison.OrdinalIgnoreCase))
        {
            value = whiteSpaceTrim;
            return true;
        }

        return false;
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

    public static readonly SvgComputedStyleMetadata WhiteSpaceCollapse = new(
        "white-space-collapse",
        inherited: true,
        initialValue: "collapse",
        IsValidWhiteSpaceCollapse,
        "white-space");

    public static readonly SvgComputedStyleMetadata TextWrapMode = new(
        "text-wrap-mode",
        inherited: true,
        initialValue: "wrap",
        static value => IsCssIdentifier(value, "wrap", "nowrap"),
        "white-space");

    public static readonly SvgComputedStyleMetadata WhiteSpaceTrim = new(
        "white-space-trim",
        inherited: false,
        initialValue: "none",
        IsValidWhiteSpaceTrim,
        "white-space");

    public static readonly SvgComputedStyleMetadata TextOverflow = new(
        "text-overflow",
        inherited: false,
        initialValue: "clip",
        IsValidTextOverflow);

    public static readonly SvgComputedStyleMetadata OverflowWrap = new(
        "overflow-wrap",
        inherited: true,
        initialValue: "normal",
        static value => IsCssIdentifier(value, "normal", "anywhere", "break-word"));

    public static readonly SvgComputedStyleMetadata WordBreak = new(
        "word-break",
        inherited: true,
        initialValue: "normal",
        static value => IsCssIdentifier(value, "normal", "break-all", "break-word", "keep-all"));

    public static readonly SvgComputedStyleMetadata LineBreak = new(
        "line-break",
        inherited: true,
        initialValue: "auto",
        static value => IsCssIdentifier(value, "auto", "loose", "normal", "strict", "anywhere"));

    public static readonly SvgComputedStyleMetadata LineHeight = new(
        "line-height",
        inherited: true,
        initialValue: "normal",
        IsValidLineHeight);

    public static readonly SvgComputedStyleMetadata FontFeatureSettings = new(
        "font-feature-settings",
        inherited: true,
        initialValue: "normal",
        IsValidFontFeatureSettings);

    public static readonly SvgComputedStyleMetadata FontKerning = new(
        "font-kerning",
        inherited: true,
        initialValue: "auto",
        static value => IsCssIdentifier(value, "auto", "normal", "none"));

    public static readonly SvgComputedStyleMetadata FontVariantLigatures = new(
        "font-variant-ligatures",
        inherited: true,
        initialValue: "normal",
        IsValidFontVariantLigatures);

    public static readonly SvgComputedStyleMetadata Direction = new(
        "direction",
        inherited: true,
        initialValue: "ltr",
        static value => IsCssIdentifier(value, "ltr", "rtl"));

    public static readonly SvgComputedStyleMetadata UnicodeBidi = new(
        "unicode-bidi",
        inherited: true,
        initialValue: "normal",
        static value => IsCssIdentifier(value, "normal", "embed", "isolate", "bidi-override", "isolate-override", "plaintext"));

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

    public static readonly SvgComputedStyleMetadata ShapePadding = new(
        "shape-padding",
        inherited: false,
        initialValue: null,
        IsValidNonNegativeSvgUnit);

    public static readonly SvgComputedStyleMetadata ShapeMargin = new(
        "shape-margin",
        inherited: false,
        initialValue: null,
        IsValidNonNegativeSvgUnit);

    public static readonly SvgComputedStyleMetadata ShapeImageThreshold = new(
        "shape-image-threshold",
        inherited: false,
        initialValue: null,
        IsValidShapeImageThreshold);

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
            "white-space-collapse" => WhiteSpaceCollapse,
            "text-wrap-mode" => TextWrapMode,
            "white-space-trim" => WhiteSpaceTrim,
            "text-overflow" => TextOverflow,
            "overflow-wrap" => OverflowWrap,
            "word-break" => WordBreak,
            "line-break" => LineBreak,
            "line-height" => LineHeight,
            "font-feature-settings" => FontFeatureSettings,
            "font-kerning" => FontKerning,
            "font-variant-ligatures" => FontVariantLigatures,
            "direction" => Direction,
            "unicode-bidi" => UnicodeBidi,
            "inline-size" => InlineSize,
            "shape-inside" => ShapeInside,
            "shape-subtract" => ShapeSubtract,
            "shape-padding" => ShapePadding,
            "shape-margin" => ShapeMargin,
            "shape-image-threshold" => ShapeImageThreshold,
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
                IsValidSvgUnitOrAuto),
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

    private static bool IsValidLineHeight(string value)
    {
        if (IsCssIdentifier(value, "normal"))
        {
            return true;
        }

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return number > 0f;
        }

        return IsValidPositiveSvgUnit(value);
    }

    private static bool IsValidFontFeatureSettings(string value)
    {
        if (IsCssIdentifier(value, "normal"))
        {
            return true;
        }

        var parts = SplitCssCommaList(value);
        if (parts.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < parts.Count; i++)
        {
            if (!IsValidFontFeatureSetting(parts[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidFontFeatureSetting(string value)
    {
        value = value.Trim();
        if (value.Length < 4)
        {
            return false;
        }

        var cursor = 0;
        string tag;
        if (value[0] is '\'' or '"')
        {
            var quote = value[0];
            var endQuote = value.IndexOf(quote, 1);
            if (endQuote != 5)
            {
                return false;
            }

            tag = value.Substring(1, 4);
            cursor = endQuote + 1;
        }
        else
        {
            if (value.Length < 4)
            {
                return false;
            }

            tag = value.Substring(0, 4);
            cursor = 4;
        }

        if (!IsOpenTypeFeatureTag(tag))
        {
            return false;
        }

        var suffix = value.Substring(cursor).Trim();
        if (suffix.Length == 0)
        {
            return true;
        }

        if (suffix.StartsWith("=", StringComparison.Ordinal))
        {
            suffix = suffix.Substring(1).Trim();
        }

        return suffix.Equals("on", StringComparison.OrdinalIgnoreCase) ||
               suffix.Equals("off", StringComparison.OrdinalIgnoreCase) ||
               uint.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsOpenTypeFeatureTag(string tag)
    {
        if (tag.Length != 4)
        {
            return false;
        }

        for (var i = 0; i < tag.Length; i++)
        {
            if (tag[i] < 0x20 || tag[i] > 0x7E)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidFontVariantLigatures(string value)
    {
        if (IsCssIdentifier(value, "normal", "none"))
        {
            return true;
        }

        var tokens = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < tokens.Length; i++)
        {
            if (!IsCssIdentifier(
                    tokens[i],
                    "common-ligatures",
                    "no-common-ligatures",
                    "discretionary-ligatures",
                    "no-discretionary-ligatures",
                    "historical-ligatures",
                    "no-historical-ligatures",
                    "contextual",
                    "no-contextual"))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> SplitCssCommaList(string value)
    {
        var parts = new List<string>();
        var start = 0;
        var quote = '\0';
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch is '\'' or '"')
            {
                quote = ch;
                continue;
            }

            if (ch != ',')
            {
                continue;
            }

            var part = value.Substring(start, i - start).Trim();
            if (part.Length > 0)
            {
                parts.Add(part);
            }

            start = i + 1;
        }

        var lastPart = value.Substring(start).Trim();
        if (lastPart.Length > 0)
        {
            parts.Add(lastPart);
        }

        return parts;
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

    private static bool IsValidNonNegativeSvgUnit(string value)
    {
        try
        {
            var unit = SvgUnitConverter.Parse(value.AsSpan());
            return unit.Value >= 0f;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidShapeImageThreshold(string value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
               number >= 0f &&
               number <= 1f;
    }

    private static bool IsValidPositiveSvgUnit(string value)
    {
        try
        {
            var unit = SvgUnitConverter.Parse(value.AsSpan());
            return unit.Value > 0f;
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
        return TryParseWhiteSpaceShorthandLonghands(value, out _, out _, out _);
    }

    private static bool IsValidWhiteSpaceCollapse(string value)
    {
        return IsCssIdentifier(
            value,
            "collapse",
            "discard",
            "preserve",
            "preserve-breaks",
            "preserve-spaces",
            "break-spaces");
    }

    private static bool IsValidWhiteSpaceTrim(string value)
    {
        return TryParseWhiteSpaceTrim(value, out _);
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
        if (!TryParseWhiteSpaceShorthandLonghands(value, out var collapse, out var wrap, out var trim))
        {
            whiteSpace = SvgWhiteSpace.Normal;
            return false;
        }

        return TryParseWhiteSpaceLonghands(collapse, wrap, trim, out whiteSpace);
    }

    internal static bool TryParseWhiteSpaceLonghands(string whiteSpaceCollapse, string textWrapMode, out SvgWhiteSpace whiteSpace)
    {
        return TryParseWhiteSpaceLonghands(whiteSpaceCollapse, textWrapMode, WhiteSpaceTrim.InitialValue!, out whiteSpace);
    }

    internal static bool TryParseWhiteSpaceLonghands(
        string whiteSpaceCollapse,
        string textWrapMode,
        string whiteSpaceTrim,
        out SvgWhiteSpace whiteSpace)
    {
        var collapse = whiteSpaceCollapse.Trim();
        var wrap = textWrapMode.Trim();
        var trim = whiteSpaceTrim.Trim();
        if (!IsValidWhiteSpaceCollapse(collapse) ||
            !IsCssIdentifier(wrap, "wrap", "nowrap") ||
            !IsValidWhiteSpaceTrim(trim) ||
            !IsCssIdentifier(trim, "none"))
        {
            whiteSpace = SvgWhiteSpace.Normal;
            return false;
        }

        if (IsCssIdentifier(collapse, "collapse"))
        {
            whiteSpace = IsCssIdentifier(wrap, "nowrap") ? SvgWhiteSpace.NoWrap : SvgWhiteSpace.Normal;
            return true;
        }

        if (IsCssIdentifier(collapse, "preserve"))
        {
            whiteSpace = IsCssIdentifier(wrap, "nowrap") ? SvgWhiteSpace.Pre : SvgWhiteSpace.PreWrap;
            return true;
        }

        if (IsCssIdentifier(collapse, "preserve-breaks") &&
            IsCssIdentifier(wrap, "wrap"))
        {
            whiteSpace = SvgWhiteSpace.PreLine;
            return true;
        }

        if (IsCssIdentifier(collapse, "break-spaces") &&
            IsCssIdentifier(wrap, "wrap"))
        {
            whiteSpace = SvgWhiteSpace.BreakSpaces;
            return true;
        }

        whiteSpace = SvgWhiteSpace.Normal;
        return false;
    }

    internal static bool TryParseWhiteSpaceShorthandLonghands(
        string value,
        out string whiteSpaceCollapse,
        out string textWrapMode,
        out string whiteSpaceTrim)
    {
        var normalized = value.Trim();
        switch (normalized)
        {
            case var token when string.Equals(token, "normal", StringComparison.OrdinalIgnoreCase):
                whiteSpaceCollapse = "collapse";
                textWrapMode = "wrap";
                whiteSpaceTrim = "none";
                return true;
            case var token when string.Equals(token, "pre", StringComparison.OrdinalIgnoreCase):
                whiteSpaceCollapse = "preserve";
                textWrapMode = "nowrap";
                whiteSpaceTrim = "none";
                return true;
            case var token when string.Equals(token, "nowrap", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(token, "no-wrap", StringComparison.OrdinalIgnoreCase):
                whiteSpaceCollapse = "collapse";
                textWrapMode = "nowrap";
                whiteSpaceTrim = "none";
                return true;
            case var token when string.Equals(token, "pre-wrap", StringComparison.OrdinalIgnoreCase):
                whiteSpaceCollapse = "preserve";
                textWrapMode = "wrap";
                whiteSpaceTrim = "none";
                return true;
            case var token when string.Equals(token, "break-spaces", StringComparison.OrdinalIgnoreCase):
                whiteSpaceCollapse = "break-spaces";
                textWrapMode = "wrap";
                whiteSpaceTrim = "none";
                return true;
            case var token when string.Equals(token, "pre-line", StringComparison.OrdinalIgnoreCase):
                whiteSpaceCollapse = "preserve-breaks";
                textWrapMode = "wrap";
                whiteSpaceTrim = "none";
                return true;
        }

        whiteSpaceCollapse = WhiteSpaceCollapse.InitialValue!;
        textWrapMode = TextWrapMode.InitialValue!;
        whiteSpaceTrim = WhiteSpaceTrim.InitialValue!;
        var tokens = normalized.Split([' ', '\t', '\r', '\n', '\f'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is 0 or > 5)
        {
            return false;
        }

        string? collapse = null;
        string? wrap = null;
        var trim = SvgWhiteSpaceTrim.None;
        var hasTrim = false;
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (IsValidWhiteSpaceCollapse(token))
            {
                if (collapse is not null)
                {
                    return false;
                }

                collapse = token;
                continue;
            }

            if (IsCssIdentifier(token, "wrap", "nowrap"))
            {
                if (wrap is not null)
                {
                    return false;
                }

                wrap = token;
                continue;
            }

            if (TryParseWhiteSpaceTrimToken(token, ref trim, ref hasTrim))
            {
                continue;
            }

            return false;
        }

        whiteSpaceCollapse = collapse ?? WhiteSpaceCollapse.InitialValue!;
        textWrapMode = wrap ?? TextWrapMode.InitialValue!;
        whiteSpaceTrim = ToCssText(trim);
        return true;
    }

    private static bool TryParseWhiteSpaceTrim(string value, out SvgWhiteSpaceTrim trim)
    {
        trim = SvgWhiteSpaceTrim.None;
        var tokens = value.Split([' ', '\t', '\r', '\n', '\f'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var hasTrim = false;
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!TryParseWhiteSpaceTrimToken(tokens[i], ref trim, ref hasTrim))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseWhiteSpaceTrimToken(string token, ref SvgWhiteSpaceTrim trim, ref bool hasTrim)
    {
        if (IsCssIdentifier(token, "none"))
        {
            if (hasTrim)
            {
                return false;
            }

            hasTrim = true;
            trim = SvgWhiteSpaceTrim.None;
            return true;
        }

        var flag = IsCssIdentifier(token, "discard-before")
            ? SvgWhiteSpaceTrim.DiscardBefore
            : IsCssIdentifier(token, "discard-after")
                ? SvgWhiteSpaceTrim.DiscardAfter
                : IsCssIdentifier(token, "discard-inner")
                    ? SvgWhiteSpaceTrim.DiscardInner
                    : SvgWhiteSpaceTrim.None;
        if (flag == SvgWhiteSpaceTrim.None ||
            trim.HasFlag(flag) ||
            (hasTrim && trim == SvgWhiteSpaceTrim.None))
        {
            return false;
        }

        hasTrim = true;
        trim |= flag;
        return true;
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

    internal static string ToCssText(SvgWhiteSpaceTrim trim)
    {
        if (trim == SvgWhiteSpaceTrim.None)
        {
            return "none";
        }

        var parts = new List<string>(3);
        if (trim.HasFlag(SvgWhiteSpaceTrim.DiscardBefore))
        {
            parts.Add("discard-before");
        }

        if (trim.HasFlag(SvgWhiteSpaceTrim.DiscardAfter))
        {
            parts.Add("discard-after");
        }

        if (trim.HasFlag(SvgWhiteSpaceTrim.DiscardInner))
        {
            parts.Add("discard-inner");
        }

        return string.Join(" ", parts);
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
    private const SvgCascadedStyleFeatureFlags AllCascadedStyleFeatureFlags =
        SvgCascadedStyleFeatureFlags.MarkerReference |
        SvgCascadedStyleFeatureFlags.MixBlendMode |
        SvgCascadedStyleFeatureFlags.Isolation;

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

    internal bool TryGetOwnCascadedStyleDeclarationValue(string propertyName, out string value)
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

        value = string.Empty;
        return false;
    }

    internal bool TryGetOwnCascadedCssDeclarationValue(string propertyName, out string value)
    {
        if (_styles.TryGetValue(propertyName, out var rules) && TryGetHighestCssDeclaration(rules, out value))
        {
            return true;
        }

        foreach (var style in _styles)
        {
            if (string.Equals(style.Key, propertyName, StringComparison.OrdinalIgnoreCase) &&
                TryGetHighestCssDeclaration(style.Value, out value))
            {
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetHighestCssDeclaration(SortedDictionary<int, string> rules, out string value)
    {
        foreach (var rule in rules.Reverse())
        {
            if (rule.Key != StyleSpecificity_PresAttribute)
            {
                value = rule.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    internal SvgCascadedStyleFeatureFlags GetOwnCascadedStyleFeatureFlags(
        SvgCascadedStyleFeatureFlags requestedFlags = AllCascadedStyleFeatureFlags)
    {
        if (requestedFlags == SvgCascadedStyleFeatureFlags.None)
        {
            return SvgCascadedStyleFeatureFlags.None;
        }

        var flags = SvgCascadedStyleFeatureFlags.None;

        if (_styles.Count > 0)
        {
            if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.MarkerReference))
            {
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "marker");
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "marker-start");
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "marker-mid");
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "marker-end");
            }

            if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.MixBlendMode))
            {
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "mix-blend-mode");
            }

            if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Isolation))
            {
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "isolation");
            }

            if (flags == requestedFlags)
            {
                return flags;
            }

            if (flags != requestedFlags)
            {
                foreach (var style in _styles)
                {
                    if (style.Value.Count > 0)
                    {
                        flags = AddCascadedStyleFeatureFlag(flags, requestedFlags, style.Key, style.Value.Last().Value);
                        if (flags == requestedFlags)
                        {
                            return flags;
                        }
                    }
                }
            }
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.MarkerReference))
        {
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "marker");
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "marker-start");
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "marker-mid");
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "marker-end");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.MixBlendMode))
        {
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "mix-blend-mode");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Isolation))
        {
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "isolation");
        }

        if (flags == requestedFlags)
        {
            return flags;
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.MarkerReference))
        {
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "marker");
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "marker-start");
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "marker-mid");
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "marker-end");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.MixBlendMode))
        {
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "mix-blend-mode");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Isolation))
        {
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "isolation");
        }

        return flags;
    }

    internal SvgCascadedStyleFeatureFlags GetSubtreeCascadedStyleFeatureFlags(
        SvgCascadedStyleFeatureFlags requestedFlags = AllCascadedStyleFeatureFlags)
    {
        if (requestedFlags == SvgCascadedStyleFeatureFlags.None)
        {
            return SvgCascadedStyleFeatureFlags.None;
        }

        var flags = GetOwnCascadedStyleFeatureFlags(requestedFlags);
        if (flags == requestedFlags)
        {
            return flags;
        }

        for (var i = 0; i < Children.Count; i++)
        {
            var remainingFlags = requestedFlags & ~flags;
            flags |= Children[i].GetSubtreeCascadedStyleFeatureFlags(remainingFlags);
            if (flags == requestedFlags)
            {
                return flags;
            }
        }

        return flags;
    }

    private SvgCascadedStyleFeatureFlags AddStyleRulesFeatureFlag(
        SvgCascadedStyleFeatureFlags flags,
        SvgCascadedStyleFeatureFlags requestedFlags,
        string propertyName)
    {
        return _styles.TryGetValue(propertyName, out var rules) && rules.Count > 0
            ? AddCascadedStyleFeatureFlag(flags, requestedFlags, propertyName, rules.Last().Value)
            : flags;
    }

    private SvgCascadedStyleFeatureFlags AddAttributeFeatureFlag(
        SvgCascadedStyleFeatureFlags flags,
        SvgCascadedStyleFeatureFlags requestedFlags,
        string propertyName)
    {
        return Attributes.TryGetValue(propertyName, out var attributeValue) && attributeValue is not null
            ? AddCascadedStyleFeatureFlag(flags, requestedFlags, propertyName, ConvertAttributeValueToStyleText(attributeValue))
            : flags;
    }

    private SvgCascadedStyleFeatureFlags AddCustomAttributeFeatureFlag(
        SvgCascadedStyleFeatureFlags flags,
        SvgCascadedStyleFeatureFlags requestedFlags,
        string propertyName)
    {
        return CustomAttributes.TryGetValue(propertyName, out var customValue) && customValue is not null
            ? AddCascadedStyleFeatureFlag(flags, requestedFlags, propertyName, customValue)
            : flags;
    }

    private static SvgCascadedStyleFeatureFlags AddCascadedStyleFeatureFlag(
        SvgCascadedStyleFeatureFlags flags,
        SvgCascadedStyleFeatureFlags requestedFlags,
        string propertyName,
        string value)
    {
        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.MarkerReference) &&
            !HasFeatureFlag(flags, SvgCascadedStyleFeatureFlags.MarkerReference) &&
            IsMarkerReferenceProperty(propertyName) &&
            IsMarkerReferenceDeclarationCandidateValue(value))
        {
            flags |= SvgCascadedStyleFeatureFlags.MarkerReference;
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.MixBlendMode) &&
            !HasFeatureFlag(flags, SvgCascadedStyleFeatureFlags.MixBlendMode) &&
            string.Equals(propertyName, "mix-blend-mode", StringComparison.OrdinalIgnoreCase) &&
            IsDeclaredComputedStyleFeatureCandidate(value, "normal"))
        {
            flags |= SvgCascadedStyleFeatureFlags.MixBlendMode;
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Isolation) &&
            !HasFeatureFlag(flags, SvgCascadedStyleFeatureFlags.Isolation) &&
            string.Equals(propertyName, "isolation", StringComparison.OrdinalIgnoreCase) &&
            IsDeclaredComputedStyleFeatureCandidate(value, "auto"))
        {
            flags |= SvgCascadedStyleFeatureFlags.Isolation;
        }

        return flags;
    }

    private static bool HasFeatureFlag(
        SvgCascadedStyleFeatureFlags flags,
        SvgCascadedStyleFeatureFlags flag)
    {
        return (flags & flag) != 0;
    }

    private static bool IsMarkerReferenceProperty(string propertyName)
    {
        return string.Equals(propertyName, "marker", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(propertyName, "marker-start", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(propertyName, "marker-mid", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(propertyName, "marker-end", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMarkerReferenceDeclarationCandidateValue(string value)
    {
        var normalizedValue = value.Trim();
        return normalizedValue.Length > 0 &&
               !string.Equals(normalizedValue, "none", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(normalizedValue, "initial", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(normalizedValue, "inherit", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(normalizedValue, "unset", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeclaredComputedStyleFeatureCandidate(string value, string initialValue)
    {
        var normalizedValue = value.Trim();
        return normalizedValue.Length > 0 &&
               !string.Equals(normalizedValue, "initial", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(normalizedValue, "unset", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(normalizedValue, initialValue, StringComparison.OrdinalIgnoreCase);
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
    private SvgComputedStyleCache? _temporaryParentComputedStyleCache;
    private SvgCascadedStyleFeatureFlags? _cascadedStyleFeatureFlags;
    private int _temporaryParentComputedStyleScopeDepth;

    internal SvgComputedStyleSnapshot GetComputedStyle(SvgElement element)
    {
        if (_temporaryParentComputedStyleScopeDepth > 0)
        {
            _temporaryParentComputedStyleCache ??= new SvgComputedStyleCache();
            return _temporaryParentComputedStyleCache.GetOrCreate(element);
        }

        _computedStyleCache ??= new SvgComputedStyleCache();
        return _computedStyleCache.GetOrCreate(element);
    }

    internal IDisposable BeginComputedStyleTemporaryParentScope()
    {
        _temporaryParentComputedStyleScopeDepth++;
        _temporaryParentComputedStyleCache = null;
        return new SvgComputedStyleTemporaryParentScope(this);
    }

    internal void InvalidateComputedStyleCache()
    {
        _computedStyleCache = null;
        _temporaryParentComputedStyleCache = null;
        _cascadedStyleFeatureFlags = null;
    }

    internal SvgCascadedStyleFeatureFlags GetCascadedStyleFeatureFlags(SvgCascadedStyleFeatureFlags requestedFlags)
    {
        _cascadedStyleFeatureFlags ??= GetSubtreeCascadedStyleFeatureFlags();
        return _cascadedStyleFeatureFlags.Value & requestedFlags;
    }

    private void EndComputedStyleTemporaryParentScope()
    {
        if (_temporaryParentComputedStyleScopeDepth > 0)
        {
            _temporaryParentComputedStyleScopeDepth--;
        }

        _temporaryParentComputedStyleCache = null;
    }

    private sealed class SvgComputedStyleTemporaryParentScope : IDisposable
    {
        private SvgDocument? _document;

        public SvgComputedStyleTemporaryParentScope(SvgDocument document)
        {
            _document = document;
        }

        public void Dispose()
        {
            var document = _document;
            if (document is null)
            {
                return;
            }

            _document = null;
            document.EndComputedStyleTemporaryParentScope();
        }
    }
}
