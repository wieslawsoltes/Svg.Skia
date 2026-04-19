namespace SvgML;

[ContentProperty("Content")]
internal partial class content
{
    public static readonly Microsoft.Maui.Controls.BindableProperty ContentProperty = 
        Microsoft.Maui.Controls.BindableProperty.Create("Content", typeof(string), typeof(content));

    public string? Content
    {
        get => (string?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }
}
