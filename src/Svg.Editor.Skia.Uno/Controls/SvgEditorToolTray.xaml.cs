using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Svg.Editor.Skia.Uno.Models;

namespace Svg.Editor.Skia.Uno.Controls;

public sealed partial class SvgEditorToolTray : UserControl
{
    public static readonly DependencyProperty IsDevModeActiveProperty =
        DependencyProperty.Register(
            nameof(IsDevModeActive),
            typeof(bool),
            typeof(SvgEditorToolTray),
            new PropertyMetadata(false, OnIsDevModeActivePropertyChanged));

    public static readonly DependencyProperty ToolGroupsProperty =
        DependencyProperty.Register(
            nameof(ToolGroups),
            typeof(IEnumerable<EditorToolGroupDefinition>),
            typeof(SvgEditorToolTray),
            new PropertyMetadata(null, OnToolGroupsPropertyChanged));

    private readonly ObservableCollection<EditorToolGroupDefinition> _visibleGroups = [];
    private readonly List<EditorToolGroupDefinition> _trackedGroups = [];
    private EditorToolTrayMode _currentMode = EditorToolTrayMode.Design;

    public SvgEditorToolTray()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
        UpdateModeVisuals();
    }

    public IEnumerable<EditorToolGroupDefinition>? ToolGroups
    {
        get => (IEnumerable<EditorToolGroupDefinition>?)GetValue(ToolGroupsProperty);
        set => SetValue(ToolGroupsProperty, value);
    }

    public bool IsDevModeActive
    {
        get => (bool)GetValue(IsDevModeActiveProperty);
        set => SetValue(IsDevModeActiveProperty, value);
    }

    public ObservableCollection<EditorToolGroupDefinition> VisibleGroups => _visibleGroups;

    public event RoutedEventHandler? ToolRequested;

    public event RoutedEventHandler? DevModeRequested;

    private static void OnIsDevModeActivePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SvgEditorToolTray)d;
        control.ApplyExternalModeState();
    }

    private static void OnToolGroupsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SvgEditorToolTray)d;
        control.UnsubscribeGroups(e.OldValue as IEnumerable<EditorToolGroupDefinition>);
        control.SubscribeGroups(e.NewValue as IEnumerable<EditorToolGroupDefinition>);
        control.EnsureModeMatchesSelection();
        control.RefreshVisibleGroups();
    }

    private void SubscribeGroups(IEnumerable<EditorToolGroupDefinition>? groups)
    {
        if (groups is null)
        {
            return;
        }

        foreach (var group in groups)
        {
            group.PropertyChanged += OnGroupPropertyChanged;
            if (!_trackedGroups.Contains(group))
            {
                _trackedGroups.Add(group);
            }

            if (group.Items is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged += OnGroupItemsCollectionChanged;
            }
        }
    }

    private void UnsubscribeGroups(IEnumerable<EditorToolGroupDefinition>? groups)
    {
        if (groups is null)
        {
            return;
        }

        foreach (var group in groups)
        {
            group.PropertyChanged -= OnGroupPropertyChanged;
            _trackedGroups.Remove(group);

            if (group.Items is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged -= OnGroupItemsCollectionChanged;
            }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeGroups(_trackedGroups.ToArray());
    }

    private void OnGroupItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        EnsureModeMatchesSelection();
        RefreshVisibleGroups();
    }

    private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorToolGroupDefinition.IsSelected) or nameof(EditorToolGroupDefinition.CurrentItem))
        {
            EnsureModeMatchesSelection();
            RefreshVisibleGroups();
        }
    }

    private void RefreshVisibleGroups()
    {
        _visibleGroups.Clear();

        if (ToolGroups is null)
        {
            UpdateModeVisuals();
            return;
        }

        foreach (var group in ToolGroups.Where(group => group.VisibleModes.HasFlag(_currentMode)))
        {
            _visibleGroups.Add(group);
        }

        SortVisibleGroups();

        UpdateModeVisuals();
    }

    private void EnsureModeMatchesSelection()
    {
        var groups = ToolGroups?.ToArray();
        if (groups is null || groups.Length == 0)
        {
            return;
        }

        if (groups.Any(group => group.IsSelected && group.VisibleModes.HasFlag(_currentMode)))
        {
            return;
        }

        if (groups.Any(group => group.IsSelected && group.VisibleModes.HasFlag(EditorToolTrayMode.Draw)))
        {
            _currentMode = EditorToolTrayMode.Draw;
        }
        else if (groups.Any(group => group.IsSelected && group.VisibleModes.HasFlag(EditorToolTrayMode.Code)))
        {
            _currentMode = EditorToolTrayMode.Code;
        }
        else if (groups.Any(group => group.IsSelected && group.VisibleModes.HasFlag(EditorToolTrayMode.Design)))
        {
            _currentMode = EditorToolTrayMode.Design;
        }
    }

    private void UpdateModeVisuals()
    {
        if (DrawModeBorder is null || DesignModeBorder is null || CodeModeBorder is null)
        {
            return;
        }

        ApplyModeChrome(DrawModeBorder, _currentMode == EditorToolTrayMode.Draw);
        ApplyModeChrome(DesignModeBorder, _currentMode == EditorToolTrayMode.Design);
        ApplyModeChrome(CodeModeBorder, _currentMode == EditorToolTrayMode.Code);
    }

    private void SortVisibleGroups()
    {
        if (_visibleGroups.Count <= 1)
        {
            return;
        }

        var sorted = _visibleGroups
            .OrderBy(GetModeSortOrder)
            .ThenBy(group => group.Label, StringComparer.Ordinal)
            .ToArray();

        _visibleGroups.Clear();
        foreach (var group in sorted)
        {
            _visibleGroups.Add(group);
        }
    }

    private int GetModeSortOrder(EditorToolGroupDefinition group)
    {
        return _currentMode switch
        {
            EditorToolTrayMode.Draw => group.Id switch
            {
                "move" => 0,
                "pen" => 1,
                "brush" => 2,
                "pencil" => 3,
                "region" => 4,
                "shape" => 5,
                "text" => 6,
                _ => 100
            },
            EditorToolTrayMode.Code => group.Id switch
            {
                "move" => 0,
                _ => 100
            },
            _ => group.Id switch
            {
                "move" => 0,
                "region" => 1,
                "shape" => 2,
                "pen-menu" => 3,
                "text" => 4,
                _ => 100
            }
        };
    }

    private Brush ResolveBrush(string key)
    {
        return (Brush)Resources[key];
    }

    private void ApplyModeChrome(Border border, bool isActive)
    {
        border.Background = ResolveBrush(isActive ? "AccentSoftBrush" : "SurfaceAltBrush");
        border.BorderBrush = ResolveBrush(isActive ? "AccentStrongBrush" : "SurfaceStrokeBrush");
        border.BorderThickness = new Thickness(1);
    }

    private void SetMode(EditorToolTrayMode mode)
    {
        if (_currentMode == mode)
        {
            return;
        }

        _currentMode = mode;
        RefreshVisibleGroups();
    }

    private void ApplyExternalModeState()
    {
        if (IsDevModeActive)
        {
            _currentMode = EditorToolTrayMode.Code;
            RefreshVisibleGroups();
        }
        else if (_currentMode == EditorToolTrayMode.Code)
        {
            _currentMode = EditorToolTrayMode.Design;
            RefreshVisibleGroups();
        }
    }

    private void OnDrawModeClick(object sender, RoutedEventArgs e)
    {
        SetMode(EditorToolTrayMode.Draw);
    }

    private void OnDesignModeClick(object sender, RoutedEventArgs e)
    {
        SetMode(EditorToolTrayMode.Design);
    }

    private void OnCodeModeClick(object sender, RoutedEventArgs e)
    {
        SetMode(EditorToolTrayMode.Code);
        DevModeRequested?.Invoke(sender, e);
    }

    private void OnGroupPrimaryClick(object sender, RoutedEventArgs e)
    {
        ToolRequested?.Invoke(sender, e);
    }

    private void OnGroupMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: EditorToolGroupDefinition group } target)
        {
            return;
        }

        var flyout = BuildGroupFlyout(group);
        flyout.ShowAt(target, new FlyoutShowOptions
        {
            Placement = FlyoutPlacementMode.TopEdgeAlignedLeft,
            ShowMode = FlyoutShowMode.Transient
        });
    }

    private Flyout BuildGroupFlyout(EditorToolGroupDefinition group)
    {
        var rows = new StackPanel
        {
            Spacing = 2
        };

        var flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.TopEdgeAlignedLeft,
            Content = new Border
            {
                Style = (Style)Resources["ZoomMenuFrameBorderStyle"],
                Child = rows
            },
            FlyoutPresenterStyle = CreateFlyoutPresenterStyle()
        };

        foreach (var tool in group.Items)
        {
            var button = new Button
            {
                Style = (Style)Resources["ZoomMenuRowButtonStyle"],
                Tag = tool,
                Content = BuildToolRow(tool)
            };

            ToolTipService.SetToolTip(button, string.IsNullOrWhiteSpace(tool.Shortcut) ? tool.Label : $"{tool.Label}  {tool.Shortcut}");
            button.Click += (_, args) =>
            {
                ToolRequested?.Invoke(button, args);
                flyout.Hide();
            };
            rows.Children.Add(button);
        }

        return flyout;
    }

    private UIElement BuildToolRow(EditorToolDefinition tool)
    {
        var grid = new Grid
        {
            ColumnSpacing = 12
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var check = new TextBlock
        {
            Text = tool.IsSelected ? "✓" : string.Empty,
            Style = (Style)Resources["ZoomMenuCheckTextStyle"],
            HorizontalAlignment = HorizontalAlignment.Center
        };
        grid.Children.Add(check);

        UIElement iconElement = tool.HasIcon
            ? new FigmaIcon
            {
                Kind = tool.IconKind,
                Width = 16,
                Height = 16,
                IconStroke = ResolveBrush("DarkSurfaceTextBrush")
            }
            : new TextBlock
            {
                Text = tool.Glyph,
                Style = (Style)Resources["ZoomMenuRowTextStyle"],
                FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 }
            };
        Grid.SetColumn(iconElement, 1);
        grid.Children.Add(iconElement);

        var label = new TextBlock
        {
            Text = tool.Label,
            Style = (Style)Resources["ZoomMenuRowTextStyle"]
        };
        Grid.SetColumn(label, 2);
        grid.Children.Add(label);

        var shortcut = new TextBlock
        {
            Text = tool.Shortcut,
            Style = (Style)Resources["ZoomMenuShortcutTextStyle"],
            Visibility = string.IsNullOrWhiteSpace(tool.Shortcut) ? Visibility.Collapsed : Visibility.Visible
        };
        Grid.SetColumn(shortcut, 3);
        grid.Children.Add(shortcut);

        return grid;
    }

    private static Style CreateFlyoutPresenterStyle()
    {
        var style = new Style(typeof(FlyoutPresenter));
        var transparent = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        style.Setters.Add(new Setter(FlyoutPresenter.BackgroundProperty, new SolidColorBrush(transparent)));
        style.Setters.Add(new Setter(FlyoutPresenter.BorderBrushProperty, new SolidColorBrush(transparent)));
        style.Setters.Add(new Setter(FlyoutPresenter.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(FlyoutPresenter.PaddingProperty, new Thickness(0)));
        return style;
    }
}
