using System.Linq;
using Microsoft.Maui.Graphics;
using Svg;

namespace MauiSvgSkiaSample;

public partial class MainPage : ContentPage
{
    private const string ColdCss = ".accent { fill: #2563eb; } .outline { stroke: #0f172a; stroke-width: 4; }";
    private const string WarmCss = ".accent { fill: #ef4444; } .outline { stroke: #7c2d12; stroke-width: 4; }";

    private bool _useWarmTheme;

    public MainPage()
    {
        InitializeComponent();

        InlineSvgView.Source = InlineSvg;
        StyledSvg.Source = StyledSvgMarkup;
        StyledSvg.CurrentCss = ColdCss;
        FilterSvg.Source = FilterSvgMarkup;

        CssStatusLabel.Text = "Current theme: cobalt";
        HitTestStatusLabel.Text = "Tap the camera SVG to inspect hit-tested elements.";
    }

    private static Point GetViewCenter(VisualElement element)
    {
        var x = element.Width > 0 ? element.Width / 2d : 200d;
        var y = element.Height > 0 ? element.Height / 2d : 130d;
        return new Point(x, y);
    }

    private string InlineSvg =>
        """
        <svg width="260" height="120" viewBox="0 0 260 120" xmlns="http://www.w3.org/2000/svg">
          <rect x="8" y="8" width="244" height="104" rx="24" fill="#0f172a" />
          <circle cx="56" cy="60" r="24" fill="#38bdf8" />
          <path d="M96 82 L130 28 L164 82 Z" fill="#f59e0b" />
          <rect x="182" y="32" width="42" height="56" rx="14" fill="#34d399" />
        </svg>
        """;

    private string StyledSvgMarkup =>
        """
        <svg width="220" height="180" viewBox="0 0 220 180" xmlns="http://www.w3.org/2000/svg">
          <rect x="12" y="12" width="196" height="156" rx="26" fill="#f8fafc" />
          <circle class="accent" cx="72" cy="88" r="34" />
          <path class="outline" d="M120 128 L162 48 L186 128 Z" fill="none" />
          <rect class="accent" x="122" y="62" width="44" height="18" rx="9" />
        </svg>
        """;

    private string FilterSvgMarkup =>
        """
        <svg width="260" height="160" viewBox="0 0 260 160" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <filter id="blurred-glow" x="-20%" y="-20%" width="140%" height="140%">
              <feGaussianBlur stdDeviation="6" />
            </filter>
          </defs>
          <rect x="12" y="12" width="236" height="136" rx="24" fill="#111827" />
          <circle cx="86" cy="82" r="30" fill="#22c55e" filter="url(#blurred-glow)" />
          <circle cx="86" cy="82" r="24" fill="#4ade80" />
          <rect x="138" y="54" width="66" height="56" rx="18" fill="#f97316" filter="url(#blurred-glow)" />
          <rect x="146" y="62" width="50" height="40" rx="14" fill="#fb923c" />
        </svg>
        """;

    private void OnSwapThemeClick(object? sender, EventArgs e)
    {
        _useWarmTheme = !_useWarmTheme;
        StyledSvg.CurrentCss = _useWarmTheme ? WarmCss : ColdCss;
        CssStatusLabel.Text = $"Current theme: {(_useWarmTheme ? "ember" : "cobalt")}";
    }

    private void OnZoomInClick(object? sender, EventArgs e)
    {
        InteractiveSvg.ZoomToPoint(InteractiveSvg.Zoom * 1.2, GetViewCenter(InteractiveSvg));
    }

    private void OnZoomOutClick(object? sender, EventArgs e)
    {
        InteractiveSvg.ZoomToPoint(InteractiveSvg.Zoom / 1.2, GetViewCenter(InteractiveSvg));
    }

    private void OnPanLeftClick(object? sender, EventArgs e) => InteractiveSvg.PanX -= 20;

    private void OnPanRightClick(object? sender, EventArgs e) => InteractiveSvg.PanX += 20;

    private void OnPanUpClick(object? sender, EventArgs e) => InteractiveSvg.PanY -= 20;

    private void OnPanDownClick(object? sender, EventArgs e) => InteractiveSvg.PanY += 20;

    private void OnResetViewClick(object? sender, EventArgs e)
    {
        InteractiveSvg.Zoom = 1.0;
        InteractiveSvg.PanX = 0.0;
        InteractiveSvg.PanY = 0.0;
        HitTestStatusLabel.Text = "Tap the camera SVG to inspect hit-tested elements.";
    }

    private void OnInteractiveSvgTapped(object? sender, TappedEventArgs e)
    {
        var point = e.GetPosition(InteractiveSvg);
        if (point is null)
        {
            HitTestStatusLabel.Text = "Tap location unavailable for this gesture.";
            return;
        }

        var hits = InteractiveSvg.HitTestElements(point.Value).ToArray();
        if (hits.Length == 0)
        {
            HitTestStatusLabel.Text = $"No SVG elements hit at ({point.Value.X:F0}, {point.Value.Y:F0}).";
            return;
        }

        var labels = hits
            .Select(static element => !string.IsNullOrWhiteSpace(element.ID) ? $"#{element.ID}" : element.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .Take(3);

        HitTestStatusLabel.Text = $"Hit {hits.Length} element(s): {string.Join(", ", labels)}";
    }

    private void OnWireframeChanged(object? sender, CheckedChangedEventArgs e)
    {
        FilterSvg.Wireframe = e.Value;
    }

    private void OnDisableFiltersChanged(object? sender, CheckedChangedEventArgs e)
    {
        FilterSvg.DisableFilters = e.Value;
    }
}
