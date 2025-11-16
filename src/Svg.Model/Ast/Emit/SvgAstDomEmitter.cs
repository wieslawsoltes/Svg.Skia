// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ExCSS;
using Svg.Ast;
using Svg.Ast.Emit;
using Svg;

namespace Svg.Model.Ast;

/// <summary>
/// Emits legacy <see cref="SvgDocument"/> instances from <see cref="SvgAstDocument"/>.
/// </summary>
public sealed class SvgAstDomEmitter : ISvgAstEmitter<SvgDocument?>
{
    public SvgDocument? Emit(SvgAstEmissionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.Document.RootElement is null)
        {
            return null;
        }

        var builder = new SvgAstDomBuilder();
        return builder.Build(context.Document);
    }

    private sealed class SvgAstDomBuilder
    {
        private static readonly Dictionary<string, Func<SvgElement>> s_elementFactories = CreateElementFactories();
        private static readonly HashSet<string> s_styleAttributes = new(StringComparer.OrdinalIgnoreCase)
        {
            "alignment-baseline","baseline-shift","clip","clip-path","clip-rule","color",
            "color-interpolation","color-interpolation-filters","color-profile","color-rendering",
            "cursor","direction","display","dominant-baseline","enable-background","fill","fill-opacity",
            "fill-rule","filter","flood-color","flood-opacity","font","font-family","font-size",
            "font-size-adjust","font-stretch","font-style","font-variant","font-weight",
            "glyph-orientation-horizontal","glyph-orientation-vertical","image-rendering","kerning",
            "letter-spacing","lighting-color","marker","marker-end","marker-mid","marker-start",
            "mask","opacity","overflow","pointer-events","shape-rendering","stop-color","stop-opacity",
            "stroke","stroke-dasharray","stroke-dashoffset","stroke-linecap","stroke-linejoin",
            "stroke-miterlimit","stroke-opacity","stroke-width","text-anchor","text-decoration",
            "text-rendering","text-transform","unicode-bidi","visibility","word-spacing","writing-mode"
        };

        private static readonly MethodInfo s_setPropertyValue = typeof(SvgElement).Assembly
            .GetType("Svg.SvgElementFactory", throwOnError: true)!
            .GetMethod("SetPropertyValue", BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly StylesheetParser s_stylesheetParser = new(true, true, tolerateInvalidValues: true);
        private const int PresentationSpecificity = 0;
        private const int InlineStyleSpecificity = 1 << 16;

        public SvgDocument Build(SvgAstDocument document)
        {
            if (document.RootElement is null)
            {
                throw new InvalidOperationException("Document has no root element.");
            }

            var svgDocument = new SvgDocument();
            ApplyAttributes(svgDocument, document.RootElement, svgDocument);
            AppendChildren(svgDocument, document.RootElement, svgDocument);
            svgDocument.FlushStyles(true);
            NormalizeColourServers(svgDocument);
            return svgDocument;
        }

        private void AppendChildren(SvgElement parent, SvgAstElement astElement, SvgDocument document)
        {
            foreach (var child in astElement.Children)
            {
                switch (child)
                {
                    case SvgAstElement childElement:
                        var domChild = CreateElement(childElement);
                        ApplyAttributes(domChild, childElement, document);
                        parent.Children.Add(domChild);
                        parent.Nodes.Add(domChild);
                        AppendChildren(domChild, childElement, document);
                        break;
                    case SvgAstText textNode:
                        var textValue = textNode.ToString();
                        if (!string.IsNullOrEmpty(textValue))
                        {
                            parent.Nodes.Add(new SvgContentNode { Content = textValue });
                        }
                        break;
                    case SvgAstComment comment:
                        parent.Nodes.Add(new SvgContentNode { Content = comment.ToString() });
                        break;
                    case SvgAstCData cdata:
                        parent.Nodes.Add(new SvgContentNode { Content = cdata.ToString() });
                        break;
                }
            }
        }

        private SvgElement CreateElement(SvgAstElement astElement)
        {
            if (string.Equals(astElement.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
            {
                return new SvgFragment();
            }

            if (s_elementFactories.TryGetValue(astElement.Name.LocalName, out var factory))
            {
                return factory();
            }

            return new SvgUnknownElement(astElement.Name.LocalName);
        }

        private void ApplyAttributes(SvgElement element, SvgAstElement astElement, SvgDocument document)
        {
            foreach (var attribute in astElement.Attributes)
            {
                var localName = attribute.Name.LocalName;
                if (string.IsNullOrEmpty(localName))
                {
                    continue;
                }

                if (IsNamespaceAttribute(attribute))
                {
                    var prefix = string.IsNullOrEmpty(attribute.Name.Prefix) ? string.Empty : localName;
                    element.Namespaces[prefix] = attribute.GetValueText();
                    continue;
                }

                var value = attribute.GetValueText();
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (string.Equals(localName, "style", StringComparison.OrdinalIgnoreCase))
                {
                    AddInlineStyle(element, value);
                    continue;
                }

                if (s_styleAttributes.Contains(localName))
                {
                    element.AddStyle(localName, value, PresentationSpecificity);
                    continue;
                }

                var ns = attribute.Name.NamespaceUri ?? string.Empty;
                InvokeSetPropertyValue(element, ns, localName, value, document);
            }
        }

        private static bool IsNamespaceAttribute(SvgAstAttribute attribute)
        {
            if (string.Equals(attribute.Name.LocalName, "xmlns", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(attribute.Name.Prefix) &&
                string.Equals(attribute.Name.Prefix, "xmlns", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static void InvokeSetPropertyValue(SvgElement element, string ns, string localName, string value, SvgDocument document)
        {
            s_setPropertyValue.Invoke(null, new object[]
            {
                element,
                ns,
                localName,
                value,
                document,
                false
            });
        }

        private static void AddInlineStyle(SvgElement element, string styleValue)
        {
            var inlineSheet = s_stylesheetParser.Parse("#a{" + styleValue + "}");
            foreach (var rule in inlineSheet.StyleRules)
            {
                foreach (var declaration in rule.Style)
                {
                    element.AddStyle(declaration.Name, declaration.Original, InlineStyleSpecificity);
                }
            }
        }

        private void NormalizeColourServers(SvgElement element)
        {
            if (element is ISvgStylable stylable)
            {
                NormalizePaintServer(stylable.Fill);
                NormalizePaintServer(stylable.Stroke);
            }

            NormalizePaintServer(element.Color);

            if (element is SvgGradientServer gradient)
            {
                foreach (var stop in gradient.Stops)
                {
                    NormalizePaintServer(stop.StopColor);
                }
            }

            foreach (var child in element.Children.OfType<SvgElement>())
            {
                NormalizeColourServers(child);
            }
        }

        private static void NormalizePaintServer(SvgPaintServer? paintServer)
        {
            if (paintServer is SvgColourServer colourServer)
            {
                var color = colourServer.Colour;
                if (!color.IsEmpty)
                {
                    colourServer.Colour = System.Drawing.Color.FromArgb(color.ToArgb());
                }
            }
            else if (paintServer is SvgDeferredPaintServer deferred && deferred.FallbackServer is { } fallback)
            {
                NormalizePaintServer(fallback);
            }
        }

        private static Dictionary<string, Func<SvgElement>> CreateElementFactories()
        {
            var factories = new Dictionary<string, Func<SvgElement>>(StringComparer.OrdinalIgnoreCase);
            var assemblyTypes = typeof(SvgElement).Assembly.GetTypes();
            foreach (var type in assemblyTypes)
            {
                if (type.IsAbstract || !typeof(SvgElement).IsAssignableFrom(type))
                {
                    continue;
                }

                var attribute = type.GetCustomAttribute<SvgElementAttribute>();
                if (attribute?.ElementName is null || factories.ContainsKey(attribute.ElementName))
                {
                    continue;
                }

                factories[attribute.ElementName] = () => (SvgElement)Activator.CreateInstance(type)!;
            }

            return factories;
        }
    }
}
