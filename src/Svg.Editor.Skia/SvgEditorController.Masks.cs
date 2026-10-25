using System;
using System.Linq;
using System.Xml.Linq;

namespace Svg.Editor.Skia;

public sealed partial class SvgEditorController
{
    /// <summary>
    /// Uses the second selected sibling as an alpha or luminance mask for the first.
    /// A neutral wrapper keeps both objects in their original parent coordinate system;
    /// neither their geometry nor the mask artwork is flattened or discarded.
    /// </summary>
    public void CreateOpacityMask(bool luminance = true)
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        if (_selection.Count != 2)
            throw new InvalidOperationException("Select the artwork first and the mask artwork second.");
        var artwork = _selection[0];
        var maskArtwork = _selection[1];
        if (artwork.Parent is null || !ReferenceEquals(artwork.Parent, maskArtwork.Parent))
            throw new InvalidOperationException("Place the artwork and mask in the same group or layer first.");
        var tree = ReadEffectDocument(GetSvg());
        var keys = CaptureSelection();
        var target = ResolveEffectElement(tree, keys[0])!;
        var maskSource = ResolveEffectElement(tree, keys[1])!;
        var parent = target.Parent!;
        var sourceWasBehind = parent.Elements().TakeWhile(element => !ReferenceEquals(element, target)).Contains(maskSource);
        var maskId = UniqueEffectId(tree, "mask");
        var groupId = UniqueEffectId(tree, "masked");
        var resource = new XElement(SvgFilterCatalog.Namespace + "mask",
            new XAttribute("id", maskId), new XAttribute("maskUnits", "objectBoundingBox"),
            new XAttribute("maskContentUnits", "userSpaceOnUse"), new XAttribute("mask-type", luminance ? "luminance" : "alpha"),
            new XAttribute("x", "-10%"), new XAttribute("y", "-10%"), new XAttribute("width", "120%"), new XAttribute("height", "120%"));
        var group = new XElement(SvgFilterCatalog.Namespace + "g", new XAttribute("id", groupId),
            new XAttribute("data-unodraw-mask-resource", maskId), new XAttribute("data-unodraw-mask-behind", sourceWasBehind ? "true" : "false"));
        SetEffectReference(group, "mask", "url(#" + maskId + ")");
        target.ReplaceWith(group);
        group.Add(target);
        maskSource.Remove();
        resource.Add(maskSource);
        var definitions = parent.Elements(SvgFilterCatalog.Namespace + "defs").FirstOrDefault();
        if (definitions is null) { definitions = new XElement(SvgFilterCatalog.Namespace + "defs"); parent.Add(definitions); }
        definitions.Add(resource);
        var time = Renderer.AnimationTime;
        ReplaceSvg(tree.ToString(SaveOptions.DisableFormatting), luminance ? "Create luminance mask" : "Create alpha mask");
        Renderer.SetAnimationTime(time);
        Select(Document!.GetElementById<SvgGroup>(groupId));
    }

    /// <summary>
    /// Removes the selected objects' mask references. Generated mask wrappers can restore
    /// an editable copy of their mask artwork; resource definitions stay available for
    /// other consumers. The wrapper is retained so later transforms/compositing survive.
    /// Imported masks are detached without interpreting their resource coordinate systems.
    /// </summary>
    public void ReleaseOpacityMasks(bool restoreMaskArtwork = true)
    {
        ThrowIfDisposed();
        EnsureSelectionEditable();
        if (_selection.Count == 0) return;
        var tree = ReadEffectDocument(GetSvg());
        var targets = CaptureSelection().Select(key => ResolveEffectElement(tree, key)!).ToArray();
        foreach (var target in targets)
        {
            var resourceId = (string?)target.Attribute("data-unodraw-mask-resource");
            if (restoreMaskArtwork && resourceId is not null && target.Name == SvgFilterCatalog.Namespace + "g")
            {
                var mask = tree.Descendants(SvgFilterCatalog.Namespace + "mask")
                    .FirstOrDefault(element => (string?)element.Attribute("id") == resourceId);
                if (mask is not null)
                {
                    if ((string?)mask.Attribute("maskContentUnits") != "userSpaceOnUse")
                        throw new InvalidOperationException("This mask's coordinate system changed. Disable its mask without restoring artwork, or restore it to user-space units first.");
                    var fragment = new XElement(SvgFilterCatalog.Namespace + "svg", mask.Nodes());
                    // Reserve against this evolving snapshot, not the original live DOM:
                    // batch releases of one shared mask must not generate duplicate IDs.
                    var remapped = SvgXmlIdRemapper.Clone(fragment,
                        tree.Root!.DescendantsAndSelf().Attributes("id").Select(id => id.Value));
                    var restored = remapped.Nodes().ToArray();
                    if ((string?)target.Attribute("data-unodraw-mask-behind") == "true") target.AddFirst(restored);
                    else target.Add(restored);
                }
            }
            SetEffectReference(target, "mask", "none");
            target.Attribute("data-unodraw-mask-resource")?.Remove();
            target.Attribute("data-unodraw-mask-behind")?.Remove();
        }
        var time = Renderer.AnimationTime;
        ReplaceSvg(tree.ToString(SaveOptions.DisableFormatting), restoreMaskArtwork ? "Release opacity masks" : "Disable opacity masks");
        Renderer.SetAnimationTime(time);
    }
}
