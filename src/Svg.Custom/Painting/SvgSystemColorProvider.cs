// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace Svg
{
    public interface ISvgSystemColorProvider
    {
        bool TryGetColor(string name, out Color color);
    }

    public sealed class SvgDictionarySystemColorProvider : ISvgSystemColorProvider
    {
        private readonly IReadOnlyDictionary<string, Color> _colors;

        public SvgDictionarySystemColorProvider(IDictionary<string, Color> colors)
        {
            if (colors == null)
            {
                throw new ArgumentNullException(nameof(colors));
            }

            _colors = new Dictionary<string, Color>(colors, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetColor(string name, out Color color)
        {
            if (name == null)
            {
                color = Color.Empty;
                return false;
            }

            return _colors.TryGetValue(name.Trim(), out color);
        }
    }

    public sealed class SvgFixedSystemColorProvider : ISvgSystemColorProvider
    {
        public static SvgFixedSystemColorProvider Instance { get; } = new SvgFixedSystemColorProvider();

        private static readonly IReadOnlyDictionary<string, Color> s_colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            ["ActiveBorder"] = FromRgb(0xD4, 0xD0, 0xC8),
            ["ActiveCaption"] = FromRgb(0x0A, 0x24, 0x6A),
            ["AppWorkspace"] = FromRgb(0x80, 0x80, 0x80),
            ["Background"] = FromRgb(0x3A, 0x6E, 0xA5),
            ["ButtonFace"] = FromRgb(0xD4, 0xD0, 0xC8),
            ["ButtonHighlight"] = FromRgb(0xFF, 0xFF, 0xFF),
            ["ButtonShadow"] = FromRgb(0x80, 0x80, 0x80),
            ["ButtonText"] = FromRgb(0x00, 0x00, 0x00),
            ["CaptionText"] = FromRgb(0xFF, 0xFF, 0xFF),
            ["GrayText"] = FromRgb(0x80, 0x80, 0x80),
            ["Highlight"] = FromRgb(0x0A, 0x24, 0x6A),
            ["HighlightText"] = FromRgb(0xFF, 0xFF, 0xFF),
            ["InactiveBorder"] = FromRgb(0xD4, 0xD0, 0xC8),
            ["InactiveCaption"] = FromRgb(0x80, 0x80, 0x80),
            ["InactiveCaptionText"] = FromRgb(0xD4, 0xD0, 0xC8),
            ["InfoBackground"] = FromRgb(0xFF, 0xFF, 0xE1),
            ["InfoText"] = FromRgb(0x00, 0x00, 0x00),
            ["Menu"] = FromRgb(0xD4, 0xD0, 0xC8),
            ["MenuText"] = FromRgb(0x00, 0x00, 0x00),
            ["Scrollbar"] = FromRgb(0xD4, 0xD0, 0xC8),
            ["ThreeDDarkShadow"] = FromRgb(0x40, 0x40, 0x40),
            ["ThreeDFace"] = FromRgb(0xD4, 0xD0, 0xC8),
            ["ThreeDHighlight"] = FromRgb(0xE3, 0xE3, 0xE3),
            ["ThreeDLightShadow"] = FromRgb(0xFF, 0xFF, 0xFF),
            ["Window"] = FromRgb(0xFF, 0xFF, 0xFF),
            ["WindowFrame"] = FromRgb(0x00, 0x00, 0x00),
            ["WindowText"] = FromRgb(0x00, 0x00, 0x00)
        };

        private SvgFixedSystemColorProvider()
        {
        }

        public bool TryGetColor(string name, out Color color)
        {
            if (name == null)
            {
                color = Color.Empty;
                return false;
            }

            return s_colors.TryGetValue(name.Trim(), out color);
        }

        private static Color FromRgb(int red, int green, int blue)
            => Color.FromArgb(255, red, green, blue);
    }

    public static class SvgSystemColorResolver
    {
        private static readonly AsyncLocal<ISvgSystemColorProvider> s_scopedProvider = new AsyncLocal<ISvgSystemColorProvider>();
        private static ISvgSystemColorProvider s_defaultProvider = SvgFixedSystemColorProvider.Instance;

        public static ISvgSystemColorProvider DefaultProvider
        {
            get { return s_defaultProvider; }
            set { s_defaultProvider = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        public static bool TryGetColor(string name, out Color color)
        {
            var provider = s_scopedProvider.Value ?? s_defaultProvider;
            return provider.TryGetColor(name, out color);
        }

        public static IDisposable PushProvider(ISvgSystemColorProvider provider)
        {
            var previous = s_scopedProvider.Value;
            s_scopedProvider.Value = provider;
            return new Scope(previous);
        }

        private sealed class Scope : IDisposable
        {
            private readonly ISvgSystemColorProvider _previous;
            private bool _disposed;

            public Scope(ISvgSystemColorProvider previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                s_scopedProvider.Value = _previous;
                _disposed = true;
            }
        }
    }
}
