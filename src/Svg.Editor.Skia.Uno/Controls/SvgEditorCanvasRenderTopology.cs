using System;

namespace Svg.Editor.Skia.Uno;

/// <summary>
/// Keeps a canvas's startup topology immutable and prevents a canceled or
/// disposed paint from publishing its first successful completion notification.
/// </summary>
internal sealed class SvgEditorCanvasRenderTopology : IDisposable
{
    private bool _started;
    private bool _disposed;
    private Action? _onFirstPaintCompleted;

    public bool IsSingleCanvas { get; private set; }
    public long Generation { get; private set; }

    public void UseSingleCanvas(Action? onFirstPaintCompleted)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started || IsSingleCanvas)
            throw new InvalidOperationException("Canvas rendering must be configured once, before its first load or paint.");

        IsSingleCanvas = true;
        _onFirstPaintCompleted = onFirstPaintCompleted;
    }

    public void Start() => _started = true;

    public bool IsCurrent(long generation) => !_disposed && Generation == generation;

    public void CompletePaint(long generation)
    {
        if (!_started || !IsSingleCanvas || !IsCurrent(generation))
            return;

        // Clear before invoking a caller: reentrant painting or a throwing
        // observer must not report this same first completion again.
        var completed = _onFirstPaintCompleted;
        _onFirstPaintCompleted = null;
        completed?.Invoke();
    }

    public void CancelPaint() => Generation++;

    public void Dispose()
    {
        _disposed = true;
        _onFirstPaintCompleted = null;
        CancelPaint();
    }
}
