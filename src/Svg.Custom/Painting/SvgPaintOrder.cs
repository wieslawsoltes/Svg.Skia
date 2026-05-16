using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace Svg
{
    [TypeConverter(typeof(SvgPaintOrderConverter))]
    public enum SvgPaintOrder
    {
        Normal,
        FillStrokeMarkers,
        FillMarkersStroke,
        StrokeFillMarkers,
        StrokeMarkersFill,
        MarkersFillStroke,
        MarkersStrokeFill
    }

    public sealed class SvgPaintOrderConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string stringValue)
            {
                return Parse(stringValue);
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is SvgPaintOrder paintOrder)
            {
                return paintOrder switch
                {
                    SvgPaintOrder.Normal => "normal",
                    SvgPaintOrder.FillStrokeMarkers => "fill stroke markers",
                    SvgPaintOrder.FillMarkersStroke => "fill markers stroke",
                    SvgPaintOrder.StrokeFillMarkers => "stroke fill markers",
                    SvgPaintOrder.StrokeMarkersFill => "stroke markers fill",
                    SvgPaintOrder.MarkersFillStroke => "markers fill stroke",
                    SvgPaintOrder.MarkersStrokeFill => "markers stroke fill",
                    _ => "normal"
                };
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }

        private static SvgPaintOrder Parse(string value)
        {
            var normalized = value?.Trim();
            if (string.Equals(normalized, "normal", StringComparison.OrdinalIgnoreCase))
            {
                return SvgPaintOrder.Normal;
            }

            if (string.IsNullOrEmpty(normalized))
            {
                throw new FormatException("Invalid paint-order value.");
            }

            var tokens = normalized.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0 || tokens.Length > 3)
            {
                throw new FormatException("Invalid paint-order value.");
            }

            var order = new List<string>(3);
            foreach (var token in tokens)
            {
                var phase = token.Trim().ToLowerInvariant();
                if (phase != "fill" && phase != "stroke" && phase != "markers")
                {
                    throw new FormatException("Invalid paint-order value.");
                }

                if (order.Contains(phase))
                {
                    throw new FormatException("Invalid paint-order value.");
                }

                order.Add(phase);
            }

            AddMissing(order, "fill");
            AddMissing(order, "stroke");
            AddMissing(order, "markers");

            var key = string.Join(" ", order.ToArray());
            switch (key)
            {
                case "fill stroke markers":
                    return SvgPaintOrder.FillStrokeMarkers;
                case "fill markers stroke":
                    return SvgPaintOrder.FillMarkersStroke;
                case "stroke fill markers":
                    return SvgPaintOrder.StrokeFillMarkers;
                case "stroke markers fill":
                    return SvgPaintOrder.StrokeMarkersFill;
                case "markers fill stroke":
                    return SvgPaintOrder.MarkersFillStroke;
                case "markers stroke fill":
                    return SvgPaintOrder.MarkersStrokeFill;
                default:
                    throw new FormatException("Invalid paint-order value.");
            }
        }

        private static void AddMissing(List<string> order, string phase)
        {
            if (!order.Contains(phase))
            {
                order.Add(phase);
            }
        }
    }

    public abstract partial class SvgVisualElement
    {
        [SvgAttribute("paint-order")]
        public virtual SvgPaintOrder PaintOrder
        {
            get { return GetAttribute("paint-order", true, SvgPaintOrder.Normal); }
            set { Attributes["paint-order"] = value; }
        }
    }
}
