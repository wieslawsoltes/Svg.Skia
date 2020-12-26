using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SvgXml.Diagnostics.Views.Svg
{
    public partial class SvgGlyphView : UserControl
    {
        public SvgGlyphView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
