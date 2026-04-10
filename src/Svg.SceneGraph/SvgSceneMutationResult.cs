namespace Svg.Skia;

public sealed class SvgSceneMutationResult
{
    internal SvgSceneMutationResult(bool succeeded, int compilationRootCount, int resourceCount)
    {
        Succeeded = succeeded;
        CompilationRootCount = compilationRootCount;
        ResourceCount = resourceCount;
    }

    public bool Succeeded { get; }

    public int CompilationRootCount { get; }

    public int ResourceCount { get; }
}
