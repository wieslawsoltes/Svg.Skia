using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Svg.Editor.Core;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public enum StudioLayerAction { ToggleVisibility, ToggleLock, Rename }

public sealed class StudioLayerActionEventArgs : EventArgs
{
    public StudioLayerActionEventArgs(SvgElement element, StudioLayerAction action)
    { Element = element; Action = action; }
    public SvgElement Element { get; }
    public StudioLayerAction Action { get; }
}

/// <summary>A compact outline row. The workspace owns all mutations and undo transactions.</summary>
public sealed partial class StudioLayerRow : UserControl
{
    public static readonly DependencyProperty ElementProperty = DependencyProperty.Register(
        nameof(Element), typeof(SvgElement), typeof(StudioLayerRow),
        new PropertyMetadata(null, (sender, _) => ((StudioLayerRow)sender).RefreshElement()));
    private bool _initialized;
    public SvgElement? Element { get => (SvgElement?)GetValue(ElementProperty); set => SetValue(ElementProperty, value); }
    public event EventHandler<StudioLayerActionEventArgs>? ActionRequested;

    public StudioLayerRow()
    {
        InitializeComponent();
        _initialized = true;
        var menu = new MenuFlyout();
        var rename = new MenuFlyoutItem { Text = "Rename SVG ID…" };
        rename.Click += (_, _) => Request(StudioLayerAction.Rename);
        menu.Items.Add(rename);
        ContextFlyout = menu;
        // Click uses RoutedEventArgs, not a handled input event. Suppress any
        // unhandled tap separately so the enclosing row does not invoke selection.
        VisibilityButton.Tapped += (_, args) => args.Handled = true;
        LockButton.Tapped += (_, args) => args.Handled = true;
        RefreshElement();
    }

    private void RefreshElement()
    {
        if (!_initialized) return;
        var element = Element;
        if (element is null)
        {
            TitleText.Text = string.Empty; KindText.Text = string.Empty;
            VisibilityButton.IsEnabled = LockButton.IsEnabled = false;
            return;
        }
        var kind = SvgElementInfo.GetElementName(element.GetType());
        var title = string.IsNullOrEmpty(element.ID) ? kind : element.ID;
        var hidden = element.Display == "none" || element.Visibility == "hidden";
        var locked = SvgEditorController.IsLocked(element);
        TitleText.Text = title;
        TitleText.Opacity = hidden ? 0.55 : 1;
        KindText.Text = kind switch
        {
            "g" => "G", "path" => "P", "text" or "tspan" => "T", "rect" => "R",
            "circle" or "ellipse" => "E", "image" => "I", "use" => "U", "defs" => "D",
            _ => kind.Length == 0 ? "?" : kind.Substring(0, Math.Min(2, kind.Length)).ToUpperInvariant()
        };
        HiddenMark.Visibility = hidden ? Visibility.Visible : Visibility.Collapsed;
        LockButton.Opacity = locked ? 1 : 0.35;
        LockButton.IsEnabled = true;
        VisibilityButton.IsEnabled = element is SvgVisualElement && !SvgEditorController.IsEditLocked(element);
        var visibilityLabel = (hidden ? "Show " : "Hide ") + title;
        var lockLabel = (locked ? "Unlock " : "Lock ") + title;
        AutomationProperties.SetName(VisibilityButton, visibilityLabel);
        AutomationProperties.SetName(LockButton, lockLabel);
        AutomationProperties.SetName(this, title + ", SVG " + kind);
        ToolTipService.SetToolTip(VisibilityButton, visibilityLabel);
        ToolTipService.SetToolTip(LockButton,
            element.Parent is { } parent && SvgEditorController.IsLocked(parent) ? "Lock inherited from the parent layer" : lockLabel);
        ToolTipService.SetToolTip(TitleText, title + " · " + kind + " · double-click to rename its SVG ID");
    }

    private void Request(StudioLayerAction action)
    {
        if (Element is { } element) ActionRequested?.Invoke(this, new StudioLayerActionEventArgs(element, action));
    }
    private void Visibility_Click(object sender, RoutedEventArgs args) => Request(StudioLayerAction.ToggleVisibility);
    private void Lock_Click(object sender, RoutedEventArgs args) => Request(StudioLayerAction.ToggleLock);
    private void Title_DoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    { args.Handled = true; Request(StudioLayerAction.Rename); }
}
