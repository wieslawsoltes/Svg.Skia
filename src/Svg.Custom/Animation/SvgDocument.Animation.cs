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

            foreach (var ns in Namespaces)
            {
                newObj.Namespaces[ns.Key] = ns.Value;
            }

            return newObj;
        }
    }
}
