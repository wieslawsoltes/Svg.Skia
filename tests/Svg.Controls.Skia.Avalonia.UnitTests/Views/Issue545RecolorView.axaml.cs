using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia.Svg.Skia.UnitTests.Views;

public partial class Issue545RecolorView : UserControl
{
    public Issue545RecolorView()
    {
        InitializeComponent();
    }

    public Svg InlineCurrentColorControl => this.FindControl<Svg>("InlineCurrentColor")!;

    public Svg InlineCssControl => this.FindControl<Svg>("InlineCss")!;

    public Svg StyledCurrentColorControl => this.FindControl<Svg>("StyledCurrentColor")!;

    public Svg StyledCssControl => this.FindControl<Svg>("StyledCss")!;
}
