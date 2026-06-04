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
    Isolation = 4,
    TextOpenType = 8,
    ClipPath = 16,
    Mask = 32,
    Filter = 64,
    Cursor = 128,
    EnableBackground = 256
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
        IsValidFilter);

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

    private static bool IsValidFilter(string value)
    {
        if (IsCssIdentifier(value, "none"))
        {
            return true;
        }

        var index = 0;
        var parsedItem = false;
        while (index < value.Length)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            if (index >= value.Length)
            {
                break;
            }

            if (TryConsumeCssUrl(value, ref index))
            {
                parsedItem = true;
                continue;
            }

            var nameStart = index;
            while (index < value.Length && (char.IsLetter(value[index]) || value[index] == '-'))
            {
                index++;
            }

            if (index == nameStart)
            {
                return false;
            }

            var name = value.Substring(nameStart, index - nameStart);
            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            if (index >= value.Length || value[index] != '(')
            {
                return false;
            }

            index++;
            var argsStart = index;
            var depth = 1;
            while (index < value.Length && depth > 0)
            {
                if (value[index] == '(')
                {
                    depth++;
                }
                else if (value[index] == ')')
                {
                    depth--;
                }

                index++;
            }

            if (depth != 0)
            {
                return false;
            }

            var args = value.Substring(argsStart, index - argsStart - 1).Trim();
            if (!IsValidCssFilterFunction(name, args))
            {
                return false;
            }

            parsedItem = true;
        }

        return parsedItem;
    }

    private static bool IsValidCssFilterFunction(string name, string args)
    {
        if (IsCssIdentifier(name, "blur"))
        {
            return IsValidCssFilterLengthArgument(args, allowEmpty: true, allowPercentage: false, allowNegative: false);
        }

        if (IsCssIdentifier(name, "brightness") ||
            IsCssIdentifier(name, "contrast") ||
            IsCssIdentifier(name, "grayscale") ||
            IsCssIdentifier(name, "invert") ||
            IsCssIdentifier(name, "opacity") ||
            IsCssIdentifier(name, "saturate") ||
            IsCssIdentifier(name, "sepia"))
        {
            return IsValidCssFilterFactorArgument(args, allowEmpty: true, allowNegative: false);
        }

        if (IsCssIdentifier(name, "hue-rotate"))
        {
            return IsValidCssFilterAngleArgument(args, allowEmpty: true);
        }

        return IsCssIdentifier(name, "drop-shadow") && IsValidCssDropShadowFilter(args);
    }

    private static bool IsValidCssFilterLengthArgument(string value, bool allowEmpty, bool allowPercentage, bool allowNegative)
    {
        value = value.Trim();
        if (value.Length == 0)
        {
            return allowEmpty;
        }

        if (ContainsVarFunction(value) || IsKnownLengthFunction(value))
        {
            return true;
        }

        try
        {
            var unit = SvgUnitConverter.Parse(value.AsSpan());
            return (allowPercentage || unit.Type != SvgUnitType.Percentage) &&
                   (allowNegative || unit.Value >= 0f) &&
                   IsFinite(unit.Value);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidCssFilterFactorArgument(string value, bool allowEmpty, bool allowNegative)
    {
        value = value.Trim();
        if (value.Length == 0)
        {
            return allowEmpty;
        }

        if (ContainsVarFunction(value) || IsKnownLengthFunction(value))
        {
            return true;
        }

        var isPercentage = value.EndsWith("%", StringComparison.Ordinal);
        if (isPercentage)
        {
            value = value.Substring(0, value.Length - 1).Trim();
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
               IsFinite(number) &&
               (allowNegative || number >= 0f);
    }

    private static bool IsValidCssFilterAngleArgument(string value, bool allowEmpty)
    {
        value = value.Trim();
        if (value.Length == 0)
        {
            return allowEmpty;
        }

        if (ContainsVarFunction(value) || IsKnownLengthFunction(value))
        {
            return true;
        }

        if (value.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 3);
        }
        else if (value.EndsWith("rad", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 3);
        }
        else if (value.EndsWith("grad", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 4);
        }
        else if (value.EndsWith("turn", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 4);
        }

        return float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
               IsFinite(number);
    }

    private static bool IsValidCssDropShadowFilter(string args)
    {
        var tokens = SplitCssFilterArgs(args);
        if (tokens.Count < 2)
        {
            return false;
        }

        var lengths = 0;
        var colors = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (lengths < 3 &&
                IsValidCssFilterLengthArgument(token, allowEmpty: false, allowPercentage: false, allowNegative: lengths < 2))
            {
                lengths++;
                continue;
            }

            if (!IsValidCssFilterColorArgument(token))
            {
                return false;
            }

            colors++;
            if (colors > 1)
            {
                return false;
            }
        }

        return lengths >= 2;
    }

    private static bool IsValidCssFilterColorArgument(string value)
    {
        value = value.Trim();
        if (value.Length == 0)
        {
            return false;
        }

        if (ContainsVarFunction(value))
        {
            return true;
        }

        if (string.Equals(value, "currentColor", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return SvgPaintServerFactory.TryParseCssConcreteColor(value, out _);
    }

    private static bool IsValidCssHexColorWithAlpha(string value)
    {
        if (value.Length != 5 && value.Length != 9)
        {
            return false;
        }

        if (value[0] != '#')
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!Uri.IsHexDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidCssFunctionalColorArgument(string value)
    {
        return IsValidCssRgbColorFunction(value) ||
               IsValidCssHslColorFunction(value);
    }

    private static bool IsValidCssRgbColorFunction(string value)
    {
        if (!TryGetCssFunctionContent(value, "rgb", "rgba", out var content) ||
            !TrySplitCssColorComponents(content, out var components, out var alpha) ||
            components.Count != 3)
        {
            return false;
        }

        return IsValidCssRgbComponent(components[0]) &&
               IsValidCssRgbComponent(components[1]) &&
               IsValidCssRgbComponent(components[2]) &&
               IsValidCssAlpha(alpha);
    }

    private static bool IsValidCssHslColorFunction(string value)
    {
        if (!TryGetCssFunctionContent(value, "hsl", "hsla", out var content) ||
            !TrySplitCssColorComponents(content, out var components, out var alpha) ||
            components.Count != 3)
        {
            return false;
        }

        return IsValidCssFilterAngleArgument(components[0], allowEmpty: false) &&
               IsValidCssPercentage(components[1]) &&
               IsValidCssPercentage(components[2]) &&
               IsValidCssAlpha(alpha);
    }

    private static bool TryGetCssFunctionContent(string value, string name, string alias, out string content)
    {
        content = string.Empty;
        var openParenthesis = value.IndexOf('(');
        if (openParenthesis <= 0 ||
            !TryFindCssFunctionEnd(value, openParenthesis, out var closeParenthesis) ||
            closeParenthesis != value.Length - 1)
        {
            return false;
        }

        var functionName = value.Substring(0, openParenthesis).Trim();
        if (!functionName.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            !functionName.Equals(alias, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        content = value.Substring(openParenthesis + 1, closeParenthesis - openParenthesis - 1).Trim();
        return content.Length > 0;
    }

    private static bool TryFindCssFunctionEnd(string value, int openParenthesisIndex, out int closeParenthesisIndex)
    {
        closeParenthesisIndex = -1;
        var quote = '\0';
        var escape = false;
        var depth = 0;
        for (var i = openParenthesisIndex; i < value.Length; i++)
        {
            var ch = value[i];
            if (quote != '\0')
            {
                if (escape)
                {
                    escape = false;
                }
                else if (ch == '\\')
                {
                    escape = true;
                }
                else if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                quote = ch;
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')')
            {
                depth--;
                if (depth == 0)
                {
                    closeParenthesisIndex = i;
                    return true;
                }

                if (depth < 0)
                {
                    return false;
                }
            }
        }

        return false;
    }

    private static bool TrySplitCssColorComponents(string value, out List<string> components, out string? alpha)
    {
        components = new List<string>(3);
        alpha = null;

        var commaParts = SplitTopLevelCssArguments(value, ',');
        if (commaParts.Count > 1)
        {
            if (commaParts.Count != 3 && commaParts.Count != 4)
            {
                return false;
            }

            AddFirstThree(components, commaParts);
            alpha = commaParts.Count == 4 ? commaParts[3] : null;
            return true;
        }

        var tokens = SplitCssColorSpaceTokens(value);
        var slashIndex = tokens.IndexOf("/");
        if (slashIndex >= 0)
        {
            if (slashIndex != 3 ||
                tokens.Count != 5 ||
                tokens.LastIndexOf("/") != slashIndex)
            {
                return false;
            }

            AddFirstThree(components, tokens);
            alpha = tokens[4];
            return true;
        }

        if (tokens.Count != 3)
        {
            return false;
        }

        components.AddRange(tokens);
        return true;
    }

    private static void AddFirstThree(List<string> target, List<string> source)
    {
        target.Add(source[0]);
        target.Add(source[1]);
        target.Add(source[2]);
    }

    private static List<string> SplitTopLevelCssArguments(string value, char separator)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
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

            if (ch == '"' || ch == '\'')
            {
                quote = ch;
                continue;
            }

            if (ch == '(')
            {
                depth++;
                continue;
            }

            if (ch == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (ch == separator && depth == 0)
            {
                parts.Add(value.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        parts.Add(value.Substring(start).Trim());
        return parts;
    }

    private static List<string> SplitCssColorSpaceTokens(string value)
    {
        var tokens = new List<string>();
        var start = -1;
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')' && depth > 0)
            {
                depth--;
            }

            if ((char.IsWhiteSpace(ch) || ch == '/') && depth == 0)
            {
                if (start >= 0)
                {
                    tokens.Add(value.Substring(start, i - start));
                    start = -1;
                }

                if (ch == '/')
                {
                    tokens.Add("/");
                }

                continue;
            }

            if (start < 0)
            {
                start = i;
            }
        }

        if (start >= 0)
        {
            tokens.Add(value.Substring(start));
        }

        return tokens;
    }

    private static bool IsValidCssRgbComponent(string value)
    {
        var componentText = value.Trim();
        if (componentText.EndsWith("%", StringComparison.Ordinal))
        {
            componentText = componentText.Substring(0, componentText.Length - 1).Trim();
        }

        return float.TryParse(componentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
               IsFinite(parsed);
    }

    private static bool IsValidCssPercentage(string value)
    {
        value = value.Trim();
        if (!value.EndsWith("%", StringComparison.Ordinal))
        {
            return false;
        }

        value = value.Substring(0, value.Length - 1).Trim();
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
               IsFinite(parsed);
    }

    private static bool IsValidCssAlpha(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var alphaText = value!.Trim();
        if (alphaText.EndsWith("%", StringComparison.Ordinal))
        {
            alphaText = alphaText.Substring(0, alphaText.Length - 1).Trim();
        }

        return float.TryParse(alphaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
               IsFinite(parsed);
    }

    private static List<string> SplitCssFilterArgs(string args)
    {
        var tokens = new List<string>();
        var start = -1;
        var depth = 0;

        for (var i = 0; i < args.Length; i++)
        {
            var ch = args[i];
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')' && depth > 0)
            {
                depth--;
            }

            if (char.IsWhiteSpace(ch) && depth == 0)
            {
                if (start >= 0)
                {
                    tokens.Add(args.Substring(start, i - start));
                    start = -1;
                }

                continue;
            }

            if (start < 0)
            {
                start = i;
            }
        }

        if (start >= 0)
        {
            tokens.Add(args.Substring(start));
        }

        return tokens;
    }

    private static bool TryConsumeCssUrl(string value, ref int index)
    {
        if (value.Length - index < 4 ||
            !value.Substring(index, 4).Equals("url(", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var start = index + 4;
        var current = start;
        var quote = '\0';
        while (current < value.Length)
        {
            var ch = value[current];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                current++;
                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                quote = ch;
                current++;
                continue;
            }

            if (ch == ')')
            {
                var inner = value.Substring(start, current - start).Trim();
                if (IsQuotedString(inner))
                {
                    inner = inner.Substring(1, inner.Length - 2).Trim();
                }

                if (inner.Length <= 0 || !Uri.TryCreate(inner, UriKind.RelativeOrAbsolute, out _))
                {
                    return false;
                }

                index = current + 1;
                return true;
            }

            current++;
        }

        return false;
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

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);

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
        SvgCascadedStyleFeatureFlags.Isolation |
        SvgCascadedStyleFeatureFlags.ClipPath |
        SvgCascadedStyleFeatureFlags.Mask |
        SvgCascadedStyleFeatureFlags.Filter |
        SvgCascadedStyleFeatureFlags.Cursor |
        SvgCascadedStyleFeatureFlags.EnableBackground;

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

    internal bool TryGetOwnPresentationStyleValue(string propertyName, out string value)
    {
        if (_styles.TryGetValue(propertyName, out var rules) &&
            rules.TryGetValue(StyleSpecificity_PresAttribute, out var presentationValue) &&
            presentationValue is not null)
        {
            value = presentationValue;
            return true;
        }

        foreach (var style in _styles)
        {
            if (string.Equals(style.Key, propertyName, StringComparison.OrdinalIgnoreCase) &&
                style.Value.TryGetValue(StyleSpecificity_PresAttribute, out presentationValue) &&
                presentationValue is not null)
            {
                value = presentationValue;
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

        if (requestedFlags == SvgCascadedStyleFeatureFlags.TextOpenType)
        {
            return GetOwnTextOpenTypeCascadedStyleFeatureFlags();
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

            if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Cursor))
            {
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "cursor");
            }

            if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.EnableBackground))
            {
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "enable-background");
            }

            if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.ClipPath))
            {
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "clip-path");
            }

            if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Mask))
            {
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "mask");
            }

            if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Filter))
            {
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "filter");
            }

            if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.TextOpenType))
            {
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "font-feature-settings");
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "font-kerning");
                flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "font-variant-ligatures");
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

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Cursor))
        {
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "cursor");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.EnableBackground))
        {
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "enable-background");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.ClipPath))
        {
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "clip-path");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Mask))
        {
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "mask");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Filter))
        {
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "filter");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.TextOpenType))
        {
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "font-feature-settings");
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "font-kerning");
            flags = AddAttributeFeatureFlag(flags, requestedFlags, "font-variant-ligatures");
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

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Cursor))
        {
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "cursor");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.EnableBackground))
        {
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "enable-background");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.ClipPath))
        {
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "clip-path");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Mask))
        {
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "mask");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Filter))
        {
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "filter");
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.TextOpenType))
        {
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "font-feature-settings");
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "font-kerning");
            flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "font-variant-ligatures");
        }

        return flags;
    }

    private SvgCascadedStyleFeatureFlags GetOwnTextOpenTypeCascadedStyleFeatureFlags()
    {
        var requestedFlags = SvgCascadedStyleFeatureFlags.TextOpenType;
        var flags = SvgCascadedStyleFeatureFlags.None;

        if (_styles.Count > 0)
        {
            flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "font-feature-settings");
            flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "font-kerning");
            flags = AddStyleRulesFeatureFlag(flags, requestedFlags, "font-variant-ligatures");
            if (flags == requestedFlags)
            {
                return flags;
            }
        }

        flags = AddAttributeFeatureFlag(flags, requestedFlags, "font-feature-settings");
        flags = AddAttributeFeatureFlag(flags, requestedFlags, "font-kerning");
        flags = AddAttributeFeatureFlag(flags, requestedFlags, "font-variant-ligatures");
        if (flags == requestedFlags)
        {
            return flags;
        }

        flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "font-feature-settings");
        flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "font-kerning");
        flags = AddCustomAttributeFeatureFlag(flags, requestedFlags, "font-variant-ligatures");
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

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Cursor) &&
            !HasFeatureFlag(flags, SvgCascadedStyleFeatureFlags.Cursor) &&
            string.Equals(propertyName, "cursor", StringComparison.OrdinalIgnoreCase) &&
            IsCursorDeclarationCandidateValue(value))
        {
            flags |= SvgCascadedStyleFeatureFlags.Cursor;
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.EnableBackground) &&
            !HasFeatureFlag(flags, SvgCascadedStyleFeatureFlags.EnableBackground) &&
            string.Equals(propertyName, "enable-background", StringComparison.OrdinalIgnoreCase) &&
            IsEnableBackgroundDeclarationCandidateValue(value))
        {
            flags |= SvgCascadedStyleFeatureFlags.EnableBackground;
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.ClipPath) &&
            !HasFeatureFlag(flags, SvgCascadedStyleFeatureFlags.ClipPath) &&
            string.Equals(propertyName, "clip-path", StringComparison.OrdinalIgnoreCase) &&
            IsDeclaredComputedStyleFeatureCandidate(value, "none"))
        {
            flags |= SvgCascadedStyleFeatureFlags.ClipPath;
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Mask) &&
            !HasFeatureFlag(flags, SvgCascadedStyleFeatureFlags.Mask) &&
            string.Equals(propertyName, "mask", StringComparison.OrdinalIgnoreCase) &&
            IsDeclaredComputedStyleFeatureCandidate(value, "none"))
        {
            flags |= SvgCascadedStyleFeatureFlags.Mask;
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.Filter) &&
            !HasFeatureFlag(flags, SvgCascadedStyleFeatureFlags.Filter) &&
            string.Equals(propertyName, "filter", StringComparison.OrdinalIgnoreCase) &&
            IsDeclaredComputedStyleFeatureCandidate(value, "none"))
        {
            flags |= SvgCascadedStyleFeatureFlags.Filter;
        }

        if (HasFeatureFlag(requestedFlags, SvgCascadedStyleFeatureFlags.TextOpenType) &&
            !HasFeatureFlag(flags, SvgCascadedStyleFeatureFlags.TextOpenType) &&
            IsTextOpenTypeFeatureProperty(propertyName, out var initialValue) &&
            IsDeclaredComputedStyleFeatureCandidate(value, initialValue))
        {
            flags |= SvgCascadedStyleFeatureFlags.TextOpenType;
        }

        return flags;
    }

    private static bool IsTextOpenTypeFeatureProperty(string propertyName, out string initialValue)
    {
        if (string.Equals(propertyName, "font-feature-settings", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "font-variant-ligatures", StringComparison.OrdinalIgnoreCase))
        {
            initialValue = "normal";
            return true;
        }

        if (string.Equals(propertyName, "font-kerning", StringComparison.OrdinalIgnoreCase))
        {
            initialValue = "auto";
            return true;
        }

        initialValue = string.Empty;
        return false;
    }

    private static bool IsCursorDeclarationCandidateValue(string value)
    {
        var normalizedValue = value.Trim();
        return normalizedValue.Length > 0 &&
               !normalizedValue.Equals("inherit", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEnableBackgroundDeclarationCandidateValue(string value)
    {
        var normalizedValue = value.Trim();
        return normalizedValue.StartsWith("new", StringComparison.OrdinalIgnoreCase) &&
               (normalizedValue.Length == 3 ||
                char.IsWhiteSpace(normalizedValue[3]) ||
                normalizedValue[3] == ',');
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
