using Windows.Foundation;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private static readonly AsyncLocal<CancellationToken> s_mcpInteractionCancellation = new();
    private CancellationToken _mcpDialogCancellation;

    // An asynchronous command carries its own context. Native button handlers in
    // its modal run in the UI event context, so they use the modal's token instead.
    private CancellationToken CurrentMcpInteractionCancellation => s_mcpInteractionCancellation.Value.CanBeCanceled
        ? s_mcpInteractionCancellation.Value : _mcpDialogCancellation;

    private async Task<T> AwaitMcpPickerAsync<T>(IAsyncOperation<T> operation)
    {
        var cancellationToken = CurrentMcpInteractionCancellation;
        if (!cancellationToken.CanBeCanceled)
            return await operation;

        using var registration = cancellationToken.Register(() => DispatcherQueue.TryEnqueue(() =>
        {
            try
            { operation.Cancel(); }
            catch (Exception) { }
        }));
        // A platform picker may not implement cancellation. Stop awaiting it and
        // discard a late result so a disconnected command cannot edit afterwards.
        var result = await operation.AsTask().WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
