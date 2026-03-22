namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class EditorSearchField : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(EditorSearchField),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(EditorSearchField),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TrailingContentProperty =
        DependencyProperty.Register(
            nameof(TrailingContent),
            typeof(object),
            typeof(EditorSearchField),
            new PropertyMetadata(null, OnTrailingContentPropertyChanged));

    public EditorSearchField()
    {
        InitializeComponent();
        UpdateTrailingContentVisibility();
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

    public object? TrailingContent
    {
        get => GetValue(TrailingContentProperty);
        set => SetValue(TrailingContentProperty, value);
    }

    public event TextChangedEventHandler? TextChanged;

    public event KeyEventHandler? SearchKeyDown;

    public void FocusTextBox()
    {
        SearchTextBox.Focus(FocusState.Programmatic);
        SearchTextBox.SelectAll();
    }

    private static void OnTrailingContentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EditorSearchField)d).UpdateTrailingContentVisibility();
    }

    private void OnSearchTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        TextChanged?.Invoke(SearchTextBox, e);
    }

    private void OnSearchTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        SearchKeyDown?.Invoke(SearchTextBox, e);
    }

    private void UpdateTrailingContentVisibility()
    {
        if (TrailingPresenter is null)
        {
            return;
        }

        TrailingPresenter.Visibility = TrailingContent is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
