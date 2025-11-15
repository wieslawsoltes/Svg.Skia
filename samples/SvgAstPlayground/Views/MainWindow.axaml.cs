using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Primitives;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using SvgAstPlayground.ViewModels;

namespace SvgAstPlayground;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly TextDocument _document;
    private bool _isUpdatingEditorSelection;
    private bool _isUpdatingTreeSelection;
    private bool _isUpdatingFromEditorText;
    private bool _isUpdatingFromViewModelText;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _document = new TextDocument(_viewModel.SvgText ?? string.Empty);
        _document.TextChanged += OnDocumentTextChanged;
        _viewModel.EditorSelectionRequested += OnEditorSelectionRequested;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ConfigureEditor();
        SyncTextFromViewModel();
    }

    private async void OnOpenSvgClicked(object? sender, RoutedEventArgs e)
    {
        var provider = StorageProvider;
        if (provider is null)
        {
            return;
        }

        var fileTypes = new FilePickerFileType("SVG files")
        {
            Patterns = new[] { "*.svg" },
            AppleUniformTypeIdentifiers = new[] { "public.svg-image" },
            MimeTypes = new[] { "image/svg+xml" }
        };

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open SVG",
            AllowMultiple = false,
            FileTypeFilter = new[] { fileTypes, FilePickerFileTypes.All }
        });

        if (files.Count == 0)
        {
            return;
        }

        var file = files[0];
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            _viewModel.LoadFromContent(content, file.Name);
        }
        catch (Exception ex)
        {
            _viewModel.ReportLoadError($"Failed to load '{file.Name}': {ex.Message}");
        }
    }

    private void OnResetSampleClicked(object? sender, RoutedEventArgs e)
    {
        _viewModel.ResetToDefault();
    }

    private void OnAstTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingTreeSelection)
        {
            return;
        }

        if (AstTree?.SelectedItem is AstTreeNodeViewModel node)
        {
            _viewModel.SelectedAstNode = node;
        }
    }

    private void UpdateSelectionFromEditor()
    {
        if (_isUpdatingEditorSelection || SourceEditor is null)
        {
            return;
        }

        var start = SourceEditor.SelectionStart;
        var length = SourceEditor.SelectionLength;
        _viewModel.UpdateSelectionFromEditor(start, length);
    }

    private void OnEditorSelectionRequested(object? sender, TextSelectionRequestedEventArgs e)
    {
        if (SourceEditor is null)
        {
            return;
        }

        _isUpdatingEditorSelection = true;
        SourceEditor.Select(e.Start, e.Length);
        SourceEditor.TextArea.Caret.Offset = e.Start + e.Length;
        SourceEditor.TextArea.Caret.BringCaretToView();
        _isUpdatingEditorSelection = false;
        EnsureTreeSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedAstNode))
        {
            EnsureTreeSelection();
        }
        else if (e.PropertyName == nameof(MainViewModel.SvgText))
        {
            SyncTextFromViewModel();
        }
    }

    private void EnsureTreeSelection()
    {
        if (AstTree is null)
        {
            return;
        }

        _isUpdatingTreeSelection = true;
        try
        {
            if (_viewModel.SelectedAstNode is null)
            {
                AstTree.SelectedItem = null;
            }
            else
            {
                ExpandPathToNode(_viewModel.SelectedAstNode);
                AstTree.SelectedItem = _viewModel.SelectedAstNode;
                AstTree.ScrollIntoView(_viewModel.SelectedAstNode);
            }
        }
        finally
        {
            _isUpdatingTreeSelection = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.EditorSelectionRequested -= OnEditorSelectionRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (SourceEditor is not null)
        {
            SourceEditor.TextArea.SelectionChanged -= OnEditorSelectionChanged;
            SourceEditor.TextArea.Caret.PositionChanged -= OnEditorSelectionChanged;
        }
        _document.TextChanged -= OnDocumentTextChanged;
        _viewModel.Dispose();
    }

    private void ConfigureEditor()
    {
        if (SourceEditor is null)
        {
            return;
        }

        SourceEditor.Document = _document;
        SourceEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        SourceEditor.TextArea.SelectionChanged += OnEditorSelectionChanged;
        SourceEditor.TextArea.Caret.PositionChanged += OnEditorSelectionChanged;
    }

    private void OnEditorSelectionChanged(object? sender, EventArgs e)
        => UpdateSelectionFromEditor();

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFromViewModelText)
        {
            return;
        }

        _isUpdatingFromEditorText = true;
        _viewModel.SvgText = _document.Text;
        _isUpdatingFromEditorText = false;
    }

    private void SyncTextFromViewModel()
    {
        if (_isUpdatingFromEditorText)
        {
            return;
        }

        var newText = _viewModel.SvgText ?? string.Empty;
        if (_document.Text == newText)
        {
            return;
        }

        _isUpdatingFromViewModelText = true;
        _document.Text = newText;
        _isUpdatingFromViewModelText = false;
    }

    private void ExpandPathToNode(AstTreeNodeViewModel node)
    {
        if (AstTree is null)
        {
            return;
        }

        var ancestors = new Stack<AstTreeNodeViewModel>();
        var current = node.Parent;
        while (current is not null)
        {
            ancestors.Push(current);
            current = current.Parent;
        }

        ItemsControl currentItemsControl = AstTree;
        while (ancestors.Count > 0)
        {
            var ancestor = ancestors.Pop();
            var container = GetContainerForItem(currentItemsControl, ancestor);
            if (container is null)
            {
                return;
            }

            if (!container.IsExpanded)
            {
                container.IsExpanded = true;
                container.UpdateLayout();
            }

            currentItemsControl = container;
        }
    }

    private TreeViewItem? GetContainerForItem(ItemsControl owner, AstTreeNodeViewModel item)
    {
        if (owner.ContainerFromItem(item) is TreeViewItem container)
        {
            return container;
        }

        owner.ApplyTemplate();
        owner.UpdateLayout();
        return owner.ContainerFromItem(item) as TreeViewItem;
    }
}
