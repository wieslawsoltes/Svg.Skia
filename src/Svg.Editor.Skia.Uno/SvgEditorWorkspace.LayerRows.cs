using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private async void LayerRow_ActionRequested(object? sender, StudioLayerActionEventArgs args)
    {
        if (_disposed || _closing || _dialogVisible || Controller is not { } controller) return;
        try
        {
            CommitInspectorEdits();
            Canvas.CancelGesture();
            var element = args.Element;
            if (!ReferenceEquals(element.OwnerDocument, controller.Document))
                throw new InvalidOperationException("The document changed. Choose the object again in the Layers panel.");
            switch (args.Action)
            {
                case StudioLayerAction.ToggleVisibility when element is SvgVisualElement visual:
                    controller.SetHierarchyVisibility(visual, visual.Display == "none" || visual.Visibility == "hidden");
                    break;
                case StudioLayerAction.ToggleLock:
                    controller.SetHierarchyLocked(element, !SvgEditorController.IsLocked(element));
                    break;
                case StudioLayerAction.Rename:
                    await RenameLayerRowAsync(controller, element);
                    break;
            }
        }
        catch (Exception exception) { await ShowErrorAsync(exception.Message); }
    }

    private async Task RenameLayerRowAsync(SvgEditorController controller, SvgElement element)
    {
        var document = controller.Document;
        var input = new TextBox { Header = "SVG ID", Text = element.ID ?? string.Empty, PlaceholderText = "A unique XML name", MaxLength = 1024 };
        var error = StudioHint(string.Empty);
        var content = new StackPanel { Spacing = 10, Width = DialogContentWidth(340) };
        content.Children.Add(StudioHint("Document-local references are updated with the ID. This is one undoable edit."));
        content.Children.Add(input); content.Children.Add(error);
        var dialog = MakeDialog("Rename object", content, "Rename");
        dialog.Opened += (_, _) => { input.Focus(FocusState.Programmatic); input.SelectAll(); };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                if (!ReferenceEquals(controller, Controller) || !ReferenceEquals(document, controller.Document)
                    || !ReferenceEquals(element.OwnerDocument, document))
                    throw new InvalidOperationException("The document changed. Close this dialog and choose the object again.");
                controller.RenameElementId(element, input.Text.Trim());
            }
            catch (Exception exception) { args.Cancel = true; error.Text = exception.Message; }
        };
        await ShowDialogAsync(dialog);
    }
}
