// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Globalization;
using System.Text;
using ShimSkiaSharp;
using Svg.Ast;
using Svg.Ast.Emit;

namespace Svg.Model.Ast;

/// <summary>
/// Emits C# code that builds <c>SkiaSharp.SKPath</c> instances for basic shapes.
/// </summary>
public sealed class SvgAstSkiaPathCodeEmitter : SvgAstVisitorEmitter<string>
{
    private readonly StringBuilder _builder = new();
    private readonly string _pathVariable;

    public SvgAstSkiaPathCodeEmitter(string pathVariable = "path")
    {
        _pathVariable = pathVariable;
    }

    protected override string GetResult(SvgAstEmissionContext context)
    {
        return _builder.ToString();
    }

    public override void VisitElement(SvgAstElement element, SvgAstEmissionContext context)
    {
        base.VisitElement(element, context);

        switch (element.Name.LocalName)
        {
            case "rect":
                EmitRectangle(element);
                break;
            case "circle":
                EmitCircle(element);
                break;
            case "ellipse":
                EmitEllipse(element);
                break;
            case "line":
                EmitLine(element);
                break;
            case "polyline":
                EmitPolyline(element, close: false);
                break;
            case "polygon":
                EmitPolyline(element, close: true);
                break;
            case "path":
                EmitPath(element);
                break;
        }
    }

    private void EmitRectangle(SvgAstElement element)
    {
        if (!TryGetNumber(element, "x", out var x))
        {
            x = 0;
        }

        if (!TryGetNumber(element, "y", out var y))
        {
            y = 0;
        }

        if (!TryGetNumber(element, "width", out var width) || !TryGetNumber(element, "height", out var height))
        {
            return;
        }

        if (width <= 0 || height <= 0)
        {
            return;
        }

        _builder.AppendLine($"{_pathVariable}.AddRect(new SKRect({Format(x)}, {Format(y)}, {Format(x + width)}, {Format(y + height)}));");
    }

    private void EmitCircle(SvgAstElement element)
    {
        if (!TryGetNumber(element, "cx", out var cx))
        {
            cx = 0;
        }

        if (!TryGetNumber(element, "cy", out var cy))
        {
            cy = 0;
        }

        if (!TryGetNumber(element, "r", out var r) || r <= 0)
        {
            return;
        }

        _builder.AppendLine($"{_pathVariable}.AddCircle({Format(cx)}, {Format(cy)}, {Format(r)});");
    }

    private void EmitEllipse(SvgAstElement element)
    {
        if (!TryGetNumber(element, "cx", out var cx))
        {
            cx = 0;
        }

        if (!TryGetNumber(element, "cy", out var cy))
        {
            cy = 0;
        }

        if (!TryGetNumber(element, "rx", out var rx) || !TryGetNumber(element, "ry", out var ry))
        {
            return;
        }

        if (rx <= 0 || ry <= 0)
        {
            return;
        }

        var left = cx - rx;
        var top = cy - ry;
        var rect = $"new SKRect({Format(left)}, {Format(top)}, {Format(left + rx * 2)}, {Format(top + ry * 2)})";
        _builder.AppendLine($"{_pathVariable}.AddOval({rect});");
    }

    private void EmitLine(SvgAstElement element)
    {
        if (!TryGetNumber(element, "x1", out var x1) ||
            !TryGetNumber(element, "y1", out var y1) ||
            !TryGetNumber(element, "x2", out var x2) ||
            !TryGetNumber(element, "y2", out var y2))
        {
            return;
        }

        _builder.AppendLine($"{_pathVariable}.MoveTo({Format(x1)}, {Format(y1)});");
        _builder.AppendLine($"{_pathVariable}.LineTo({Format(x2)}, {Format(y2)});");
    }

    private void EmitPolyline(SvgAstElement element, bool close)
    {
        if (!element.TryGetAttribute("points", out var pointsAttr))
        {
            return;
        }

        if (!SvgAstRenderUtilities.TryParsePoints(pointsAttr.GetValueText(), out var points) || points.Count == 0)
        {
            return;
        }

        _builder.AppendLine($"{_pathVariable}.AddPoly(new SKPoint[] {{");
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            _builder.AppendLine($"    new SKPoint({Format(point.X)}, {Format(point.Y)}){(i == points.Count - 1 ? string.Empty : ",")}");
        }
        _builder.AppendLine($"}}, {Format(close)});");
    }

    private void EmitPath(SvgAstElement element)
    {
        if (!element.TryGetAttribute("d", out var dAttr))
        {
            return;
        }

        var data = dAttr.GetValueText();
        if (string.IsNullOrWhiteSpace(data))
        {
            return;
        }

        var escaped = data.Replace("\"", "\\\"");
        _builder.AppendLine($"// path data");
        _builder.AppendLine($"var parsedPath = SKPath.ParseSvgPathData(\"{escaped}\");");
        _builder.AppendLine($"if (parsedPath is not null) {{ {_pathVariable}.AddPath(parsedPath); }}");
    }

    private static bool TryGetNumber(SvgAstElement element, string name, out float value)
    {
        value = 0;
        if (!element.TryGetAttribute(name, out var attribute))
        {
            return false;
        }

        return SvgAstRenderUtilities.TryParseNumber(attribute.GetValueText(), out value);
    }

    private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Format(bool value) => value ? "true" : "false";
}
