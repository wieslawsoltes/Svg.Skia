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

    public TextEditorWindow(string text, string fontFamily, SvgFontWeight weight, float letter, float word)
    {
        InitializeComponent();
        _editor = this.FindControl<TextBox>("Editor");
        _fontFamilyBox = this.FindControl<ComboBox>("FontFamilyBox");
        _fontWeightBox = this.FindControl<ComboBox>("FontWeightBox");
        _letterBox = this.FindControl<TextBox>("LetterBox");
        _wordBox = this.FindControl<TextBox>("WordBox");

        _editor.Text = text;
        _fontFamilyBox.Items = FontManager.Current?.InstalledFontFamilyNames;
        _fontFamilyBox.SelectedItem = fontFamily;
        _fontWeightBox.Items = Enum.GetValues(typeof(FontWeight)).Cast<FontWeight>();
        _fontWeightBox.SelectedItem = ToFontWeight(weight);
        _letterBox.Text = letter.ToString();
        _wordBox.Text = word.ToString();
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

    private static SvgFontWeight FromFontWeight(FontWeight w) => w.Weight switch
    {
        100 => SvgFontWeight.W100,
        200 => SvgFontWeight.W200,
        300 => SvgFontWeight.W300,
        400 => SvgFontWeight.W400,
        500 => SvgFontWeight.W500,
        600 => SvgFontWeight.W600,
        700 => SvgFontWeight.W700,
        800 => SvgFontWeight.W800,
        900 => SvgFontWeight.W900,
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

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TextResult = _editor.Text;
        FontFamilyResult = _fontFamilyBox.SelectedItem as string ?? string.Empty;
        FontWeightResult = FromFontWeight((FontWeight)_fontWeightBox.SelectedItem!);
        float.TryParse(_letterBox.Text, out var ls);
        float.TryParse(_wordBox.Text, out var ws);
        LetterSpacingResult = ls;
        WordSpacingResult = ws;
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
