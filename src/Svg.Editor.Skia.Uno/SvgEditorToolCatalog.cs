using Svg.Editor.Skia.Uno.Models;
using Svg.Editor.Svg;

namespace Svg.Editor.Skia.Uno;

public static class SvgEditorToolCatalog
{
    public static IReadOnlyList<EditorToolDefinition> CreateDefault()
    {
        return
        [
            new EditorToolDefinition(ToolService.Tool.Select, string.Empty, "Move", "V", FigmaIconKind.Move, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Hand, string.Empty, "Hand tool", "H", FigmaIconKind.Hand, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Scale, string.Empty, "Scale", "K", FigmaIconKind.Scale, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Frame, string.Empty, "Frame", "F", FigmaIconKind.Frame, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Section, string.Empty, "Section", "⇧S", FigmaIconKind.Section, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Slice, string.Empty, "Slice", "S", FigmaIconKind.Slice, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Rect, string.Empty, "Rectangle", "R", FigmaIconKind.Rectangle, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Line, string.Empty, "Line", "L", FigmaIconKind.Line, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Arrow, string.Empty, "Arrow", "⇧L", FigmaIconKind.Arrow, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Ellipse, string.Empty, "Ellipse", "O", FigmaIconKind.Ellipse, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Polygon, string.Empty, "Polygon", string.Empty, FigmaIconKind.Polygon, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Star, string.Empty, "Star", string.Empty, FigmaIconKind.Star, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Image, string.Empty, "Image / video…", string.Empty, FigmaIconKind.Image, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.PathLine, string.Empty, "Pen", "P", FigmaIconKind.Pen, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Brush, string.Empty, "Brush", "B", FigmaIconKind.Brush, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Pencil, string.Empty, "Pencil", "⇧P", FigmaIconKind.Pencil, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Text, string.Empty, "Text", "T", FigmaIconKind.Text, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Symbol, string.Empty, "Instance", "U", FigmaIconKind.Instance, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Comment, string.Empty, "Comment", "C", FigmaIconKind.Comment, hasIcon: true),
            new EditorToolDefinition(ToolService.Tool.Freehand, string.Empty, "Freehand", string.Empty, FigmaIconKind.Brush, hasIcon: true)
        ];
    }

    public static IReadOnlyList<EditorToolGroupDefinition> CreateGroups(IReadOnlyList<EditorToolDefinition> tools)
    {
        var lookup = tools.ToDictionary(item => item.Tool);

        return
        [
            new EditorToolGroupDefinition(
                "move",
                "Move",
                EditorToolTrayMode.Design | EditorToolTrayMode.Draw | EditorToolTrayMode.Code,
                [lookup[ToolService.Tool.Select], lookup[ToolService.Tool.Hand], lookup[ToolService.Tool.Scale]]),
            new EditorToolGroupDefinition(
                "region",
                "Region",
                EditorToolTrayMode.Design | EditorToolTrayMode.Draw,
                [lookup[ToolService.Tool.Frame], lookup[ToolService.Tool.Section], lookup[ToolService.Tool.Slice]]),
            new EditorToolGroupDefinition(
                "shape",
                "Shape",
                EditorToolTrayMode.Design | EditorToolTrayMode.Draw,
                [lookup[ToolService.Tool.Rect], lookup[ToolService.Tool.Line], lookup[ToolService.Tool.Arrow], lookup[ToolService.Tool.Ellipse], lookup[ToolService.Tool.Polygon], lookup[ToolService.Tool.Star], lookup[ToolService.Tool.Image]]),
            new EditorToolGroupDefinition(
                "pen-menu",
                "Pen",
                EditorToolTrayMode.Design,
                [lookup[ToolService.Tool.PathLine], lookup[ToolService.Tool.Pencil]]),
            new EditorToolGroupDefinition(
                "text",
                "Text",
                EditorToolTrayMode.Design | EditorToolTrayMode.Draw,
                [lookup[ToolService.Tool.Text]]),
            new EditorToolGroupDefinition(
                "pen",
                "Pen",
                EditorToolTrayMode.Draw,
                [lookup[ToolService.Tool.PathLine]]),
            new EditorToolGroupDefinition(
                "brush",
                "Brush",
                EditorToolTrayMode.Draw,
                [lookup[ToolService.Tool.Brush]]),
            new EditorToolGroupDefinition(
                "pencil",
                "Pencil",
                EditorToolTrayMode.Draw,
                [lookup[ToolService.Tool.Pencil]])
        ];
    }

    public static string GetLabel(ToolService.Tool tool)
    {
        return tool switch
        {
            ToolService.Tool.Select => "Move",
            ToolService.Tool.Hand => "Hand tool",
            ToolService.Tool.Scale => "Scale",
            ToolService.Tool.Frame => "Frame",
            ToolService.Tool.Section => "Section",
            ToolService.Tool.Slice => "Slice",
            ToolService.Tool.Symbol => "Instance",
            ToolService.Tool.PathLine => "Pen",
            ToolService.Tool.Rect => "Rectangle",
            ToolService.Tool.Ellipse => "Ellipse",
            ToolService.Tool.Line => "Line",
            ToolService.Tool.Arrow => "Arrow",
            ToolService.Tool.Text => "Text",
            ToolService.Tool.Polygon => "Polygon",
            ToolService.Tool.Star => "Star",
            ToolService.Tool.Image => "Image / video",
            ToolService.Tool.Freehand => "Freehand",
            ToolService.Tool.Brush => "Brush",
            ToolService.Tool.Pencil => "Pencil",
            ToolService.Tool.Comment => "Comment",
            _ => tool.ToString()
        };
    }
}
