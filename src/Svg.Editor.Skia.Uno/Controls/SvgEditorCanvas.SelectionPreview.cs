using SkiaSharp;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorCanvas
{
    private SKPicture? _selectionGhost;

    private void BeginSelectionGesture(Gesture gesture)
    {
        ReleaseSelectionGhost();
        if (!CanEditSelection)
        {
            _gesture = Gesture.None;
            return;
        }

        _gesture = gesture;
        if (Controller is not null)
        {
            // Record the selected retained scene once. Ancestor compositing state and original
            // SVG paint order are included, without cloning or reparsing the source document.
            _selectionGhost = Controller.Renderer.CreateRetainedSceneSelectionPicture(Controller.SelectedElements);
        }
    }

    private void DrawSelectionGhost(SKCanvas canvas, CanvasResources resources)
    {
        if (!_moved || _selectionGhost is null || _gesture is not (Gesture.Move or Gesture.Resize or Gesture.Rotate))
        {
            return;
        }

        using var restore = new SKAutoCanvasRestore(canvas);
        canvas.Concat(_previewTransform);
        canvas.DrawPicture(_selectionGhost, resources.GhostPaint);
    }

    private void ReleaseSelectionGhost()
    {
        _selectionGhost?.Dispose();
        _selectionGhost = null;
    }
}
