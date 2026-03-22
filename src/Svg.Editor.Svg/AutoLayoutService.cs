using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SkiaSharp;
using Svg.Editor.Core;
using Svg.Transforms;

namespace Svg.Editor.Svg;

public sealed class AutoLayoutService
{
    public const string FrameContentAttribute = "data-frame-content";
    public const string EnabledAttribute = "data-auto-layout";
    public const string FlowAttribute = "data-auto-layout-flow";
    public const string WidthModeAttribute = "data-auto-layout-width-mode";
    public const string HeightModeAttribute = "data-auto-layout-height-mode";
    public const string HorizontalAlignmentAttribute = "data-auto-layout-align-x";
    public const string VerticalAlignmentAttribute = "data-auto-layout-align-y";
    public const string GapAttribute = "data-auto-layout-gap";
    public const string PaddingHorizontalAttribute = "data-auto-layout-padding-x";
    public const string PaddingVerticalAttribute = "data-auto-layout-padding-y";
    public const string ClipContentAttribute = "data-auto-layout-clip";
    public const string ClipPathIdAttribute = "data-auto-layout-clip-id";

    private const float Epsilon = 0.01f;

    public bool IsFrameContentGroup(SvgElement? element)
    {
        return element is SvgGroup group
            && group.CustomAttributes.TryGetValue(FrameContentAttribute, out var flag)
            && string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }

    public bool TryGetFrameBackground(SvgGroup frame, out SvgRectangle background)
    {
        return FrameService.TryGetBackground(frame, out background);
    }

    public SvgGroup EnsureContentGroup(SvgGroup frame)
    {
        var contentGroup = frame.Children
            .OfType<SvgGroup>()
            .FirstOrDefault(IsFrameContentGroup);

        if (contentGroup is null)
        {
            contentGroup = new SvgGroup
            {
                ID = string.IsNullOrWhiteSpace(frame.ID) ? null : $"{frame.ID}-content"
            };
            contentGroup.CustomAttributes[FrameContentAttribute] = "true";

            var insertIndex = TryGetFrameBackground(frame, out var background)
                ? frame.Children.IndexOf(background) + 1
                : frame.Children.Count;
            if (insertIndex >= frame.Children.Count)
            {
                frame.Children.Add(contentGroup);
            }
            else
            {
                frame.Children.Insert(insertIndex, contentGroup);
            }
        }
        else
        {
            contentGroup.CustomAttributes[FrameContentAttribute] = "true";
        }

        var orphans = frame.Children
            .OfType<SvgElement>()
            .Where(child =>
                !ReferenceEquals(child, contentGroup)
                && !FrameService.IsFrameBackground(child))
            .ToList();

        foreach (var orphan in orphans)
        {
            frame.Children.Remove(orphan);
            contentGroup.Children.Add(orphan);
        }

        return contentGroup;
    }

    public SvgDefinitionList EnsureDefinitions(SvgDocument document)
    {
        var definitions = document.Children.OfType<SvgDefinitionList>().FirstOrDefault();
        if (definitions is not null)
        {
            return definitions;
        }

        definitions = new SvgDefinitionList
        {
            ID = "defs-generated"
        };
        document.Children.Insert(0, definitions);
        return definitions;
    }

    public AutoLayoutSettings ReadSettings(SvgGroup frame)
    {
        var settings = new AutoLayoutSettings
        {
            IsEnabled = ReadBool(frame, EnabledAttribute, false),
            Flow = ReadEnum(frame, FlowAttribute, AutoLayoutFlow.Vertical),
            WidthMode = ReadEnum(frame, WidthModeAttribute, AutoLayoutSizeMode.Fixed),
            HeightMode = ReadEnum(frame, HeightModeAttribute, AutoLayoutSizeMode.Fixed),
            HorizontalAlignment = ReadEnum(frame, HorizontalAlignmentAttribute, AutoLayoutAlignment.Start),
            VerticalAlignment = ReadEnum(frame, VerticalAlignmentAttribute, AutoLayoutAlignment.Start),
            Gap = ReadFloat(frame, GapAttribute, 24f),
            PaddingHorizontal = ReadFloat(frame, PaddingHorizontalAttribute, 24f),
            PaddingVertical = ReadFloat(frame, PaddingVerticalAttribute, 24f),
            ClipContent = ReadBool(frame, ClipContentAttribute, false)
        };

        return settings;
    }

    public void WriteSettings(SvgGroup frame, AutoLayoutSettings settings)
    {
        if (!settings.IsEnabled)
        {
            frame.CustomAttributes.Remove(EnabledAttribute);
            frame.CustomAttributes.Remove(FlowAttribute);
            frame.CustomAttributes.Remove(WidthModeAttribute);
            frame.CustomAttributes.Remove(HeightModeAttribute);
            frame.CustomAttributes.Remove(HorizontalAlignmentAttribute);
            frame.CustomAttributes.Remove(VerticalAlignmentAttribute);
            frame.CustomAttributes.Remove(GapAttribute);
            frame.CustomAttributes.Remove(PaddingHorizontalAttribute);
            frame.CustomAttributes.Remove(PaddingVerticalAttribute);
            frame.CustomAttributes.Remove(ClipContentAttribute);
            return;
        }

        frame.CustomAttributes[EnabledAttribute] = "true";
        frame.CustomAttributes[FlowAttribute] = settings.Flow.ToString();
        frame.CustomAttributes[WidthModeAttribute] = settings.WidthMode.ToString();
        frame.CustomAttributes[HeightModeAttribute] = settings.HeightMode.ToString();
        frame.CustomAttributes[HorizontalAlignmentAttribute] = settings.HorizontalAlignment.ToString();
        frame.CustomAttributes[VerticalAlignmentAttribute] = settings.VerticalAlignment.ToString();
        frame.CustomAttributes[GapAttribute] = settings.Gap.ToString(CultureInfo.InvariantCulture);
        frame.CustomAttributes[PaddingHorizontalAttribute] = settings.PaddingHorizontal.ToString(CultureInfo.InvariantCulture);
        frame.CustomAttributes[PaddingVerticalAttribute] = settings.PaddingVertical.ToString(CultureInfo.InvariantCulture);
        frame.CustomAttributes[ClipContentAttribute] = settings.ClipContent ? "true" : "false";
    }

    public void UpdateClipPath(SvgDocument document, SvgGroup frame, bool clipContent)
    {
        var contentGroup = EnsureContentGroup(frame);
        var definitions = document.Children.OfType<SvgDefinitionList>().FirstOrDefault();

        if (!clipContent || !TryGetFrameBackground(frame, out var background))
        {
            contentGroup.ClipPath = null!;
            if (definitions is not null
                && frame.CustomAttributes.TryGetValue(ClipPathIdAttribute, out var existingId)
                && definitions.Children.OfType<SvgClipPath>().FirstOrDefault(def => string.Equals(def.ID, existingId, StringComparison.Ordinal)) is { } clipPath)
            {
                definitions.Children.Remove(clipPath);
            }

            return;
        }

        definitions ??= EnsureDefinitions(document);
        var clipId = GetOrCreateClipPathId(frame);
        var clip = definitions.Children.OfType<SvgClipPath>().FirstOrDefault(def => string.Equals(def.ID, clipId, StringComparison.Ordinal));
        if (clip is null)
        {
            clip = new SvgClipPath { ID = clipId };
            definitions.Children.Add(clip);
        }

        clip.Children.Clear();
        clip.Children.Add(new SvgRectangle
        {
            X = new SvgUnit(background.X.Type, background.X.Value),
            Y = new SvgUnit(background.Y.Type, background.Y.Value),
            Width = new SvgUnit(background.Width.Type, background.Width.Value),
            Height = new SvgUnit(background.Height.Type, background.Height.Value),
            CornerRadiusX = new SvgUnit(background.CornerRadiusX.Type, background.CornerRadiusX.Value),
            CornerRadiusY = new SvgUnit(background.CornerRadiusY.Type, background.CornerRadiusY.Value),
            Fill = SvgPaintServer.None
        });
        contentGroup.ClipPath = new Uri($"#{clipId}", UriKind.Relative);
    }

    public bool ApplyLayout(SvgDocument document, SvgGroup frame, Func<SvgVisualElement, SKRect?> boundsProvider)
    {
        var settings = ReadSettings(frame);
        var isFrameContainer = FrameService.GetContainerKind(frame) == FrameContainerKind.Frame;
        EnsureContentGroup(frame);
        UpdateClipPath(document, frame, isFrameContainer && settings.IsEnabled && settings.ClipContent);

        if (!settings.IsEnabled
            || !isFrameContainer
            || !TryGetFrameBackground(frame, out var background))
        {
            return false;
        }

        var backgroundBounds = boundsProvider(background) ?? new SKRect(
            background.X.Value,
            background.Y.Value,
            background.X.Value + background.Width.Value,
            background.Y.Value + background.Height.Value);

        var frameWidth = Math.Max(background.Width.Value, 1f);
        var frameHeight = Math.Max(background.Height.Value, 1f);
        var padX = Math.Max(0f, settings.PaddingHorizontal);
        var padY = Math.Max(0f, settings.PaddingVertical);

        var contentGroup = EnsureContentGroup(frame);
        var items = contentGroup.Children
            .OfType<SvgVisualElement>()
            .Select(element =>
            {
                var bounds = boundsProvider(element);
                return bounds.HasValue ? new LayoutMeasurement(element, bounds.Value) : null;
            })
            .Where(static measurement => measurement is not null)
            .Cast<LayoutMeasurement>()
            .ToList();

        var arrangement = Arrange(items, settings, frameWidth, frameHeight, padX, padY);
        var nextWidth = settings.WidthMode == AutoLayoutSizeMode.Hug
            ? Math.Max(arrangement.ContentWidth + (padX * 2f), 1f)
            : frameWidth;
        var nextHeight = settings.HeightMode == AutoLayoutSizeMode.Hug
            ? Math.Max(arrangement.ContentHeight + (padY * 2f), 1f)
            : frameHeight;

        var changed = false;
        changed |= SetUnitValue(background.Width, nextWidth, value => background.Width = new SvgUnit(background.Width.Type, value));
        changed |= SetUnitValue(background.Height, nextHeight, value => background.Height = new SvgUnit(background.Height.Type, value));

        FrameService.SyncMetadata(frame);

        var innerWidth = Math.Max(nextWidth - (padX * 2f), 0f);
        var innerHeight = Math.Max(nextHeight - (padY * 2f), 0f);
        var originX = backgroundBounds.Left + padX + AlignOffset(innerWidth, arrangement.ContentWidth, settings.HorizontalAlignment, settings.WidthMode == AutoLayoutSizeMode.Fixed);
        var originY = backgroundBounds.Top + padY + AlignOffset(innerHeight, arrangement.ContentHeight, settings.VerticalAlignment, settings.HeightMode == AutoLayoutSizeMode.Fixed);

        foreach (var placement in arrangement.Placements)
        {
            changed |= MoveElementTo(
                placement.Item.Element,
                placement.Item.Bounds,
                originX + placement.OffsetX,
                originY + placement.OffsetY);
        }

        if (settings.ClipContent)
        {
            UpdateClipPath(document, frame, true);
        }

        return changed;
    }

    private static Arrangement Arrange(
        IReadOnlyList<LayoutMeasurement> items,
        AutoLayoutSettings settings,
        float frameWidth,
        float frameHeight,
        float paddingHorizontal,
        float paddingVertical)
    {
        if (items.Count == 0)
        {
            return new Arrangement([], 0f, 0f);
        }

        return settings.Flow switch
        {
            AutoLayoutFlow.Horizontal => ArrangeHorizontal(items, settings, frameWidth, frameHeight, paddingHorizontal, paddingVertical),
            AutoLayoutFlow.Wrap => ArrangeWrap(items, settings, frameWidth, frameHeight, paddingHorizontal, paddingVertical),
            AutoLayoutFlow.Grid => ArrangeGrid(items, settings, frameWidth, frameHeight, paddingHorizontal, paddingVertical),
            _ => ArrangeVertical(items, settings, frameWidth, frameHeight, paddingHorizontal, paddingVertical)
        };
    }

    private static Arrangement ArrangeHorizontal(
        IReadOnlyList<LayoutMeasurement> items,
        AutoLayoutSettings settings,
        float frameWidth,
        float frameHeight,
        float paddingHorizontal,
        float paddingVertical)
    {
        var contentWidth = items.Sum(item => item.Width) + (Math.Max(items.Count - 1, 0) * settings.Gap);
        var contentHeight = items.Max(item => item.Height);
        var placements = new List<ItemPlacement>(items.Count);
        var cursor = 0f;
        foreach (var item in items)
        {
            var offsetY = AlignOffset(contentHeight, item.Height, settings.VerticalAlignment, true);
            placements.Add(new ItemPlacement(item, cursor, offsetY));
            cursor += item.Width + settings.Gap;
        }

        return new Arrangement(placements, contentWidth, contentHeight);
    }

    private static Arrangement ArrangeVertical(
        IReadOnlyList<LayoutMeasurement> items,
        AutoLayoutSettings settings,
        float frameWidth,
        float frameHeight,
        float paddingHorizontal,
        float paddingVertical)
    {
        var contentWidth = items.Max(item => item.Width);
        var contentHeight = items.Sum(item => item.Height) + (Math.Max(items.Count - 1, 0) * settings.Gap);
        var placements = new List<ItemPlacement>(items.Count);
        var cursor = 0f;
        foreach (var item in items)
        {
            var offsetX = AlignOffset(contentWidth, item.Width, settings.HorizontalAlignment, true);
            placements.Add(new ItemPlacement(item, offsetX, cursor));
            cursor += item.Height + settings.Gap;
        }

        return new Arrangement(placements, contentWidth, contentHeight);
    }

    private static Arrangement ArrangeWrap(
        IReadOnlyList<LayoutMeasurement> items,
        AutoLayoutSettings settings,
        float frameWidth,
        float frameHeight,
        float paddingHorizontal,
        float paddingVertical)
    {
        var availableWidth = settings.WidthMode == AutoLayoutSizeMode.Fixed
            ? Math.Max(frameWidth - (paddingHorizontal * 2f), 0f)
            : float.PositiveInfinity;

        var rows = new List<RowArrangement>();
        var current = new List<LayoutMeasurement>();
        var rowWidth = 0f;
        var rowHeight = 0f;

        foreach (var item in items)
        {
            var itemWidth = item.Width;
            var nextWidth = current.Count == 0 ? itemWidth : rowWidth + settings.Gap + itemWidth;
            if (current.Count > 0 && !float.IsInfinity(availableWidth) && nextWidth > availableWidth + Epsilon)
            {
                rows.Add(new RowArrangement(current.ToList(), rowWidth, rowHeight));
                current.Clear();
                rowWidth = 0f;
                rowHeight = 0f;
            }

            current.Add(item);
            rowWidth = current.Count == 1 ? itemWidth : rowWidth + settings.Gap + itemWidth;
            rowHeight = Math.Max(rowHeight, item.Height);
        }

        if (current.Count > 0)
        {
            rows.Add(new RowArrangement(current.ToList(), rowWidth, rowHeight));
        }

        var contentWidth = rows.Count == 0 ? 0f : rows.Max(row => row.Width);
        var contentHeight = rows.Sum(row => row.Height) + (Math.Max(rows.Count - 1, 0) * settings.Gap);
        var placements = new List<ItemPlacement>(items.Count);
        var cursorY = 0f;
        foreach (var row in rows)
        {
            var rowStartX = AlignOffset(contentWidth, row.Width, settings.HorizontalAlignment, true);
            var cursorX = rowStartX;
            foreach (var item in row.Items)
            {
                var offsetY = cursorY + AlignOffset(row.Height, item.Height, settings.VerticalAlignment, true);
                placements.Add(new ItemPlacement(item, cursorX, offsetY));
                cursorX += item.Width + settings.Gap;
            }

            cursorY += row.Height + settings.Gap;
        }

        return new Arrangement(placements, contentWidth, contentHeight);
    }

    private static Arrangement ArrangeGrid(
        IReadOnlyList<LayoutMeasurement> items,
        AutoLayoutSettings settings,
        float frameWidth,
        float frameHeight,
        float paddingHorizontal,
        float paddingVertical)
    {
        var maxWidth = items.Max(item => item.Width);
        var maxHeight = items.Max(item => item.Height);
        var availableWidth = settings.WidthMode == AutoLayoutSizeMode.Fixed
            ? Math.Max(frameWidth - (paddingHorizontal * 2f), 0f)
            : float.PositiveInfinity;

        var columns = float.IsInfinity(availableWidth)
            ? Math.Max(1, (int)Math.Ceiling(Math.Sqrt(items.Count)))
            : Math.Max(1, (int)Math.Floor((availableWidth + settings.Gap) / Math.Max(maxWidth + settings.Gap, 1f)));
        var rows = (int)Math.Ceiling(items.Count / (double)columns);
        var contentWidth = (columns * maxWidth) + (Math.Max(columns - 1, 0) * settings.Gap);
        var contentHeight = (rows * maxHeight) + (Math.Max(rows - 1, 0) * settings.Gap);
        var placements = new List<ItemPlacement>(items.Count);

        for (var index = 0; index < items.Count; index++)
        {
            var row = index / columns;
            var column = index % columns;
            var cellX = column * (maxWidth + settings.Gap);
            var cellY = row * (maxHeight + settings.Gap);
            var item = items[index];
            placements.Add(new ItemPlacement(
                item,
                cellX + AlignOffset(maxWidth, item.Width, settings.HorizontalAlignment, true),
                cellY + AlignOffset(maxHeight, item.Height, settings.VerticalAlignment, true)));
        }

        return new Arrangement(placements, contentWidth, contentHeight);
    }

    private static float AlignOffset(float available, float actual, AutoLayoutAlignment alignment, bool clamp)
    {
        var remaining = available - actual;
        if (clamp)
        {
            remaining = Math.Max(remaining, 0f);
        }

        return alignment switch
        {
            AutoLayoutAlignment.Center => remaining / 2f,
            AutoLayoutAlignment.End => remaining,
            _ => 0f
        };
    }

    private static bool MoveElementTo(SvgVisualElement element, SKRect currentBounds, float targetLeft, float targetTop)
    {
        var deltaX = targetLeft - currentBounds.Left;
        var deltaY = targetTop - currentBounds.Top;
        if (Math.Abs(deltaX) <= Epsilon && Math.Abs(deltaY) <= Epsilon)
        {
            return false;
        }

        var (translateX, translateY) = GetTranslation(element);
        SetTranslation(element, translateX + deltaX, translateY + deltaY);
        return true;
    }

    private static (float X, float Y) GetTranslation(SvgVisualElement element)
    {
        if (element.Transforms is null)
        {
            return (0f, 0f);
        }

        var translate = element.Transforms.OfType<SvgTranslate>().FirstOrDefault();
        return translate is null ? (0f, 0f) : (translate.X, translate.Y);
    }

    private static void SetTranslation(SvgVisualElement element, float x, float y)
    {
        element.Transforms ??= new SvgTransformCollection();
        var translate = element.Transforms.OfType<SvgTranslate>().FirstOrDefault();
        if (translate is not null)
        {
            translate.X = x;
            translate.Y = y;
            return;
        }

        element.Transforms.Add(new SvgTranslate(x, y));
    }

    private static bool SetUnitValue(SvgUnit unit, float value, Action<float> apply)
    {
        if (Math.Abs(unit.Value - value) <= Epsilon)
        {
            return false;
        }

        apply(value);
        return true;
    }

    private static bool ReadBool(SvgGroup frame, string key, bool fallback)
    {
        return frame.CustomAttributes.TryGetValue(key, out var rawValue)
            ? string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase)
            : fallback;
    }

    private static float ReadFloat(SvgGroup frame, string key, float fallback)
    {
        return frame.CustomAttributes.TryGetValue(key, out var rawValue)
               && float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static TEnum ReadEnum<TEnum>(SvgGroup frame, string key, TEnum fallback)
        where TEnum : struct
    {
        return frame.CustomAttributes.TryGetValue(key, out var rawValue)
               && Enum.TryParse<TEnum>(rawValue, true, out var parsed)
            ? parsed
            : fallback;
    }

    private static string GetOrCreateClipPathId(SvgGroup frame)
    {
        if (frame.CustomAttributes.TryGetValue(ClipPathIdAttribute, out var existingId)
            && !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId;
        }

        var clipId = !string.IsNullOrWhiteSpace(frame.ID)
            ? $"{frame.ID}-clip"
            : $"frame-clip-{Guid.NewGuid():N}";
        frame.CustomAttributes[ClipPathIdAttribute] = clipId;
        return clipId;
    }

    private sealed class LayoutMeasurement
    {
        public LayoutMeasurement(SvgVisualElement element, SKRect bounds)
        {
            Element = element;
            Bounds = bounds;
        }

        public SvgVisualElement Element { get; }

        public SKRect Bounds { get; }

        public float Width => Math.Max(Bounds.Width, 1f);

        public float Height => Math.Max(Bounds.Height, 1f);
    }

    private sealed class ItemPlacement
    {
        public ItemPlacement(LayoutMeasurement item, float offsetX, float offsetY)
        {
            Item = item;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        public LayoutMeasurement Item { get; }

        public float OffsetX { get; }

        public float OffsetY { get; }
    }

    private sealed class Arrangement
    {
        public Arrangement(IReadOnlyList<ItemPlacement> placements, float contentWidth, float contentHeight)
        {
            Placements = placements;
            ContentWidth = contentWidth;
            ContentHeight = contentHeight;
        }

        public IReadOnlyList<ItemPlacement> Placements { get; }

        public float ContentWidth { get; }

        public float ContentHeight { get; }
    }

    private sealed class RowArrangement
    {
        public RowArrangement(IReadOnlyList<LayoutMeasurement> items, float width, float height)
        {
            Items = items;
            Width = width;
            Height = height;
        }

        public IReadOnlyList<LayoutMeasurement> Items { get; }

        public float Width { get; }

        public float Height { get; }
    }
}
