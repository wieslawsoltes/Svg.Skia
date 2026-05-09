namespace Svg.JavaScript;

public sealed class SvgJavaScriptSettings
{
    public bool EnableExternalJavaScript { get; set; }

    public int TimeoutMilliseconds { get; set; }

    public int MaxStatements { get; set; }

    public bool ThrowOnError { get; set; }

    public SvgJavaScriptSettings()
    {
        EnableExternalJavaScript = true;
        TimeoutMilliseconds = 1000;
        MaxStatements = 10000;
        ThrowOnError = false;
    }
}
