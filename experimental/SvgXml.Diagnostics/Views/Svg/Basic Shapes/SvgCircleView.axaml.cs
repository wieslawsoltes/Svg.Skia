using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SvgXml.Diagnostics.Views.Svg
{
    public partial class SvgCircleView : UserControl
    {
        public SvgCircleView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
