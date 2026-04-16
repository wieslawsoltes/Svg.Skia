#nullable enable

namespace Svg
{
    public abstract partial class SvgElement
    {
        private bool _sceneGraphAddressKeyCacheInitialized;
        private SvgElement? _sceneGraphAddressKeyParent;
        private string? _sceneGraphAddressKeyParentKey;
        private int _sceneGraphAddressKeyChildIndex = -1;
        private string? _sceneGraphAddressKey;

        internal bool TryGetSceneGraphAddressKey(
            SvgElement? parent,
            string? parentKey,
            out string? addressKey)
        {
            if (!_sceneGraphAddressKeyCacheInitialized ||
                !object.ReferenceEquals(_sceneGraphAddressKeyParent, parent) ||
                parent is null ||
                _sceneGraphAddressKeyChildIndex < 0 ||
                _sceneGraphAddressKeyChildIndex >= parent.Children.Count ||
                !object.ReferenceEquals(parent.Children[_sceneGraphAddressKeyChildIndex], this) ||
                !string.Equals(_sceneGraphAddressKeyParentKey, parentKey, System.StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(_sceneGraphAddressKey))
            {
                addressKey = null;
                return false;
            }

            addressKey = _sceneGraphAddressKey;
            return true;
        }

        internal void SetSceneGraphAddressKey(
            SvgElement? parent,
            string? parentKey,
            int childIndex,
            string? addressKey)
        {
            _sceneGraphAddressKeyCacheInitialized = true;
            _sceneGraphAddressKeyParent = parent;
            _sceneGraphAddressKeyParentKey = parentKey;
            _sceneGraphAddressKeyChildIndex = childIndex;
            _sceneGraphAddressKey = addressKey;
        }
    }
}

#nullable restore
