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
        Resources.MergedDictionaries.Add(new EditorTheme());
        _feedback = Hint("Ready. Use Tab to inspect focus without touching the document.");
        var body = new StackPanel { Spacing = 16 };
        body.Children.Add(new TextBlock
        {
            Text = "EditorTheme",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(Hint("These are the actual controls used by UnoDraw. Resize the window, use Tab, edit fields, and open menus. Nothing here edits your artwork."));
        var fields = new StudioFieldGrid { MinimumColumnWidth = 220, MaximumColumns = 2, Spacing = 12 };
        fields.Children.Add(new TextBox { Header = "Object name", Text = "Editorial artwork", PlaceholderText = "Name your object" });
        fields.Children.Add(new ComboBox { Header = "Coordinate system", ItemsSource = new[] { "Object bounding box", "Document coordinates", "Preserve imported geometry" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch });
        fields.Children.Add(new NumberBox { Header = "Width · expressions supported", Value = 240.5, NumberFormatter = new EditorNumberFormatter(), SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact });
        fields.Children.Add(new TextBox { Header = "Locked property", Text = "Inherited from parent", IsEnabled = false });
        fields.Children.Add(new ComboBox { Header = "Editable choice", ItemsSource = new[] { "Pixels", "Points", "Millimeters" }, IsEditable = true, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch });
        fields.Children.Add(new NumberBox { Header = "Inline increment controls", Value = 12, Minimum = 0, Maximum = 100, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline });
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
        flyout.Items.Add(new MenuFlyoutSeparator());
        var checkedItem = new ToggleMenuFlyoutItem { Text = "Show guides", IsChecked = true, KeyboardAcceleratorTextOverride = "Ctrl+;" };
        checkedItem.Click += (_, _) => _feedback.Text = checkedItem.IsChecked ? "Gallery guides enabled." : "Gallery guides disabled.";
        flyout.Items.Add(checkedItem);
        var submenu = new MenuFlyoutSubItem { Text = "Arrange" };
        submenu.Items.Add(new MenuFlyoutItem { Text = "Bring to front", KeyboardAcceleratorTextOverride = "Ctrl+Shift+]" });
        submenu.Items.Add(new MenuFlyoutItem { Text = "Send to back", KeyboardAcceleratorTextOverride = "Ctrl+Shift+[" });
        submenu.Items.Add(new MenuFlyoutItem { Text = "Unavailable action", IsEnabled = false });
        flyout.Items.Add(submenu);
        menu.Flyout = flyout;
        StudioTheme.ApplyRole(primary, StudioActionRole.Primary, this);
        StudioTheme.ApplyRole(quiet, StudioActionRole.Quiet, this);
        StudioTheme.ApplyRole(destructive, StudioActionRole.Destructive, this);
        foreach (var button in new[] { primary, normal, quiet, destructive, disabled, menu })
            actions.Children.Add(button);
        body.Children.Add(Section("Action hierarchy", actions));

        var choices = new StudioFieldGrid { MinimumColumnWidth = 160, MaximumColumns = 3 };
        choices.Children.Add(new CheckBox { Content = "Snap to grid", IsChecked = true });
        choices.Children.Add(new CheckBox { Content = "Mixed values", IsThreeState = true, IsChecked = null });
        choices.Children.Add(new CheckBox { Content = "Unavailable", IsEnabled = false });
        choices.Children.Add(new RadioButton { Content = "Selection", GroupName = "EditorGalleryBounds", IsChecked = true });
        choices.Children.Add(new RadioButton { Content = "Artboard", GroupName = "EditorGalleryBounds" });
        choices.Children.Add(new ToggleSwitch { Header = "Preview", OnContent = "Enabled", OffContent = "Disabled", IsOn = true });
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
            ItemsSource = new[] { "Selection bounds", "Artboard", "Fixed key object" },
            SelectedIndex = 1
        };
        strip.SelectionChanged += (_, _) => _feedback.Text = "Selected choice " + (strip.SelectedIndex + 1) + "; the artwork is unchanged.";
        AutomationProperties.SetAutomationId(strip, "StudioGalleryChoice");
        selections.Children.Add(strip);
        selections.Children.Add(new StudioChoiceStrip
        {
            Header = "Disabled choice retains its selection",
            ItemsSource = new[] { "Fill", "Stroke" },
            SelectedIndex = 0,
            IsEnabled = false
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

        var ranges = new StudioFieldGrid { MinimumColumnWidth = 220, MaximumColumns = 2, Spacing = 12 };
        var opacity = new Slider { Header = "Opacity", Minimum = 0, Maximum = 100, Value = 65, StepFrequency = 1, TickFrequency = 25, TickPlacement = TickPlacement.Outside };
        var progress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 65, VerticalAlignment = VerticalAlignment.Center };
        opacity.ValueChanged += (_, args) => progress.Value = args.NewValue;
        ranges.Children.Add(opacity);
        ranges.Children.Add(progress);
        ranges.Children.Add(new Slider { Header = "Disabled range", Minimum = 0, Maximum = 100, Value = 40, IsEnabled = false });
        var activity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        activity.Children.Add(new ProgressRing { IsActive = true, Width = 24, Height = 24 });
        activity.Children.Add(new ProgressBar { IsIndeterminate = true, Width = 160, VerticalAlignment = VerticalAlignment.Center });
        ranges.Children.Add(activity);
        body.Children.Add(Section("Ranges and progress", ranges));

        var navigation = new StudioFieldGrid { MinimumColumnWidth = 220, MaximumColumns = 2, Spacing = 12 };
        var layers = new TreeView { SelectionMode = TreeViewSelectionMode.Multiple, Height = 168 };
        var artwork = new TreeViewNode { Content = "Artwork", IsExpanded = true };
        foreach (var label in new[] { "Background", "Illustration", "Typography", "Guides", "Mask", "Highlights" })
            artwork.Children.Add(new TreeViewNode { Content = label });
        layers.RootNodes.Add(artwork);
        AutomationProperties.SetAutomationId(layers, "EditorGalleryTree");
        navigation.Children.Add(layers);
        var items = new ListView
        {
            ItemsSource = new[] { "Default", "Fine outline", "Heavy outline", "Dashed", "Tapered", "Calligraphic", "Pattern" },
            Height = 168,
            SelectionMode = ListViewSelectionMode.Multiple,
            IsMultiSelectCheckBoxEnabled = true
        };
        AutomationProperties.SetAutomationId(items, "EditorGalleryList");
        navigation.Children.Add(items);
        body.Children.Add(Section("Layers and scrolling lists", navigation));

        var tabs = new TabView { IsAddTabButtonVisible = true, TabWidthMode = TabViewWidthMode.SizeToContent, CanReorderTabs = true };
        var tabNumber = 0;
        void AddTab()
        {
            var number = ++tabNumber;
            var tab = new TabViewItem
            {
                Header = "Document " + number,
                Content = new TextBlock { Text = "Tab " + number + " content. Use Ctrl+Tab, close a tab, or add another.", Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap }
            };
            tabs.TabItems.Add(tab);
            tabs.SelectedItem = tab;
        }
        AddTab();
        AddTab();
        AddTab();
        tabs.AddTabButtonClick += (_, _) => AddTab();
        tabs.TabCloseRequested += (_, args) => tabs.TabItems.Remove(args.Tab);
        AutomationProperties.SetAutomationId(tabs, "EditorGalleryTabs");
        body.Children.Add(Section("Document tabs", tabs));

        var color = new ColorPicker
        {
            Color = Windows.UI.Color.FromArgb(210, 72, 132, 198),
            PreviousColor = Windows.UI.Color.FromArgb(255, 44, 94, 148),
            IsAlphaEnabled = true,
            IsAlphaSliderVisible = true,
            IsAlphaTextInputVisible = true,
            IsMoreButtonVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        color.ColorChanged += (_, args) => _feedback.Text = $"Gallery color #{args.NewColor.A:X2}{args.NewColor.R:X2}{args.NewColor.G:X2}{args.NewColor.B:X2}.";
        AutomationProperties.SetAutomationId(color, "EditorGalleryColor");
        var appearance = new Expander { Header = "Color and transparency", Content = color, IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        body.Children.Add(Section("Expandable color controls", appearance));
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
