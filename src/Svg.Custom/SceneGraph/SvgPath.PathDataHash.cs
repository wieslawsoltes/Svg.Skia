namespace Svg;

public partial class SvgPath
{
    private SceneGraphPathDataHash _sceneGraphPathDataHash;
    private bool _sceneGraphPathDataHashInitialized;
    private bool _sceneGraphPathDataHashTrackingInitialized;

    internal bool TryGetSceneGraphPathDataHash(out SceneGraphPathDataHash hash)
    {
        EnsureSceneGraphPathDataHashTracking();

        if (_sceneGraphPathDataHashInitialized)
        {
            hash = _sceneGraphPathDataHash;
            return true;
        }

        hash = default;
        return false;
    }

    internal void SetSceneGraphPathDataHash(SceneGraphPathDataHash hash)
    {
        EnsureSceneGraphPathDataHashTracking();
        _sceneGraphPathDataHash = hash;
        _sceneGraphPathDataHashInitialized = true;
    }

    private void EnsureSceneGraphPathDataHashTracking()
    {
        if (_sceneGraphPathDataHashTrackingInitialized)
        {
            return;
        }

        AttributeChanged += OnSceneGraphPathDataAttributeChanged;
        _sceneGraphPathDataHashTrackingInitialized = true;
    }

    private void OnSceneGraphPathDataAttributeChanged(object sender, AttributeEventArgs e)
    {
        if (e?.Attribute != "d")
        {
            return;
        }

        _sceneGraphPathDataHashInitialized = false;
    }
}
