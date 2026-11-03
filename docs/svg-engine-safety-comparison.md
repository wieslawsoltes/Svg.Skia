# SVG processing limits in other engines

This source audit was performed on 2026-11-03 for [issue #568](https://github.com/wieslawsoltes/Svg.Skia/issues/568).
It compares implementation boundaries, not measured browser-version acceptance
thresholds. No Safari, Firefox, or Chrome executable was run for this comparison.
The browser sources are pinned to the audited revisions:

- Chromium/Blink: `6420f775e2a014025182ba04297331bd30e27b33`.
- Firefox/Gecko: `2b71df5bf2e07525d1129be46e0ee02ec70d34d8`.
- WebKit: `45db78458845ca828e28e3bf0d6e4057e38d377d`.

## There is no single browser SVG depth limit

| Engine | XML/DOM boundary | Other relevant boundaries and behavior |
| --- | --- | --- |
| Blink | The XML tree builder has a 5,000-node-stack guard and reports a fatal parsing error. Its libxml path enables `XML_PARSE_HUGE`, whose bundled parser separately uses a 2,048 depth guard. | Inline HTML parsing has a different 512-depth policy. SVG resource checks distinguish active, completed-acyclic, and cyclic states. Nested use shadow trees are expanded through a queue. |
| Gecko | Expat integration checks a depth of 5,000 before delivering another start element to the DOM sink, and stops parsing on excess. | The CSS parser defaults to 75 nested blocks/functions. SVG effect-reference guards default to a chain length of 10 and also detect active-frame reentry. |
| WebKit | WebCore has a 5,000-node-stack XML guard and stops parsing after a fatal nesting error; its libxml parser may impose a lower limit. | Default HTML-parser and render-tree limits are 512. CSS nested-rule depth is 128. Over-deep CSS rules or render subtrees are suppressed. |
| librsvg | Loading and rendering have separate implementation-limit errors. | Up to 1,000,000 loaded elements, 500,000 resolved element references, and 20 levels of nested file loading. Layer depth is 500 normally and 50 on Linux/musl. |
| resvg/usvg | Internal SVG conversion checks a depth counter against 1,024 and caps accumulated nodes around 1,000,000. | Both paths return `NodesLimitReached`; use expansion participates in conversion depth and node accounting. |

These counters count different things. An XML parser may use an iterative DOM stack
while a renderer has several recursive frames per SVG node. HTML parsing may flatten
overflow into sibling nodes, while standalone SVG/XML reports an error. It would be
incorrect to treat the largest browser parser constant as a safe CLR rendering limit.

Sources: [Blink XML][blink-xml], [Blink libxml][blink-libxml],
[Blink HTML limit][blink-html], [Blink use expansion][blink-use],
[Blink resource checks][blink-resource], [Gecko XML][gecko-xml],
[Gecko CSS][gecko-css], [Gecko reference guard][gecko-reference],
[WebKit XML][webkit-xml], [WebKit settings][webkit-settings],
[WebKit CSS][webkit-css], [librsvg limits][librsvg-limits],
[resvg conversion][resvg-parse].

### A Firefox preference that needs qualification

Gecko has an eight-level recursive `<use>` cloning preference, but its audited
default enable mode is `2`: the restriction applies only in the parent process.
It must not be presented as a universal eight-level limit for ordinary Firefox web
content. See the [preference definition][gecko-preferences] and
[process/ancestor checks][gecko-use].

## Cycles, repeated work, and expansion are different problems

An active-path set detects a cycle. It must release a target when traversal leaves
that path, otherwise valid sibling references to the same target become false cycles.
Gecko's scoped frame guards and WebKit's scoped renderer-identity tracking follow
this pattern. Blink additionally caches completed resource-cycle checks.

A completed-work cache addresses another problem: the same acyclic subtree can be
reached through exponentially many paths. WebKit fixed exponential nested-pattern
invalidation by carrying visited-renderer state through the entire invalidation
operation. The [WebKit fix][webkit-dag-fix] is direct precedent for remembering work
that has already finished, separately from the current active path.

Finally, successfully validating a shared graph does not make its expanded output
small. A short chain of groups that each instantiate the next group several times
can request enormous output. Librsvg therefore applies a separate cumulative
reference quota. The [Firefox expansion discussion][gecko-expansion] also explains
why permanently accumulating a quota on a long-lived document is incorrect and why
separate counters per nested document can be bypassed.

## Changes made in Svg.Skia after this comparison

### Preserve the measured stack boundaries

XML depth 256, reference depth 128, CSS depth 64, and shared guarded processing depth
256 remain unchanged. The existing runtime execution-stack check remains necessary
for small stacks and for callers that have already consumed substantial stack.
Librsvg's different layer limits on glibc and musl reinforce that a fixed depth alone
does not describe stack safety on every host. The marker-reference metadata scan
now uses an explicit continuation stack with O(depth) storage, preserving the
accepted XML boundary when additional outer processing scopes are active.

### Memoize completed reference validation without hiding deep paths

Reference validation now separates active targets from fully validated targets.
The completed cache retains the deepest reference depth at which a target was
validated. A shallower or equal reuse can skip redundant traversal. A deeper reuse
must be checked again, so an early short path cannot conceal a later path beyond
the 128-reference boundary. Memoization is local to one traversal; it cannot become
stale after a caller mutates the document.

Target identity also has to preserve URI fragments and document scope. Ordinary
[`Uri` equality][dotnet-uri-equality] is unsuitable for SVG targets because absolute-URI equality ignores
the fragment. Separate targets such as `document.svg#a` and `document.svg#b` must
remain distinct, and an unbased document's local IDs belong to that document alone.

### Add a separate, scoped reference-work quota

One synchronous operation may perform up to 500,000 SVG resource-reference operations.
This follows librsvg's scale while retaining Svg.Skia's own counting boundaries.
Element/image references, deferred paints including cached lookups, and concrete
pattern expansion consume the budget. Resolving the same resource in multiple
phases can consume multiple operations; the number is not a unique-URL count.

The outer processing scope owns the budget. Nested scenes and documents share it.
Compilation, resource indexing, mutation, rendering, and native picture creation
must not reset it between their synchronous phases. Ending the outer operation
releases it, whether the operation succeeded or threw. Independent threads and
later animation frames or loads receive independent budgets. Thread-local scopes
are never held across `yield` or `await`.

This quota is not a general memory, input-size, decoded-image-size, or elapsed-time
limit. Those are separate resource policies.

### Keep an explicit library failure contract

Browsers often discard only an invalid rule, reference, or subtree. Their partial
rendering behavior is not a uniform contract across processing layers. Svg.Skia
keeps its catchable `SvgProcessingLimitException` for exceeding a safety boundary;
ordinary detected cycles retain their existing non-rendering behavior.

Diagnostics now distinguish `Depth`, `ReferenceCount`, and `ExecutionStack` through
`Kind`, with nullable `Limit`. An early available-stack rejection has no numerical
depth claim and preserves the original runtime exception as its inner exception.

See [input-stack-safety.md](input-stack-safety.md) for the public limits, catch example,
and regression commands.

[blink-xml]: https://github.com/chromium/chromium/blob/6420f775e2a014025182ba04297331bd30e27b33/third_party/blink/renderer/core/xml/parser/xml_document_parser.cc
[blink-libxml]: https://github.com/chromium/chromium/blob/6420f775e2a014025182ba04297331bd30e27b33/third_party/libxml/src/parser.c
[blink-html]: https://github.com/chromium/chromium/blob/6420f775e2a014025182ba04297331bd30e27b33/third_party/blink/renderer/core/html/parser/html_construction_site.h
[blink-use]: https://github.com/chromium/chromium/blob/6420f775e2a014025182ba04297331bd30e27b33/third_party/blink/renderer/core/dom/document.cc#L3314-L3324
[blink-resource]: https://github.com/chromium/chromium/blob/6420f775e2a014025182ba04297331bd30e27b33/third_party/blink/renderer/core/svg/svg_resource.cc
[gecko-xml]: https://github.com/mozilla-firefox/firefox/blob/2b71df5bf2e07525d1129be46e0ee02ec70d34d8/parser/htmlparser/nsExpatDriver.cpp
[gecko-css]: https://github.com/mozilla-firefox/firefox/blob/2b71df5bf2e07525d1129be46e0ee02ec70d34d8/third_party/rust/cssparser/src/parser.rs
[gecko-reference]: https://github.com/mozilla-firefox/firefox/blob/2b71df5bf2e07525d1129be46e0ee02ec70d34d8/layout/svg/AutoReferenceChainGuard.h
[gecko-preferences]: https://github.com/mozilla-firefox/firefox/blob/2b71df5bf2e07525d1129be46e0ee02ec70d34d8/modules/libpref/init/StaticPrefList.yaml#L20019-L20034
[gecko-use]: https://github.com/mozilla-firefox/firefox/blob/2b71df5bf2e07525d1129be46e0ee02ec70d34d8/dom/svg/SVGUseElement.cpp
[gecko-expansion]: https://bugzilla.mozilla.org/show_bug.cgi?id=455100
[webkit-xml]: https://github.com/WebKit/WebKit/blob/45db78458845ca828e28e3bf0d6e4057e38d377d/Source/WebCore/xml/parser/XMLDocumentParser.cpp
[webkit-settings]: https://github.com/WebKit/WebKit/blob/45db78458845ca828e28e3bf0d6e4057e38d377d/Source/WebCore/page/SettingsBase.h
[webkit-css]: https://github.com/WebKit/WebKit/blob/45db78458845ca828e28e3bf0d6e4057e38d377d/Source/WebCore/css/parser/CSSParser.cpp
[webkit-dag-fix]: https://github.com/WebKit/WebKit/commit/00f03d987c0cec7d2821e3d1086bdf15dc807c0d
[librsvg-limits]: https://gnome.pages.gitlab.gnome.org/librsvg/internals/src/rsvg/limits.rs.html
[resvg-parse]: https://github.com/linebender/resvg/blob/main/crates/usvg/src/parser/svgtree/parse.rs
[dotnet-uri-equality]: https://learn.microsoft.com/dotnet/api/system.uri.equals
