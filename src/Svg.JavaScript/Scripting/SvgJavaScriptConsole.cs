using System.Diagnostics;
using System.Linq;

namespace Svg.JavaScript;

public sealed class SvgJavaScriptConsole
{
    public void log(params object?[] values)
    {
        Trace.TraceInformation(string.Join(" ", values.Select(static value => value?.ToString() ?? string.Empty)));
    }

    public void warn(params object?[] values)
    {
        Trace.TraceWarning(string.Join(" ", values.Select(static value => value?.ToString() ?? string.Empty)));
    }

    public void error(params object?[] values)
    {
        Trace.TraceError(string.Join(" ", values.Select(static value => value?.ToString() ?? string.Empty)));
    }
}
