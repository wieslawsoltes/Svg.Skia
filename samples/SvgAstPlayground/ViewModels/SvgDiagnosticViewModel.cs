using Svg.Ast;

namespace SvgAstPlayground.ViewModels;

public sealed class SvgDiagnosticViewModel
{
    public SvgDiagnosticViewModel(string stage, SvgDiagnostic diagnostic, SvgLinePosition position)
    {
        Stage = stage;
        Severity = diagnostic.Severity.ToString();
        Code = diagnostic.Code;
        Message = diagnostic.Message;
        Location = $"L{position.Line}:C{position.Column} (+{diagnostic.Length})";
        IsError = diagnostic.Severity == SvgDiagnosticSeverity.Error;
    }

    public string Stage { get; }

    public string Severity { get; }

    public string Code { get; }

    public string Message { get; }

    public string Location { get; }

    public bool IsError { get; }
}
