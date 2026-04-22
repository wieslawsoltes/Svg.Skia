namespace SvgML;

[ContentProperty("Content")]
public partial class content
{
    public new static readonly Microsoft.Maui.Controls.BindableProperty ContentProperty =
        Microsoft.Maui.Controls.BindableProperty.Create("Content", typeof(string), typeof(content));

    public new string? Content
    {
        get => (string?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }
}
