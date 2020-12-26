using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SvgXml.Diagnostics.Views.Xml
{
    public partial class UnknownElementView : UserControl
    {
        public UnknownElementView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
