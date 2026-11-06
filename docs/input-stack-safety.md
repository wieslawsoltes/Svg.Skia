# Input depth and stack safety

SVG element nesting, resource references, and CSS can grow the managed call stack
independently of the file's size. Svg.Custom and Svg.Skia apply finite processing
limits and reject excessive depth or reference work with `SvgProcessingLimitException`, a catchable
`InvalidOperationException` subtype. These checks address
[issue #568](https://github.com/wieslawsoltes/Svg.Skia/issues/568).

## Supported limits

| Structure or operation | Maximum |
| --- | ---: |
| Element depth, including the SVG root and leaf | 256 |
| Reference-chain depth | 128 |
| CSS delimiter nesting and selector-tree depth | 64 |
| Components in an individual CSS selector | 256 |
| Active CSS import chain | 32 |
| Combined CSS import/media source expansion | 96 |
| Simultaneously active guarded processing calls | 256 |
| SVG resource-reference operations in one synchronous operation | 500,000 |

The structural limits measure depth: many sibling elements, independent selector-list
branches, and repeated sibling imports do not consume the depth budget cumulatively. Quoted,
commented, and escaped CSS delimiters do not count as nesting.

The separate reference-work limit bounds expansion of shallow, acyclic shared graphs.
It counts element, image, and deferred-paint reference attempts, including cached
lookups, and concrete pattern expansion. The counter spans nested documents and
scenes and is released when the outer synchronous operation exits. It is not a
lifetime quota on a document or thread. Several processing phases may resolve the
same reference, so this limit does not represent a number of distinct URLs.

The shared processing budget covers nested scene compilation, paint fallback
processing, patterns, masks, text references, and conversion of generated filter,
shader, and picture graphs. A document can therefore exceed the processing budget
even when each of its individual XML or reference chains is below its limit.

The runtime also checks available execution stack before guarded recursive work.
A host using a smaller stack, or calling SVG processing with a substantially occupied
stack, can receive the exception earlier. The limits are fixed safety boundaries;
there is no option to disable them or restore unbounded recursion.

## Handling rejection

The exception exposes `Operation`, `Kind`, and nullable `Limit` for diagnostics.
`Kind` distinguishes `Depth`, `ReferenceCount`, and `ExecutionStack`. The last kind
has no numeric limit and retains the runtime's `InsufficientExecutionStackException`
as its inner exception. It must not be reported as having reached depth 256.
For example:

```csharp
using Svg;
using Svg.Skia;

try
{
    using var svg = new SKSvg();
    svg.Load(inputStream);
    // Use svg.Picture while svg remains alive.
}
catch (SvgProcessingLimitException exception)
{
    Console.Error.WriteLine(
        $"SVG processing stopped during {exception.Operation} ({exception.Kind}).");
    if (exception.Limit is { } limit)
    {
        Console.Error.WriteLine($"Supported limit: {limit}.");
    }
}
```

The same exception applies to the public `SvgDocument` and
`SvgDocumentCompatibilityLoader` loading APIs. Caller-owned input streams remain
open when loading is rejected. A rejected operation releases its processing budget,
so the same thread can subsequently load a valid document.

## Enforcement

- Both XML loaders check reader and construction-stack depth before creating an
  element, attaching it to its parent, or applying styles. File, string, stream,
  XML-reader, and XML-document entry points share these boundaries.
- Stylesheets, inline declarations, and selectors are checked before ExCSS parsing
  and before recursive selector specificity/delegate processing. Import traversal
  tracks active chains and retains existing cycle recovery and cascade order.
- Generated attribute setters and tolerant parser/resource catches propagate the
  safety exception while retaining their ordinary malformed-input recovery.
- Caller-created documents and fragments receive iterative validation before
  viewport, font, style, animation, or rendering traversals. The validator uses
  memory proportional to depth, including for wide trees.
- Reference-cycle detection uses an explicit stack and active-path tracking.
  Reusing one resource in separate sibling branches remains valid. Completed
  subgraphs are memoized with their validated reference depth, so shared graphs do
  not cause exponential validation work and deeper reuses still enforce the limit.
- Synchronous nested rendering shares a thread-local budget, including paths that
  create a new scene compiler or load a different SVG document. Scope disposal
  restores the preceding budget after both success and failure.
- Filter metadata uses reference identity so a flat list of filter primitives
  cannot trigger recursive record hashing of the generated input graph.

These boundaries do not impose a general file-size, image-allocation, memory, or
elapsed-time limit. See the [engine comparison](svg-engine-safety-comparison.md)
for the separate limits and failure behavior used by Blink, Gecko, WebKit, librsvg,
and resvg, and the rationale for retaining CLR-specific depth limits.

## Regression validation

The focused suite covers public loader variants, exact XML/CSS/import boundaries,
valid wide documents, shared references, cycles, caller-created DOMs, recursive
patterns, generated filter/picture graphs, paint fallbacks, and recovery after
rejection. Reference-work tests additionally cover exact quota boundaries, shared
scope lifetime, independent threads, cached paints, direct patterns, images, and
shallow acyclic expansion. It can be run with:

```sh
dotnet test tests/Svg.Skia.UnitTests/Svg.Skia.UnitTests.csproj \
  -c Release -f net10.0 --filter FullyQualifiedName~SvgProcessing

dotnet test tests/Svg.Model.UnitTests/Svg.Model.UnitTests.csproj \
  -c Release -f net10.0 --filter FullyQualifiedName~SvgReferenceTraversalLimitTests
```

Isolated baseline processes on .NET 10 with a 256 KiB worker stack reproduced
process termination for deep element nesting, nested stylesheets, reference chains,
and paint fallbacks. The corresponding guarded builds return catchable exceptions
and keep the process alive. Ordinary CSS at the supported nesting boundary also
loads on that small stack.

See Microsoft's documentation for
[`EnsureSufficientExecutionStack`](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.runtimehelpers.ensuresufficientexecutionstack)
for the runtime's recoverable early stack check.
