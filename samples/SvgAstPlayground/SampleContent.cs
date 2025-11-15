namespace SvgAstPlayground;

internal static class SampleContent
{
    public const string DefaultSvg =
        """
        <svg width="100" height="100" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="grad">
              <stop offset="0%" stop-color="#ff0000" />
              <stop offset="100%" stop-color="#0000ff" />
            </linearGradient>
          </defs>
          <rect width="100" height="100" fill="url(#grad)" />
          <circle cx="50" cy="50" r="30" fill="none" stroke="white" stroke-width="4" />
        </svg>
        """;
}
