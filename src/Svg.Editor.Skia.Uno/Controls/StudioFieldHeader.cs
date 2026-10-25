using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Native inputs treat a non-null HeaderTemplate as a visible header even without
/// Header content. Apply our wrapping header only to nonempty text, so toolbar
/// inputs retain their single-row geometry and custom header content stays native.
/// Observers are detached while unloaded.
/// </summary>
public sealed partial class StudioFieldHeader : DependencyObject
{
    public static readonly DependencyProperty TemplateProperty = DependencyProperty.RegisterAttached(
        "Template", typeof(DataTemplate), typeof(StudioFieldHeader), new PropertyMetadata(null, TemplateChanged));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(HeaderState), typeof(StudioFieldHeader), new PropertyMetadata(null));

    public static DataTemplate? GetTemplate(DependencyObject target) => (DataTemplate?)target.GetValue(TemplateProperty);
    public static void SetTemplate(DependencyObject target, DataTemplate? template) => target.SetValue(TemplateProperty, template);

    private static void TemplateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        (sender.GetValue(StateProperty) as HeaderState)?.Dispose();
        sender.ClearValue(StateProperty);
        if (sender is not Control control || args.NewValue is not DataTemplate template) return;
        var properties = control switch
        {
            TextBox => (TextBox.HeaderProperty, TextBox.HeaderTemplateProperty),
            NumberBox => (NumberBox.HeaderProperty, NumberBox.HeaderTemplateProperty),
            ComboBox => (ComboBox.HeaderProperty, ComboBox.HeaderTemplateProperty),
            _ => throw new ArgumentException("Studio field headers support TextBox, NumberBox and ComboBox.", nameof(sender))
        };
        var state = new HeaderState(control, properties.Item1, properties.Item2, template);
        sender.SetValue(StateProperty, state);
    }

    private sealed class HeaderState : IDisposable
    {
        private readonly Control _control;
        private readonly DependencyProperty _header;
        private readonly DependencyProperty _headerTemplate;
        private readonly DataTemplate _template;
        private long? _token;
        private bool _owned;

        public HeaderState(Control control, DependencyProperty header, DependencyProperty headerTemplate, DataTemplate template)
        {
            _control = control; _header = header; _headerTemplate = headerTemplate; _template = template;
            control.Loaded += Loaded; control.Unloaded += Unloaded;
            Observe();
        }
        private void Loaded(object sender, RoutedEventArgs args) => Observe();
        private void Unloaded(object sender, RoutedEventArgs args) => Unobserve();
        private void Observe()
        {
            _token ??= _control.RegisterPropertyChangedCallback(_header, (_, _) => Update());
            Update();
        }
        private void Unobserve()
        {
            if (_token is not { } token) return;
            _control.UnregisterPropertyChangedCallback(_header, token); _token = null;
        }
        private void Update()
        {
            var current = _control.GetValue(_headerTemplate);
            // An explicitly supplied consumer template takes priority over our theme.
            if (current is not null && !ReferenceEquals(current, _template)) { _owned = false; return; }
            var header = _control.GetValue(_header);
            if (header is string { Length: > 0 })
            {
                _control.SetValue(_headerTemplate, _template); _owned = true;
            }
            else if (_owned || ReferenceEquals(current, _template))
            {
                _control.ClearValue(_headerTemplate); _owned = false;
            }
        }
        public void Dispose()
        {
            Unobserve(); _control.Loaded -= Loaded; _control.Unloaded -= Unloaded;
            if (_owned && ReferenceEquals(_control.GetValue(_headerTemplate), _template))
                _control.ClearValue(_headerTemplate);
        }
    }
}
