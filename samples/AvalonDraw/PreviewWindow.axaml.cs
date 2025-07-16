using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Svg;

namespace AvalonDraw;

public partial class PreviewWindow : Window
{
    public PreviewWindow(SvgDocument doc)
    {
        InitializeComponent();
        if (PreviewSvg?.SkSvg != null)
        {
            PreviewSvg.SkSvg.FromSvgDocument(doc);
            PreviewSvg.InvalidateVisual();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
