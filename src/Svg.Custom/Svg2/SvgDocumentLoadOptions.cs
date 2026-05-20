namespace Svg
{
    /// <summary>
    /// Parser/model options for SVG 2 static-subset loading.
    /// </summary>
    public sealed class SvgDocumentLoadOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SvgDocumentLoadOptions"/> class.
        /// </summary>
        public SvgDocumentLoadOptions()
        {
            ProcessingMode = SvgProcessingMode.Static;
            ExternalResources = SvgExternalResourcePolicy.Enabled;
            PreserveUnknownElements = true;
            PreferSvg2Href = true;
        }

        /// <summary>
        /// Gets or sets the SVG processing mode.
        /// </summary>
        public SvgProcessingMode ProcessingMode { get; set; }

        /// <summary>
        /// Gets or sets the external resource policy.
        /// </summary>
        public SvgExternalResourcePolicy ExternalResources { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether unsupported elements should be preserved in the model.
        /// </summary>
        public bool PreserveUnknownElements { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether SVG 2 unnamespaced href should win over xlink:href.
        /// </summary>
        public bool PreferSvg2Href { get; set; }

        /// <summary>
        /// Creates a copy of this options object.
        /// </summary>
        /// <returns>A detached copy with the same option values.</returns>
        public SvgDocumentLoadOptions Clone()
        {
            return new SvgDocumentLoadOptions
            {
                ProcessingMode = ProcessingMode,
                ExternalResources = ExternalResources,
                PreserveUnknownElements = PreserveUnknownElements,
                PreferSvg2Href = PreferSvg2Href
            };
        }
    }
}
