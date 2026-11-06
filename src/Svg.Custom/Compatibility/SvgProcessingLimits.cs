using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Svg;

/// <summary>
/// Shared safety boundaries for synchronous, input-driven SVG processing.
/// </summary>
internal static class SvgProcessingLimits
{
    internal const int MaximumElementDepth = 256;
    internal const int MaximumProcessingDepth = 256;
    internal const int MaximumReferenceDepth = 128;
    internal const int MaximumCssDepth = 64;
    internal const int MaximumCssImportDepth = 32;
    internal const int MaximumCssSelectorComplexity = 256;
    internal const int MaximumReferenceResolutions = 500_000;

    // Rendering can reenter through another document, a pattern, mask, filter, or custom
    // asset loader. A counter on an individual scene context would reset at those boundaries.
    // All guarded work is synchronous; distinct threads must have independent budgets.
    [ThreadStatic]
    private static int s_processingDepth;

    // A shallow, acyclic reference DAG can expand exponentially without ever reaching
    // a depth limit. Count work across the complete synchronous operation, including
    // nested documents. This is not a lifetime quota on a document or worker thread.
    [ThreadStatic]
    private static int s_referenceResolutions;

    internal static void CheckDepth(int depth, int maximum, string operation)
    {
        if (depth > maximum)
        {
            throw new SvgProcessingLimitException(operation, maximum);
        }
    }

    internal static void CheckElementDepth(int depth)
    {
        CheckDepth(depth, MaximumElementDepth, "element nesting");
    }

    internal static void CountReferenceResolution()
    {
        if (s_referenceResolutions >= MaximumReferenceResolutions)
        {
            throw new SvgProcessingLimitException("reference resolution", SvgProcessingLimitKind.ReferenceCount, MaximumReferenceResolutions);
        }

        s_referenceResolutions++;
    }

    internal static RecursionScope Enter(string operation)
    {
        var previousDepth = s_processingDepth;
        CheckDepth(previousDepth + 1, MaximumProcessingDepth, operation);

        // A fixed input limit also needs to account for hosts with unusually small stacks
        // and calls made with an already occupied stack. This is a catchable early check;
        // a StackOverflowException itself cannot be recovered from.
        try
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
        }
        catch (InsufficientExecutionStackException exception)
        {
            throw new SvgProcessingLimitException(operation, SvgProcessingLimitKind.ExecutionStack, null, exception);
        }

        if (previousDepth == 0)
        {
            s_referenceResolutions = 0;
        }

        s_processingDepth = previousDepth + 1;
        return new RecursionScope(previousDepth);
    }

    internal static void ValidateElementTree(SvgElement root)
    {
        if (root is null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        // Check ancestors before a caller asks OwnerDocument (which follows Parent).
        // A caller-created parent cycle is rejected by the same finite depth boundary.
        var ancestorDepth = 1;
        for (var parent = root.Parent; parent is not null; parent = parent.Parent)
        {
            CheckElementDepth(++ancestorDepth);
        }

        // Explicit DFS uses O(depth) memory, including for very wide documents. Never
        // enumerate Descendants() here: the upstream implementation is recursive.
        var pending = new Stack<(SvgElement Element, int ChildIndex)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            var frame = pending.Pop();
            if (frame.ChildIndex >= frame.Element.Children.Count)
            {
                continue;
            }

            var child = frame.Element.Children[frame.ChildIndex];
            pending.Push((frame.Element, frame.ChildIndex + 1));
            CheckElementDepth(ancestorDepth + pending.Count);
            pending.Push((child, 0));
        }
    }

    internal readonly struct RecursionScope : IDisposable
    {
        private readonly int _previousDepth;

        internal RecursionScope(int previousDepth)
        {
            _previousDepth = previousDepth;
        }

        public void Dispose()
        {
            s_processingDepth = _previousDepth;
            if (_previousDepth == 0)
            {
                s_referenceResolutions = 0;
            }
        }
    }
}
