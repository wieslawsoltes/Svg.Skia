using System;
using System.Collections.Generic;
using System.Globalization;
using System.Timers;
using Svg;
using Svg.Transforms;

namespace Svg.Skia;

/// <summary>
/// Simple animator for <animateTransform> elements.
/// </summary>
public sealed class SKSvgAnimator : IDisposable
{
    private readonly SKSvg _svg;
    private readonly SvgDocument _document;
    private readonly List<TransformAnimation> _animations = new();
    private readonly Timer _timer;
    private DateTime _start;

    /// <summary>
    /// Occurs when animation frame is updated.
    /// </summary>
    public event EventHandler? Updated;

    /// <summary>
    /// Initializes a new instance of the <see cref="SKSvgAnimator"/> class.
    /// </summary>
    /// <param name="svg">The target <see cref="SKSvg"/>.</param>
    /// <param name="document">SVG document used for animations.</param>
    /// <param name="fps">Timer frames per second.</param>
    public SKSvgAnimator(SKSvg svg, SvgDocument document, double fps = 60)
    {
        _svg = svg;
        _document = document;
        _timer = new Timer(1000.0 / fps);
        _timer.Elapsed += OnElapsed;
        ParseAnimations(document);
    }

    /// <summary>
    /// Starts the animation timer.
    /// </summary>
    public void Start()
    {
        _start = DateTime.Now;
        _timer.Start();
    }

    /// <summary>
    /// Stops the animation timer.
    /// </summary>
    public void Stop() => _timer.Stop();

    private void OnElapsed(object? sender, ElapsedEventArgs e)
    {
        UpdateAnimations();
        Updated?.Invoke(this, EventArgs.Empty);
    }

    private static TimeSpan ParseDuration(string value)
    {
        value = value.Trim();
        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(value.Substring(0, value.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
        {
            return TimeSpan.FromMilliseconds(ms);
        }
        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(value.Substring(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
        {
            return TimeSpan.FromSeconds(s);
        }
        return TimeSpan.Zero;
    }

    private static float ParseFloat(string? s)
    {
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            return v;
        }
        return 0f;
    }

    private static bool TryGetElementName(SvgElement element, out string name)
    {
        var prop = element.GetType().GetProperty(
            "ElementName",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (prop?.GetValue(element) is string n)
        {
            name = n;
            return true;
        }

        name = string.Empty;
        return false;
    }

    private void ParseAnimations(SvgElement root)
    {
        foreach (var element in root.Descendants())
        {
            if (element is SvgUnknownElement unknown &&
                TryGetElementName(unknown, out var name) &&
                string.Equals(name, "animateTransform", StringComparison.OrdinalIgnoreCase) &&
                element.Parent is SvgVisualElement target)
            {
                if (!unknown.CustomAttributes.TryGetValue("type", out var type) ||
                    !unknown.CustomAttributes.TryGetValue("from", out var from) ||
                    !unknown.CustomAttributes.TryGetValue("to", out var to) ||
                    !unknown.CustomAttributes.TryGetValue("dur", out var dur))
                {
                    continue;
                }

                var fromParts = from.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                var toParts = to.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                var anim = new TransformAnimation
                {
                    Target = target,
                    Type = type,
                    From1 = fromParts.Length > 0 ? ParseFloat(fromParts[0]) : 0f,
                    From2 = fromParts.Length > 1 ? ParseFloat(fromParts[1]) : 0f,
                    To1 = toParts.Length > 0 ? ParseFloat(toParts[0]) : 0f,
                    To2 = toParts.Length > 1 ? ParseFloat(toParts[1]) : 0f,
                    Duration = ParseDuration(dur)
                };

                if (anim.Duration > TimeSpan.Zero)
                {
                    _animations.Add(anim);
                }
            }
        }
    }

    private void UpdateAnimations()
    {
        var elapsed = DateTime.Now - _start;
        foreach (var anim in _animations)
        {
            var ms = anim.Duration.TotalMilliseconds;
            if (ms <= 0)
            {
                continue;
            }

            var progress = (float)((elapsed.TotalMilliseconds % ms) / ms);
            var v1 = anim.From1 + (anim.To1 - anim.From1) * progress;
            var v2 = anim.From2 + (anim.To2 - anim.From2) * progress;
            var transforms = new SvgTransformCollection();
            switch (anim.Type.ToLowerInvariant())
            {
                case "translate":
                    transforms.Add(new SvgTranslate(v1, v2));
                    break;
                case "scale":
                    transforms.Add(new SvgScale(v1, v2));
                    break;
                case "rotate":
                    transforms.Add(new SvgRotate(v1));
                    break;
                default:
                    continue;
            }
            anim.Target.Transforms = transforms;
        }

        _svg.FromSvgDocument(_document);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer.Dispose();
    }

    private sealed class TransformAnimation
    {
        public SvgVisualElement Target { get; set; } = default!;
        public string Type { get; set; } = string.Empty;
        public float From1 { get; set; }
        public float From2 { get; set; }
        public float To1 { get; set; }
        public float To2 { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
