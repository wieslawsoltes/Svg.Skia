using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Svg.Editor.Core;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// An interactive gallery for consumers of the reusable studio controls. Examples are
/// detached from the document and can exercise focus, input, disabled and selected states.
/// </summary>
public sealed class StudioControlGallery : UserControl
{
    private readonly TextBlock _feedback;

    public StudioControlGallery()
    {
        IsTabStop = false;
        Resources.MergedDictionaries.Add(new StudioThemeResources());
        _feedback = Hint("Ready. Use Tab to inspect focus without touching the document.");
        var body = new StackPanel { Spacing = 16 };
        body.Children.Add(new TextBlock
        {
            Text = "One system. Every workspace.", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(Hint("These are the actual controls used by UnoDraw. Resize the window, use Tab, edit fields, and open menus. Nothing here edits your artwork."));
        var fields = new StudioFieldGrid { MinimumColumnWidth = 220, MaximumColumns = 2, Spacing = 12 };
        fields.Children.Add(new TextBox { Header = "Object name", Text = "Editorial artwork", PlaceholderText = "Name your object" });
        fields.Children.Add(new ComboBox { Header = "Coordinate system", ItemsSource = new[] { "Object bounding box", "Document coordinates", "Preserve imported geometry" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch });
        fields.Children.Add(new NumberBox { Header = "Width · expressions supported", Value = 240.5, NumberFormatter = new EditorNumberFormatter(), SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact });
        fields.Children.Add(new TextBox { Header = "Locked property", Text = "Inherited from parent", IsEnabled = false });
        body.Children.Add(Section("Inputs", fields));

        var actions = new StudioFieldGrid { MaximumColumns = 3, MinimumColumnWidth = 120 };
        var primary = CreateButton("Apply changes", "Primary action invoked.");
        var normal = CreateButton("Duplicate", "Secondary action invoked.");
        var quiet = CreateButton("Reset", "Quiet action invoked.");
        var destructive = CreateButton("Remove", "Destructive role preview; no document was changed.");
        var disabled = new Button { Content = "Unavailable", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        var menu = new Button { Content = "Options…", HorizontalAlignment = HorizontalAlignment.Stretch };
        var flyout = new MenuFlyout();
        foreach (var label in new[] { "Align to selection", "Align to artboard", "Use a fixed key" })
        {
            var item = new MenuFlyoutItem { Text = label };
            item.Click += (_, _) => _feedback.Text = label;
            flyout.Items.Add(item);
        }
        menu.Flyout = flyout;
        StudioTheme.ApplyRole(primary, StudioActionRole.Primary, this);
        StudioTheme.ApplyRole(quiet, StudioActionRole.Quiet, this);
        StudioTheme.ApplyRole(destructive, StudioActionRole.Destructive, this);
        foreach (var button in new[] { primary, normal, quiet, destructive, disabled, menu }) actions.Children.Add(button);
        body.Children.Add(Section("Action hierarchy", actions));

        var choices = new StudioFieldGrid { MinimumColumnWidth = 160, MaximumColumns = 3 };
        choices.Children.Add(new CheckBox { Content = "Snap to grid", IsChecked = true });
        choices.Children.Add(new CheckBox { Content = "Mixed values", IsThreeState = true, IsChecked = null });
        choices.Children.Add(new CheckBox { Content = "Unavailable", IsEnabled = false });
        body.Children.Add(Section("Selection states", choices));

        var custom = new StudioFieldGrid { MinimumColumnWidth = 160, MaximumColumns = 3 };
        custom.Children.Add(new StudioNumericField { Header = "X", Value = 24.5, Suffix = "px" });
        custom.Children.Add(new StudioNumericField { Header = "W", IsMixed = true, Suffix = "px" });
        custom.Children.Add(new StudioPaintChip { Label = "Fill", Paint = "#417C73" });
        custom.Children.Add(new StudioPaintChip { Label = "Stroke", Paint = "none" });
        custom.Children.Add(new StudioPaintChip { Label = "Mixed", IsMixed = true });
        custom.Children.Add(new StudioPaintChip { Label = "Linked", Paint = "url(#gradient)" });
        body.Children.Add(Section("Precision and paint", custom));

        var selections = new StackPanel { Spacing = 12 };
        var strip = new StudioChoiceStrip
        {
            Header = "Direct choices · arrows and Home/End",
            ItemsSource = new[] { "Selection bounds", "Artboard", "Fixed key object" }, SelectedIndex = 1
        };
        strip.SelectionChanged += (_, _) => _feedback.Text = "Selected choice " + (strip.SelectedIndex + 1) + "; the artwork is unchanged.";
        AutomationProperties.SetAutomationId(strip, "StudioGalleryChoice");
        selections.Children.Add(strip);
        selections.Children.Add(new StudioChoiceStrip
        {
            Header = "Disabled choice retains its selection", ItemsSource = new[] { "Fill", "Stroke" },
            SelectedIndex = 0, IsEnabled = false
        });
        var toggles = new StudioFieldGrid { MaximumColumns = 3, MinimumColumnWidth = 120 };
        toggles.Children.Add(new ToggleButton { Content = "Selected", IsChecked = true, HorizontalAlignment = HorizontalAlignment.Stretch });
        toggles.Children.Add(new ToggleButton { Content = "Mixed", IsChecked = null, IsThreeState = true, HorizontalAlignment = HorizontalAlignment.Stretch });
        toggles.Children.Add(new ToggleButton { Content = "Disabled", IsChecked = true, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch });
        selections.Children.Add(toggles);
        body.Children.Add(Section("Segmented choices and toggles", selections));

        var tools = new StudioWrapPanel { Spacing = 6 };
        foreach (var kind in new[] { SvgEditorToolKind.Select, SvgEditorToolKind.PathSelect, SvgEditorToolKind.PathCubic, SvgEditorToolKind.Rect, SvgEditorToolKind.Ellipse, SvgEditorToolKind.Text })
            tools.Children.Add(new StudioToolButton { Label = kind.ToString(), IsChecked = kind == SvgEditorToolKind.Select, Content = new StudioToolIcon { Kind = kind } });
        tools.Children.Add(new StudioToolButton { Label = "Disabled tool", IsEnabled = false, Content = new StudioToolIcon { Kind = SvgEditorToolKind.Freehand } });
        body.Children.Add(Section("Vector tool controls", tools));
        AutomationProperties.SetAutomationId(_feedback, "StudioGalleryFeedback");
        body.Children.Add(_feedback);
        Content = body;
    }
    private Button CreateButton(string label, string response)
    {
        var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => _feedback.Text = response;
        return button;
    }
    private TextBlock Hint(string text) => new() { Text = text, Style = (Style)Resources["StudioHintStyle"] };
    private static StudioSection Section(string title, object content) => new() { Header = title, Content = content };
}
