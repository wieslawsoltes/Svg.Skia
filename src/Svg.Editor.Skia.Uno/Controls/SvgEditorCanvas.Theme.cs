using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    public static readonly DependencyProperty PasteboardBrushProperty = RegisterCanvasBrush(nameof(PasteboardBrush));
    public static readonly DependencyProperty ArtboardLabelBrushProperty = RegisterCanvasBrush(nameof(ArtboardLabelBrush));
    public static readonly DependencyProperty ArtboardOutlineBrushProperty = RegisterCanvasBrush(nameof(ArtboardOutlineBrush));
    public static readonly DependencyProperty ArtboardShadowBrushProperty = RegisterCanvasBrush(nameof(ArtboardShadowBrush));
    public static readonly DependencyProperty CanvasAccentBrushProperty = RegisterCanvasBrush(nameof(CanvasAccentBrush));
    public static readonly DependencyProperty HandleFillBrushProperty = RegisterCanvasBrush(nameof(HandleFillBrush));
    public static readonly DependencyProperty GridLineBrushProperty = RegisterCanvasBrush(nameof(GridLineBrush));
    public static readonly DependencyProperty GuideLineBrushProperty = RegisterCanvasBrush(nameof(GuideLineBrush));
    public static readonly DependencyProperty GuidePreviewBrushProperty = RegisterCanvasBrush(nameof(GuidePreviewBrush));
    public static readonly DependencyProperty SmartGuideBrushProperty = RegisterCanvasBrush(nameof(SmartGuideBrush));
    public static readonly DependencyProperty CheckerBackgroundBrushProperty = RegisterCanvasBrush(nameof(CheckerBackgroundBrush));
    public static readonly DependencyProperty CheckerForegroundBrushProperty = RegisterCanvasBrush(nameof(CheckerForegroundBrush));
    private StudioBrushSubscriptions? _canvasBrushSubscriptions;

    public Brush? PasteboardBrush { get => (Brush?)GetValue(PasteboardBrushProperty); set => SetValue(PasteboardBrushProperty, value); }
    public Brush? ArtboardLabelBrush { get => (Brush?)GetValue(ArtboardLabelBrushProperty); set => SetValue(ArtboardLabelBrushProperty, value); }
    public Brush? ArtboardOutlineBrush { get => (Brush?)GetValue(ArtboardOutlineBrushProperty); set => SetValue(ArtboardOutlineBrushProperty, value); }
    public Brush? ArtboardShadowBrush { get => (Brush?)GetValue(ArtboardShadowBrushProperty); set => SetValue(ArtboardShadowBrushProperty, value); }
    public Brush? CanvasAccentBrush { get => (Brush?)GetValue(CanvasAccentBrushProperty); set => SetValue(CanvasAccentBrushProperty, value); }
    public Brush? HandleFillBrush { get => (Brush?)GetValue(HandleFillBrushProperty); set => SetValue(HandleFillBrushProperty, value); }
    public Brush? GridLineBrush { get => (Brush?)GetValue(GridLineBrushProperty); set => SetValue(GridLineBrushProperty, value); }
    public Brush? GuideLineBrush { get => (Brush?)GetValue(GuideLineBrushProperty); set => SetValue(GuideLineBrushProperty, value); }
    public Brush? GuidePreviewBrush { get => (Brush?)GetValue(GuidePreviewBrushProperty); set => SetValue(GuidePreviewBrushProperty, value); }
    public Brush? SmartGuideBrush { get => (Brush?)GetValue(SmartGuideBrushProperty); set => SetValue(SmartGuideBrushProperty, value); }
    public Brush? CheckerBackgroundBrush { get => (Brush?)GetValue(CheckerBackgroundBrushProperty); set => SetValue(CheckerBackgroundBrushProperty, value); }
    public Brush? CheckerForegroundBrush { get => (Brush?)GetValue(CheckerForegroundBrushProperty); set => SetValue(CheckerForegroundBrushProperty, value); }

    private static DependencyProperty RegisterCanvasBrush(string name)
        => DependencyProperty.Register(name, typeof(Brush), typeof(SvgEditorCanvas), new PropertyMetadata(null, OnCanvasBrushChanged));

    private static void OnCanvasBrushChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var canvas = (SvgEditorCanvas)sender;
        if (canvas._disposed)
            return;
        (canvas._canvasBrushSubscriptions ??= new StudioBrushSubscriptions(canvas.Invalidate)).Observe(args.Property, args.NewValue as Brush);
        canvas.Invalidate();
    }

    private void OnCanvasThemeChanged(FrameworkElement sender, object args)
    {
        if (!_disposed)
            Invalidate();
    }

    private void ApplyCanvasPalette(CanvasResources resources)
    {
        // Fallbacks retain the legacy standalone IsDarkTheme API. EditorTheme binds
        // every brush below, including system high-contrast colors and host overrides.
        resources.Background = StudioBrushSubscriptions.Color(PasteboardBrush, IsDarkTheme ? new SKColor(30, 30, 30) : new SKColor(186, 186, 186));
        resources.Label = StudioBrushSubscriptions.Color(ArtboardLabelBrush, IsDarkTheme ? new SKColor(184, 184, 184) : new SKColor(70, 70, 70));
        resources.Border = StudioBrushSubscriptions.Color(ArtboardOutlineBrush, new SKColor(128, 128, 128, 90));
        resources.Shadow = StudioBrushSubscriptions.Color(ArtboardShadowBrush, new SKColor(0, 0, 0, (byte)(IsDarkTheme ? 10 : 6)));
        resources.Accent = StudioBrushSubscriptions.Color(CanvasAccentBrush, new SKColor(63, 111, 242));
        resources.Handle = StudioBrushSubscriptions.Color(HandleFillBrush, SKColors.White);
        resources.Grid = StudioBrushSubscriptions.Color(GridLineBrush, new SKColor(150, 150, 150, 54));
        resources.Guide = StudioBrushSubscriptions.Color(GuideLineBrush, new SKColor(62, 207, 218));
        resources.GuidePreview = StudioBrushSubscriptions.Color(GuidePreviewBrush, new SKColor(242, 129, 220));
        resources.SmartGuide = StudioBrushSubscriptions.Color(SmartGuideBrush, new SKColor(215, 85, 205));
        resources.SetCheckerColors(StudioBrushSubscriptions.Color(CheckerBackgroundBrush, SKColors.White),
            StudioBrushSubscriptions.Color(CheckerForegroundBrush, new SKColor(239, 241, 245)));
    }
}
