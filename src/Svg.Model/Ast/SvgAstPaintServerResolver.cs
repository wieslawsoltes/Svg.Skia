// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using ShimSkiaSharp;
using Svg.Ast;

namespace Svg.Model.Ast;

internal enum SvgAstCoordinateUnits
{
    ObjectBoundingBox,
    UserSpaceOnUse
}

internal sealed class SvgAstPaintServerResolver
{
    private readonly Dictionary<string, SvgAstGradientDefinition> _gradients = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgAstElement> _gradientElements = new(StringComparer.Ordinal);
    private readonly HashSet<string> _gradientBuildStack = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgAstClipPathDefinition> _clipPaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SvgAstMaskDefinition> _masks = new(StringComparer.Ordinal);

    public SvgAstPaintServerResolver(SvgAstDocument document)
    {
        if (document?.RootElement is { } root)
        {
            CollectDefinitions(root);
        }
    }

    public SKShader? TryCreateShader(string id, SKRect geometryBounds)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var definition = EnsureGradientDefinition(id);
        return definition?.CreateShader(geometryBounds);
    }

    public bool TryGetMaskRect(string id, SKRect geometryBounds, out SKRect maskRect)
    {
        if (!string.IsNullOrEmpty(id) && _masks.TryGetValue(id, out var mask))
        {
            maskRect = mask.ResolveRect(geometryBounds);
            return true;
        }

        maskRect = SKRect.Empty;
        return false;
    }

    public ClipPath? TryCreateClipPath(string id, SKRect geometryBounds, Func<SvgAstElement, SKPath?> geometryFactory)
    {
        if (string.IsNullOrEmpty(id) || geometryFactory is null)
        {
            return null;
        }

        return _clipPaths.TryGetValue(id, out var definition)
            ? definition.CreateClipPath(geometryBounds, geometryFactory)
            : null;
    }

    private void CollectDefinitions(SvgAstElement element)
    {
        var localName = element.Name.LocalName;
        switch (localName)
        {
            case "linearGradient":
            case "radialGradient":
                RegisterGradientElement(element);
                break;
            case "mask":
                TryAddMask(element);
                break;
            case "clipPath":
                TryAddClipPath(element);
                break;
        }

        foreach (var child in element.Children)
        {
            if (child is SvgAstElement childElement)
            {
                CollectDefinitions(childElement);
            }
        }
    }

    private void RegisterGradientElement(SvgAstElement element)
    {
        if (!TryGetElementId(element, out var id))
        {
            return;
        }

        _gradientElements[id] = element;
    }

    private SvgAstGradientDefinition? EnsureGradientDefinition(string id)
    {
        if (_gradients.TryGetValue(id, out var existing))
        {
            return existing;
        }

        if (!_gradientElements.TryGetValue(id, out var element))
        {
            return null;
        }

        if (_gradientBuildStack.Contains(id))
        {
            return null;
        }

        _gradientBuildStack.Add(id);
        var definition = BuildGradientDefinition(id, element);
        _gradientBuildStack.Remove(id);

        if (definition is not null)
        {
            _gradients[id] = definition;
        }

        return definition;
    }

    private SvgAstGradientDefinition? BuildGradientDefinition(string id, SvgAstElement element)
    {
        return element.Name.LocalName switch
        {
            "linearGradient" => BuildLinearGradient(id, element),
            "radialGradient" => BuildRadialGradient(id, element),
            _ => null
        };
    }

    private SvgAstGradientDefinition? BuildLinearGradient(string id, SvgAstElement element)
    {
        var inherited = ResolveHref(element) as SvgAstLinearGradientDefinition;
        var stops = ParseStops(element);
        var effectiveStops = stops.Count > 0
            ? (IReadOnlyList<SvgAstGradientStop>)stops
            : inherited?.Stops ?? Array.Empty<SvgAstGradientStop>();

        if (effectiveStops.Count == 0)
        {
            return null;
        }

        var units = ParseUnits(element, "gradientUnits", SvgAstCoordinateUnits.ObjectBoundingBox, out var unitsSpecified);
        if (!unitsSpecified && inherited is not null)
        {
            units = inherited.Units;
        }

        var spread = ParseTileMode(element, out var spreadSpecified);
        if (!spreadSpecified && inherited is not null)
        {
            spread = inherited.SpreadMethod;
        }

        var localMatrix = ParseTransform(element, "gradientTransform") ?? inherited?.LocalMatrix;

        var x1 = ReadCoordinate(element, "x1", 0f, out var hasX1);
        if (!hasX1 && inherited is not null)
        {
            x1 = inherited.X1;
        }

        var y1 = ReadCoordinate(element, "y1", 0f, out var hasY1);
        if (!hasY1 && inherited is not null)
        {
            y1 = inherited.Y1;
        }

        var x2 = ReadCoordinate(element, "x2", 1f, out var hasX2);
        if (!hasX2 && inherited is not null)
        {
            x2 = inherited.X2;
        }

        var y2 = ReadCoordinate(element, "y2", 0f, out var hasY2);
        if (!hasY2 && inherited is not null)
        {
            y2 = inherited.Y2;
        }

        return new SvgAstLinearGradientDefinition(id, units, spread, localMatrix, effectiveStops, x1, y1, x2, y2);
    }

    private SvgAstGradientDefinition? BuildRadialGradient(string id, SvgAstElement element)
    {
        var inherited = ResolveHref(element) as SvgAstRadialGradientDefinition;
        var stops = ParseStops(element);
        var effectiveStops = stops.Count > 0
            ? (IReadOnlyList<SvgAstGradientStop>)stops
            : inherited?.Stops ?? Array.Empty<SvgAstGradientStop>();

        if (effectiveStops.Count == 0)
        {
            return null;
        }

        var units = ParseUnits(element, "gradientUnits", SvgAstCoordinateUnits.ObjectBoundingBox, out var unitsSpecified);
        if (!unitsSpecified && inherited is not null)
        {
            units = inherited.Units;
        }

        var spread = ParseTileMode(element, out var spreadSpecified);
        if (!spreadSpecified && inherited is not null)
        {
            spread = inherited.SpreadMethod;
        }

        var localMatrix = ParseTransform(element, "gradientTransform") ?? inherited?.LocalMatrix;

        var cx = ReadCoordinate(element, "cx", 0.5f, out var hasCx);
        if (!hasCx && inherited is not null)
        {
            cx = inherited.Cx;
        }

        var cy = ReadCoordinate(element, "cy", 0.5f, out var hasCy);
        if (!hasCy && inherited is not null)
        {
            cy = inherited.Cy;
        }

        var r = ReadCoordinate(element, "r", 0.5f, out var hasR);
        if (!hasR && inherited is not null)
        {
            r = inherited.Radius;
        }

        float fx;
        if (element.TryGetAttribute("fx", out var fxAttr) && SvgAstRenderUtilities.TryParseNumber(fxAttr.GetValueText(), out var fxValue))
        {
            fx = fxValue;
        }
        else if (inherited is not null)
        {
            fx = inherited.Fx;
        }
        else
        {
            fx = cx;
        }

        float fy;
        if (element.TryGetAttribute("fy", out var fyAttr) && SvgAstRenderUtilities.TryParseNumber(fyAttr.GetValueText(), out var fyValue))
        {
            fy = fyValue;
        }
        else if (inherited is not null)
        {
            fy = inherited.Fy;
        }
        else
        {
            fy = cy;
        }

        return new SvgAstRadialGradientDefinition(id, units, spread, localMatrix, effectiveStops, cx, cy, fx, fy, r);
    }

    private SvgAstGradientDefinition? ResolveHref(SvgAstElement element)
    {
        if (TryGetHrefId(element, out var hrefId))
        {
            return EnsureGradientDefinition(hrefId);
        }

        return null;
    }

    private void TryAddMask(SvgAstElement element)
    {
        if (!TryGetElementId(element, out var id))
        {
            return;
        }

        if (_masks.ContainsKey(id))
        {
            return;
        }

        var units = ParseUnits(element, "maskUnits", SvgAstCoordinateUnits.ObjectBoundingBox, out _);
        var x = ReadCoordinate(element, "x", 0f, out _);
        var y = ReadCoordinate(element, "y", 0f, out _);
        var width = ReadCoordinate(element, "width", 1f, out _);
        var height = ReadCoordinate(element, "height", 1f, out _);

        _masks[id] = new SvgAstMaskDefinition(id, units, x, y, width, height);
    }

    private void TryAddClipPath(SvgAstElement element)
    {
        if (!TryGetElementId(element, out var id))
        {
            return;
        }

        if (_clipPaths.ContainsKey(id))
        {
            return;
        }

        var units = ParseUnits(element, "clipPathUnits", SvgAstCoordinateUnits.UserSpaceOnUse, out _);
        var transform = ParseTransform(element, "transform");
        _clipPaths[id] = new SvgAstClipPathDefinition(id, units, transform, element);
    }

    private static bool TryGetElementId(SvgAstElement element, out string id)
    {
        if (element.TryGetAttribute("id", out var attribute))
        {
            id = attribute.GetValueText();
            return !string.IsNullOrEmpty(id);
        }

        id = string.Empty;
        return false;
    }

    private static bool TryGetHrefId(SvgAstElement element, out string id)
    {
        if (element.TryGetAttribute("href", out var attribute) ||
            element.TryGetAttribute("xlink:href", out attribute))
        {
            var value = attribute.GetValueText().Trim();
            if (value.StartsWith("#", StringComparison.Ordinal) && value.Length > 1)
            {
                id = value.Substring(1);
                return true;
            }
        }

        id = string.Empty;
        return false;
    }

    private static SvgAstCoordinateUnits ParseUnits(
        SvgAstElement element,
        string attributeName,
        SvgAstCoordinateUnits defaultValue,
        out bool specified)
    {
        specified = false;
        if (element.TryGetAttribute(attributeName, out var attribute))
        {
            specified = true;
            var value = attribute.GetValueText().Trim();
            if (string.Equals(value, "userSpaceOnUse", StringComparison.OrdinalIgnoreCase))
            {
                return SvgAstCoordinateUnits.UserSpaceOnUse;
            }

            if (string.Equals(value, "objectBoundingBox", StringComparison.OrdinalIgnoreCase))
            {
                return SvgAstCoordinateUnits.ObjectBoundingBox;
            }
        }

        return defaultValue;
    }

    private static SKMatrix? ParseTransform(SvgAstElement element, string attributeName)
    {
        if (element.TryGetAttribute(attributeName, out var attribute))
        {
            var matrix = SvgAstRenderUtilities.ParseTransform(attribute.GetValueText());
            return matrix.IsIdentity ? null : matrix;
        }

        return null;
    }

    private static float ReadCoordinate(SvgAstElement element, string attributeName, float defaultValue, out bool specified)
    {
        specified = false;
        if (element.TryGetAttribute(attributeName, out var attribute) &&
            SvgAstRenderUtilities.TryParseNumber(attribute.GetValueText(), out var value))
        {
            specified = true;
            return value;
        }

        return defaultValue;
    }

    private static SKShaderTileMode ParseTileMode(SvgAstElement element, out bool specified)
    {
        specified = false;
        if (element.TryGetAttribute("spreadMethod", out var attribute))
        {
            specified = true;
            var value = attribute.GetValueText().Trim().ToLowerInvariant();
            return value switch
            {
                "repeat" => SKShaderTileMode.Repeat,
                "reflect" => SKShaderTileMode.Mirror,
                _ => SKShaderTileMode.Clamp
            };
        }

        return SKShaderTileMode.Clamp;
    }

    private static List<SvgAstGradientStop> ParseStops(SvgAstElement gradientElement)
    {
        var stops = new List<SvgAstGradientStop>();

        foreach (var child in gradientElement.Children)
        {
            if (child is not SvgAstElement stopElement || !string.Equals(stopElement.Name.LocalName, "stop", StringComparison.Ordinal))
            {
                continue;
            }

            if (!stopElement.TryGetAttribute("offset", out var offsetAttribute))
            {
                continue;
            }

            if (!SvgAstRenderUtilities.TryParseNumberOrPercentage(offsetAttribute.GetValueText(), out var offset, out _))
            {
                offset = 0;
            }

            offset = SvgAstRenderUtilities.Clamp01(offset);

            var color = ParseStopColor(stopElement, out var opacity);
            color = SvgAstRenderUtilities.ApplyOpacity(color, opacity);
            stops.Add(new SvgAstGradientStop(offset, color));
        }

        return stops;
    }

    private static SKColor ParseStopColor(SvgAstElement stopElement, out float opacity)
    {
        opacity = 1f;
        string? colorText = null;

        if (stopElement.TryGetAttribute("stop-color", out var colorAttribute))
        {
            colorText = colorAttribute.GetValueText();
        }

        var styleMap = SvgAstRenderUtilities.ParseStyleAttribute(stopElement);
        if (styleMap.TryGetValue("stop-color", out var styleColor))
        {
            colorText = styleColor;
        }

        if (stopElement.TryGetAttribute("stop-opacity", out var opacityAttribute) &&
            SvgAstRenderUtilities.TryParseNumber(opacityAttribute.GetValueText(), out var parsedOpacity))
        {
            opacity = SvgAstRenderUtilities.Clamp01(parsedOpacity);
        }
        else if (styleMap.TryGetValue("stop-opacity", out var styleOpacity) &&
                 SvgAstRenderUtilities.TryParseNumber(styleOpacity, out var styleOpacityValue))
        {
            opacity = SvgAstRenderUtilities.Clamp01(styleOpacityValue);
        }

        if (!SvgAstColorParser.TryParse(colorText, out var color))
        {
            color = new SKColor(0, 0, 0, 255);
        }

        return color;
    }

    private abstract class SvgAstGradientDefinition
    {
        protected SvgAstGradientDefinition(
            string id,
            SvgAstCoordinateUnits units,
            SKShaderTileMode spreadMethod,
            SKMatrix? localMatrix,
            IReadOnlyList<SvgAstGradientStop> stops)
        {
            Id = id;
            Units = units;
            SpreadMethod = spreadMethod;
            LocalMatrix = localMatrix;
            Stops = stops;
        }

        public string Id { get; }

        public SvgAstCoordinateUnits Units { get; }

        public SKShaderTileMode SpreadMethod { get; }

        public SKMatrix? LocalMatrix { get; }

        public IReadOnlyList<SvgAstGradientStop> Stops { get; }

        public abstract SKShader? CreateShader(SKRect geometryBounds);

        protected SKRect NormalizeBounds(SKRect bounds)
        {
            var width = bounds.Width;
            var height = bounds.Height;

            if (Math.Abs(width) < float.Epsilon)
            {
                width = 1f;
            }

            if (Math.Abs(height) < float.Epsilon)
            {
                height = 1f;
            }

            return SKRect.Create(bounds.Left, bounds.Top, width, height);
        }

        protected (SKPoint start, SKPoint end) ResolveLine(SKRect bounds, float x1, float y1, float x2, float y2)
        {
            var start = ResolvePoint(bounds, x1, y1);
            var end = ResolvePoint(bounds, x2, y2);
            return (start, end);
        }

        protected SKPoint ResolvePoint(SKRect bounds, float x, float y)
        {
            if (Units == SvgAstCoordinateUnits.ObjectBoundingBox)
            {
                return new SKPoint(
                    bounds.Left + bounds.Width * x,
                    bounds.Top + bounds.Height * y);
            }

            return new SKPoint(x, y);
        }

        protected float ResolveLength(SKRect bounds, float value)
        {
            if (Units == SvgAstCoordinateUnits.ObjectBoundingBox)
            {
                var diagonal = (float)Math.Sqrt((bounds.Width * bounds.Width) + (bounds.Height * bounds.Height));
                return diagonal * value;
            }

            return value;
        }

        protected (SKColorF[] colors, float[] positions) CreateStops()
        {
            var colors = new SKColorF[Stops.Count];
            var positions = new float[Stops.Count];

            for (var i = 0; i < Stops.Count; i++)
            {
                colors[i] = Stops[i].Color;
                positions[i] = Stops[i].Offset;
            }

            return (colors, positions);
        }
    }

    private sealed class SvgAstLinearGradientDefinition : SvgAstGradientDefinition
    {
        private readonly float _x1;
        private readonly float _y1;
        private readonly float _x2;
        private readonly float _y2;

        public SvgAstLinearGradientDefinition(
            string id,
            SvgAstCoordinateUnits units,
            SKShaderTileMode spreadMethod,
            SKMatrix? localMatrix,
            IReadOnlyList<SvgAstGradientStop> stops,
            float x1,
            float y1,
            float x2,
            float y2)
            : base(id, units, spreadMethod, localMatrix, stops)
        {
            _x1 = x1;
            _y1 = y1;
            _x2 = x2;
            _y2 = y2;
        }

        public float X1 => _x1;

        public float Y1 => _y1;

        public float X2 => _x2;

        public float Y2 => _y2;

        public override SKShader? CreateShader(SKRect geometryBounds)
        {
            if (Stops.Count == 0)
            {
                return null;
            }

            var bounds = NormalizeBounds(geometryBounds);
            var (start, end) = ResolveLine(bounds, _x1, _y1, _x2, _y2);
            var (colors, positions) = CreateStops();

            if (LocalMatrix is { } matrix)
            {
                return SKShader.CreateLinearGradient(start, end, colors, SKColorSpace.Srgb, positions, SpreadMethod, matrix);
            }

            return SKShader.CreateLinearGradient(start, end, colors, SKColorSpace.Srgb, positions, SpreadMethod);
        }
    }

    private sealed class SvgAstRadialGradientDefinition : SvgAstGradientDefinition
    {
        private readonly float _cx;
        private readonly float _cy;
        private readonly float _fx;
        private readonly float _fy;
        private readonly float _r;

        public SvgAstRadialGradientDefinition(
            string id,
            SvgAstCoordinateUnits units,
            SKShaderTileMode spreadMethod,
            SKMatrix? localMatrix,
            IReadOnlyList<SvgAstGradientStop> stops,
            float cx,
            float cy,
            float fx,
            float fy,
            float r)
            : base(id, units, spreadMethod, localMatrix, stops)
        {
            _cx = cx;
            _cy = cy;
            _fx = fx;
            _fy = fy;
            _r = r;
        }

        public float Cx => _cx;

        public float Cy => _cy;

        public float Fx => _fx;

        public float Fy => _fy;

        public float Radius => _r;

        public override SKShader? CreateShader(SKRect geometryBounds)
        {
            if (Stops.Count == 0)
            {
                return null;
            }

            var bounds = NormalizeBounds(geometryBounds);
            var center = ResolvePoint(bounds, _cx, _cy);
            var focus = ResolvePoint(bounds, _fx, _fy);
            var radius = ResolveLength(bounds, _r);
            var (colors, positions) = CreateStops();

            if (Math.Abs(center.X - focus.X) < float.Epsilon && Math.Abs(center.Y - focus.Y) < float.Epsilon)
            {
                if (LocalMatrix is { } matrix)
                {
                    return SKShader.CreateRadialGradient(center, radius, colors, SKColorSpace.Srgb, positions, SpreadMethod, matrix);
                }

                return SKShader.CreateRadialGradient(center, radius, colors, SKColorSpace.Srgb, positions, SpreadMethod);
            }
            else
            {
                if (LocalMatrix is { } matrixConical)
                {
                    return SKShader.CreateTwoPointConicalGradient(focus, 0, center, radius, colors, SKColorSpace.Srgb, positions, SpreadMethod, matrixConical);
                }

                return SKShader.CreateTwoPointConicalGradient(focus, 0, center, radius, colors, SKColorSpace.Srgb, positions, SpreadMethod);
            }
        }
    }

    private sealed class SvgAstClipPathDefinition
    {
        private readonly SvgAstCoordinateUnits _units;
        private readonly SKMatrix? _localMatrix;
        private readonly SvgAstElement _element;

        public SvgAstClipPathDefinition(string id, SvgAstCoordinateUnits units, SKMatrix? localMatrix, SvgAstElement element)
        {
            _units = units;
            _localMatrix = localMatrix;
            _element = element;
        }

        public ClipPath? CreateClipPath(SKRect geometryBounds, Func<SvgAstElement, SKPath?> geometryFactory)
        {
            var clipPath = new ClipPath();
            var baseTransform = CreateTransform(geometryBounds);

            foreach (var child in _element.Children)
            {
                if (child is not SvgAstElement childElement)
                {
                    continue;
                }

                var childPath = geometryFactory(childElement);
                if (childPath is null)
                {
                    continue;
                }

                var transform = baseTransform;
                if (childElement.TryGetAttribute("transform", out var transformAttribute))
                {
                    var childMatrix = SvgAstRenderUtilities.ParseTransform(transformAttribute.GetValueText());
                    if (!childMatrix.IsIdentity)
                    {
                        transform = transform is null ? childMatrix : transform.Value.PreConcat(childMatrix);
                    }
                }

                var pathClip = new PathClip
                {
                    Path = childPath,
                    Transform = transform
                };
                clipPath.Clips?.Add(pathClip);
            }

            return clipPath.IsEmpty ? null : clipPath;
        }

        private SKMatrix? CreateTransform(SKRect bounds)
        {
            SKMatrix? transform = null;

            if (_units == SvgAstCoordinateUnits.ObjectBoundingBox)
            {
                var width = Math.Max(bounds.Width, 1f);
                var height = Math.Max(bounds.Height, 1f);
                var scale = SKMatrix.CreateScale(width, height);
                var translate = SKMatrix.CreateTranslation(bounds.Left, bounds.Top);
                transform = translate.PreConcat(scale);
            }

            if (_localMatrix is { } matrix)
            {
                transform = transform is null ? matrix : transform.Value.PreConcat(matrix);
            }

            return transform;
        }
    }

    private sealed class SvgAstGradientStop
    {
        public SvgAstGradientStop(float offset, SKColor color)
        {
            Offset = offset;
            Color = color;
        }

        public float Offset { get; }

        public SKColor Color { get; }
    }

    private sealed class SvgAstMaskDefinition
    {
        private readonly string _id;
        private readonly SvgAstCoordinateUnits _units;
        private readonly float _x;
        private readonly float _y;
        private readonly float _width;
        private readonly float _height;

        public SvgAstMaskDefinition(string id, SvgAstCoordinateUnits units, float x, float y, float width, float height)
        {
            _id = id;
            _units = units;
            _x = x;
            _y = y;
            _width = width;
            _height = height;
        }

        public SKRect ResolveRect(SKRect geometryBounds)
        {
            var bounds = geometryBounds;
            if (Math.Abs(bounds.Width) < float.Epsilon || Math.Abs(bounds.Height) < float.Epsilon)
            {
                bounds = SKRect.Create(bounds.Left, bounds.Top, 1f, 1f);
            }

            if (_units == SvgAstCoordinateUnits.ObjectBoundingBox)
            {
                var width = bounds.Width * _width;
                var height = bounds.Height * _height;

                if (width <= 0 || height <= 0)
                {
                    return bounds;
                }

                return SKRect.Create(
                    bounds.Left + bounds.Width * _x,
                    bounds.Top + bounds.Height * _y,
                    width,
                    height);
            }
            else
            {
                var width = _width > 0 ? _width : bounds.Width;
                var height = _height > 0 ? _height : bounds.Height;
                return SKRect.Create(_x, _y, width, height);
            }
        }
    }
}
