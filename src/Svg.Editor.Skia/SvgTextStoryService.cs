using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Svg.Editor.Skia;

/// <summary>
/// Native SVG storage for linked paragraph frames. Rendering uses ordinary text and tspan
/// elements; the source story, rich ranges and frame chain are retained in editor metadata.
/// </summary>
public static class SvgTextStoryService
{
    public const string StoryAttribute = "data-unodraw-text-story";
    public const string FrameAttribute = "data-unodraw-text-frame";
    public const string OverflowAttribute = "data-unodraw-text-overflow";
    public const string OverflowCountAttribute = "data-unodraw-text-overflow-count";
    public const string HorizontalOverflowAttribute = "data-unodraw-text-overflow-horizontal";
    public const string LayoutAttribute = "data-unodraw-text-layout";

    public static SvgGroup? FindStory(SvgElement? element)
    {
        for (var current = element; current is not null; current = current.Parent)
            if (current is SvgGroup group && current.CustomAttributes.ContainsKey(StoryAttribute))
                return group;
        return null;
    }

    /// <summary>Creates a frame around the selection in root SVG user units, accounting for the viewport transform.</summary>
    public static SvgTextFrameDefinition CreateFrameFromSelection(SvgEditorController controller)
    {
        if (controller?.Document is not { } document)
            throw new ArgumentException("Open a document before creating text frames.", nameof(controller));
        if (controller.SelectionBounds is not { } pictureBounds)
            return new SvgTextFrameDefinition { X = document.ViewBox.MinX + 60, Y = document.ViewBox.MinY + 60, Width = 420, Height = 300 };
        var root = controller.GetSceneNode(document) ?? controller.Renderer.RetainedSceneGraph?.Root;
        var matrix = root is null ? SkiaSharp.SKMatrix.Identity : new SkiaSharp.SKMatrix(
            root.TotalTransform.ScaleX, root.TotalTransform.SkewX, root.TotalTransform.TransX,
            root.TotalTransform.SkewY, root.TotalTransform.ScaleY, root.TotalTransform.TransY,
            root.TotalTransform.Persp0, root.TotalTransform.Persp1, root.TotalTransform.Persp2);
        if (!matrix.TryInvert(out var inverse))
            throw new InvalidOperationException("The document viewport has a singular transform.");
        var bounds = inverse.MapRect(pictureBounds);
        var width = bounds.Width > 0 ? bounds.Width : 80;
        var height = bounds.Height > 0 ? bounds.Height : 80;
        return new SvgTextFrameDefinition
        {
            X = bounds.Left,
            Y = bounds.Top,
            Width = width,
            Height = height,
            Padding = Math.Min(12, Math.Min(width, height) / 4)
        };
    }

    /// <summary>Refuses stale generated text so manual SVG/node edits are never silently overwritten.</summary>
    public static SvgTextStory Read(SvgGroup group)
    {
        if (group is null)
            throw new ArgumentNullException(nameof(group));
        if (!group.CustomAttributes.TryGetValue(StoryAttribute, out var source))
            throw new ArgumentException("The selection is not a linked text story.", nameof(group));
        if (!group.CustomAttributes.TryGetValue(LayoutAttribute, out var fingerprint) || fingerprint != Fingerprint(group))
            throw new InvalidOperationException("The story's generated SVG text was edited directly. Preserve those edits by keeping this artwork; create a new paragraph story to reflow new content.");
        return Deserialize(source);
    }

    /// <summary>
    /// Applies one atomic edit. Existing group identity, transforms, appearance and external
    /// references survive. A new story is inserted at the document root in root SVG user units.
    /// </summary>
    public static SvgGroup Apply(SvgEditorController controller, SvgTextStory story, SvgGroup? existing = null)
    {
        if (controller?.Document is not { } document)
            throw new ArgumentException("Open a document before creating text frames.", nameof(controller));
        if (existing is not null)
        {
            if (!document.Descendants().Any(element => ReferenceEquals(element, existing)))
                throw new ArgumentException("The story does not belong to this document.", nameof(existing));
            if (SvgEditorController.IsEditLocked(existing))
                throw new InvalidOperationException("Unlock the story and its frames before editing.");
            _ = Read(existing);
        }
        var layout = SvgTextLayoutService.Layout(story, controller.Renderer.AssetLoader);
        var generated = CreateGroup(story, layout);
        var target = existing ?? new SvgGroup();
        if (existing is null)
            target.ID = NewId(document, "text-story");
        controller.Execute(existing is null ? "Create paragraph frames" : "Reflow paragraph frames", () =>
        {
            target.Children.Clear();
            target.Nodes.Clear();
            foreach (var child in generated.Children.ToArray())
            {
                generated.Children.Remove(child);
                target.Children.Add(child);
            }
            foreach (var name in new[] { StoryAttribute, LayoutAttribute, OverflowAttribute, OverflowCountAttribute, HorizontalOverflowAttribute })
            {
                target.CustomAttributes.Remove(name);
                if (generated.CustomAttributes.TryGetValue(name, out var value))
                    target.CustomAttributes[name] = value;
            }
            if (existing is null)
                document.Children.Add(target);
            controller.Select(target);
        });
        return target;
    }

    /// <summary>
    /// Commits independent chains under a shared appearance container. Retaining the
    /// original group's opacity, mask, filters and transform once avoids changing the
    /// composited appearance when frames overlap after unlinking.
    /// </summary>
    public static SvgGroup ApplyIndependentStories(SvgEditorController controller, IReadOnlyList<SvgTextStory> stories, SvgGroup? existing = null)
    {
        if (stories is null || stories.Count == 0)
            throw new ArgumentException("At least one text story is required.", nameof(stories));
        if (stories.Count == 1)
            return Apply(controller, stories[0], existing);
        if (controller?.Document is not { } document)
            throw new ArgumentException("Open a document before splitting text frames.", nameof(controller));
        if (existing is not null)
        {
            if (!document.Descendants().Contains(existing) || SvgEditorController.IsEditLocked(existing))
                throw new InvalidOperationException("The text story must be editable in the current document.");
            _ = Read(existing);
        }
        var generated = stories.Select(story => CreateGroup(story, SvgTextLayoutService.Layout(story, controller.Renderer.AssetLoader))).ToArray();
        var container = existing ?? new SvgGroup { ID = NewId(document, "text-flow") };
        controller.Execute("Split linked text stories", () =>
        {
            container.Children.Clear();
            container.Nodes.Clear();
            container.Content = null;
            foreach (var name in new[] { StoryAttribute, LayoutAttribute, OverflowAttribute, OverflowCountAttribute, HorizontalOverflowAttribute })
                container.CustomAttributes.Remove(name);
            container.CustomAttributes["data-unodraw-text-flow"] = "true";
            if (existing is null)
                document.Children.Add(container);
            foreach (var story in generated)
            {
                story.ID = NewId(document, "text-story");
                container.Children.Add(story);
            }
            controller.Select(generated[0]);
        });
        return generated[0];
    }

    /// <summary>Splits a chain before the chosen frame, preserving rich text in both independent stories.</summary>
    public static (SvgTextStory Before, SvgTextStory After) SplitAtFrame(SvgTextStory story, int frameIndex, global::Svg.Model.ISvgAssetLoader assets)
    {
        SvgTextLayoutService.Validate(story);
        if (frameIndex <= 0 || frameIndex >= story.Frames.Count)
            throw new ArgumentOutOfRangeException(nameof(frameIndex), "Choose a linked frame after the first frame.");
        var layout = SvgTextLayoutService.Layout(story, assets);
        var split = layout.Lines.Where(line => line.FrameIndex >= frameIndex).Select(line => line.Start).DefaultIfEmpty(story.Text.Length).First();
        var before = Slice(story, 0, split);
        var after = Slice(story, split, story.Text.Length - split);
        before.Frames.RemoveRange(frameIndex, before.Frames.Count - frameIndex);
        after.Frames.RemoveRange(0, frameIndex);
        if (split > 0 && story.Text[split - 1] != '\n')
        {
            var paragraphIndex = story.Text.Substring(0, split).Count(c => c == '\n');
            var format = story.ParagraphOverrides.TryGetValue(paragraphIndex, out var custom) ? custom.Clone() : story.Paragraph.Clone();
            format.FirstLineIndent = 0;
            format.SpaceBefore = 0;
            after.ParagraphOverrides[0] = format;
        }
        return (before, after);
    }

    private static SvgTextStory Slice(SvgTextStory source, int start, int length)
    {
        var result = Deserialize(Serialize(source));
        result.Text = source.Text.Substring(start, length);
        result.Styles.Clear();
        foreach (var range in source.Styles)
        {
            var from = Math.Max(start, range.Start);
            var to = Math.Min(start + length, range.Start + range.Length);
            if (to > from)
                result.Styles.Add(new SvgStoryStyleRange { Start = from - start, Length = to - from, Style = range.Style.Clone() });
        }
        var paragraphOffset = source.Text.Substring(0, start).Count(c => c == '\n');
        var lastParagraph = paragraphOffset + result.Text.Count(c => c == '\n');
        result.ParagraphOverrides.Clear();
        foreach (var pair in source.ParagraphOverrides)
            if (pair.Key >= paragraphOffset && pair.Key <= lastParagraph)
                result.ParagraphOverrides.Add(pair.Key - paragraphOffset, pair.Value.Clone());
        return result;
    }

    public static SvgGroup CreateGroup(SvgTextStory story, SvgTextLayoutResult? layout = null)
    {
        SvgTextLayoutService.Validate(story);
        layout ??= SvgTextLayoutService.Layout(story);
        var group = new SvgGroup();
        for (var index = 0; index < story.Frames.Count; index++)
        {
            var frame = story.Frames[index];
            var frameGroup = new SvgGroup();
            frameGroup.CustomAttributes[FrameAttribute] = frame.Key;
            frameGroup.CustomAttributes["data-unodraw-frame-name"] = frame.Name;
            frameGroup.CustomAttributes["data-unodraw-frame-bounds"] = string.Join(" ", new[] { frame.X, frame.Y, frame.Width, frame.Height }.Select(Format));
            foreach (var line in layout.Lines.Where(value => value.FrameIndex == index))
            {
                var text = new SvgText { SpaceHandling = XmlSpaceHandling.Preserve, TextAnchor = SvgTextAnchor.Start };
                text.CustomAttributes["data-unodraw-text-start"] = line.Start.ToString(CultureInfo.InvariantCulture);
                foreach (var run in line.Runs)
                {
                    var span = new SvgTextSpan
                    {
                        Text = run.Text,
                        X = new SvgUnitCollection { line.RightToLeft ? run.X + run.Advance + line.JustificationSpacing * run.Text.Count(c => c == ' ') : run.X },
                        Y = new SvgUnitCollection { line.Baseline },
                        FontFamily = run.FontFamily,
                        FontSize = run.Style.FontSize,
                        FontWeight = FontWeight(run.Style.FontWeight),
                        FontStyle = run.Style.Italic ? SvgFontStyle.Italic : SvgFontStyle.Normal,
                        Fill = (SvgPaintServer)TypeDescriptor.GetConverter(typeof(SvgPaintServer)).ConvertFromInvariantString(run.Style.Fill)!,
                        LetterSpacing = run.Style.LetterSpacing,
                        WordSpacing = line.JustificationSpacing,
                        SpaceHandling = XmlSpaceHandling.Preserve
                    };
                    span.CustomAttributes["font-feature-settings"] = run.Style.FontFeatureSettings;
                    if (line.RightToLeft)
                    {
                        span.CustomAttributes["direction"] = "rtl";
                        span.CustomAttributes["unicode-bidi"] = "embed";
                    }
                    text.Children.Add(span);
                    text.Nodes.Add(span);
                }
                frameGroup.Children.Add(text);
            }
            group.Children.Add(frameGroup);
        }
        group.CustomAttributes[StoryAttribute] = Serialize(story);
        if (layout.HasOverflow)
        {
            group.CustomAttributes[OverflowAttribute] = layout.OverflowStart.ToString(CultureInfo.InvariantCulture);
            group.CustomAttributes[OverflowCountAttribute] = layout.OverflowLength.ToString(CultureInfo.InvariantCulture);
        }
        var horizontal = layout.Lines.Count(line => line.Width > line.AvailableWidth + 0.001f);
        if (horizontal > 0)
            group.CustomAttributes[HorizontalOverflowAttribute] = horizontal.ToString(CultureInfo.InvariantCulture);
        // Adopt the same native attribute/mixed-content representation used after a
        // normal save/reload. This keeps the first undo-to-save checkpoint stable too.
        var container = new SvgDocument();
        container.Children.Add(group);
        var normalized = SvgDocument.FromSvg<SvgDocument>(container.GetXML());
        group = (SvgGroup)normalized.Children[0];
        normalized.Children.Remove(group);
        // Parsed deferred paints retain their temporary parse document. Recreate them
        // without that document so the actual owning SVG resolves shared gradients and
        // swatches, including later edits to those resources.
        var spans = group.Descendants().OfType<SvgTextSpan>().ToArray();
        var styles = layout.Lines.SelectMany(line => line.Runs).Select(run => run.Style).ToArray();
        for (var index = 0; index < spans.Length; index++)
            spans[index].Fill = (SvgPaintServer)TypeDescriptor.GetConverter(typeof(SvgPaintServer)).ConvertFromInvariantString(styles[index].Fill)!;
        group.CustomAttributes[LayoutAttribute] = Fingerprint(group);
        return group;
    }

    /// <summary>
    /// Edits story text without flattening surviving styled ranges. Unchanged prefix/suffix
    /// ranges retain their style, and inserted text receives the style at the edit boundary.
    /// </summary>
    public static void ReplaceText(SvgTextStory story, string value)
    {
        if (story is null || value is null)
            throw new ArgumentNullException(story is null ? nameof(story) : nameof(value));
        value = value.Replace("\r\n", "\n").Replace('\r', '\n');
        if (value.Length > SvgTextLayoutService.MaximumCharacters)
            throw new ArgumentException("A story accepts up to 200,000 characters.", nameof(value));
        XmlConvert.VerifyXmlChars(value);
        var old = story.Text;
        if (old == value)
            return;
        var prefix = 0;
        while (prefix < old.Length && prefix < value.Length && old[prefix] == value[prefix])
            prefix++;
        if (prefix > 0 && prefix < old.Length && char.IsLowSurrogate(old[prefix]))
            prefix--;
        var suffix = 0;
        while (suffix < old.Length - prefix && suffix < value.Length - prefix && old[old.Length - 1 - suffix] == value[value.Length - 1 - suffix])
            suffix++;
        if (suffix > 0 && char.IsLowSurrogate(old[old.Length - suffix]))
            suffix--;
        var previousEnd = old.Length - suffix;
        var newEnd = value.Length - suffix;
        var delta = value.Length - old.Length;
        var preserved = new List<SvgStoryStyleRange>();
        var insertionStyle = story.Styles.LastOrDefault(range => range.Start <= prefix && range.Start + range.Length > prefix)?.Style;
        foreach (var range in story.Styles)
        {
            var end = range.Start + range.Length;
            if (range.Start < prefix)
                preserved.Add(new SvgStoryStyleRange { Start = range.Start, Length = Math.Min(end, prefix) - range.Start, Style = range.Style.Clone() });
            if (end > previousEnd)
            {
                var start = Math.Max(range.Start, previousEnd);
                preserved.Add(new SvgStoryStyleRange { Start = start + delta, Length = end - start, Style = range.Style.Clone() });
            }
        }
        if (insertionStyle is not null && newEnd > prefix)
            preserved.Add(new SvgStoryStyleRange { Start = prefix, Length = newEnd - prefix, Style = insertionStyle.Clone() });
        story.Styles.Clear();
        story.Styles.AddRange(preserved.Where(range => range.Length > 0).OrderBy(range => range.Start));
        // Overrides are attached to existing paragraph starts, not arbitrary numeric rows.
        var oldStarts = ParagraphStarts(old);
        var newStarts = ParagraphStarts(value);
        var overrides = new Dictionary<int, SvgParagraphFormat>();
        foreach (var pair in story.ParagraphOverrides)
        {
            if (pair.Key >= oldStarts.Count)
                continue;
            var start = oldStarts[pair.Key];
            var mapped = start <= prefix ? start : start >= previousEnd ? start + delta : -1;
            var newIndex = newStarts.IndexOf(mapped);
            if (newIndex >= 0)
                overrides[newIndex] = pair.Value;
        }
        story.ParagraphOverrides.Clear();
        foreach (var pair in overrides)
            story.ParagraphOverrides.Add(pair.Key, pair.Value);
        story.Text = value;
    }

    public static void ApplyStyle(SvgTextStory story, int start, int length, SvgStoryTextStyle style)
    {
        if (story is null || style is null)
            throw new ArgumentNullException(story is null ? nameof(story) : nameof(style));
        if (start < 0 || length <= 0 || start > story.Text.Length - length)
            throw new ArgumentOutOfRangeException(nameof(start));
        var replacement = new List<SvgStoryStyleRange>();
        var end = start + length;
        foreach (var range in story.Styles)
        {
            var rangeEnd = range.Start + range.Length;
            if (rangeEnd <= start || range.Start >= end)
                replacement.Add(range);
            else
            {
                if (range.Start < start)
                    replacement.Add(new SvgStoryStyleRange { Start = range.Start, Length = start - range.Start, Style = range.Style.Clone() });
                if (rangeEnd > end)
                    replacement.Add(new SvgStoryStyleRange { Start = end, Length = rangeEnd - end, Style = range.Style.Clone() });
            }
        }
        replacement.Add(new SvgStoryStyleRange { Start = start, Length = length, Style = style.Clone() });
        var clone = Deserialize(Serialize(story));
        clone.Styles.Clear();
        clone.Styles.AddRange(replacement.OrderBy(range => range.Start));
        SvgTextLayoutService.Validate(clone);
        story.Styles.Clear();
        story.Styles.AddRange(clone.Styles);
    }

    public static string Serialize(SvgTextStory story)
    {
        SvgTextLayoutService.Validate(story);
        var root = new XElement("story", new XAttribute("version", 1), new XElement("content", story.Text),
            WriteStyle(story.DefaultStyle), WriteParagraph(story.Paragraph));
        root.Add(new XElement("frames", story.Frames.Select(frame => new XElement("frame",
            new XAttribute("key", frame.Key), new XAttribute("name", frame.Name),
            new XAttribute("x", Format(frame.X)), new XAttribute("y", Format(frame.Y)),
            new XAttribute("width", Format(frame.Width)), new XAttribute("height", Format(frame.Height)),
            new XAttribute("padding", Format(frame.Padding)), new XAttribute("columns", frame.Columns), new XAttribute("gap", Format(frame.ColumnGap))))));
        root.Add(new XElement("ranges", story.Styles.Select(range => new XElement("range", new XAttribute("start", range.Start),
            new XAttribute("length", range.Length), WriteStyle(range.Style)))));
        root.Add(new XElement("paragraphs", story.ParagraphOverrides.OrderBy(pair => pair.Key).Select(pair => new XElement("override",
            new XAttribute("index", pair.Key), WriteParagraph(pair.Value)))));
        return root.ToString(SaveOptions.DisableFormatting);
    }

    public static SvgTextStory Deserialize(string xml)
    {
        if (xml is null || xml.Length > 4 * 1024 * 1024)
            throw new ArgumentException("Invalid paragraph story metadata.", nameof(xml));
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 });
        var root = XElement.Load(reader);
        if (root.Name != "story" || (string?)root.Attribute("version") != "1")
            throw new FormatException("Unsupported paragraph story version.");
        var story = new SvgTextStory
        {
            Text = (string?)root.Element("content") ?? string.Empty,
            DefaultStyle = ReadStyle(Required(root, "style")),
            Paragraph = ReadParagraph(Required(root, "paragraph"))
        };
        foreach (var frame in Required(root, "frames").Elements("frame"))
            story.Frames.Add(new SvgTextFrameDefinition
            {
                Key = (string?)frame.Attribute("key") ?? string.Empty,
                Name = (string?)frame.Attribute("name") ?? "Text frame",
                X = Number(frame, "x"),
                Y = Number(frame, "y"),
                Width = Number(frame, "width"),
                Height = Number(frame, "height"),
                Padding = Number(frame, "padding"),
                Columns = Integer(frame, "columns"),
                ColumnGap = Number(frame, "gap")
            });
        foreach (var range in root.Element("ranges")?.Elements("range") ?? Enumerable.Empty<XElement>())
            story.Styles.Add(new SvgStoryStyleRange { Start = Integer(range, "start"), Length = Integer(range, "length"), Style = ReadStyle(Required(range, "style")) });
        foreach (var paragraph in root.Element("paragraphs")?.Elements("override") ?? Enumerable.Empty<XElement>())
            story.ParagraphOverrides.Add(Integer(paragraph, "index"), ReadParagraph(Required(paragraph, "paragraph")));
        SvgTextLayoutService.Validate(story);
        return story;
    }

    private static XElement WriteStyle(SvgStoryTextStyle style) => new("style", new XAttribute("family", style.FontFamily),
        new XAttribute("size", Format(style.FontSize)), new XAttribute("weight", style.FontWeight), new XAttribute("italic", style.Italic),
        new XAttribute("fill", style.Fill), new XAttribute("tracking", Format(style.LetterSpacing)), new XAttribute("features", style.FontFeatureSettings));

    private static SvgStoryTextStyle ReadStyle(XElement element) => new()
    {
        FontFamily = (string?)element.Attribute("family") ?? "sans-serif",
        FontSize = Number(element, "size"),
        FontWeight = Integer(element, "weight"),
        Italic = bool.Parse((string?)element.Attribute("italic") ?? "false"),
        Fill = (string?)element.Attribute("fill") ?? "#000000",
        LetterSpacing = Number(element, "tracking"),
        FontFeatureSettings = (string?)element.Attribute("features") ?? "normal"
    };

    private static XElement WriteParagraph(SvgParagraphFormat format) => new("paragraph", new XAttribute("align", format.Alignment),
        new XAttribute("line", Format(format.LineHeight)), new XAttribute("before", Format(format.SpaceBefore)), new XAttribute("after", Format(format.SpaceAfter)),
        new XAttribute("first", Format(format.FirstLineIndent)), new XAttribute("left", Format(format.LeftIndent)), new XAttribute("right", Format(format.RightIndent)), new XAttribute("rtl", format.RightToLeft));

    private static SvgParagraphFormat ReadParagraph(XElement element) => new()
    {
        Alignment = (SvgParagraphAlignment)Enum.Parse(typeof(SvgParagraphAlignment), (string?)element.Attribute("align") ?? "Start"),
        LineHeight = Number(element, "line"),
        SpaceBefore = Number(element, "before"),
        SpaceAfter = Number(element, "after"),
        FirstLineIndent = Number(element, "first"),
        LeftIndent = Number(element, "left"),
        RightIndent = Number(element, "right"),
        RightToLeft = bool.Parse((string?)element.Attribute("rtl") ?? "false")
    };

    private static string Fingerprint(SvgGroup group)
    {
        var xml = new StringBuilder();
        foreach (var child in group.Children)
        {
            var buffer = new StringBuilder();
            using (var writer = XmlWriter.Create(buffer, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = false }))
                child.Write(writer);
            var element = XElement.Parse(buffer.ToString(), LoadOptions.PreserveWhitespace);
            foreach (var current in element.DescendantsAndSelf())
            {
                // Newly constructed SVG.NET elements inherit the SVG namespace only
                // when written inside a document. Parsed elements store it explicitly.
                if (current.Name.NamespaceName.Length == 0)
                    current.Name = XName.Get(current.Name.LocalName, "http://www.w3.org/2000/svg");
                // The document writer adds indentation between explicitly positioned
                // spans. Preserve whitespace inside each span, where it is real text.
                if (current.Name.LocalName is not ("tspan" or "textPath"))
                    current.Nodes().OfType<XText>().Where(node => string.IsNullOrWhiteSpace(node.Value)).Remove();
                var attributes = current.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration).OrderBy(attribute => attribute.Name.ToString(), StringComparer.Ordinal)
                    .Select(attribute => new XAttribute(attribute)).ToArray();
                current.RemoveAttributes();
                current.Add(attributes);
            }
            xml.Append(element.ToString(SaveOptions.DisableFormatting));
        }
        using var hash = SHA256.Create();
        return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(xml.ToString())));
    }

    private static SvgFontWeight FontWeight(int weight) => weight switch
    {
        < 150 => SvgFontWeight.W100,
        < 250 => SvgFontWeight.W200,
        < 350 => SvgFontWeight.W300,
        < 450 => SvgFontWeight.W400,
        < 550 => SvgFontWeight.W500,
        < 650 => SvgFontWeight.W600,
        < 750 => SvgFontWeight.W700,
        < 850 => SvgFontWeight.W800,
        _ => SvgFontWeight.W900
    };

    private static string NewId(SvgDocument document, string prefix)
    {
        var used = new HashSet<string>(document.Descendants().Select(element => element.ID).Where(id => id is not null), StringComparer.Ordinal);
        if (document.ID is not null)
            used.Add(document.ID);
        var id = prefix;
        for (var index = 2; used.Contains(id); index++)
            id = prefix + "-" + index.ToString(CultureInfo.InvariantCulture);
        return id;
    }

    private static List<int> ParagraphStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < text.Length; index++)
            if (text[index] == '\n')
                starts.Add(index + 1);
        return starts;
    }

    private static XElement Required(XElement element, string name) => element.Element(name) ?? throw new FormatException("Missing story " + name + ".");
    private static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static float Number(XElement element, string name) => float.Parse((string?)element.Attribute(name) ?? throw new FormatException("Missing " + name), CultureInfo.InvariantCulture);
    private static int Integer(XElement element, string name) => int.Parse((string?)element.Attribute(name) ?? throw new FormatException("Missing " + name), CultureInfo.InvariantCulture);
}
