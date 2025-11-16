// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Text;
using ShimSkiaSharp;
using Svg.Ast;
using Svg.Ast.Emit;

namespace Svg.Model.Ast;

internal sealed class SvgAstSkiaEmitter : SvgAstVisitorEmitter<SKPicture?>
{
    private readonly SvgAstRenderOptions _renderOptions;
    private readonly Stack<SvgAstRenderState> _stateStack = new();
    private SKPictureRecorder? _recorder;
    private SKCanvas? _canvas;
    private SKPicture? _picture;
    private SvgAstPaintServerResolver? _paintServers;

    public SvgAstSkiaEmitter(SvgAstRenderOptions? renderOptions = null)
    {
        _renderOptions = renderOptions ?? SvgAstRenderOptions.Default;
    }

    public override void VisitDocument(SvgAstDocument document, SvgAstEmissionContext context)
    {
        _paintServers = new SvgAstPaintServerResolver(document);
        PrepareRecording(document);
        if (_canvas is null)
        {
            return;
        }

        base.VisitDocument(document, context);
        CompleteRecording();
    }

    protected override SKPicture? GetResult(SvgAstEmissionContext context) => _picture;

    public override void VisitElement(SvgAstElement element, SvgAstEmissionContext context)
    {
        if (_canvas is null)
        {
            return;
        }

        var state = PushState(element);
        using var transformScope = PushTransform(element);

        RenderElement(element, state);

        foreach (var child in element.Children)
        {
            VisitNode(child, context);
        }

        _stateStack.Pop();
    }

    private void PrepareRecording(SvgAstDocument document)
    {
        _picture = null;
        _recorder = new SKPictureRecorder();
        var (viewport, viewBoxMatrix) = DetermineViewport(document.RootElement);
        _canvas = _recorder.BeginRecording(viewport);
        _canvas.SetMatrix(viewBoxMatrix);
        _stateStack.Clear();
        _stateStack.Push(new SvgAstRenderState());
    }

    private void CompleteRecording()
    {
        if (_recorder is null)
        {
            _picture = null;
            _canvas = null;
            return;
        }

        _picture = _recorder.EndRecording();
        _recorder = null;
        _canvas = null;
        _paintServers = null;
    }

    private (SKRect viewport, SKMatrix viewBoxMatrix) DetermineViewport(SvgAstElement? root)
    {
        var width = _renderOptions.DefaultViewportWidth;
        var height = _renderOptions.DefaultViewportHeight;

        if (root is not null)
        {
            if (root.TryGetAttribute("width", out var widthAttr) &&
                SvgAstRenderUtilities.TryParseNumber(widthAttr.GetValueText(), out var parsedWidth) &&
                parsedWidth > 0)
            {
                width = parsedWidth;
            }

            if (root.TryGetAttribute("height", out var heightAttr) &&
                SvgAstRenderUtilities.TryParseNumber(heightAttr.GetValueText(), out var parsedHeight) &&
                parsedHeight > 0)
            {
                height = parsedHeight;
            }
        }

        var viewport = SKRect.Create(0f, 0f, width, height);
        var matrix = SKMatrix.Identity;

        if (root is not null && SvgAstRenderUtilities.TryParseViewBox(root, out var viewBox))
        {
            matrix = SvgAstRenderUtilities.CreateViewBoxMatrix(viewBox, viewport, _renderOptions.PreserveAspectRatio);
        }

        return (viewport, matrix);
    }

    private SvgAstRenderState PushState(SvgAstElement element)
    {
        var current = _stateStack.Count > 0 ? _stateStack.Peek().Clone() : new SvgAstRenderState();
        SvgAstStyleHelper.ApplyStyles(element, current);
        _stateStack.Push(current);
        return current;
    }

    private TransformScope PushTransform(SvgAstElement element)
    {
        if (_canvas is null || !element.TryGetAttribute("transform", out var transformAttr))
        {
            return default;
        }

        var matrix = SvgAstRenderUtilities.ParseTransform(transformAttr.GetValueText());
        if (matrix.IsIdentity)
        {
            return default;
        }

        _canvas.Save();
        _canvas.SetMatrix(matrix);
        return new TransformScope(_canvas);
    }

    private void RenderElement(SvgAstElement element, SvgAstRenderState state)
    {
        if (!state.Display || !state.Visible || _canvas is null)
        {
            return;
        }

        var localName = element.Name.LocalName;
        switch (localName)
        {
            case "svg":
            case "g":
            case "defs":
                break;
            case "rect":
                DrawRectangle(element, state);
                break;
            case "circle":
                DrawCircle(element, state);
                break;
            case "ellipse":
                DrawEllipse(element, state);
                break;
            case "line":
                DrawLine(element, state);
                break;
            case "polyline":
                DrawPolyline(element, state, close: false);
                break;
            case "polygon":
                DrawPolyline(element, state, close: true);
                break;
            case "path":
                DrawPathElement(element, state);
                break;
            case "text":
                DrawText(element, state);
                break;
        }
    }

    private void DrawRectangle(SvgAstElement element, SvgAstRenderState state)
    {
        if (_canvas is null)
        {
            return;
        }

        var path = CreateRectanglePath(element);
        if (path is null)
        {
            return;
        }

        DrawPath(path, state, fillAllowed: true, strokeAllowed: true);
    }

    private void DrawCircle(SvgAstElement element, SvgAstRenderState state)
    {
        if (_canvas is null)
        {
            return;
        }

        var path = CreateCirclePath(element);
        if (path is null)
        {
            return;
        }

        DrawPath(path, state, fillAllowed: true, strokeAllowed: true);
    }

    private void DrawEllipse(SvgAstElement element, SvgAstRenderState state)
    {
        if (_canvas is null)
        {
            return;
        }

        var path = CreateEllipsePath(element);
        if (path is null)
        {
            return;
        }

        DrawPath(path, state, fillAllowed: true, strokeAllowed: true);
    }

    private void DrawLine(SvgAstElement element, SvgAstRenderState state)
    {
        if (_canvas is null)
        {
            return;
        }

        var path = CreateLinePath(element);
        if (path is null)
        {
            return;
        }

        DrawPath(path, state, fillAllowed: false, strokeAllowed: true);
    }

    private void DrawPolyline(SvgAstElement element, SvgAstRenderState state, bool close)
    {
        if (_canvas is null)
        {
            return;
        }

        var path = CreatePolylinePath(element, close);
        if (path is null)
        {
            return;
        }

        DrawPath(path, state, fillAllowed: close, strokeAllowed: true);
    }

    private void DrawPathElement(SvgAstElement element, SvgAstRenderState state)
    {
        if (_canvas is null)
        {
            return;
        }

        var path = CreatePathDataPath(element);
        if (path is null)
        {
            return;
        }

        DrawPath(path, state, fillAllowed: true, strokeAllowed: true);
    }

    private float GetNumber(SvgAstElement element, string name, float defaultValue)
    {
        if (element.TryGetAttribute(name, out var attribute) &&
            SvgAstRenderUtilities.TryParseNumber(attribute.GetValueText(), out var value))
        {
            return value;
        }

        return defaultValue;
    }

    private SKPath? CreateGeometryPath(SvgAstElement element)
    {
        return element.Name.LocalName switch
        {
            "rect" => CreateRectanglePath(element),
            "circle" => CreateCirclePath(element),
            "ellipse" => CreateEllipsePath(element),
            "line" => CreateLinePath(element),
            "polyline" => CreatePolylinePath(element, close: false),
            "polygon" => CreatePolylinePath(element, close: true),
            "path" => CreatePathDataPath(element),
            _ => null
        };
    }

    private SKPath? CreateRectanglePath(SvgAstElement element)
    {
        var width = GetNumber(element, "width", 0f);
        var height = GetNumber(element, "height", 0f);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var x = GetNumber(element, "x", 0f);
        var y = GetNumber(element, "y", 0f);
        var rx = GetNumber(element, "rx", 0f);
        var ry = GetNumber(element, "ry", rx);

        var rect = SKRect.Create(x, y, width, height);
        var path = new SKPath();
        if (rx > 0 || ry > 0)
        {
            path.Commands?.Add(new AddRoundRectPathCommand(rect, rx, ry));
        }
        else
        {
            path.Commands?.Add(new AddRectPathCommand(rect));
        }
        return path;
    }

    private SKPath? CreateCirclePath(SvgAstElement element)
    {
        var cx = GetNumber(element, "cx", 0f);
        var cy = GetNumber(element, "cy", 0f);
        var r = GetNumber(element, "r", 0f);
        if (r <= 0)
        {
            return null;
        }

        var path = new SKPath();
        path.Commands?.Add(new AddCirclePathCommand(cx, cy, r));
        return path;
    }

    private SKPath? CreateEllipsePath(SvgAstElement element)
    {
        var cx = GetNumber(element, "cx", 0f);
        var cy = GetNumber(element, "cy", 0f);
        var rx = GetNumber(element, "rx", 0f);
        var ry = GetNumber(element, "ry", 0f);
        if (rx <= 0 || ry <= 0)
        {
            return null;
        }

        var rect = SKRect.Create(cx - rx, cy - ry, rx * 2f, ry * 2f);
        var path = new SKPath();
        path.Commands?.Add(new AddOvalPathCommand(rect));
        return path;
    }

    private SKPath? CreateLinePath(SvgAstElement element)
    {
        var x1 = GetNumber(element, "x1", 0f);
        var y1 = GetNumber(element, "y1", 0f);
        var x2 = GetNumber(element, "x2", 0f);
        var y2 = GetNumber(element, "y2", 0f);

        var path = new SKPath();
        path.Commands?.Add(new MoveToPathCommand(x1, y1));
        path.Commands?.Add(new LineToPathCommand(x2, y2));
        return path;
    }

    private SKPath? CreatePolylinePath(SvgAstElement element, bool close)
    {
        if (!element.TryGetAttribute("points", out var pointsAttribute))
        {
            return null;
        }

        if (!SvgAstRenderUtilities.TryParsePoints(pointsAttribute.GetValueText(), out var points) || points.Count == 0)
        {
            return null;
        }

        var path = new SKPath();
        path.Commands?.Add(new AddPolyPathCommand(points, close));
        return path;
    }

    private SKPath? CreatePathDataPath(SvgAstElement element)
    {
        if (!element.TryGetAttribute("d", out var dAttribute))
        {
            return null;
        }

        var fillRule = UsesEvenOddFill(element);
        return SvgAstRenderUtilities.ParsePathData(dAttribute.GetValueText(), fillRule);
    }

    private static bool UsesEvenOddFill(SvgAstElement element)
    {
        return element.TryGetAttribute("fill-rule", out var fillAttr) &&
               string.Equals(fillAttr.GetValueText(), "evenodd", StringComparison.OrdinalIgnoreCase);
    }

    private void DrawPath(SKPath path, SvgAstRenderState state, bool fillAllowed, bool strokeAllowed)
    {
        if (_canvas is null || _paintServers is null)
        {
            return;
        }

        var geometryBounds = NormalizeBounds(path.Bounds);

        using var clipScope = ApplyClip(state, geometryBounds);
        using var maskScope = ApplyMask(state, geometryBounds);

        if (fillAllowed)
        {
            var fillPaint = state.CreateFillPaint(_paintServers, geometryBounds, _renderOptions.EnableAntialiasing);
            if (fillPaint is not null)
            {
                _canvas.DrawPath(path, fillPaint);
            }
        }

        if (strokeAllowed)
        {
            var strokePaint = state.CreateStrokePaint(_paintServers, geometryBounds, _renderOptions.EnableAntialiasing);
            if (strokePaint is not null)
            {
                _canvas.DrawPath(path, strokePaint);
            }
        }
    }

    private void DrawText(SvgAstElement element, SvgAstRenderState state)
    {
        if (_canvas is null || _paintServers is null)
        {
            return;
        }

        var rawText = ExtractText(element);
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return;
        }
        var text = rawText!;

        var x = GetNumber(element, "x", 0f);
        var y = GetNumber(element, "y", 0f);

        var estimatedBounds = SKRect.Create(
            x,
            y - state.FontSize,
            Math.Max(state.FontSize, state.FontSize * text.Length * 0.6f),
            state.FontSize * 1.5f);

        using var maskScope = ApplyMask(state, estimatedBounds);

        var paint = state.CreateTextPaint(_paintServers, estimatedBounds, _renderOptions.EnableAntialiasing);
        if (paint is null)
        {
            return;
        }

        _canvas.DrawText(text, x, y, paint);
    }

    private static string ExtractText(SvgAstElement element)
    {
        var builder = new StringBuilder();
        var previousWasWhitespace = true;
        AppendTextChildren(element, element.XmlSpace, builder, ref previousWasWhitespace);

        if (element.XmlSpace != SvgXmlSpace.Preserve && builder.Length > 0 && builder[builder.Length - 1] == ' ')
        {
            builder.Length--;
        }

        return builder.ToString();
    }

    private static void AppendTextChildren(SvgAstElement element, SvgXmlSpace inheritedSpace, StringBuilder builder, ref bool previousWasWhitespace)
    {
        foreach (var child in element.Children)
        {
            AppendTextNode(child, inheritedSpace, builder, ref previousWasWhitespace);
        }
    }

    private static void AppendTextNode(SvgAstNode node, SvgXmlSpace inheritedSpace, StringBuilder builder, ref bool previousWasWhitespace)
    {
        var space = node.XmlSpace == SvgXmlSpace.Default ? inheritedSpace : node.XmlSpace;
        switch (node)
        {
            case SvgAstText textNode:
                AppendTextData(textNode.ToString(), space, builder, ref previousWasWhitespace);
                break;
            case SvgAstElement childElement when string.Equals(childElement.Name.LocalName, "tspan", StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(childElement.Name.LocalName, "text", StringComparison.OrdinalIgnoreCase):
                AppendTextChildren(childElement, space, builder, ref previousWasWhitespace);
                break;
        }
    }

    private static void AppendTextData(string text, SvgXmlSpace space, StringBuilder builder, ref bool previousWasWhitespace)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (space == SvgXmlSpace.Preserve)
        {
            builder.Append(text);
            previousWasWhitespace = char.IsWhiteSpace(text[text.Length - 1]);
            return;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasWhitespace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }
            }
            else
            {
                builder.Append(ch);
                previousWasWhitespace = false;
            }
        }
    }

    private SKRect NormalizeBounds(SKRect bounds)
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

    private MaskScope ApplyMask(SvgAstRenderState state, SKRect bounds)
    {
        if (_canvas is null || _paintServers is null || string.IsNullOrEmpty(state.MaskId))
        {
            return default;
        }

        if (!_paintServers.TryGetMaskRect(state.MaskId!, bounds, out var maskRect))
        {
            return default;
        }

        _canvas.Save();
        _canvas.ClipRect(maskRect, SKClipOperation.Intersect, true);
        return new MaskScope(_canvas);
    }

    private ClipScope ApplyClip(SvgAstRenderState state, SKRect bounds)
    {
        if (_canvas is null || _paintServers is null || string.IsNullOrEmpty(state.ClipPathId))
        {
            return default;
        }

        var clipPath = _paintServers.TryCreateClipPath(state.ClipPathId!, bounds, BuildClipGeometry);
        if (clipPath is null || clipPath.IsEmpty)
        {
            return default;
        }

        _canvas.Save();
        _canvas.ClipPath(clipPath, SKClipOperation.Intersect, true);
        return new ClipScope(_canvas);
    }

    private SKPath? BuildClipGeometry(SvgAstElement element)
    {
        return CreateGeometryPath(element);
    }

    private readonly struct TransformScope : IDisposable
    {
        private readonly SKCanvas? _canvas;
        private readonly bool _applied;

        public TransformScope(SKCanvas? canvas)
        {
            _canvas = canvas;
            _applied = true;
        }

        public void Dispose()
        {
            if (_applied)
            {
                _canvas?.Restore();
            }
        }
    }

    private readonly struct MaskScope : IDisposable
    {
        private readonly SKCanvas? _canvas;
        private readonly bool _applied;

        public MaskScope(SKCanvas? canvas)
        {
            _canvas = canvas;
            _applied = true;
        }

        public void Dispose()
        {
            if (_applied)
            {
                _canvas?.Restore();
            }
        }
    }

    private readonly struct ClipScope : IDisposable
    {
        private readonly SKCanvas? _canvas;
        private readonly bool _applied;

        public ClipScope(SKCanvas? canvas)
        {
            _canvas = canvas;
            _applied = true;
        }

        public void Dispose()
        {
            if (_applied)
            {
                _canvas?.Restore();
            }
        }
    }
}
