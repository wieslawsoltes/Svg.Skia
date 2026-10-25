using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

public enum SvgPrintLayout { Fit, ActualSize, Tile }
public enum SvgPrintColorMode { Rgb, Cmyk }
public enum SvgPreflightSeverity { Information, Warning, Error }

/// <summary>Physical print parameters. Page dimensions describe the finished trim size.</summary>
public sealed class SvgPrintSettings
{
    public double WidthMillimeters { get; set; } = 210;
    public double HeightMillimeters { get; set; } = 297;
    public double MarginMillimeters { get; set; } = 10;
    public double BleedMillimeters { get; set; } = 3;
    public double TileOverlapMillimeters { get; set; } = 5;
    public double Scale { get; set; } = 1;
    public double Dpi { get; set; } = 300;
    public bool CropMarks { get; set; } = true;
    public bool RegistrationMarks { get; set; }
    public SvgPrintLayout Layout { get; set; }
    public SvgPrintColorMode ColorMode { get; set; }
    public double BlackGeneration { get; set; } = 1;
    public double TotalInkLimit { get; set; } = 3;

    public void Validate()
    {
        void Range(double value, double minimum, double maximum, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(name, $"{name} must be between {minimum} and {maximum}.");
        }
        Range(WidthMillimeters, 1, 5000, nameof(WidthMillimeters));
        Range(HeightMillimeters, 1, 5000, nameof(HeightMillimeters));
        Range(MarginMillimeters, 0, Math.Min(WidthMillimeters, HeightMillimeters) / 2 - 0.01, nameof(MarginMillimeters));
        Range(BleedMillimeters, 0, 100, nameof(BleedMillimeters));
        Range(TileOverlapMillimeters, 0, Layout == SvgPrintLayout.Tile
            ? Math.Min(WidthMillimeters, HeightMillimeters) - 2 * MarginMillimeters - 0.01 : 5000, nameof(TileOverlapMillimeters));
        Range(Scale, 0.0001, 100, nameof(Scale));
        Range(Dpi, 36, 2400, nameof(Dpi));
        Range(BlackGeneration, 0, 1, nameof(BlackGeneration));
        Range(TotalInkLimit, 1, 4, nameof(TotalInkLimit));
        if (!Enum.IsDefined(typeof(SvgPrintLayout), Layout) || !Enum.IsDefined(typeof(SvgPrintColorMode), ColorMode))
            throw new ArgumentException("Choose a supported print layout and color mode.");
    }

    public string Serialize()
    {
        Validate();
        return new XElement("print", new XAttribute("version", 1),
            new XAttribute("width", WidthMillimeters), new XAttribute("height", HeightMillimeters),
            new XAttribute("margin", MarginMillimeters), new XAttribute("bleed", BleedMillimeters),
            new XAttribute("overlap", TileOverlapMillimeters), new XAttribute("scale", Scale),
            new XAttribute("dpi", Dpi), new XAttribute("crop", CropMarks), new XAttribute("registration", RegistrationMarks),
            new XAttribute("layout", Layout), new XAttribute("color", ColorMode),
            new XAttribute("black", BlackGeneration), new XAttribute("ink", TotalInkLimit)).ToString(SaveOptions.DisableFormatting);
    }

    public static SvgPrintSettings FromDocument(SvgDocument document)
    {
        var settings = new SvgPrintSettings();
        if (!document.CustomAttributes.TryGetValue("data-unodraw-print", out var value))
            return settings;
        try
        {
            if (value.Length > 16384)
                throw new ArgumentException("Print settings are too large.");
            var xml = XElement.Parse(value);
            if (xml.Name != "print" || (string?)xml.Attribute("version") != "1")
                throw new ArgumentException("Unsupported print settings version.");
            double Number(string key, double fallback) => double.TryParse((string?)xml.Attribute(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : fallback;
            bool Flag(string key, bool fallback) => bool.TryParse((string?)xml.Attribute(key), out var flag) ? flag : fallback;
            settings.WidthMillimeters = Number("width", settings.WidthMillimeters);
            settings.HeightMillimeters = Number("height", settings.HeightMillimeters);
            settings.MarginMillimeters = Number("margin", settings.MarginMillimeters);
            settings.BleedMillimeters = Number("bleed", settings.BleedMillimeters);
            settings.TileOverlapMillimeters = Number("overlap", settings.TileOverlapMillimeters);
            settings.Scale = Number("scale", settings.Scale);
            settings.Dpi = Number("dpi", settings.Dpi);
            settings.CropMarks = Flag("crop", settings.CropMarks);
            settings.RegistrationMarks = Flag("registration", settings.RegistrationMarks);
            settings.BlackGeneration = Number("black", settings.BlackGeneration);
            settings.TotalInkLimit = Number("ink", settings.TotalInkLimit);
            if (Enum.TryParse<SvgPrintLayout>((string?)xml.Attribute("layout"), out var layout))
                settings.Layout = layout;
            if (Enum.TryParse<SvgPrintColorMode>((string?)xml.Attribute("color"), out var color))
                settings.ColorMode = color;
            settings.Validate();
            return settings;
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or ArgumentException)
        {
            throw new InvalidOperationException("The document's print settings are invalid. Reset page setup before exporting.", exception);
        }
    }

    public void Apply(SvgEditorController controller)
    {
        var serialized = Serialize();
        if (controller.Document is not { } document)
            throw new InvalidOperationException("Open a document before changing page setup.");
        controller.Execute("Page setup", () => document.CustomAttributes["data-unodraw-print"] = serialized);
    }
}

public sealed class SvgPrintPage
{
    internal SvgPrintPage(int number, int column, int row, SKSize media, SKRect trim, SKRect bleed, SKMatrix artworkTransform)
        => (Number, Column, Row, MediaSize, TrimBox, BleedBox, ArtworkTransform) = (number, column, row, media, trim, bleed, artworkTransform);
    public int Number { get; }
    public int Column { get; }
    public int Row { get; }
    public SKSize MediaSize { get; }
    public SKRect TrimBox { get; }
    public SKRect BleedBox { get; }
    public SKMatrix ArtworkTransform { get; }
}

public sealed class SvgPreflightIssue
{
    public SvgPreflightIssue(string code, SvgPreflightSeverity severity, string message, string? elementId = null)
        => (Code, Severity, Message, ElementId) = (code, severity, message, elementId);
    public string Code { get; }
    public SvgPreflightSeverity Severity { get; }
    public string Message { get; }
    public string? ElementId { get; }
    public override string ToString() => $"{Severity} · {Code}{(string.IsNullOrEmpty(ElementId) ? string.Empty : " · #" + ElementId)}\n{Message}";
}

public static class SvgPrintLayoutService
{
    public const double PointsPerMillimeter = 72.0 / 25.4;
    public const double PointsPerSvgPixel = 72.0 / 96.0;

    public static IReadOnlyList<SvgPrintPage> CreatePages(SKRect artworkBounds, SvgPrintSettings settings)
    {
        if (settings is null)
            throw new ArgumentNullException(nameof(settings));
        settings.Validate();
        if (float.IsNaN(artworkBounds.Left) || float.IsInfinity(artworkBounds.Left) ||
            float.IsNaN(artworkBounds.Top) || float.IsInfinity(artworkBounds.Top) ||
            float.IsNaN(artworkBounds.Width) || float.IsInfinity(artworkBounds.Width) ||
            float.IsNaN(artworkBounds.Height) || float.IsInfinity(artworkBounds.Height) ||
            artworkBounds.Width <= 0 || artworkBounds.Height <= 0)
            throw new ArgumentException("Artwork must have finite, positive dimensions.", nameof(artworkBounds));
        var markMargin = settings.CropMarks || settings.RegistrationMarks ? 8.0 : 0;
        var inset = (settings.BleedMillimeters + markMargin) * PointsPerMillimeter;
        var width = settings.WidthMillimeters * PointsPerMillimeter;
        var height = settings.HeightMillimeters * PointsPerMillimeter;
        var margin = settings.MarginMillimeters * PointsPerMillimeter;
        var overlap = settings.TileOverlapMillimeters * PointsPerMillimeter;
        var contentWidth = width - 2 * margin;
        var contentHeight = height - 2 * margin;
        var scale = settings.Layout == SvgPrintLayout.Fit
            ? Math.Min(contentWidth / artworkBounds.Width, contentHeight / artworkBounds.Height) * settings.Scale
            : PointsPerSvgPixel * settings.Scale;
        var columnCount = settings.Layout == SvgPrintLayout.Tile
            ? Math.Max(1, Math.Ceiling((artworkBounds.Width * scale - overlap) / (contentWidth - overlap))) : 1;
        var rowCount = settings.Layout == SvgPrintLayout.Tile
            ? Math.Max(1, Math.Ceiling((artworkBounds.Height * scale - overlap) / (contentHeight - overlap))) : 1;
        if (columnCount * rowCount > 256)
            throw new InvalidOperationException("This print layout exceeds 256 pages. Reduce scale or increase the page size.");
        var columns = (int)columnCount;
        var rows = (int)rowCount;
        var trim = SKRect.Create((float)inset, (float)inset, (float)width, (float)height);
        var bleed = trim;
        bleed.Inflate((float)(settings.BleedMillimeters * PointsPerMillimeter), (float)(settings.BleedMillimeters * PointsPerMillimeter));
        var media = new SKSize((float)(width + 2 * inset), (float)(height + 2 * inset));
        var pages = new List<SvgPrintPage>(columns * rows);
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var x = settings.Layout == SvgPrintLayout.Tile ? inset + margin - column * (contentWidth - overlap)
                    : inset + (width - artworkBounds.Width * scale) / 2;
                var y = settings.Layout == SvgPrintLayout.Tile ? inset + margin - row * (contentHeight - overlap)
                    : inset + (height - artworkBounds.Height * scale) / 2;
                var transform = new SKMatrix((float)scale, 0, (float)(x - artworkBounds.Left * scale), 0, (float)scale,
                    (float)(y - artworkBounds.Top * scale), 0, 0, 1);
                pages.Add(new SvgPrintPage(pages.Count + 1, column, row, media, trim, bleed, transform));
            }
        return pages;
    }
}
