#nullable enable
using System.Collections.Generic;

namespace Svg
{
    public abstract partial class SvgElement
    {
        private uint _sceneGraphCompileMetadataVersion;
        private bool _sceneGraphCompileMetadataTrackingInitialized;
        private List<string>? _sceneGraphPendingChangedAttributes;

        internal uint GetSceneGraphCompileMetadataVersion()
        {
            EnsureSceneGraphCompileMetadataTracking();

            var version = _sceneGraphCompileMetadataVersion;
            if (Parent is SvgElement parent)
            {
                version = CombineSceneGraphCompileMetadataVersion(
                    parent.GetSceneGraphCompileMetadataVersion(),
                    version);
            }

            return version;
        }

        internal IReadOnlyCollection<string>? ConsumeSceneGraphPendingChangedAttributes()
        {
            if (_sceneGraphPendingChangedAttributes is not { Count: > 0 } changedAttributes)
            {
                return null;
            }

            _sceneGraphPendingChangedAttributes = null;
            return changedAttributes;
        }

        private void EnsureSceneGraphCompileMetadataTracking()
        {
            if (_sceneGraphCompileMetadataTrackingInitialized)
            {
                return;
            }

            AttributeChanged += OnSceneGraphCompileMetadataAttributeChanged;
            _sceneGraphCompileMetadataTrackingInitialized = true;
        }

        private void OnSceneGraphCompileMetadataAttributeChanged(object? sender, AttributeEventArgs e)
        {
            if (e is null)
            {
                return;
            }

            var attribute = e.Attribute;
            if (string.IsNullOrWhiteSpace(attribute))
            {
                return;
            }

            AddSceneGraphPendingChangedAttribute(attribute!);

            unchecked
            {
                _sceneGraphCompileMetadataVersion++;
            }
        }

        private void AddSceneGraphPendingChangedAttribute(string attributeName)
        {
            _sceneGraphPendingChangedAttributes ??= new List<string>(2);
            for (var i = 0; i < _sceneGraphPendingChangedAttributes.Count; i++)
            {
                if (string.Equals(_sceneGraphPendingChangedAttributes[i], attributeName, System.StringComparison.Ordinal))
                {
                    return;
                }
            }

            _sceneGraphPendingChangedAttributes.Add(attributeName);
        }

        private static uint CombineSceneGraphCompileMetadataVersion(uint inheritedVersion, uint localVersion)
        {
            unchecked
            {
                return (inheritedVersion * 16777619u) ^ localVersion;
            }
        }
    }
}

#nullable restore
