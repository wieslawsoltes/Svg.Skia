using System;
using System.Linq;
using Microsoft.UI.Xaml.Markup;
using Xunit;

namespace Uno.Svg.Skia.UnitTests;

public class SvgMLContentTests
{
    [Fact]
    public void TextElementContentProperty_UsesMixedContentNodes()
    {
        var attribute = typeof(SvgML.text)
            .GetCustomAttributes(typeof(ContentPropertyAttribute), inherit: true)
            .OfType<ContentPropertyAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(nameof(SvgML.element.ContentNodes), attribute!.Name);
    }

    [Fact]
    public void TspanContentProperty_UsesText()
    {
        var attribute = typeof(SvgML.tspan)
            .GetCustomAttributes(typeof(ContentPropertyAttribute), inherit: true)
            .OfType<ContentPropertyAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(nameof(SvgML.text_base.Text), attribute!.Name);
    }

    [Fact]
    public void TextPathContentProperty_UsesText()
    {
        var attribute = typeof(SvgML.textPath)
            .GetCustomAttributes(typeof(ContentPropertyAttribute), inherit: true)
            .OfType<ContentPropertyAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(nameof(SvgML.text_base.Text), attribute!.Name);
    }

    [Fact]
    public void EnumBridge_FormatsSvgEnumsWithoutTypeDescriptor()
    {
        Assert.Equal("visiblePainted", new SvgML.SvgPointerEventsValue(global::Svg.SvgPointerEvents.VisiblePainted).ToString());
        Assert.Equal("whenNotActive", new SvgML.SvgAnimationRestartValue(global::Svg.SvgAnimationRestart.WhenNotActive).ToString());
        Assert.Equal(global::Svg.SvgPointerEvents.VisiblePainted, SvgML.SvgPointerEventsValue.Parse("visiblePainted").Value);
    }

    [Fact]
    public void EnumBridge_FormatsSvg2EnumsWithoutTypeDescriptor()
    {
        Assert.Equal("alpha", new SvgML.ModelMaskTypeValue(global::Svg.Model.MaskType.Alpha).ToString());
        Assert.Equal("right", new SvgML.SvgTextPathSideValue(global::Svg.SvgTextPathSide.Right).ToString());
        Assert.Equal("fill-box", new SvgML.SvgTransformBoxValue(global::Svg.SvgTransformBox.FillBox).ToString());
        Assert.Equal("non-scaling-stroke", new SvgML.SvgVectorEffectValue(global::Svg.SvgVectorEffect.NonScalingStroke).ToString());
        Assert.Equal("nowrap", new SvgML.SvgWhiteSpaceValue(global::Svg.SvgWhiteSpace.NoWrap).ToString());

        Assert.Equal(global::Svg.Model.MaskType.Luminance, SvgML.ModelMaskTypeValue.Parse("luminance").Value);
        Assert.Equal(global::Svg.SvgTransformBox.StrokeBox, SvgML.SvgTransformBoxValue.Parse("stroke-box").Value);
        Assert.Equal(global::Svg.SvgWhiteSpace.PreWrap, SvgML.SvgWhiteSpaceValue.Parse("pre-wrap").Value);
    }

    [Fact]
    public void EnumBridge_FormatsManualEnumsWithoutTypeDescriptor()
    {
        Assert.Equal("color-dodge", new SvgML.BlendModeValue(SvgML.blend_mode.color_dodge).ToString());
        Assert.Equal(SvgML.blend_mode.color_dodge, SvgML.BlendModeValue.Parse("color-dodge").Value);
        Assert.Equal("hueRotate", new SvgML.TypeFeColorMatrixValue(SvgML.type_feColorMatrix.hueRotate).ToString());
    }

    [Fact]
    public void Svg2GeneratedProperties_AreAvailableOnUnoSvgMLSurface()
    {
        AssertProperty<global::Svg.SvgProcessingMode>(typeof(SvgML.svg), nameof(SvgML.svg.ProcessingMode));
        AssertProperty<global::Svg.SvgExternalResourcePolicy>(typeof(SvgML.svg), nameof(SvgML.svg.ExternalResources));
        AssertProperty<bool>(typeof(SvgML.svg), nameof(SvgML.svg.PreserveUnknownElements));
        AssertProperty<bool>(typeof(SvgML.svg), nameof(SvgML.svg.PreferSvg2Href));
        AssertProperty<float>(typeof(SvgML.rect), nameof(SvgML.rect.pathLength));
        AssertProperty<string>(typeof(SvgML.rect), nameof(SvgML.rect.paint_order));
        AssertProperty<SvgML.SvgVectorEffectValue>(typeof(SvgML.rect), nameof(SvgML.rect.vector_effect));
        AssertProperty<SvgML.SvgTransformBoxValue>(typeof(SvgML.rect), nameof(SvgML.rect.transform_box));
        AssertProperty<string>(typeof(SvgML.rect), nameof(SvgML.rect.transform_origin));
        AssertProperty<SvgML.SvgWhiteSpaceValue>(typeof(SvgML.rect), nameof(SvgML.rect.white_space));
        AssertProperty<global::Svg.SvgUnit>(typeof(SvgML.symbol), nameof(SvgML.symbol.refX));
        AssertProperty<global::Svg.SvgUnit>(typeof(SvgML.symbol), nameof(SvgML.symbol.refY));
        AssertProperty<SvgML.ModelMaskTypeValue>(typeof(SvgML.mask), nameof(SvgML.mask.mask_type));
        AssertProperty<string>(typeof(SvgML.textPath), nameof(SvgML.textPath.path));
        AssertProperty<SvgML.SvgTextPathSideValue>(typeof(SvgML.textPath), nameof(SvgML.textPath.side));
        AssertProperty<global::Svg.SvgUnit>(typeof(SvgML.feDropShadow), nameof(SvgML.feDropShadow.dx));
        AssertProperty<global::Svg.SvgUnit>(typeof(SvgML.feDropShadow), nameof(SvgML.feDropShadow.dy));
        AssertProperty<SvgML.numbers>(typeof(SvgML.feDropShadow), nameof(SvgML.feDropShadow.stdDeviation));
        AssertProperty<string>(typeof(SvgML.feDropShadow), nameof(SvgML.feDropShadow.flood_color));
        AssertProperty<float>(typeof(SvgML.feDropShadow), nameof(SvgML.feDropShadow.flood_opacity));
    }

    private static void AssertProperty<TProperty>(Type type, string propertyName)
    {
        var property = type.GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(typeof(TProperty), property!.PropertyType);
    }
}
