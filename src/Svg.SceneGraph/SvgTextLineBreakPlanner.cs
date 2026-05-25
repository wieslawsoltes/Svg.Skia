#nullable enable

using System.Collections.Generic;

namespace Svg.Skia;

internal enum SvgTextBreakOpportunityKind
{
    Soft,
    Whitespace,
    Invisible,
    ForcedLine
}

internal enum SvgTextBreakPriority
{
    Emergency = 1,
    Soft = 2,
    Natural = 3,
    Whitespace = 4,
    Forced = 5
}

internal readonly record struct SvgTextLineBreakOptions(
    bool OverflowWrapAnywhere,
    bool WordBreakBreakAll,
    bool WordBreakKeepAll,
    bool LineBreakAnywhere,
    bool LineBreakLoose,
    bool StrictLineBreak)
{
    public bool AllowsCharacterBreaks => OverflowWrapAnywhere || WordBreakBreakAll || LineBreakAnywhere;

    public SvgTextLineBreakOptions WithoutEmergencyBreaks() =>
        this with
        {
            OverflowWrapAnywhere = false,
            WordBreakBreakAll = false,
            LineBreakAnywhere = false
        };
}

internal readonly record struct SvgTextBreakOpportunity(
    int BeforeCodepointIndex,
    int AfterCodepointIndex,
    int BreakCodepointIndex,
    SvgTextBreakOpportunityKind Kind,
    SvgTextBreakPriority Priority,
    bool ConsumesCodepoint);

internal static class SvgTextLineBreakPlanner
{
    private static ISvgTextBoundaryResolver BoundaryResolver => SvgTextBoundaryResolver.Default;

    public static SvgTextBreakOpportunity? GetBreakOpportunity(
        IReadOnlyList<string> codepoints,
        int index,
        SvgTextLineBreakOptions options,
        bool insideBidiFormatting,
        string? previousCodepoint = null,
        string? nextCodepoint = null,
        bool isClusterBoundaryAfter = true)
    {
        return BoundaryResolver.TryGetBreakOpportunity(
            codepoints,
            index,
            options,
            insideBidiFormatting,
            previousCodepoint,
            nextCodepoint,
            isClusterBoundaryAfter,
            out var opportunity)
                ? opportunity
                : null;
    }

    public static bool AllowsSoftWrapOpportunity(
        IReadOnlyList<string> codepoints,
        int index,
        SvgTextLineBreakOptions options)
    {
        return BoundaryResolver.AllowsSoftWrapOpportunity(codepoints, index, options);
    }

    public static bool IsBreakOpportunityWhitespace(
        string codepoint,
        string? previousCodepoint = null,
        string? nextCodepoint = null,
        bool insideBidiFormatting = false)
    {
        return BoundaryResolver.IsBreakOpportunityWhitespace(
            codepoint,
            previousCodepoint,
            nextCodepoint,
            insideBidiFormatting);
    }

    public static bool IsInvisibleBreakOpportunity(
        string codepoint,
        string? previousCodepoint = null,
        string? nextCodepoint = null,
        bool insideBidiFormatting = false)
    {
        return BoundaryResolver.IsInvisibleBreakOpportunity(
            codepoint,
            previousCodepoint,
            nextCodepoint,
            insideBidiFormatting);
    }

    public static int UpdateBidiFormattingDepth(int depth, string codepoint)
    {
        return BoundaryResolver.UpdateBidiFormattingDepth(depth, codepoint);
    }

    public static bool IsNoBreakAdjacentFormatControl(string? codepoint)
    {
        return BoundaryResolver.IsNoBreakAdjacentFormatControl(codepoint);
    }

    public static bool IsCombiningOrJoiningCodepoint(int scalar)
    {
        return BoundaryResolver.IsCombiningOrJoiningCodepoint(scalar);
    }
}
