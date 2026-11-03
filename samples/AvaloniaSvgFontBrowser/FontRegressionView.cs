// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using SkiaSharp;
using Svg.Skia;

namespace AvaloniaSvgFontBrowser;

internal sealed class FontRegressionView : UserControl
{
    private const string EmbeddedFamily = "avares://Avalonia.Fonts.Inter/Assets#Inter";
    private const string Label = "Flow 42 — Zażółć gęślą jaźń";
    private const int ImageWidth = 660;
    private const int ImageHeight = 50;
    private readonly StackPanel _rows = new() { Spacing = 12, Margin = new Thickness(24) };
    private readonly List<SvgSource> _sources = new();
    private int _run;

    private sealed record FontCase(string Name, string? Family, int Weight = 400, bool Italic = false, bool UseAppDefault = false);

    private static readonly FontCase[] s_cases =
    {
        new("Inter regular", "Inter"),
        new("Inter bold", "Inter", 700),
        new("Inter italic", "Inter", Italic: true),
        new("CSS family order", "'Missing SVG Font', Inter, sans-serif"),
        new("Embedded font URI", EmbeddedFamily),
        new("Generic sans-serif", "sans-serif", UseAppDefault: true),
        new("Unspecified family", null, UseAppDefault: true),
        new("Mermaid family list", "'trebuchet ms', verdana, arial, sans-serif", UseAppDefault: true)
    };

    public FontRegressionView()
    {
        Background = new SolidColorBrush(Color.Parse("#F3F5F9"));
        Content = new ScrollViewer { Content = _rows };
    }

    internal void RunChecks()
    {
        _rows.Children.Clear();
        foreach (var source in _sources)
        {
            source.Dispose();
        }
        _sources.Clear();
        _run++;

        var output = new StringBuilder();
        var passed = 0;

        try
        {
            if (SvgSource.DefaultSettings.TypefaceProviders?.Any(provider => provider is AvaloniaTypefaceProvider) != true)
            {
                throw new InvalidOperationException("The automatic Avalonia typeface provider is missing.");
            }

            foreach (var test in s_cases)
            {
                var source = SvgSource.LoadFromSvg(CreateSvg(test));
                _sources.Add(source);
                AddRow(test, source);

                try
                {
                    var family = Validate(test, source);
                    output.AppendLine($"PASS  {test.Name}: {family}, recorded glyphs checked, pixels match explicit font");
                    passed++;
                }
                catch (Exception exception)
                {
                    output.AppendLine($"FAIL  {test.Name}: {exception.Message}");
                }
            }
        }
        catch (Exception exception)
        {
            output.AppendLine($"FAIL  Setup: {exception}");
        }

        output.Insert(0, $"Run {_run}: {passed}/{s_cases.Length} font cases passed in WebAssembly.\n" +
            $"Avalonia default: {FontManager.Current.DefaultFontFamily}.\n");
        Program.Report(output.ToString(), passed == s_cases.Length);
    }

    private void AddRow(FontCase test, SvgSource source)
    {
        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text = test.Name,
            FontWeight = FontWeight.SemiBold,
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.Parse("#334155"))
        });
        content.Children.Add(new TextBlock
        {
            Text = "Avalonia text",
            FontSize = 11,
            Foreground = Brushes.SlateGray
        });
        content.Children.Add(new TextBlock
        {
            Text = Label,
            FontFamily = GetExpectedFamily(test),
            FontSize = 26,
            FontWeight = (FontWeight)test.Weight,
            FontStyle = test.Italic ? FontStyle.Italic : FontStyle.Normal,
            Foreground = new SolidColorBrush(Color.Parse("#172554"))
        });
        content.Children.Add(new TextBlock
        {
            Text = "Svg.Skia rendering",
            FontSize = 11,
            Foreground = Brushes.SlateGray
        });
        content.Children.Add(new Image
        {
            Source = new SvgImage { Source = source },
            Width = ImageWidth,
            Height = ImageHeight,
            MaxWidth = ImageWidth,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left
        });

        _rows.Children.Add(new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#DDE4EE")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 12),
            Child = content
        });
    }

    private static string Validate(FontCase test, SvgSource source)
    {
        var svg = source.Svg ?? throw new InvalidOperationException("No SVG renderer was created.");
        if (svg.Settings.EnableTextSelectionRendering)
        {
            throw new InvalidOperationException("ConfigureDefaultSettings was not applied to the SVG.");
        }

        var modelTypeface = ShimSkiaSharp.SKTypeface.FromFamilyName(
            test.Family!,
            (ShimSkiaSharp.SKFontStyleWeight)test.Weight,
            ShimSkiaSharp.SKFontStyleWidth.Normal,
            test.Italic ? ShimSkiaSharp.SKFontStyleSlant.Italic : ShimSkiaSharp.SKFontStyleSlant.Upright);
        var expectedFamily = GetExpectedFamily(test);
        var expectedTypeface = new Typeface(expectedFamily,
            test.Italic ? FontStyle.Italic : FontStyle.Normal, (FontWeight)test.Weight);
        if (!FontManager.Current.TryGetGlyphTypeface(expectedTypeface, out var expectedGlyphTypeface))
        {
            throw new InvalidOperationException($"Avalonia could not resolve '{expectedFamily}'.");
        }

        using var font = svg.SkiaModel.ToSKFont(new ShimSkiaSharp.SKFont(modelTypeface, 26))
            ?? throw new InvalidOperationException("No font was created.");
        var familyName = expectedGlyphTypeface.FamilyName;
        ValidateFont(font, test, familyName, Label);

        if (ValidateRecordedText(svg.SkiaModel, svg.Model, test, familyName) == 0)
        {
            throw new InvalidOperationException("The SVG did not record any text commands.");
        }

        using var reference = SvgSource.LoadFromSvg(CreateSvg(test with { Family = expectedFamily.ToString() }));
        using var actualPixels = Rasterize(source);
        using var expectedPixels = Rasterize(reference);
        var actual = actualPixels.Pixels;
        if (!actual.Any(pixel => pixel.Alpha > 0))
        {
            throw new InvalidOperationException("SVG text produced no visible pixels.");
        }

        if (!actual.AsSpan().SequenceEqual(expectedPixels.Pixels))
        {
            throw new InvalidOperationException("Rendered pixels differ from the explicit-font reference.");
        }

        return familyName;
    }

    private static FontFamily GetExpectedFamily(FontCase test) => test.UseAppDefault
        ? FontManager.Current.DefaultFontFamily
        : new FontFamily(EmbeddedFamily);

    private static void ValidateFont(SKFont font, FontCase test, string expectedFamily, string text)
    {
        var typeface = font.Typeface ?? throw new InvalidOperationException("The recorded font has no typeface.");
        if (!typeface.FamilyName.Equals(expectedFamily, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Resolved '{typeface.FamilyName}' instead of Avalonia's '{expectedFamily}'.");
        }

        foreach (var rune in text.EnumerateRunes())
        {
            if (!font.ContainsGlyph(rune.Value))
            {
                throw new InvalidOperationException($"Missing glyph U+{rune.Value:X4} in '{expectedFamily}'.");
            }
        }

        if (typeface.FontStyle.Weight != test.Weight ||
            (test.Italic && typeface.FontStyle.Slant == SKFontStyleSlant.Upright && font.SkewX == 0))
        {
            throw new InvalidOperationException($"Requested font style was not preserved: {typeface.FontStyle}.");
        }
    }

    private static int ValidateRecordedText(SkiaModel model, ShimSkiaSharp.SKPicture? picture, FontCase test, string family)
    {
        var count = 0;
        foreach (var command in picture?.Commands ?? Array.Empty<ShimSkiaSharp.CanvasCommand>())
        {
            switch (command)
            {
                case ShimSkiaSharp.DrawPictureCanvasCommand nested:
                    count += ValidateRecordedText(model, nested.Picture, test, family);
                    break;
                case ShimSkiaSharp.DrawTextCanvasCommand text:
                    Check(text.Font, text.Paint, text.Text);
                    break;
                case ShimSkiaSharp.DrawTextOnPathCanvasCommand path:
                    Check(path.Font, path.Paint, path.Text);
                    break;
                case ShimSkiaSharp.DrawPositionedTextRunCanvasCommand run:
                    Check(run.Font, run.Paint, string.Concat(run.Fragments?.Select(fragment => fragment.Text) ?? Array.Empty<string>()));
                    break;
                case ShimSkiaSharp.DrawTextBlobCanvasCommand blob:
                    if (blob.TextBlob?.Glyphs?.Contains((ushort)0) == true)
                    {
                        throw new InvalidOperationException("The recorded text blob contains a missing glyph.");
                    }
                    Check(blob.TextBlob?.Font, blob.Paint, blob.TextBlob?.Text ?? string.Empty);
                    break;
            }
        }

        return count;

        void Check(ShimSkiaSharp.SKFont? sourceFont, ShimSkiaSharp.SKPaint? paint, string text)
        {
            using var font = sourceFont is not null ? model.ToSKFont(sourceFont)
                : paint is not null ? model.ToSKFont(paint) : null;
            if (font is null)
            {
                throw new InvalidOperationException("A recorded text command has no font.");
            }
            ValidateFont(font, test, family, text);
            count++;
        }
    }

    private static SKBitmap Rasterize(SvgSource source)
    {
        var picture = source.Picture ?? throw new InvalidOperationException("SVG did not produce a picture.");
        var bitmap = new SKBitmap(ImageWidth, ImageHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(picture);
        canvas.Flush();
        return bitmap;
    }

    private static string CreateSvg(FontCase test)
    {
        var family = test.Family is null ? string.Empty : $" font-family=\"{Escape(test.Family)}\"";
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{ImageWidth}\" height=\"{ImageHeight}\">" +
            $"<text x=\"0\" y=\"34\" font-size=\"26\" font-weight=\"{test.Weight}\"" +
            $" font-style=\"{(test.Italic ? "italic" : "normal")}\" fill=\"#172554\"{family}>{Escape(Label)}</text></svg>";
    }

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}
