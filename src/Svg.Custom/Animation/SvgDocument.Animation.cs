using System;

namespace Svg
{
    public partial class SvgDocument
    {
        public override SvgElement DeepCopy()
        {
            return DeepCopy<SvgDocument>();
        }

        public override SvgElement DeepCopy<T>()
        {
            var newObj = base.DeepCopy<T>() as SvgDocument;

            if (newObj == null)
            {
                return null;
            }

            newObj.Ppi = Ppi;
            newObj.BaseUri = BaseUri;
            newObj.ExternalCSSHref = ExternalCSSHref;
            newObj.LoadOptions = LoadOptions;
            CopyCompatibilityStyleSourcesTo(newObj);
            CopyCompatibilityStyleStateTo(newObj);

            foreach (var ns in Namespaces)
            {
                newObj.Namespaces[ns.Key] = ns.Value;
            }

            return newObj;
        }

        internal void RebindSameDocumentDeferredPaintServers()
        {
            RebindSameDocumentDeferredPaintServers(this);

            foreach (var element in Descendants())
            {
                RebindSameDocumentDeferredPaintServers(element);
            }
        }

        private void RebindSameDocumentDeferredPaintServers(SvgElement element)
        {
            foreach (var attribute in element.Attributes)
            {
                if (attribute.Value is SvgDeferredPaintServer deferredPaintServer &&
                    IsSameDocumentDeferredPaintServer(deferredPaintServer))
                {
                    deferredPaintServer.RebindDocument(this);
                }
            }
        }

        private static bool IsSameDocumentDeferredPaintServer(SvgDeferredPaintServer paintServer)
        {
            var deferredId = paintServer.DeferredId?.Trim();
            if (string.IsNullOrEmpty(deferredId))
            {
                return false;
            }

            if (string.Equals(deferredId, "currentColor", StringComparison.Ordinal))
            {
                return true;
            }

            if (deferredId[0] == '#')
            {
                return true;
            }

            if (!deferredId.StartsWith("url(", StringComparison.OrdinalIgnoreCase) ||
                !deferredId.EndsWith(")", StringComparison.Ordinal))
            {
                return false;
            }

            var url = deferredId.Substring(4, deferredId.Length - 5).Trim();
            if (url.Length >= 2 &&
                ((url[0] == '"' && url[url.Length - 1] == '"') ||
                 (url[0] == '\'' && url[url.Length - 1] == '\'')))
            {
                url = url.Substring(1, url.Length - 2).Trim();
            }

            return url.StartsWith("#", StringComparison.Ordinal);
        }
    }
}
