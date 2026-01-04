using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Svg.Model.Services;
using Svg.Skia;
using ShimSkiaSharp;

namespace Svg.Skia.Benchmarks;

[MemoryDiagnoser]
public class SkiaAssetLoaderBenchmarks
{
    private SkiaModel _skiaModel = null!;
    private SkiaSvgAssetLoader _assetLoader = null!;
    private TextEntry[] _entries = Array.Empty<TextEntry>();

    [ParamsSource(nameof(SvgNames))]
    public string SvgName { get; set; } = string.Empty;

    public IEnumerable<string> SvgNames => BenchmarkAssets.SvgNames;

    [GlobalSetup]
    public void Setup()
    {
        _skiaModel = new SkiaModel(new SKSvgSettings());
        _assetLoader = new SkiaSvgAssetLoader(_skiaModel);

        var svgText = BenchmarkAssets.GetSvgText(SvgName);
        var document = SvgService.FromSvg(svgText) ?? throw new InvalidOperationException("Failed to parse SVG document.");
        var model = SvgService.ToModel(document, _assetLoader, out _, out _) ?? throw new InvalidOperationException("Failed to build SVG model.");

        var collector = new TextEntryCollector();
        collector.Collect(model);
        _entries = collector.Freeze();

        if (_entries.Length == 0)
        {
            _entries = new[]
            {
                new TextEntry("SvgSkia", new SKPaint { TextSize = 16f }),
                new TextEntry("SvgSkia1234567890", new SKPaint { TextSize = 12f })
            };
        }
    }

    [Benchmark]
    public void FindTypefaces()
    {
        foreach (var entry in _entries)
        {
            _assetLoader.FindTypefaces(entry.Text, entry.Paint);
        }
    }

    [Benchmark]
    public void MeasureText()
    {
        var bounds = default(SKRect);
        foreach (var entry in _entries)
        {
            _assetLoader.MeasureText(entry.Text, entry.Paint, ref bounds);
        }
    }

    [Benchmark]
    public void GetFontMetrics()
    {
        foreach (var entry in _entries)
        {
            _assetLoader.GetFontMetrics(entry.Paint);
        }
    }

    [Benchmark]
    public void GetTextPath()
    {
        foreach (var entry in _entries)
        {
            _assetLoader.GetTextPath(entry.Text, entry.Paint, 0f, 0f);
        }
    }

    private sealed class TextEntryCollector
    {
        private readonly List<TextEntry> _entries = new();

        public void Collect(SKPicture picture)
        {
            if (picture.Commands is null)
            {
                return;
            }

            foreach (var command in picture.Commands)
            {
                switch (command)
                {
                    case DrawTextCanvasCommand drawTextCommand when drawTextCommand.Paint is { }:
                        AddEntry(drawTextCommand.Text, drawTextCommand.Paint);
                        break;
                    case DrawTextBlobCanvasCommand drawTextBlobCommand when drawTextBlobCommand.TextBlob?.Text is { } text && drawTextBlobCommand.Paint is { }:
                        AddEntry(text, drawTextBlobCommand.Paint);
                        break;
                    case DrawTextOnPathCanvasCommand drawTextOnPathCommand when drawTextOnPathCommand.Paint is { }:
                        AddEntry(drawTextOnPathCommand.Text, drawTextOnPathCommand.Paint);
                        break;
                }
            }
        }

        public TextEntry[] Freeze() => _entries.ToArray();

        private void AddEntry(string text, SKPaint paint)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _entries.Add(new TextEntry(text, paint));
        }
    }

    private readonly record struct TextEntry(string Text, SKPaint Paint);
}
