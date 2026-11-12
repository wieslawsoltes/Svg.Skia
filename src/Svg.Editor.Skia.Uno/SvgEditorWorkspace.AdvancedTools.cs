using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia;
using Svg.Editor.Svg;
using Svg.Editor.Svg.Models;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private async Task<bool> ShowAdvancedEditorToolOptionsAsync()
    {
        if (SvgEditorCanvas.TryGetDeformationKind(Canvas.InteractionMode, out _))
        {
            var current = Canvas.DeformationOptions;
            var diameter = NumberInput("Brush diameter", current.Radius * 2, 0.1, 10000);
            var strength = NumberInput("Strength (%)", Math.Abs(current.Strength) * 100, 1, 100);
            var detail = NumberInput("Detail", current.Detail, 2, 64);
            var precision = NumberInput("Curve precision", current.Flatness, 0.05, 8);
            var rate = NumberInput("Application rate (per second)", current.DwellRate, 0, 20);
            var panel = new StackPanel { Spacing = 10, Width = DialogContentWidth(380) };
            foreach (var field in new UIElement[] { diameter, strength, detail, precision })
                panel.Children.Add(field);
            if (Canvas.InteractionMode != SvgEditorInteractionMode.Warp)
                panel.Children.Add(rate);
            panel.Children.Add(StudioHint("Brush selected artwork. Alt reverses the effect. "
                + (Canvas.InteractionMode == SvgEditorInteractionMode.Warp
                    ? "Drag to move the artwork under the brush. "
                    : "Hold to continue applying the effect; a zero application rate applies only at initial contact. ")
                + "Lower precision values retain finer curve detail."));
            if (await ShowDialogAsync(MakeDialog(ToolContextLabel() + " Options", panel)) == ContentDialogResult.Primary)
                Canvas.DeformationOptions = new SvgDeformationOptions
                {
                    Radius = (float)diameter.Value / 2,
                    Strength = (float)strength.Value / 100,
                    Detail = (int)detail.Value,
                    Flatness = (float)precision.Value,
                    MaximumPoints = current.MaximumPoints,
                    DwellRate = (float)rate.Value
                };
            return true;
        }
        if (Canvas.InteractionMode != SvgEditorInteractionMode.SymbolSprayer && !SvgEditorCanvas.TryGetSymbolBrushKind(Canvas.InteractionMode, out _))
            return false;
        var options = Canvas.SymbolBrushOptions;
        var radius = NumberInput("Brush diameter", options.Radius * 2, 0.1, 10000);
        var size = NumberInput("Symbol size", options.Size, 0.1, 10000);
        var density = NumberInput("Symbols per spray", options.Density, 1, 100);
        var variation = NumberInput("Size variation (%)", options.SizeVariation * 100, 0, 95);
        var rotation = NumberInput("Rotation variation (°)", options.RotationVariation, 0, 180);
        var influence = NumberInput("Influence (%)", options.Strength * 100, 1, 100);
        var styles = new AppearanceService();
        styles.Load(Controller?.Document);
        var style = new ComboBox
        {
            Header = "Graphic style",
            ItemsSource = styles.Styles,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = styles.Styles.FirstOrDefault(item => item.Name == options.GraphicStyleName)
        };
        var symbols = Controller?.Document?.Descendants().OfType<SvgSymbol>().ToArray() ?? Array.Empty<SvgSymbol>();
        var symbol = new ComboBox
        {
            Header = "Symbol",
            ItemsSource = symbols.Select(item => item.ID).ToArray(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = Controller?.Tools.SymbolId
        };
        var content = new StackPanel { Spacing = 10, Width = DialogContentWidth(390) };
        content.Children.Add(radius);
        content.Children.Add(influence);
        if (Canvas.InteractionMode == SvgEditorInteractionMode.SymbolSprayer)
            foreach (var field in new UIElement[] { symbol, size, density, variation, rotation })
                content.Children.Add(field);
        if (Canvas.InteractionMode == SvgEditorInteractionMode.SymbolStyler)
            content.Children.Add(style);
        content.Children.Add(StudioHint(Canvas.InteractionMode == SvgEditorInteractionMode.SymbolStyler
            ? "Choose a document graphic style. Alt brushing restores the original symbol. Create styles in the Graphic Styles panel."
            : "Symbols remain reusable native instances. Select instances to restrict the brush; with no selection it affects unlocked instances."));
        if (await ShowDialogAsync(MakeDialog(ToolContextLabel() + " Options", content)) == ContentDialogResult.Primary)
        {
            Canvas.SymbolBrushOptions = new SvgSymbolBrushOptions
            {
                Radius = (float)radius.Value / 2,
                Size = (float)size.Value,
                Density = (int)density.Value,
                SizeVariation = (float)variation.Value / 100,
                RotationVariation = (float)rotation.Value,
                Strength = (float)influence.Value / 100,
                Seed = options.Seed,
                GraphicStyleName = (style.SelectedItem as StyleEntry)?.Name ?? options.GraphicStyleName
            };
            if (symbol.SelectedItem is string id && Controller is { } controller)
                controller.Tools.SymbolId = id;
        }
        return true;
    }
}
