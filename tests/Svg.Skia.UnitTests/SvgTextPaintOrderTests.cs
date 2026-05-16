using System.Collections.Generic;
using System.Linq;
using ShimSkiaSharp;
using ShimSkiaSharp.Editing;
using Xunit;

namespace Svg.Skia.UnitTests;

public class SvgTextPaintOrderTests
{
    [Fact]
    public void RetainedText_AppliesStrokeBeforeFillPaintOrder()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="80">
              <text id="target" x="12" y="54" font-family="sans-serif" font-size="48" fill="red" stroke="blue" stroke-width="8" paint-order="stroke fill">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var commands = svg.Model!
            .FindCommandsBySourceElementId<DrawTextCanvasCommand>("target")
            .Where(static command => command.Paint?.Style is SKPaintStyle.Fill or SKPaintStyle.Stroke)
            .ToList();

        Assert.Equal(2, commands.Count);
        Assert.Equal(SKPaintStyle.Stroke, commands[0].Paint!.Style);
        Assert.Equal(SKPaintStyle.Fill, commands[1].Paint!.Style);
    }

    [Fact]
    public void RetainedText_TreatsDecorationsAsMarkerPhaseForPaintOrder()
    {
        const string svgMarkup = """
            <svg xmlns="http://www.w3.org/2000/svg" width="160" height="90">
              <text id="target" x="12" y="58" font-family="sans-serif" font-size="48" fill="red" stroke="blue" stroke-width="4" text-decoration="underline" paint-order="stroke markers fill">A</text>
            </svg>
            """;

        using var svg = new SKSvg();
        svg.FromSvg(svgMarkup);

        var commands = GetTextAndDecorationPaintCommands(svg.Model!, "target");

        Assert.Equal(
            [
                (typeof(DrawTextCanvasCommand), SKPaintStyle.Stroke),
                (typeof(DrawPathCanvasCommand), SKPaintStyle.Stroke),
                (typeof(DrawPathCanvasCommand), SKPaintStyle.Fill),
                (typeof(DrawTextCanvasCommand), SKPaintStyle.Fill)
            ],
            commands);
    }

    private static List<(System.Type CommandType, SKPaintStyle Style)> GetTextAndDecorationPaintCommands(SKPicture picture, string sourceElementId)
    {
        var commands = new List<(System.Type CommandType, SKPaintStyle Style)>();
        foreach (var command in picture.FindCommandsBySourceElementId(sourceElementId))
        {
            switch (command)
            {
                case DrawTextCanvasCommand { Paint.Style: SKPaintStyle.Fill or SKPaintStyle.Stroke } drawText:
                    commands.Add((typeof(DrawTextCanvasCommand), drawText.Paint!.Style));
                    break;

                case DrawPathCanvasCommand { Paint.Style: SKPaintStyle.Fill or SKPaintStyle.Stroke } drawPath:
                    commands.Add((typeof(DrawPathCanvasCommand), drawPath.Paint!.Style));
                    break;
            }
        }

        return commands;
    }
}
