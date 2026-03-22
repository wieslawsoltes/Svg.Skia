using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia.Uno;
using Svg.Editor.Skia.Uno.Controls;

namespace UnoSvgEditorSample;

public sealed partial class MainPage : SvgEditorWorkspacePage
{
    public MainPage()
    {
        InitializeComponent();
        InitializeEditorView();
    }

    protected override Grid CanvasHostControl => CanvasHost;

    protected override Grid EditorSurfaceHostControl => EditorSurfaceHost;

    protected override Uno.Svg.Skia.Svg EditorSvgControl => EditorSvg;

    protected override SvgEditorOverlayCanvas EditorOverlayControl => EditorOverlay;

    protected override Canvas? InlineTextEditorLayerControl => InlineTextEditorLayer;

    protected override TextBox? InlineTextEditorControl => InlineTextEditor;
}
