using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Globalization.NumberFormatting;

namespace Svg.Editor.Skia.Uno;

/// <summary>Validates native numeric input before a dialog or retained draft is applied.</summary>
internal static class StudioNumberInput
{
    public static NumberBox? FindInvalid(DependencyObject element)
    {
        if (element is FrameworkElement { Visibility: Visibility.Collapsed } ||
            element is Control { IsEnabled: false })
            return null;
        if (element is NumberBox number)
            return TryCommit(number) ? null : number;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            if (FindInvalid(VisualTreeHelper.GetChild(element, index)) is { } invalid)
                return invalid;
        return null;
    }

    public static bool TryCommit(NumberBox number)
    {
        // Default actions can run on KeyDown before NumberBox commits on KeyUp.
        // Read its actual input and retain the native formatter/expression parser.
        if (FindInput(number) is { } input)
        {
            var text = input.Text;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            if (!number.AcceptsExpression && number.NumberFormatter is INumberParser parser &&
                !InRange(number, parser.ParseDouble(text)))
                return false;
            number.Text = text;
            // Disabled validation retains invalid expressions instead of silently
            // replacing them with the previous value. Accepted expressions have
            // been normalized to a number by the native control at this point.
            if (number.ValidationMode == NumberBoxValidationMode.Disabled &&
                number.NumberFormatter is INumberParser retainedParser &&
                !InRange(number, retainedParser.ParseDouble(input.Text)))
                return false;
        }
        return InRange(number, number.Value);
    }

    public static bool InRange(NumberBox number, double? value)
        => value is { } actual && double.IsFinite(actual) && actual >= number.Minimum && actual <= number.Maximum;

    public static void SetValue(NumberBox number, double value)
    {
        number.Value = value;
        var text = number.NumberFormatter.FormatDouble(value);
        number.Text = text;
        // Reset can restore the same numeric value while the native input still
        // contains an incomplete expression. A same-value DP assignment is a no-op.
        if (FindInput(number) is { } input)
            input.Text = text;
    }

    private static TextBox? FindInput(DependencyObject element)
    {
        if (element is TextBox { Name: "InputBox" } input)
            return input;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            if (FindInput(VisualTreeHelper.GetChild(element, index)) is { } found)
                return found;
        return null;
    }
}
