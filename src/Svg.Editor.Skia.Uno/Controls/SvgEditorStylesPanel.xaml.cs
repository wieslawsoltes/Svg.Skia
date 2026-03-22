using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorStylesPanel : UserControl
{
    public static readonly DependencyProperty TextStylesProperty =
        DependencyProperty.Register(
            nameof(TextStyles),
            typeof(IEnumerable<EditorTextStyleItem>),
            typeof(SvgEditorStylesPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ColorStylesProperty =
        DependencyProperty.Register(
            nameof(ColorStyles),
            typeof(IEnumerable<ColorSwatchItem>),
            typeof(SvgEditorStylesPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty EffectStylesProperty =
        DependencyProperty.Register(
            nameof(EffectStyles),
            typeof(IEnumerable<EditorEffectStyleItem>),
            typeof(SvgEditorStylesPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty LayoutGuideStylesProperty =
        DependencyProperty.Register(
            nameof(LayoutGuideStyles),
            typeof(IEnumerable<EditorLayoutGuideStyleItem>),
            typeof(SvgEditorStylesPanel),
            new PropertyMetadata(null));

    public SvgEditorStylesPanel()
    {
        InitializeComponent();
    }

    public event EventHandler<EditorStyleRequestedEventArgs>? StyleRequested;

    public IEnumerable<EditorTextStyleItem>? TextStyles
    {
        get => (IEnumerable<EditorTextStyleItem>?)GetValue(TextStylesProperty);
        set => SetValue(TextStylesProperty, value);
    }

    public IEnumerable<ColorSwatchItem>? ColorStyles
    {
        get => (IEnumerable<ColorSwatchItem>?)GetValue(ColorStylesProperty);
        set => SetValue(ColorStylesProperty, value);
    }

    public IEnumerable<EditorEffectStyleItem>? EffectStyles
    {
        get => (IEnumerable<EditorEffectStyleItem>?)GetValue(EffectStylesProperty);
        set => SetValue(EffectStylesProperty, value);
    }

    public IEnumerable<EditorLayoutGuideStyleItem>? LayoutGuideStyles
    {
        get => (IEnumerable<EditorLayoutGuideStyleItem>?)GetValue(LayoutGuideStylesProperty);
        set => SetValue(LayoutGuideStylesProperty, value);
    }

    private void OnCreateStyleMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string rawKind }
            || !Enum.TryParse<EditorStyleKind>(rawKind, true, out var kind))
        {
            return;
        }

        StyleRequested?.Invoke(this, new EditorStyleRequestedEventArgs(kind, EditorStyleAction.Create));
    }

    private void OnTextStyleApplyClick(object sender, RoutedEventArgs e)
    {
        RaiseStyleRequested<EditorTextStyleItem>(sender, EditorStyleKind.Text, EditorStyleAction.Apply);
    }

    private void OnTextStyleEditClick(object sender, RoutedEventArgs e)
    {
        RaiseStyleRequested<EditorTextStyleItem>(sender, EditorStyleKind.Text, EditorStyleAction.Edit);
    }

    private void OnColorStyleApplyClick(object sender, RoutedEventArgs e)
    {
        RaiseStyleRequested<ColorSwatchItem>(sender, EditorStyleKind.Color, EditorStyleAction.Apply);
    }

    private void OnColorStyleEditClick(object sender, RoutedEventArgs e)
    {
        RaiseStyleRequested<ColorSwatchItem>(sender, EditorStyleKind.Color, EditorStyleAction.Edit);
    }

    private void OnEffectStyleApplyClick(object sender, RoutedEventArgs e)
    {
        RaiseStyleRequested<EditorEffectStyleItem>(sender, EditorStyleKind.Effect, EditorStyleAction.Apply);
    }

    private void OnEffectStyleEditClick(object sender, RoutedEventArgs e)
    {
        RaiseStyleRequested<EditorEffectStyleItem>(sender, EditorStyleKind.Effect, EditorStyleAction.Edit);
    }

    private void OnLayoutGuideStyleApplyClick(object sender, RoutedEventArgs e)
    {
        RaiseStyleRequested<EditorLayoutGuideStyleItem>(sender, EditorStyleKind.LayoutGuide, EditorStyleAction.Apply);
    }

    private void OnLayoutGuideStyleEditClick(object sender, RoutedEventArgs e)
    {
        RaiseStyleRequested<EditorLayoutGuideStyleItem>(sender, EditorStyleKind.LayoutGuide, EditorStyleAction.Edit);
    }

    private void RaiseStyleRequested<T>(object sender, EditorStyleKind kind, EditorStyleAction action)
    {
        if (sender is not FrameworkElement { DataContext: T item })
        {
            return;
        }

        StyleRequested?.Invoke(this, new EditorStyleRequestedEventArgs(kind, action, item));
    }
}
