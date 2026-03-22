using System.Globalization;
using Svg;
using Windows.UI;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgSymbolPreview : UserControl
{
    private const string PreviewSymbolId = "preview-symbol";

    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(
            nameof(Symbol),
            typeof(SvgSymbol),
            typeof(SvgSymbolPreview),
            new PropertyMetadata(null, OnPreviewPropertyChanged));

    public static readonly DependencyProperty PreviewBackgroundProperty =
        DependencyProperty.Register(
            nameof(PreviewBackground),
            typeof(Brush),
            typeof(SvgSymbolPreview),
            new PropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 248, 248, 247))));

    public static readonly DependencyProperty PreviewCornerRadiusProperty =
        DependencyProperty.Register(
            nameof(PreviewCornerRadius),
            typeof(CornerRadius),
            typeof(SvgSymbolPreview),
            new PropertyMetadata(new CornerRadius(16)));

    public SvgSymbolPreview()
    {
        InitializeComponent();
    }

    public SvgSymbol? Symbol
    {
        get => (SvgSymbol?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public Brush PreviewBackground
    {
        get => (Brush)GetValue(PreviewBackgroundProperty);
        set => SetValue(PreviewBackgroundProperty, value);
    }

    public CornerRadius PreviewCornerRadius
    {
        get => (CornerRadius)GetValue(PreviewCornerRadiusProperty);
        set => SetValue(PreviewCornerRadiusProperty, value);
    }

    private static void OnPreviewPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SvgSymbolPreview)d).UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (Symbol is null)
        {
            PreviewSvg.Source = null;
            return;
        }

        try
        {
            var symbol = (SvgSymbol)Symbol.DeepCopy();
            symbol.ID = PreviewSymbolId;

            var width = GetLength(symbol, "width", symbol.ViewBox.Width, 240f);
            var height = GetLength(symbol, "height", symbol.ViewBox.Height, 160f);
            var minX = symbol.ViewBox.MinX;
            var minY = symbol.ViewBox.MinY;

            var document = new SvgDocument
            {
                Width = new SvgUnit(width),
                Height = new SvgUnit(height),
                ViewBox = new SvgViewBox(minX, minY, width, height)
            };

            var defs = new SvgDefinitionList();
            defs.Children.Add(symbol);
            document.Children.Add(defs);
            document.Children.Add(new SvgUse
            {
                ReferencedElement = new Uri($"#{PreviewSymbolId}", UriKind.Relative),
                X = new SvgUnit(SvgUnitType.User, minX),
                Y = new SvgUnit(SvgUnitType.User, minY),
                Width = new SvgUnit(SvgUnitType.User, width),
                Height = new SvgUnit(SvgUnitType.User, height)
            });

            PreviewSvg.Source = document.GetXML();
        }
        catch
        {
            PreviewSvg.Source = null;
        }
    }

    private static float GetLength(SvgSymbol symbol, string key, float primary, float fallback)
    {
        if (primary > 0)
        {
            return primary;
        }

        if (symbol.CustomAttributes.TryGetValue(key, out var value)
            && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return fallback;
    }
}
