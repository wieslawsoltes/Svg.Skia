using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Svg.Editor.Skia;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private async Task DivideSelectionAsync()
    {
        if (Controller is not { } controller) return;
        var ring = new ProgressRing { IsActive = true, Width = 36, Height = 36 };
        var message = new TextBlock { Text = "Preparing geometry…", TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 14, Width = DialogContentWidth(400) };
        panel.Children.Add(ring);
        panel.Children.Add(message);
        panel.Children.Add(new TextBlock
        {
            Text = "The document stays unchanged until Apply. Cancellation is checked between native geometry operations; one running native call must finish first.",
            TextWrapping = TextWrapping.Wrap, Opacity = 0.7
        });
        var dialog = MakeDialog("Divide paths", panel);
        dialog.IsPrimaryButtonEnabled = false;
        using var cancellation = new CancellationTokenSource();
        var closed = false;
        SvgPreparedDivision? prepared = null;
        Task? computation = null;
        var progress = new Progress<SvgPathDivisionProgress>(value =>
        {
            if (!closed)
                message.Text = $"Partitioning outlines · {value.OperandsCompleted}/{value.OperandCount} objects · {value.PieceCount} regions";
        });
        async Task ComputeAsync()
        {
            try
            {
                prepared = await controller.PrepareDivisionAsync(cancellationToken: cancellation.Token,
                    progress: progress, useWorkerThread: !OperatingSystem.IsBrowser());
                if (closed) return;
                ring.IsActive = false;
                message.Text = $"Ready: {prepared.PieceCount} editable SVG paths. Apply commits one undoable edit.";
                dialog.IsPrimaryButtonEnabled = true;
            }
            catch (OperationCanceledException)
            {
                if (!closed) { ring.IsActive = false; message.Text = "Cancelled. The document has not changed."; }
            }
            catch (Exception exception)
            {
                if (!closed) { ring.IsActive = false; message.Text = exception.Message; }
            }
        }
        dialog.Opened += (_, _) => computation = ComputeAsync();
        dialog.Closing += (_, _) => cancellation.Cancel();
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                if (prepared is null) throw new InvalidOperationException("The division is not ready yet.");
                controller.ApplyPreparedDivision(prepared);
            }
            catch (Exception exception) { args.Cancel = true; message.Text = exception.Message; dialog.IsPrimaryButtonEnabled = false; }
        };
        try { await ShowDialogAsync(dialog); }
        finally
        {
            closed = true;
            cancellation.Cancel();
            if (computation is not null) await computation;
        }
    }
}
