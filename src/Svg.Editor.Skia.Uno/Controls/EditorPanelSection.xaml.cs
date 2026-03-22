namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class EditorPanelSection : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(EditorPanelSection),
            new PropertyMetadata(string.Empty, OnSectionPropertyChanged));

    public static readonly DependencyProperty MetaProperty =
        DependencyProperty.Register(
            nameof(Meta),
            typeof(string),
            typeof(EditorPanelSection),
            new PropertyMetadata(string.Empty, OnSectionPropertyChanged));

    public static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(
            nameof(HeaderContent),
            typeof(object),
            typeof(EditorPanelSection),
            new PropertyMetadata(null, OnSectionPropertyChanged));

    public static readonly DependencyProperty SectionContentProperty =
        DependencyProperty.Register(
            nameof(SectionContent),
            typeof(object),
            typeof(EditorPanelSection),
            new PropertyMetadata(null, OnSectionPropertyChanged));

    public static readonly DependencyProperty ShowDividerProperty =
        DependencyProperty.Register(
            nameof(ShowDivider),
            typeof(bool),
            typeof(EditorPanelSection),
            new PropertyMetadata(true, OnSectionPropertyChanged));

    public static readonly DependencyProperty SectionPaddingProperty =
        DependencyProperty.Register(
            nameof(SectionPadding),
            typeof(Thickness),
            typeof(EditorPanelSection),
            new PropertyMetadata(new Thickness(0, 16, 0, 0)));

    public EditorPanelSection()
    {
        InitializeComponent();
        UpdateView();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Meta
    {
        get => (string)GetValue(MetaProperty);
        set => SetValue(MetaProperty, value);
    }

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public object? SectionContent
    {
        get => GetValue(SectionContentProperty);
        set => SetValue(SectionContentProperty, value);
    }

    public bool ShowDivider
    {
        get => (bool)GetValue(ShowDividerProperty);
        set => SetValue(ShowDividerProperty, value);
    }

    public Thickness SectionPadding
    {
        get => (Thickness)GetValue(SectionPaddingProperty);
        set => SetValue(SectionPaddingProperty, value);
    }

    private static void OnSectionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EditorPanelSection)d).UpdateView();
    }

    private void UpdateView()
    {
        DividerBorder.BorderBrush = (Brush)Application.Current.Resources["ShellDividerBrush"];
        DividerBorder.BorderThickness = ShowDivider ? new Thickness(0, 1, 0, 0) : new Thickness(0);

        TitleTextBlock.Text = Title ?? string.Empty;
        TitleTextBlock.Visibility = string.IsNullOrWhiteSpace(Title) ? Visibility.Collapsed : Visibility.Visible;

        MetaTextBlock.Text = Meta ?? string.Empty;
        MetaTextBlock.Visibility = string.IsNullOrWhiteSpace(Meta) ? Visibility.Collapsed : Visibility.Visible;

        HeaderPresenter.Content = HeaderContent;
        HeaderPresenter.Visibility = HeaderContent is null ? Visibility.Collapsed : Visibility.Visible;

        SectionPresenter.Content = SectionContent;
        HeaderGrid.Visibility = TitleTextBlock.Visibility == Visibility.Collapsed
            && MetaTextBlock.Visibility == Visibility.Collapsed
            && HeaderPresenter.Visibility == Visibility.Collapsed
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
