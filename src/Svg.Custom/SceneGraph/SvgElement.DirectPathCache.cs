namespace Svg
{
    public abstract partial class SvgElement
    {
        private uint _sceneGraphDirectVisualPathVersion;
        private bool _sceneGraphDirectVisualPathTrackingInitialized;

        internal uint GetSceneGraphDirectVisualPathVersion()
        {
            EnsureSceneGraphDirectVisualPathTracking();
            return _sceneGraphDirectVisualPathVersion;
        }

        private void EnsureSceneGraphDirectVisualPathTracking()
        {
            if (_sceneGraphDirectVisualPathTrackingInitialized)
            {
                return;
            }

            AttributeChanged += OnSceneGraphDirectVisualPathAttributeChanged;
            _sceneGraphDirectVisualPathTrackingInitialized = true;
        }

        private void OnSceneGraphDirectVisualPathAttributeChanged(object sender, AttributeEventArgs e)
        {
            if (!ShouldInvalidateSceneGraphDirectVisualPathCache(e?.Attribute))
            {
                return;
            }

            unchecked
            {
                _sceneGraphDirectVisualPathVersion++;
            }
        }

        private bool ShouldInvalidateSceneGraphDirectVisualPathCache(string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
            {
                return false;
            }

            switch (this)
            {
                case SvgPath _:
                    return attributeName == "d";
                case SvgRectangle _:
                    return attributeName == "x" ||
                           attributeName == "y" ||
                           attributeName == "width" ||
                           attributeName == "height" ||
                           attributeName == "rx" ||
                           attributeName == "ry";
                case SvgCircle _:
                    return attributeName == "cx" ||
                           attributeName == "cy" ||
                           attributeName == "r";
                case SvgEllipse _:
                    return attributeName == "cx" ||
                           attributeName == "cy" ||
                           attributeName == "rx" ||
                           attributeName == "ry";
                case SvgLine _:
                    return attributeName == "x1" ||
                           attributeName == "y1" ||
                           attributeName == "x2" ||
                           attributeName == "y2";
                case SvgPolyline _:
                case SvgPolygon _:
                    return attributeName == "points";
                default:
                    return false;
            }
        }
    }
}
