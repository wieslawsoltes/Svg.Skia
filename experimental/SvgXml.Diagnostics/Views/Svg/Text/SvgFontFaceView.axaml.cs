using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SvgXml.Diagnostics.Views.Svg
{
    public partial class SvgFontFaceView : UserControl
    {
        public SvgFontFaceView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
