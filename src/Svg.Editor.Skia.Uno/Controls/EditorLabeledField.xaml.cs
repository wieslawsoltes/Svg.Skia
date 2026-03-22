namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class EditorLabeledField : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(EditorLabeledField),
            new PropertyMetadata(string.Empty, OnLabelPropertyChanged));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(EditorLabeledField),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(EditorLabeledField),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty InlineLabelProperty =
        DependencyProperty.Register(
            nameof(InlineLabel),
            typeof(string),
            typeof(EditorLabeledField),
            new PropertyMetadata(string.Empty, OnLabelPropertyChanged));

    public static readonly DependencyProperty SuffixTextProperty =
        DependencyProperty.Register(
            nameof(SuffixText),
            typeof(string),
            typeof(EditorLabeledField),
            new PropertyMetadata(string.Empty, OnLabelPropertyChanged));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(EditorLabeledField),
            new PropertyMetadata(false));

    public static readonly DependencyProperty FieldTagProperty =
        DependencyProperty.Register(
            nameof(FieldTag),
            typeof(object),
            typeof(EditorLabeledField),
            new PropertyMetadata(null, OnFieldTagPropertyChanged));

    public EditorLabeledField()
    {
        InitializeComponent();
        UpdateLabel();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public string InlineLabel
    {
        get => (string)GetValue(InlineLabelProperty);
        set => SetValue(InlineLabelProperty, value);
    }

    public string SuffixText
    {
        get => (string)GetValue(SuffixTextProperty);
        set => SetValue(SuffixTextProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public object? FieldTag
    {
        get => GetValue(FieldTagProperty);
        set => SetValue(FieldTagProperty, value);
    }

    public event RoutedEventHandler? Committed;

    public event KeyEventHandler? EditorKeyDown;

    private static void OnLabelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EditorLabeledField)d).UpdateLabel();
    }

    private static void OnFieldTagPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EditorLabeledField)d).InputTextBox.Tag = e.NewValue;
    }

    private void UpdateLabel()
    {
        LabelTextBlock.Text = Label ?? string.Empty;
        LabelTextBlock.Visibility = string.IsNullOrWhiteSpace(Label) ? Visibility.Collapsed : Visibility.Visible;

        InlineLabelTextBlock.Text = InlineLabel ?? string.Empty;
        InlineLabelTextBlock.Visibility = string.IsNullOrWhiteSpace(InlineLabel) ? Visibility.Collapsed : Visibility.Visible;

        SuffixTextBlock.Text = SuffixText ?? string.Empty;
        SuffixTextBlock.Visibility = string.IsNullOrWhiteSpace(SuffixText) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnInputTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        Committed?.Invoke(InputTextBox, e);
    }

    private void OnInputTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        EditorKeyDown?.Invoke(InputTextBox, e);
    }
}
