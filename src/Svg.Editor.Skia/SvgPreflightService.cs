using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>Inspects the native document and its retained scene before print export.</summary>
public static class SvgPreflightService
{
    public static IReadOnlyList<SvgPreflightIssue> Inspect(SvgEditorController controller, SvgPrintSettings settings,
        bool hasOutputProfile = false, double minimumImageDpi = 150)
    {
        if (controller?.Document is not { } document)
            throw new ArgumentException("Open a document before running preflight.", nameof(controller));
        settings.Validate();
        var issues = new List<SvgPreflightIssue>();
        void Report(string code, SvgPreflightSeverity severity, string message, string? id = null)
            => issues.Add(new SvgPreflightIssue(code, severity, message, id));
        var elements = new[] { (SvgElement)document }.Concat(document.Descendants()).ToArray();
        if (elements.Any(element => element.CustomAttributes.ContainsKey("data-unodraw-spot-name")))
            Report("named-inks", SvgPreflightSeverity.Warning, "Named inks are retained by the separation package. PDF export requires explicit conversion to process colors.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in elements)
            if (!string.IsNullOrEmpty(element.ID) && !ids.Add(element.ID))
                Report("duplicate-id", SvgPreflightSeverity.Error, "Duplicate IDs make resource references ambiguous.", element.ID);
        var tree = XDocument.Parse(document.GetXML());
        foreach (var element in tree.Root!.DescendantsAndSelf())
        {
            var id = (string?)element.Attribute("id");
            foreach (var attribute in element.Attributes())
            {
                foreach (Match match in Regex.Matches(attribute.Value, @"url\(\s*['""\s]*#([^)'""\s]+)"))
                    if (!ids.Contains(Uri.UnescapeDataString(match.Groups[1].Value)))
                        Report("missing-resource", SvgPreflightSeverity.Error, $"The resource #{match.Groups[1].Value} does not exist.", id);
                if (attribute.Name.LocalName != "href" || element.Name.LocalName == "a")
                    continue;
                var href = attribute.Value;
                if (href.StartsWith("#", StringComparison.Ordinal))
                {
                    if (!ids.Contains(Uri.UnescapeDataString(href.Substring(1))))
                        Report("missing-reference", SvgPreflightSeverity.Error, $"The referenced object {href} does not exist.", id);
                }
                else if (!href.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && href.Length > 0)
                {
                    Uri? uri = null;
                    var baseUri = document.BaseUri;
                    foreach (var ancestor in element.AncestorsAndSelf().Reverse())
                    {
                        var xmlBase = (string?)ancestor.Attribute(XNamespace.Xml + "base");
                        if (string.IsNullOrEmpty(xmlBase))
                            continue;
                        if (Uri.TryCreate(xmlBase, UriKind.Absolute, out var absoluteBase))
                            baseUri = absoluteBase;
                        else if (baseUri is not null && Uri.TryCreate(baseUri, xmlBase, out var relativeBase))
                            baseUri = relativeBase;
                    }
                    if (!Uri.TryCreate(href, UriKind.Absolute, out uri) && baseUri is not null)
                        Uri.TryCreate(baseUri, href, out uri);
                    var missing = uri is { IsFile: true } && !File.Exists(uri.LocalPath);
                    Report(missing ? "missing-linked-file" : "linked-resource", missing ? SvgPreflightSeverity.Error : SvgPreflightSeverity.Warning,
                        missing ? "A linked file is missing: " + href : "Embed this linked resource for a self-contained print job: " + href, id);
                }
            }
            if (element.Name.LocalName is "animate" or "animateTransform" or "animateMotion" or "set")
                Report("animated-content", SvgPreflightSeverity.Warning, "Print output captures one timeline position. Choose the frame before opening Print studio; bake it before exporting named-ink separations.", id);
            if (element.Name.LocalName is "script" or "foreignObject")
                Report("dynamic-content", SvgPreflightSeverity.Warning, "Review this dynamic or embedded content in the exported print preview.", id);
            if (int.TryParse((string?)element.Attribute("data-unodraw-text-overflow-count"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var overflow) && overflow > 0)
                Report("text-overflow", SvgPreflightSeverity.Error, "This text story has content beyond its linked frames.", id);
            if (int.TryParse((string?)element.Attribute("data-unodraw-text-overflow-horizontal"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var horizontalOverflow) && horizontalOverflow > 0)
                Report("text-width-overflow", SvgPreflightSeverity.Error, "This text story contains a grapheme wider than its frame.", id);
        }
        var scale = controller.Renderer.Picture is { } picture
            ? SvgPrintLayoutService.CreatePages(picture.CullRect, settings)[0].ArtworkTransform.ScaleX / SvgPrintLayoutService.PointsPerSvgPixel : 1;
        var checkedFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in elements)
        {
            if (element is SvgTextBase text && !string.IsNullOrWhiteSpace(text.FontFamily))
            {
                var family = text.FontFamily.Split(',')[0].Trim().Trim('\'', '"');
                if (family is not ("sans-serif" or "serif" or "monospace" or "cursive" or "fantasy") && checkedFamilies.Add(family))
                {
                    using var face = SKFontManager.Default.MatchFamily(family);
                    if (face is null || !string.Equals(face.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                        Report("font-substitution", SvgPreflightSeverity.Warning, $"Font '{family}' is unavailable or substituted on this machine.", element.ID);
                }
            }
            if (element is not SvgImage image || string.IsNullOrEmpty(image.Href))
                continue;
            try
            {
                byte[]? bytes = null;
                if (image.Href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var comma = image.Href.IndexOf(',');
                    if (comma > 0 && image.Href.Substring(0, comma).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                        bytes = Convert.FromBase64String(image.Href.Substring(comma + 1));
                }
                if (bytes is null)
                    continue;
                using var encoded = new SKMemoryStream(bytes);
                using var codec = SKCodec.Create(encoded);
                if (codec is null)
                    continue; // Embedded SVG is vector, not a low-resolution bitmap.
                if (controller.GetSceneNode(image) is not { } node)
                    continue;
                var bounds = node.TransformedBounds;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                    continue;
                var dpi = Math.Min(codec.Info.Width / bounds.Width, codec.Info.Height / bounds.Height) * 96 / scale;
                if (dpi < minimumImageDpi)
                    Report("image-resolution", SvgPreflightSeverity.Warning, $"Effective image resolution is {dpi.ToString("0", CultureInfo.InvariantCulture)} dpi; the requested minimum is {minimumImageDpi:0} dpi.", image.ID);
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                Report("invalid-image", SvgPreflightSeverity.Error, "An embedded image cannot be decoded: " + exception.Message, image.ID);
            }
        }
        if (settings.ColorMode == SvgPrintColorMode.Cmyk && !hasOutputProfile)
            Report("unprofiled-cmyk", SvgPreflightSeverity.Warning, "CMYK output uses generic arithmetic separation. Load the press ICC output profile for device-characterized color conversion.");
        if (settings.ColorMode == SvgPrintColorMode.Cmyk)
            Report("cmyk-rasterization", SvgPreflightSeverity.Information, $"CMYK print output is flattened at {settings.Dpi:0} dpi. RGB PDF output retains vector geometry and text.");
        return issues;
    }
}
