using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Core;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private void InitializePathUtilityStatus()
    {
        Canvas.MeasurementChanged += (_, _) =>
        {
            if (Canvas.LastMeasurement is { } value)
                StatusText.Text = $"Distance {value.Distance:0.###}  ·  ΔX {value.DeltaX:0.###}  ΔY {value.DeltaY:0.###}  ·  Angle {value.AngleDegrees:0.##}°  ·  SVG user units";
            else if (Canvas.InteractionMode == SvgEditorInteractionMode.Measure)
                StatusText.Text = "Measure · Drag between two points · Shift constrains to 45°";
        };
    }

    private async Task<bool> ShowPathUtilityOptionsAsync()
    {
        var mode = Canvas.InteractionMode;
        if (SvgEditorCanvas.TryGetRefinementKind(mode, out _) || mode == SvgEditorInteractionMode.BlobBrush)
        {
            var currentDiameter = mode switch
            {
                SvgEditorInteractionMode.Smooth => Canvas.SmoothDiameter,
                SvgEditorInteractionMode.PathEraser => Canvas.PathEraserDiameter,
                SvgEditorInteractionMode.Join => Canvas.JoinDiameter,
                _ => Canvas.BlobBrushDiameter
            };
            var diameter = NumberInput("Brush diameter", currentDiameter, 0.1, 10000);
            var strength = NumberInput("Smoothing (%)", Canvas.SmoothStrength * 100, 1, 100);
            var merge = new CheckBox { Content = "Merge touching fills of the same color", IsChecked = Canvas.BlobBrushMerge };
            var content = new StackPanel { Spacing = 10, Width = DialogContentWidth(390) };
            content.Children.Add(diameter);
            if (mode == SvgEditorInteractionMode.Smooth)
                content.Children.Add(strength);
            if (mode == SvgEditorInteractionMode.BlobBrush)
                content.Children.Add(merge);
            content.Children.Add(StudioHint(mode switch
            {
                SvgEditorInteractionMode.Smooth => "Brush near anchors to reduce sharp changes while preserving open endpoints. Select artwork to restrict the brush.",
                SvgEditorInteractionMode.PathEraser => "Remove portions of a path while keeping its editable centerline, curves and stroke appearance.",
                SvgEditorInteractionMode.Join => "Brush across two open endpoints to join them. Joined contours take the first path's appearance.",
                _ => "Paint filled vector shapes with round ends. The active fill color controls the new artwork."
            }));
            if (await ShowDialogAsync(MakeDialog(ToolContextLabel() + " Options", content)) == ContentDialogResult.Primary)
            {
                switch (mode)
                {
                    case SvgEditorInteractionMode.Smooth:
                        Canvas.SmoothDiameter = (float)diameter.Value;
                        Canvas.SmoothStrength = (float)strength.Value / 100;
                        break;
                    case SvgEditorInteractionMode.PathEraser:
                        Canvas.PathEraserDiameter = (float)diameter.Value;
                        break;
                    case SvgEditorInteractionMode.Join:
                        Canvas.JoinDiameter = (float)diameter.Value;
                        break;
                    default:
                        Canvas.BlobBrushDiameter = (float)diameter.Value;
                        Canvas.BlobBrushMerge = merge.IsChecked == true;
                        break;
                }
            }
            return true;
        }
        if (mode == SvgEditorInteractionMode.Measure)
        {
            var content = new StackPanel { Spacing = 10, Width = DialogContentWidth(360) };
            if (Canvas.LastMeasurement is { } value)
            {
                content.Children.Add(StudioHint($"Start: {value.Start.X:0.###}, {value.Start.Y:0.###}\nEnd: {value.End.X:0.###}, {value.End.Y:0.###}\nDistance: {value.Distance:0.###}\nHorizontal: {value.DeltaX:0.###}\nVertical: {value.DeltaY:0.###}\nAngle: {value.AngleDegrees:0.##}°"));
                content.Children.Add(StudioHint("Dimensions use SVG user units, including the document's imported viewBox scale."));
            }
            else
                content.Children.Add(StudioHint("Drag between two points to measure their distance and angle. Hold Shift to constrain the direction."));
            await ShowDialogAsync(new ContentDialog { Title = "Measurement", Content = content, CloseButtonText = "Close", XamlRoot = XamlRoot });
            return true;
        }
        if (mode == SvgEditorInteractionMode.None && Canvas.Tool == SvgEditorToolKind.Freehand)
        {
            var fidelity = NumberInput("Fidelity tolerance (screen pixels)", Canvas.PencilFidelity, 0, 20);
            var smooth = new CheckBox { Content = "Fit smooth curves", IsChecked = Canvas.PencilSmooth };
            var content = new StackPanel { Spacing = 10, Width = DialogContentWidth(380) };
            content.Children.Add(fidelity);
            content.Children.Add(smooth);
            content.Children.Add(StudioHint("Lower tolerance follows the pointer closely. Higher tolerance reduces anchors. Curve fitting rounds transitions between sampled points."));
            if (await ShowDialogAsync(MakeDialog("Pencil Options", content)) == ContentDialogResult.Primary)
            { Canvas.PencilFidelity = fidelity.Value; Canvas.PencilSmooth = smooth.IsChecked == true; }
            return true;
        }
        if (mode == SvgEditorInteractionMode.RoundedRectangle)
        {
            var radius = NumberInput("Corner radius (% of shortest side)", Canvas.RoundedRectangleRadiusRatio * 100, 0, 50);
            if (await ShowDialogAsync(MakeDialog("Rounded Rectangle Options", radius)) == ContentDialogResult.Primary)
                Canvas.RoundedRectangleRadiusRatio = (float)radius.Value / 100;
            return true;
        }
        if (mode == SvgEditorInteractionMode.Star)
        {
            var points = NumberInput("Points", Canvas.ShapeDivisions, 3, 64);
            var inner = NumberInput("Inner radius (%)", Canvas.StarInnerRadiusRatio * 100, 1, 99);
            var content = new StackPanel { Spacing = 10, Width = DialogContentWidth(360) };
            content.Children.Add(points);
            content.Children.Add(inner);
            if (await ShowDialogAsync(MakeDialog("Star Options", content)) == ContentDialogResult.Primary)
            { Canvas.ShapeDivisions = (int)points.Value; Canvas.StarInnerRadiusRatio = (float)inner.Value / 100; }
            return true;
        }
        return false;
    }
}
