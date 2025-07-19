using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using AvalonDraw.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Svg;

namespace AvalonDraw;

public partial class SymbolEditorWindow : Window
{
    private readonly TextBox _editor;
    private readonly SymbolService.SymbolEntry _entry;
    private readonly SymbolService _service;

    public SymbolEditorWindow(SymbolService.SymbolEntry entry, SymbolService service)
    {
        InitializeComponent();
        _entry = entry;
        _service = service;
        _editor = this.FindControl<TextBox>("Editor");
        _editor.Text = SymbolToXml(entry.Symbol);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private static string SymbolToXml(SvgSymbol symbol)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(new StringWriter(sb), new XmlWriterSettings { Indent = true });
        symbol.Write(writer);
        writer.Flush();
        return sb.ToString();
    }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var xml = $"<svg xmlns='http://www.w3.org/2000/svg'><defs>{_editor.Text}</defs></svg>";
        try
        {
            var doc = SvgDocument.FromSvg<SvgDocument>(xml);
            var defs = doc.Children.OfType<SvgDefinitionList>().FirstOrDefault();
            var newSymbol = defs?.Children.OfType<SvgSymbol>().FirstOrDefault();
            if (newSymbol is null)
                throw new SvgException("Symbol not found");
            _service.ReplaceSymbol(_entry.Symbol, newSymbol);
            Close(true);
        }
        catch
        {
            Close(false);
        }
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
