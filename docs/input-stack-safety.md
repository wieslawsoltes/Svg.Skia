# Input depth and stack safety (issue #568)

## Problem

SVG element nesting, reference chains, and nested stylesheets can grow the managed
call stack in proportion to untrusted input. A stack overflow terminates the host
process, so catching an exception after recursive processing begins is insufficient.

## Implementation and acceptance criteria

- Enforce finite depth limits before input reaches recursive parsing, stylesheet
  processing, or rendering code. Report rejected input with a catchable exception.
- Cover public loading paths and rendering of caller-created SVG documents.
- Bound reference expansion, including acyclic chains and cycles, without treating
  ordinary repeated use of a shared resource as a cycle.
- Preserve ordinary SVG rendering, caller-owned stream behavior, and existing APIs.
- Add regressions for deep XML, stylesheets, and references, alongside legitimate
  documents near the supported limits and recovery after a rejected document.
- Run formatting, focused regressions, solution build, and solution tests; record
  any environmental limitations and the exact validation results in the PR.

This document tracks the work for
https://github.com/wieslawsoltes/Svg.Skia/issues/568.
