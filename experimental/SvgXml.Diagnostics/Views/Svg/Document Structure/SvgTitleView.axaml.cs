using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SvgXml.Diagnostics.Views.Svg
{
    public partial class SvgTitleView : UserControl
    {
        public SvgTitleView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
