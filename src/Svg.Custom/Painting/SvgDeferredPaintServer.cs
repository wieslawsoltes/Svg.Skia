using System;
using System.ComponentModel;
using System.Text;

namespace Svg
{
    /// <summary>
    /// A wrapper for a paint server which isn't defined currently in the parse process,
    /// but should be defined by the time the image needs to render.
    ///
    /// Compared to the original upstream file, this Svg.Custom override keeps track of the
    /// document that originally parsed the deferred paint server. The upstream implementation
    /// resolved deferred <c>url(...)</c> paint servers through <c>styleOwner.OwnerDocument</c>
    /// only, which is fine for direct rendering but breaks our retained-scene expansion of
    /// external/internal <c>&lt;use&gt;</c> content because referenced elements are temporarily
    /// reparented to inherit local styles. When that happens, the style owner can point at a
    /// different document than the one that actually declared the gradient or pattern.
    ///
    /// This override mirrors the old submodule fix from Svg.Custom instead: it captures the
    /// source document during parsing, prefers that document during deferred resolution, and
    /// preserves it across deep copies so the original paint server lookup stays stable.
    /// </summary>
    [TypeConverter(typeof(SvgDeferredPaintServerFactory))]
    public partial class SvgDeferredPaintServer : SvgPaintServer
    {
        private bool _serverLoaded;

        private SvgPaintServer _concreteServer;
        private SvgPaintServer _fallbackServer;

        [Obsolete("Will be removed.")]
        public SvgDocument Document { get; set; }

        public string DeferredId { get; set; }

        public SvgPaintServer FallbackServer { get; private set; }

        public SvgDeferredPaintServer()
        {
        }

        [Obsolete("Will be removed.")]
        public SvgDeferredPaintServer(SvgDocument document, string id)
        {
            Document = document;
            DeferredId = id;
        }

        // The original SVG file only preserved the source document for the
        // (SvgDocument, string) overload. We also need it for url(...) values that include a
        // fallback paint server, otherwise deferred resolution loses the original document as
        // soon as the node is cloned or temporarily reparented under a <use> host.
        public SvgDeferredPaintServer(SvgDocument document, string id, SvgPaintServer fallbackServer)
            : this(id, fallbackServer)
        {
            Document = document;
        }

        /// <summary>
        /// Initializes new instance of <see cref="SvgDeferredPaintServer"/> class.
        /// </summary>
        /// <param name="id">&lt;FuncIRI&gt;, &lt;IRI&gt; or &quot;currentColor&quot;.</param>
        public SvgDeferredPaintServer(string id)
            : this(id, null)
        {
        }

        /// <summary>
        /// Initializes new instance of <see cref="SvgDeferredPaintServer"/> class.
        /// </summary>
        /// <param name="id">&lt;FuncIRI&gt;, &lt;IRI&gt; or &quot;currentColor&quot;.</param>
        /// <param name="fallbackServer">&quot;none&quot;, &quot;currentColor&quot; or <see cref="SvgColourServer"/> server.</param>
        public SvgDeferredPaintServer(string id, SvgPaintServer fallbackServer)
        {
            DeferredId = id;
            FallbackServer = fallbackServer;
        }

        public void EnsureServer(SvgElement styleOwner)
        {
            if (DeferredId == "currentColor" && styleOwner != null)
            {
                for (var current = styleOwner; current is not null; current = current.Parent)
                {
                    var color = GetOwnColor(current);
                    if (IsConcreteCurrentColor(color))
                    {
                        _concreteServer = color;
                        return;
                    }
                }

                _concreteServer = new SvgColourServer(System.Drawing.Color.Black);
                return;
            }

            if (!_serverLoaded && styleOwner != null)
            {
                // Prefer the document captured when the paint server was parsed. The
                // original upstream code always used styleOwner.OwnerDocument, but our
                // retained scene compiler intentionally changes the temporary parent to let
                // inherited styles come from the <use> host. Paint server lookup must stay
                // anchored to the document that defined the referenced gradient/pattern.
                var document = Document ?? styleOwner.OwnerDocument;
                _concreteServer = document?.IdManager.GetElementById(DeferredId) as SvgPaintServer;

                _fallbackServer = FallbackServer ?? None;
                _serverLoaded = true;
            }
        }

        public override SvgElement DeepCopy()
        {
            return DeepCopy<SvgDeferredPaintServer>();
        }

        public override SvgElement DeepCopy<T>()
        {
            var newObj = base.DeepCopy<T>() as SvgDeferredPaintServer;

            // The original source copied the deferred identifier and fallback server only.
            // We also carry the captured document forward so cloned retained nodes keep
            // resolving deferred paint servers against the same source document.
            newObj.Document = Document;
            newObj.DeferredId = DeferredId;
            newObj.FallbackServer = FallbackServer?.DeepCopy() as SvgPaintServer;
            return newObj;
        }

        internal void RebindDocument(SvgDocument document)
        {
            Document = document;
            _concreteServer = null;
            _fallbackServer = null;
            _serverLoaded = false;

            if (FallbackServer is SvgDeferredPaintServer deferredFallback)
            {
                deferredFallback.RebindDocument(document);
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as SvgDeferredPaintServer;
            if (other == null)
                return false;

            return DeferredId == other.DeferredId;
        }

        public override int GetHashCode()
        {
            return DeferredId == null ? 0 : DeferredId.GetHashCode();
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(DeferredId))
                return string.Empty;
            if (FallbackServer == null)
                return DeferredId;
            return new StringBuilder(DeferredId).Append(" ").Append(FallbackServer.ToString()).ToString();
        }

        public static T TryGet<T>(SvgPaintServer server, SvgElement parent) where T : SvgPaintServer
        {
            if (!(server is SvgDeferredPaintServer))
                return server as T;

            var deferred = (SvgDeferredPaintServer)server;
            deferred.EnsureServer(parent);
            return (deferred._concreteServer ?? deferred._fallbackServer) as T;
        }

        private static bool IsConcreteCurrentColor(SvgPaintServer server)
        {
            if (server == null ||
                server == None ||
                server == NotSet ||
                server == Inherit)
            {
                return false;
            }

            return server is SvgColourServer;
        }

        private static SvgPaintServer GetOwnColor(SvgElement element)
        {
            if (!element.Attributes.ContainsKey("color"))
            {
                if (element.TryGetOwnCascadedStyleDeclarationValue("color", out var styleColor) &&
                    !string.IsNullOrWhiteSpace(styleColor))
                {
                    return SvgPaintServerFactory.Create(styleColor, element.OwnerDocument);
                }

                return NotSet;
            }

            var value = element.Attributes.GetAttribute<object>("color");
            return value as SvgPaintServer ?? NotSet;
        }
    }
}
