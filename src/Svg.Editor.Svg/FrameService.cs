using System;
using System.Globalization;
using System.Linq;
using Svg;
using Svg.Editor.Core;

namespace Svg.Editor.Svg;

public static class FrameService
{
    public const string FrameAttribute = "data-frame";
    public const string FrameKindAttribute = "data-frame-kind";
    public const string FrameBackgroundAttribute = "data-frame-bg";
    public const string FramePresetAttribute = "data-frame-preset";

    public static bool IsFrameLikeGroup(SvgElement? element)
    {
        return element is SvgGroup group
            && group.CustomAttributes.TryGetValue(FrameAttribute, out var flag)
            && string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFrameBackground(SvgElement? element)
    {
        return element is SvgRectangle rect
            && rect.CustomAttributes.TryGetValue(FrameBackgroundAttribute, out var flag)
            && string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static FrameContainerKind GetContainerKind(SvgGroup group)
    {
        if (!IsFrameLikeGroup(group))
        {
            return FrameContainerKind.Group;
        }

        if (group.CustomAttributes.TryGetValue(FrameKindAttribute, out var rawValue)
            && Enum.TryParse<FrameContainerKind>(rawValue, true, out var parsed)
            && parsed is FrameContainerKind.Frame or FrameContainerKind.Section)
        {
            return parsed;
        }

        return FrameContainerKind.Frame;
    }

    public static string GetContainerLabel(FrameContainerKind kind)
    {
        return kind switch
        {
            FrameContainerKind.Frame => "Frame",
            FrameContainerKind.Section => "Section",
            _ => "Group"
        };
    }

    public static bool TryGetBackground(SvgGroup group, out SvgRectangle background)
    {
        background = group.Children
            .OfType<SvgRectangle>()
            .FirstOrDefault(IsFrameBackground)!;
        return background is not null;
    }

    public static SvgRectangle CreateBackgroundRect(
        string? id,
        float x,
        float y,
        float width,
        float height,
        FrameContainerKind kind)
    {
        var background = new SvgRectangle
        {
            ID = id,
            X = new SvgUnit(SvgUnitType.User, x),
            Y = new SvgUnit(SvgUnitType.User, y),
            Width = new SvgUnit(SvgUnitType.User, Math.Max(width, 0f)),
            Height = new SvgUnit(SvgUnitType.User, Math.Max(height, 0f))
        };

        background.CustomAttributes[FrameBackgroundAttribute] = "true";
        ApplyDefaultAppearance(background, kind);
        return background;
    }

    public static SvgRectangle EnsureBackgroundRect(
        SvgGroup group,
        float x,
        float y,
        float width,
        float height)
    {
        if (!TryGetBackground(group, out var background))
        {
            background = CreateBackgroundRect(
                string.IsNullOrWhiteSpace(group.ID) ? null : $"{group.ID}-bg",
                x,
                y,
                width,
                height,
                GetContainerKind(group));
            group.Children.Insert(0, background);
        }

        background.X = new SvgUnit(background.X.Type, x);
        background.Y = new SvgUnit(background.Y.Type, y);
        background.Width = new SvgUnit(background.Width.Type, Math.Max(width, 0f));
        background.Height = new SvgUnit(background.Height.Type, Math.Max(height, 0f));
        background.CustomAttributes[FrameBackgroundAttribute] = "true";
        return background;
    }

    public static void SetContainerKind(SvgGroup group, FrameContainerKind kind)
    {
        if (kind == FrameContainerKind.Group)
        {
            group.CustomAttributes.Remove(FrameAttribute);
            group.CustomAttributes.Remove(FrameKindAttribute);
            group.CustomAttributes.Remove(FramePresetAttribute);
            group.CustomAttributes.Remove("width");
            group.CustomAttributes.Remove("height");
            return;
        }

        group.CustomAttributes[FrameAttribute] = "true";
        group.CustomAttributes[FrameKindAttribute] = kind.ToString();
    }

    public static void SetPresetId(SvgGroup group, string? presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
        {
            group.CustomAttributes.Remove(FramePresetAttribute);
            return;
        }

        group.CustomAttributes[FramePresetAttribute] = presetId;
    }

    public static string? GetPresetId(SvgGroup group)
    {
        return group.CustomAttributes.TryGetValue(FramePresetAttribute, out var presetId)
            ? presetId
            : null;
    }

    public static void SyncMetadata(SvgGroup group)
    {
        if (!TryGetBackground(group, out var background))
        {
            return;
        }

        var kind = GetContainerKind(group);
        if (kind == FrameContainerKind.Group)
        {
            kind = FrameContainerKind.Frame;
        }

        SetContainerKind(group, kind);
        group.CustomAttributes["width"] = background.Width.Value.ToString(CultureInfo.InvariantCulture);
        group.CustomAttributes["height"] = background.Height.Value.ToString(CultureInfo.InvariantCulture);
    }

    public static void ApplyDefaultAppearance(SvgRectangle background, FrameContainerKind kind)
    {
        background.CustomAttributes[FrameBackgroundAttribute] = "true";

        switch (kind)
        {
            case FrameContainerKind.Frame:
                background.CornerRadiusX = new SvgUnit(SvgUnitType.User, 20f);
                background.CornerRadiusY = new SvgUnit(SvgUnitType.User, 20f);
                background.Fill = new SvgColourServer(System.Drawing.Color.White);
                background.FillOpacity = 1f;
                background.Stroke = new SvgColourServer(System.Drawing.Color.FromArgb(216, 219, 223));
                background.StrokeOpacity = 1f;
                background.StrokeWidth = new SvgUnit(1.5f);
                break;
            case FrameContainerKind.Section:
                background.CornerRadiusX = new SvgUnit(SvgUnitType.User, 24f);
                background.CornerRadiusY = new SvgUnit(SvgUnitType.User, 24f);
                background.Fill = new SvgColourServer(System.Drawing.Color.FromArgb(242, 249, 255));
                background.FillOpacity = 1f;
                background.Stroke = new SvgColourServer(System.Drawing.Color.FromArgb(13, 153, 255));
                background.StrokeOpacity = 1f;
                background.StrokeWidth = new SvgUnit(1.5f);
                break;
            default:
                background.CornerRadiusX = new SvgUnit(SvgUnitType.User, 0f);
                background.CornerRadiusY = new SvgUnit(SvgUnitType.User, 0f);
                background.Fill = SvgPaintServer.None;
                background.FillOpacity = 1f;
                background.Stroke = SvgPaintServer.None;
                background.StrokeOpacity = 1f;
                background.StrokeWidth = new SvgUnit(0f);
                break;
        }
    }
}
