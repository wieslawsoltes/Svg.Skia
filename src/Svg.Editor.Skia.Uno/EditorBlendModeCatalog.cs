using System.Collections.Generic;
using Svg.Editor.Skia.Uno.Models;
using Svg.Model.Services;

namespace Svg.Editor.Skia.Uno;

internal static class EditorBlendModeCatalog
{
    public const string MixedId = "mixed";

    public static IReadOnlyList<EditorBlendModeItem> CreateDefault()
    {
        return
        [
            new EditorBlendModeItem(MixedId, "Mixed"),
            new EditorBlendModeItem(BlendModeService.PassThroughToken, "Pass through"),
            new EditorBlendModeItem(BlendModeService.NormalToken, "Normal"),
            new EditorBlendModeItem(BlendModeService.DarkenToken, "Darken"),
            new EditorBlendModeItem(BlendModeService.MultiplyToken, "Multiply"),
            new EditorBlendModeItem(BlendModeService.PlusDarkerToken, "Plus darker"),
            new EditorBlendModeItem(BlendModeService.ColorBurnToken, "Color burn"),
            new EditorBlendModeItem(BlendModeService.LightenToken, "Lighten"),
            new EditorBlendModeItem(BlendModeService.ScreenToken, "Screen"),
            new EditorBlendModeItem(BlendModeService.PlusLighterToken, "Plus lighter"),
            new EditorBlendModeItem(BlendModeService.ColorDodgeToken, "Color dodge"),
            new EditorBlendModeItem(BlendModeService.OverlayToken, "Overlay"),
            new EditorBlendModeItem(BlendModeService.SoftLightToken, "Soft light"),
            new EditorBlendModeItem(BlendModeService.HardLightToken, "Hard light"),
            new EditorBlendModeItem(BlendModeService.DifferenceToken, "Difference"),
            new EditorBlendModeItem(BlendModeService.ExclusionToken, "Exclusion"),
            new EditorBlendModeItem(BlendModeService.HueToken, "Hue"),
            new EditorBlendModeItem(BlendModeService.SaturationToken, "Saturation"),
            new EditorBlendModeItem(BlendModeService.ColorToken, "Color"),
            new EditorBlendModeItem(BlendModeService.LuminosityToken, "Luminosity")
        ];
    }
}
