using System;
using Svg.Skia;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>
    /// Creates an independently owned preview renderer. The caller must dispose it.
    /// Resource base URI, rendering preferences and animation time match the editor;
    /// live document state, history and retained picture are not changed.
    /// </summary>
    public SKSvg CreateFilterPreviewRenderer(SvgFilterEdit edit, bool editSharedResource = false)
    {
        ThrowIfDisposed();
        var xml = BuildFilterPreviewSvg(edit, editSharedResource);
        var renderer = CreateRenderer(ParseDocument(xml, edit.BaseUri));
        try
        {
            renderer.SetAnimationTime(Renderer.AnimationTime);
            return renderer;
        }
        catch
        {
            renderer.Dispose();
            throw;
        }
    }
}
