using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Svg;

namespace AvalonDraw;

public partial class TextEditorWindow : Window
{
    private readonly TextBox _editor;
    private readonly ComboBox _fontFamilyBox;
    private readonly ComboBox _fontWeightBox;
    private readonly TextBox _letterBox;
    private readonly TextBox _wordBox;
    private readonly TextBox _orientationBox;
    private readonly CheckBox _warpBox;

    public TextEditorWindow(string text, string fontFamily, SvgFontWeight weight, float letter, float word, float orientation, bool warp)
    {
        InitializeComponent();
        _editor = this.FindControl<TextBox>("Editor");
        _fontFamilyBox = this.FindControl<ComboBox>("FontFamilyBox");
        _fontWeightBox = this.FindControl<ComboBox>("FontWeightBox");
        _letterBox = this.FindControl<TextBox>("LetterBox");
        _wordBox = this.FindControl<TextBox>("WordBox");
        _orientationBox = this.FindControl<TextBox>("OrientationBox");
        _warpBox = this.FindControl<CheckBox>("WarpBox");

        _editor.Text = text;
        _fontFamilyBox.ItemsSource = FontManager.Current?.SystemFonts;
        _fontFamilyBox.SelectedItem = fontFamily;
        _fontWeightBox.ItemsSource = Enum.GetValues(typeof(FontWeight)).Cast<FontWeight>();
        _fontWeightBox.SelectedItem = ToFontWeight(weight);
        _letterBox.Text = letter.ToString();
        _wordBox.Text = word.ToString();
        _orientationBox.Text = orientation.ToString();
        _warpBox.IsChecked = warp;
    }

    private static FontWeight ToFontWeight(SvgFontWeight w) => w switch
    {
        SvgFontWeight.W100 => FontWeight.Thin,
        SvgFontWeight.W200 => FontWeight.ExtraLight,
        SvgFontWeight.W300 => FontWeight.Light,
        SvgFontWeight.W400 => FontWeight.Normal,
        SvgFontWeight.W500 => FontWeight.Medium,
        SvgFontWeight.W600 => FontWeight.SemiBold,
        SvgFontWeight.W700 => FontWeight.Bold,
        SvgFontWeight.W800 => FontWeight.ExtraBold,
        SvgFontWeight.W900 => FontWeight.Black,
        SvgFontWeight.Bold => FontWeight.Bold,
        _ => FontWeight.Normal
    };

    private static SvgFontWeight FromFontWeight(FontWeight w) => w switch
    {
        FontWeight.Thin => SvgFontWeight.W100,
        FontWeight.ExtraLight => SvgFontWeight.W200,
        FontWeight.Light => SvgFontWeight.W300,
        FontWeight.Normal => SvgFontWeight.W400,
        FontWeight.Medium => SvgFontWeight.W500,
        FontWeight.SemiBold => SvgFontWeight.W600,
        FontWeight.Bold => SvgFontWeight.W700,
        FontWeight.ExtraBold => SvgFontWeight.W800,
        FontWeight.Black => SvgFontWeight.W900,
        _ => SvgFontWeight.Normal
    };

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string TextResult { get; private set; } = string.Empty;
    public string FontFamilyResult { get; private set; } = string.Empty;
    public SvgFontWeight FontWeightResult { get; private set; }
    public float LetterSpacingResult { get; private set; }
    public float WordSpacingResult { get; private set; }
    public float OrientationResult { get; private set; }
    public bool WarpResult { get; private set; }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TextResult = _editor.Text;
        FontFamilyResult = _fontFamilyBox.SelectedItem as string ?? string.Empty;
        FontWeightResult = FromFontWeight((FontWeight)_fontWeightBox.SelectedItem!);
        float.TryParse(_letterBox.Text, out var ls);
        float.TryParse(_wordBox.Text, out var ws);
        float.TryParse(_orientationBox.Text, out var or);
        LetterSpacingResult = ls;
        WordSpacingResult = ws;
        OrientationResult = or;
        WarpResult = _warpBox.IsChecked ?? false;
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
