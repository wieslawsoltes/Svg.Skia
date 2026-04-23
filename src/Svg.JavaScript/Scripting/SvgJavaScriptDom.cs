using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using ShimSkiaSharp;
using Svg;
using Svg.Model;

namespace Svg.JavaScript;

public interface ISvgJavaScriptAnimationHost
{
    TimeSpan CurrentTime { get; }
    void Seek(TimeSpan time);
    bool BeginElement(SvgAnimationElement animation, TimeSpan offset);
    bool EndElement(SvgAnimationElement animation, TimeSpan offset);
    bool TryGetStartTime(SvgAnimationElement animation, out TimeSpan startTime);
    bool TryGetBaseAttributeValue(SvgElement element, string attributeName, out string value);
}

public interface ISvgJavaScriptTextContentHost
{
    double GetComputedTextLength(SvgTextBase textContentElement);
    int GetNumberOfChars(SvgTextBase textContentElement);
    double GetSubStringLength(SvgTextBase textContentElement, int charnum, int nchars);
    SvgJavaScriptPoint GetStartPositionOfChar(SvgTextBase textContentElement, int charnum);
    SvgJavaScriptPoint GetEndPositionOfChar(SvgTextBase textContentElement, int charnum);
    SvgJavaScriptRect GetExtentOfChar(SvgTextBase textContentElement, int charnum);
    double GetRotationOfChar(SvgTextBase textContentElement, int charnum);
    int GetCharNumAtPosition(SvgTextBase textContentElement, SvgJavaScriptPoint point);
    void SelectSubString(SvgTextBase textContentElement, int charnum, int nchars);
}

public sealed class SvgJavaScriptNodeList
{
    private readonly object?[] _items;

    public SvgJavaScriptNodeList(IEnumerable<object?> items)
    {
        _items = items is object?[] array ? array : new List<object?>(items).ToArray();
    }

    public int length => _items.Length;

    public object? item(int index)
    {
        return index >= 0 && index < _items.Length ? _items[index] : null;
    }

    public object? this[int index] => item(index);
}

public sealed class SvgJavaScriptDomImplementation
{
    private static readonly HashSet<string> s_supportedFeatures = new(StringComparer.OrdinalIgnoreCase)
    {
        "core",
        "xml",
        "events",
        "uievents",
        "mouseevents",
        "mutationevents",
        "stylesheets",
        "traversal",
        "views",
        "css",
        "css2",
        "svg",
        "svg.static",
        "svg.animation",
        "svg.dynamic",
        "svg.all",
        "org.w3c.svg",
        "org.w3c.svg.lang",
        "org.w3c.svg.static",
        "org.w3c.svg.dynamic",
        "org.w3c.svg.all",
        "org.w3c.dom.svg",
        "org.w3c.dom.svg.static",
        "org.w3c.dom.svg.animation",
        "org.w3c.dom.svg.dynamic",
        "org.w3c.dom.svg.all"
    };

    public bool hasFeature(string? feature, string? version)
    {
        _ = version;
        if (string.IsNullOrWhiteSpace(feature))
        {
            return false;
        }

        return s_supportedFeatures.Contains(feature.Trim());
    }
}

internal sealed class SvgJavaScriptAssetLoader : ISvgAssetLoader
{
    public bool EnableSvgFonts => false;

    public SKImage LoadImage(Stream stream)
    {
        var data = SKImage.FromStream(stream);
        _ = TryReadImageDimensions(data, out var width, out var height);
        return new SKImage
        {
            Data = data,
            Width = width,
            Height = height
        };
    }

    public List<TypefaceSpan> FindTypefaces(string? text, SKPaint paintPreferredTypeface)
    {
        _ = text;
        _ = paintPreferredTypeface;
        return new List<TypefaceSpan>();
    }

    public SKFontMetrics GetFontMetrics(SKPaint paint)
    {
        var textSize = paint.TextSize <= 0f ? 12f : paint.TextSize;
        return new SKFontMetrics
        {
            Top = -textSize,
            Ascent = -textSize * 0.8f,
            Descent = textSize * 0.2f,
            Bottom = textSize * 0.3f,
            Leading = textSize * 0.2f,
            StrikeoutPosition = -textSize * 0.3f,
            StrikeoutThickness = Math.Max(1f, textSize / 12f),
            UnderlinePosition = textSize * 0.08f,
            UnderlineThickness = Math.Max(1f, textSize / 14f)
        };
    }

    public float MeasureText(string? text, SKPaint paint, ref SKRect bounds)
    {
        var content = text ?? string.Empty;
        var textSize = paint.TextSize <= 0f ? 12f : paint.TextSize;
        var width = EstimateTextWidth(content, textSize);
        bounds = new SKRect(0f, -textSize * 0.75f, width, textSize * 0.25f);
        return width;
    }

    public SKPath? GetTextPath(string? text, SKPaint paint, float x, float y)
    {
        _ = text;
        _ = paint;
        _ = x;
        _ = y;
        return null;
    }

    private static bool TryReadImageDimensions(byte[]? data, out float width, out float height)
    {
        width = 0f;
        height = 0f;
        if (data is null || data.Length < 10)
        {
            return false;
        }

        if (TryReadPngDimensions(data, out width, out height) ||
            TryReadGifDimensions(data, out width, out height) ||
            TryReadBmpDimensions(data, out width, out height) ||
            TryReadJpegDimensions(data, out width, out height))
        {
            return width > 0f && height > 0f;
        }

        return false;
    }

    private static float EstimateTextWidth(string text, float textSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var advance = 0f;
        for (var i = 0; i < text.Length; i++)
        {
            advance += EstimateGlyphAdvance(text[i], textSize);
        }

        return advance;
    }

    private static float EstimateGlyphAdvance(char value, float textSize)
    {
        if (char.IsWhiteSpace(value))
        {
            return textSize * 0.28f;
        }

        return value switch
        {
            'i' or 'l' or 'I' or '!' or '\'' or '"' or '.' or ',' or ':' or ';' or '|' => textSize * 0.28f,
            'f' or 'j' or 'r' or 't' => textSize * 0.38f,
            'm' or 'w' or 'M' or 'W' or '@' or '#' or '%' or '&' => textSize * 0.78f,
            _ when char.IsUpper(value) || char.IsDigit(value) => textSize * 0.58f,
            _ when char.IsLower(value) => textSize * 0.48f,
            _ => textSize * 0.52f
        };
    }

    private static bool TryReadPngDimensions(byte[] data, out float width, out float height)
    {
        width = 0f;
        height = 0f;
        if (data.Length < 24 ||
            data[0] != 0x89 ||
            data[1] != 0x50 ||
            data[2] != 0x4E ||
            data[3] != 0x47)
        {
            return false;
        }

        width = ReadUInt32BigEndian(data, 16);
        height = ReadUInt32BigEndian(data, 20);
        return true;
    }

    private static bool TryReadGifDimensions(byte[] data, out float width, out float height)
    {
        width = 0f;
        height = 0f;
        if (data.Length < 10 ||
            data[0] != 0x47 ||
            data[1] != 0x49 ||
            data[2] != 0x46)
        {
            return false;
        }

        width = ReadUInt16LittleEndian(data, 6);
        height = ReadUInt16LittleEndian(data, 8);
        return true;
    }

    private static bool TryReadBmpDimensions(byte[] data, out float width, out float height)
    {
        width = 0f;
        height = 0f;
        if (data.Length < 26 || data[0] != 0x42 || data[1] != 0x4D)
        {
            return false;
        }

        width = Math.Abs(ReadInt32LittleEndian(data, 18));
        height = Math.Abs(ReadInt32LittleEndian(data, 22));
        return true;
    }

    private static bool TryReadJpegDimensions(byte[] data, out float width, out float height)
    {
        width = 0f;
        height = 0f;
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return false;
        }

        for (var index = 2; index + 8 < data.Length;)
        {
            if (data[index] != 0xFF)
            {
                index++;
                continue;
            }

            while (index < data.Length && data[index] == 0xFF)
            {
                index++;
            }

            if (index >= data.Length)
            {
                break;
            }

            var marker = data[index++];
            if (marker is 0xD8 or 0xD9)
            {
                continue;
            }

            if (index + 1 >= data.Length)
            {
                break;
            }

            var segmentLength = ReadUInt16BigEndian(data, index);
            if (segmentLength < 2 || index + segmentLength > data.Length)
            {
                break;
            }

            if ((marker >= 0xC0 && marker <= 0xC3) ||
                (marker >= 0xC5 && marker <= 0xC7) ||
                (marker >= 0xC9 && marker <= 0xCB) ||
                (marker >= 0xCD && marker <= 0xCF))
            {
                if (segmentLength >= 7)
                {
                    height = ReadUInt16BigEndian(data, index + 3);
                    width = ReadUInt16BigEndian(data, index + 5);
                    return true;
                }

                return false;
            }

            index += segmentLength;
        }

        return false;
    }

    private static ushort ReadUInt16BigEndian(byte[] data, int index)
    {
        return (ushort)((data[index] << 8) | data[index + 1]);
    }

    private static ushort ReadUInt16LittleEndian(byte[] data, int index)
    {
        return (ushort)(data[index] | (data[index + 1] << 8));
    }

    private static uint ReadUInt32BigEndian(byte[] data, int index)
    {
        return ((uint)data[index] << 24) |
               ((uint)data[index + 1] << 16) |
               ((uint)data[index + 2] << 8) |
               data[index + 3];
    }

    private static int ReadInt32LittleEndian(byte[] data, int index)
    {
        return data[index] |
               (data[index + 1] << 8) |
               (data[index + 2] << 16) |
               (data[index + 3] << 24);
    }
}

internal static class SvgJavaScriptDomConstants
{
    public static ObjectInstance CreateNodeObject(SvgJavaScriptRuntime runtime)
    {
        var node = runtime.CreatePlainObject();
        node.FastSetDataProperty("ELEMENT_NODE", JsNumber.Create(1));
        node.FastSetDataProperty("ATTRIBUTE_NODE", JsNumber.Create(2));
        node.FastSetDataProperty("TEXT_NODE", JsNumber.Create(3));
        node.FastSetDataProperty("DOCUMENT_NODE", JsNumber.Create(9));
        return node;
    }

    public static ObjectInstance CreateDomExceptionObject(SvgJavaScriptRuntime runtime)
    {
        var domException = runtime.CreatePlainObject();
        domException.FastSetDataProperty("INDEX_SIZE_ERR", JsNumber.Create(1));
        domException.FastSetDataProperty("NO_MODIFICATION_ALLOWED_ERR", JsNumber.Create(7));
        domException.FastSetDataProperty("INVALID_STATE_ERR", JsNumber.Create(11));
        domException.FastSetDataProperty("SYNTAX_ERR", JsNumber.Create(12));
        return domException;
    }

    public static ObjectInstance CreateSvgTransformObject(SvgJavaScriptRuntime runtime)
    {
        var svgTransform = runtime.CreatePlainObject();
        svgTransform.FastSetDataProperty("SVG_TRANSFORM_UNKNOWN", JsNumber.Create(0));
        svgTransform.FastSetDataProperty("SVG_TRANSFORM_MATRIX", JsNumber.Create(1));
        svgTransform.FastSetDataProperty("SVG_TRANSFORM_TRANSLATE", JsNumber.Create(2));
        svgTransform.FastSetDataProperty("SVG_TRANSFORM_SCALE", JsNumber.Create(3));
        svgTransform.FastSetDataProperty("SVG_TRANSFORM_ROTATE", JsNumber.Create(4));
        svgTransform.FastSetDataProperty("SVG_TRANSFORM_SKEWX", JsNumber.Create(5));
        svgTransform.FastSetDataProperty("SVG_TRANSFORM_SKEWY", JsNumber.Create(6));
        return svgTransform;
    }

    public static ObjectInstance CreateSvgLengthObject(SvgJavaScriptRuntime runtime)
    {
        var svgLength = runtime.CreatePlainObject();
        svgLength.FastSetDataProperty("SVG_LENGTHTYPE_UNKNOWN", JsNumber.Create(0));
        svgLength.FastSetDataProperty("SVG_LENGTHTYPE_NUMBER", JsNumber.Create(1));
        svgLength.FastSetDataProperty("SVG_LENGTHTYPE_PERCENTAGE", JsNumber.Create(2));
        svgLength.FastSetDataProperty("SVG_LENGTHTYPE_EMS", JsNumber.Create(3));
        svgLength.FastSetDataProperty("SVG_LENGTHTYPE_EXS", JsNumber.Create(4));
        svgLength.FastSetDataProperty("SVG_LENGTHTYPE_PX", JsNumber.Create(5));
        svgLength.FastSetDataProperty("SVG_LENGTHTYPE_CM", JsNumber.Create(6));
        svgLength.FastSetDataProperty("SVG_LENGTHTYPE_MM", JsNumber.Create(7));
        svgLength.FastSetDataProperty("SVG_LENGTHTYPE_IN", JsNumber.Create(8));
        svgLength.FastSetDataProperty("SVG_LENGTHTYPE_PT", JsNumber.Create(9));
        svgLength.FastSetDataProperty("SVG_LENGTHTYPE_PC", JsNumber.Create(10));
        return svgLength;
    }

    public static ObjectInstance CreateSvgAngleObject(SvgJavaScriptRuntime runtime)
    {
        var svgAngle = runtime.CreatePlainObject();
        svgAngle.FastSetDataProperty("SVG_ANGLETYPE_UNKNOWN", JsNumber.Create(0));
        svgAngle.FastSetDataProperty("SVG_ANGLETYPE_UNSPECIFIED", JsNumber.Create(1));
        svgAngle.FastSetDataProperty("SVG_ANGLETYPE_DEG", JsNumber.Create(2));
        svgAngle.FastSetDataProperty("SVG_ANGLETYPE_RAD", JsNumber.Create(3));
        svgAngle.FastSetDataProperty("SVG_ANGLETYPE_GRAD", JsNumber.Create(4));
        return svgAngle;
    }

    public static ObjectInstance CreateSvgPreserveAspectRatioObject(SvgJavaScriptRuntime runtime)
    {
        var svgPreserveAspectRatio = runtime.CreatePlainObject();
        svgPreserveAspectRatio.FastSetDataProperty("SVG_PRESERVEASPECTRATIO_UNKNOWN", JsNumber.Create(0));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_PRESERVEASPECTRATIO_NONE", JsNumber.Create(1));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_PRESERVEASPECTRATIO_XMINYMIN", JsNumber.Create(2));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_PRESERVEASPECTRATIO_XMIDYMIN", JsNumber.Create(3));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_PRESERVEASPECTRATIO_XMAXYMIN", JsNumber.Create(4));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_PRESERVEASPECTRATIO_XMINYMID", JsNumber.Create(5));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_PRESERVEASPECTRATIO_XMIDYMID", JsNumber.Create(6));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_PRESERVEASPECTRATIO_XMAXYMID", JsNumber.Create(7));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_PRESERVEASPECTRATIO_XMINYMAX", JsNumber.Create(8));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_PRESERVEASPECTRATIO_XMIDYMAX", JsNumber.Create(9));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_PRESERVEASPECTRATIO_XMAXYMAX", JsNumber.Create(10));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_MEETORSLICE_UNKNOWN", JsNumber.Create(0));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_MEETORSLICE_MEET", JsNumber.Create(1));
        svgPreserveAspectRatio.FastSetDataProperty("SVG_MEETORSLICE_SLICE", JsNumber.Create(2));
        return svgPreserveAspectRatio;
    }
}

internal static class SvgJavaScriptParsing
{
    private static readonly char[] s_whitespaceSeparators = [' ', '\t', '\r', '\n', ','];

    public static string FormatNumber(double value)
    {
        return value.ToString("0.###############", CultureInfo.InvariantCulture);
    }

    public static string[] ParseTokenList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(s_whitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
    }

    public static bool TryParseFloat(string? value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    public static bool TryParseDouble(string? value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
