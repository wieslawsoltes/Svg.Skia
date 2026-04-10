using System;
using Svg.Model.Services;
using Xunit;

namespace Svg.Model.UnitTests;

public class AnimationElementTests
{
    [Fact]
    public void FromSvg_ParsesAnimationElementsAndPreservesAnimationAttributes()
    {
        var document = LoadDocument();

        var explicitTarget = Assert.IsType<SvgCircle>(document!.GetElementById("explicitTarget"));
        var parentRect = Assert.IsType<SvgRectangle>(document.GetElementById("parentRect"));
        var motionPath = Assert.IsType<SvgPath>(document.GetElementById("motionPath"));

        var animate = Assert.IsType<SvgAnimate>(document.GetElementById("animate1"));
        var set = Assert.IsType<SvgSet>(document.GetElementById("set1"));
        var animateColor = Assert.IsType<SvgAnimateColor>(document.GetElementById("animateColor1"));
        var animateTransform = Assert.IsType<SvgAnimateTransform>(document.GetElementById("animateTransform1"));
        var animateMotion = Assert.IsType<SvgAnimateMotion>(document.GetElementById("animateMotion1"));
        var mpath = Assert.IsType<SvgMPath>(document.GetElementById("mpath1"));

        Assert.Equal(new Uri("#explicitTarget", UriKind.RelativeOrAbsolute), animate.ReferencedElement);
        Assert.Same(explicitTarget, animate.TargetElement);
        Assert.Equal("cx", animate.AnimationAttributeName);
        Assert.Equal(SvgAnimationAttributeType.Xml, animate.AttributeType);
        Assert.Equal(SvgAnimationRestart.WhenNotActive, animate.Restart);
        Assert.Equal(SvgAnimationFill.Freeze, animate.AnimationFill);
        Assert.Equal(SvgAnimationCalcMode.Spline, animate.CalcMode);
        Assert.Equal(SvgAnimationAdditive.Sum, animate.Additive);
        Assert.Equal(SvgAnimationAccumulate.Sum, animate.Accumulate);
        Assert.True(animate.ExternalResourcesRequired);
        Assert.Equal("http://www.w3.org/TR/SVG11/feature#Animation", animate.RequiredFeatures);
        Assert.Equal("http://example.com/ext", animate.RequiredExtensions);
        Assert.Equal("en,pl", animate.SystemLanguage);
        Assert.Equal("0s", animate.Begin);
        Assert.Equal("2s", animate.Duration);
        Assert.Equal("5s", animate.End);
        Assert.Equal("1s", animate.Minimum);
        Assert.Equal("10s", animate.Maximum);
        Assert.Equal("indefinite", animate.RepeatCount);
        Assert.Equal("20s", animate.RepeatDuration);
        Assert.Equal("20", animate.From);
        Assert.Equal("60", animate.To);
        Assert.Equal("40", animate.By);
        Assert.Equal("20; 40; 60", animate.Values);
        Assert.Equal("0.42 0 0.58 1;0.42 0 0.58 1", animate.KeySplines);
        Assert.Equal("beginAnimation()", animate.OnBeginScript);
        Assert.Equal("endAnimation()", animate.OnEndScript);
        Assert.Equal("repeatAnimation()", animate.OnRepeatScript);
        Assert.Equal("loadAnimation()", animate.OnLoadScript);
        Assert.NotNull(animate.KeyTimes);
        Assert.Equal(3, animate.KeyTimes.Count);
        Assert.Equal(0f, animate.KeyTimes[0]);
        Assert.Equal(0.5f, animate.KeyTimes[1]);
        Assert.Equal(1f, animate.KeyTimes[2]);

        Assert.Equal("visibility", set.AnimationAttributeName);
        Assert.Equal("hidden", set.To);
        Assert.Same(parentRect, set.TargetElement);

        Assert.Equal("fill", animateColor.AnimationAttributeName);
        Assert.Equal("red", animateColor.From);
        Assert.Equal("blue", animateColor.To);

        Assert.Equal("transform", animateTransform.AnimationAttributeName);
        Assert.Equal(SvgAnimateTransformType.Rotate, animateTransform.TransformType);
        Assert.Equal("0 35 30", animateTransform.From);
        Assert.Equal("90 35 30", animateTransform.To);
        Assert.Same(parentRect, animateTransform.TargetElement);

        Assert.Same(parentRect, animateMotion.TargetElement);
        Assert.Equal("0s", animateMotion.Begin);
        Assert.Equal("4s", animateMotion.Duration);
        Assert.Equal(SvgAnimationCalcMode.Paced, animateMotion.CalcMode);
        Assert.Equal(SvgAnimationAdditive.Sum, animateMotion.Additive);
        Assert.Equal(SvgAnimationAccumulate.Sum, animateMotion.Accumulate);
        Assert.Equal("0; 0.5; 1", animateMotion.Values);
        Assert.Equal("0.25 0.1 0.25 1;0.25 0.1 0.25 1", animateMotion.KeySplines);
        Assert.Equal("0,0", animateMotion.From);
        Assert.Equal("10,10", animateMotion.To);
        Assert.Equal("5,5", animateMotion.By);
        Assert.Equal("auto", animateMotion.Rotate);
        Assert.Equal("default", animateMotion.Origin);
        Assert.NotNull(animateMotion.PathData);
        Assert.True(animateMotion.PathData.Count > 0);
        Assert.NotNull(animateMotion.KeyTimes);
        Assert.Equal(3, animateMotion.KeyTimes.Count);
        Assert.NotNull(animateMotion.KeyPoints);
        Assert.Equal(3, animateMotion.KeyPoints.Count);
        Assert.Equal(0f, animateMotion.KeyPoints[0]);
        Assert.Equal(0.5f, animateMotion.KeyPoints[1]);
        Assert.Equal(1f, animateMotion.KeyPoints[2]);

        Assert.Equal(new Uri("#motionPath", UriKind.RelativeOrAbsolute), mpath.ReferencedPath);
        Assert.True(mpath.ExternalResourcesRequired);
        Assert.Same(motionPath, mpath.TargetPath);

        var animateXml = animate.GetXML();
        Assert.Contains("begin=\"0s\"", animateXml, StringComparison.Ordinal);
        Assert.Contains("dur=\"2s\"", animateXml, StringComparison.Ordinal);
        Assert.Contains("end=\"5s\"", animateXml, StringComparison.Ordinal);
        Assert.Contains("repeatCount=\"indefinite\"", animateXml, StringComparison.Ordinal);
        Assert.Contains("repeatDur=\"20s\"", animateXml, StringComparison.Ordinal);
        Assert.Contains("values=\"20; 40; 60\"", animateXml, StringComparison.Ordinal);
        Assert.Contains("from=\"20\"", animateXml, StringComparison.Ordinal);
        Assert.Contains("to=\"60\"", animateXml, StringComparison.Ordinal);
        Assert.Contains("by=\"40\"", animateXml, StringComparison.Ordinal);
        Assert.Contains("keySplines=\"0.42 0 0.58 1;0.42 0 0.58 1\"", animateXml, StringComparison.Ordinal);
        Assert.Contains("keyTimes=\"0;0.5;1\"", animateXml, StringComparison.Ordinal);

        var animateMotionXml = animateMotion.GetXML();
        Assert.Contains("rotate=\"auto\"", animateMotionXml, StringComparison.Ordinal);
        Assert.Contains("origin=\"default\"", animateMotionXml, StringComparison.Ordinal);
        Assert.Contains("keyPoints=\"0;0.5;1\"", animateMotionXml, StringComparison.Ordinal);
    }

    [Fact]
    public void AnimationElements_ExposeSpecDefaults()
    {
        var animate = new SvgAnimate();
        var set = new SvgSet();
        var animateMotion = new SvgAnimateMotion();
        var animateTransform = new SvgAnimateTransform();

        Assert.Equal(SvgAnimationAttributeType.Auto, animate.AttributeType);
        Assert.Equal(SvgAnimationRestart.Always, animate.Restart);
        Assert.Equal(SvgAnimationFill.Remove, animate.AnimationFill);
        Assert.Equal(SvgAnimationCalcMode.Linear, animate.CalcMode);
        Assert.Equal(SvgAnimationAdditive.Replace, animate.Additive);
        Assert.Equal(SvgAnimationAccumulate.None, animate.Accumulate);

        Assert.Equal(SvgAnimationAttributeType.Auto, set.AttributeType);
        Assert.Equal(SvgAnimationRestart.Always, set.Restart);
        Assert.Equal(SvgAnimationFill.Remove, set.AnimationFill);

        Assert.Equal(SvgAnimationCalcMode.Paced, animateMotion.CalcMode);
        Assert.Equal(SvgAnimationAdditive.Replace, animateMotion.Additive);
        Assert.Equal(SvgAnimationAccumulate.None, animateMotion.Accumulate);
        Assert.Equal("0", animateMotion.Rotate);

        Assert.Equal(SvgAnimationAttributeType.Auto, animateTransform.AttributeType);
        Assert.Equal(SvgAnimationCalcMode.Linear, animateTransform.CalcMode);
        Assert.Equal(SvgAnimateTransformType.Translate, animateTransform.TransformType);
    }

    [Fact]
    public void SvgElementAnimationValueApi_ReadsWritesAndClearsTypedAttributes()
    {
        var rectangle = new SvgRectangle();

        Assert.True(rectangle.TrySetAnimationValue("width", "25"));
        Assert.True(rectangle.ContainsAttribute("width"));

        var width = Assert.IsType<SvgUnit>(rectangle.GetAnimationValue("width"));
        Assert.Equal(25f, width.Value);

        Assert.True(rectangle.ClearAnimationValue("width"));
        Assert.False(rectangle.ContainsAttribute("width"));
    }

    [Fact]
    public void AnimationElements_DeepCopyPreservesConcreteTypesAndChildren()
    {
        var document = LoadDocument();

        var animate = Assert.IsType<SvgAnimate>(document!.GetElementById("animate1"));
        var set = Assert.IsType<SvgSet>(document.GetElementById("set1"));
        var animateColor = Assert.IsType<SvgAnimateColor>(document.GetElementById("animateColor1"));
        var animateTransform = Assert.IsType<SvgAnimateTransform>(document.GetElementById("animateTransform1"));
        var animateMotion = Assert.IsType<SvgAnimateMotion>(document.GetElementById("animateMotion1"));
        var mpath = Assert.IsType<SvgMPath>(document.GetElementById("mpath1"));

        var animateClone = Assert.IsType<SvgAnimate>(animate.DeepCopy());
        var setClone = Assert.IsType<SvgSet>(set.DeepCopy());
        var animateColorClone = Assert.IsType<SvgAnimateColor>(animateColor.DeepCopy());
        var animateTransformClone = Assert.IsType<SvgAnimateTransform>(animateTransform.DeepCopy());
        var animateMotionClone = Assert.IsType<SvgAnimateMotion>(animateMotion.DeepCopy());
        var mpathClone = Assert.IsType<SvgMPath>(mpath.DeepCopy());

        Assert.NotSame(animate, animateClone);
        Assert.NotSame(set, setClone);
        Assert.NotSame(animateColor, animateColorClone);
        Assert.NotSame(animateTransform, animateTransformClone);
        Assert.NotSame(animateMotion, animateMotionClone);
        Assert.NotSame(mpath, mpathClone);

        Assert.Equal(animate.GetXML(), animateClone.GetXML());
        Assert.Equal(set.GetXML(), setClone.GetXML());
        Assert.Equal(animateColor.GetXML(), animateColorClone.GetXML());
        Assert.Equal(animateTransform.GetXML(), animateTransformClone.GetXML());
        Assert.Equal(animateMotion.GetXML(), animateMotionClone.GetXML());
        Assert.Equal(mpath.GetXML(), mpathClone.GetXML());

        var clonedMPath = Assert.IsType<SvgMPath>(Assert.Single(animateMotionClone.Children));
        Assert.NotSame(mpath, clonedMPath);
        Assert.Equal(mpath.ReferencedPath, clonedMPath.ReferencedPath);
    }

    private static SvgDocument? LoadDocument()
    {
        return SvgService.FromSvg(AnimationSvg);
    }

    private const string AnimationSvg = """
        <svg xmlns="http://www.w3.org/2000/svg"
             xmlns:xlink="http://www.w3.org/1999/xlink"
             width="120"
             height="120"
             viewBox="0 0 120 120">
          <defs>
            <path id="motionPath" d="M0,0 L100,0" />
          </defs>
          <circle id="explicitTarget" cx="20" cy="20" r="5" />
          <rect id="parentRect" x="10" y="10" width="50" height="40" fill="red">
            <animate id="animate1"
                     xlink:href="#explicitTarget"
                     requiredFeatures="http://www.w3.org/TR/SVG11/feature#Animation"
                     requiredExtensions="http://example.com/ext"
                     systemLanguage="en,pl"
                     externalResourcesRequired="true"
                     begin="0s"
                     dur="2s"
                     end="5s"
                     min="1s"
                     max="10s"
                     restart="whenNotActive"
                     repeatCount="indefinite"
                     repeatDur="20s"
                     fill="freeze"
                     attributeName="cx"
                     attributeType="xml"
                     calcMode="spline"
                     values="20; 40; 60"
                     keyTimes="0;0.5;1"
                     keySplines="0.42 0 0.58 1;0.42 0 0.58 1"
                     from="20"
                     to="60"
                     by="40"
                     additive="sum"
                     accumulate="sum"
                     onbegin="beginAnimation()"
                     onend="endAnimation()"
                     onrepeat="repeatAnimation()"
                     onload="loadAnimation()" />
            <set id="set1"
                 begin="1s"
                 dur="3s"
                 attributeName="visibility"
                 to="hidden" />
            <animateColor id="animateColor1"
                          attributeName="fill"
                          from="red"
                          to="blue" />
            <animateTransform id="animateTransform1"
                              attributeName="transform"
                              type="rotate"
                              from="0 35 30"
                              to="90 35 30" />
            <animateMotion id="animateMotion1"
                           begin="0s"
                           dur="4s"
                           values="0; 0.5; 1"
                           keyTimes="0;0.5;1"
                           keySplines="0.25 0.1 0.25 1;0.25 0.1 0.25 1"
                           from="0,0"
                           to="10,10"
                           by="5,5"
                           additive="sum"
                           accumulate="sum"
                           path="M0,0 L10,10"
                           keyPoints="0;0.5;1"
                           rotate="auto"
                           origin="default">
              <mpath id="mpath1"
                     xlink:href="#motionPath"
                     externalResourcesRequired="true" />
            </animateMotion>
          </rect>
        </svg>
        """;
}
