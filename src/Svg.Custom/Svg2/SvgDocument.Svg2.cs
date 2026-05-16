namespace Svg
{
    public partial class SvgDocument
    {
        private SvgDocumentLoadOptions _loadOptions = new SvgDocumentLoadOptions();

        /// <summary>
        /// Gets or sets parser/model options for SVG 2 static-subset processing.
        /// </summary>
        public SvgDocumentLoadOptions LoadOptions
        {
            get { return _loadOptions; }
            set { _loadOptions = value?.Clone() ?? new SvgDocumentLoadOptions(); }
        }
    }
}
