using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using SkiaSharp;

namespace Svg.Editor.Skia;

/// <summary>
/// Detached solid-color choice for a nonmodal editor. Spectrum changes never edit the
/// document. Apply validates both target identity and document revision, then uses the
/// existing fill/stroke command to produce a single history entry. Color alpha remains
/// independent from SVG fill-opacity/stroke-opacity and object opacity.
/// </summary>
public sealed class SvgSolidPaintDraft
{
    private readonly SvgEditorSelectionContext _context;
    private readonly long _revision;
    private bool _applied;
    public bool IsStroke { get; }
    public bool IsMixed { get; }
    public bool HasNonSolidPaint { get; }
    public SKColor? InitialColor { get; }

    public SvgSolidPaintDraft(SvgEditorController controller, bool stroke = false)
    {
        if (controller is null) throw new ArgumentNullException(nameof(controller));
        if (controller.IsEditing) throw new InvalidOperationException("Complete the current gesture before choosing a color.");
        if (controller.SelectedElements.Count == 0) throw new InvalidOperationException("Select artwork to edit its color.");
        controller.EnsureSelectionEditable();
        _context = new SvgEditorSelectionContext(controller);
        _revision = controller.DocumentRevision;
        IsStroke = stroke;
        var paints = controller.SelectedElements.Select(element => stroke ? element.Stroke : element.Fill).ToArray();
        var converter = TypeDescriptor.GetConverter(typeof(SvgPaintServer));
        string Key(SvgPaintServer? paint) => paint is null ? "none" : converter.ConvertToInvariantString(paint) ?? "none";
        var first = Key(paints[0]);
        IsMixed = paints.Skip(1).Any(paint => Key(paint) != first);
        HasNonSolidPaint = paints.Any(paint => paint is not SvgColourServer);
        if (paints[0] is SvgColourServer solid)
            InitialColor = new SKColor(solid.Colour.R, solid.Colour.G, solid.Colour.B, solid.Colour.A);
    }

    public bool IsCurrent(SvgEditorController? controller)
        => !_applied && controller is not null && !controller.IsEditing && _context.IsCurrent(controller)
            && controller.DocumentRevision == _revision;

    public void Apply(SvgEditorController controller, SKColor color) => ApplyPaint(controller, FormatColor(color));
    public void ApplyNone(SvgEditorController controller) => ApplyPaint(controller, "none");

    public static string FormatColor(SKColor color) => color.Alpha == 255
        ? $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
        : string.Format(CultureInfo.InvariantCulture, "rgba({0},{1},{2},{3})", color.Red, color.Green, color.Blue,
            (color.Alpha / 255d).ToString("0.#########", CultureInfo.InvariantCulture));

    private void ApplyPaint(SvgEditorController controller, string paint)
    {
        if (!IsCurrent(controller))
            throw new InvalidOperationException("The color target or document changed. Reset the color draft before applying it.");
        controller.EnsureSelectionEditable();
        if (IsStroke) controller.SetStroke(paint); else controller.SetFill(paint);
        _applied = true;
    }
}
