# UnoDraw command center

The command center uses the shared studio resources and a native search field and virtualized result list. Open it with **Ctrl+K**, the top-bar Search action, or **View > Search commands**. Every result separates the action name from its category and shortcut badge rather than packing all three into one text string.

## Finding and running actions

Enter a command name, a category with an action, or a displayed shortcut. Queries are case-insensitive and culture-invariant. Words may appear in a different order: `window transform` finds the Transform panel and `editor gradient` finds the Gradient editor. `Ctrl+S` selects Save, while `Ctrl+Shift+S` selects Save as. Exact action names rank before shortcuts, prefixes and broader all-word matches.

An empty search places up to sixteen recent choices before the rest of the catalog. Recent choices are held in memory for that workspace instance; they are not saved to the SVG, included in undo history, or uploaded elsewhere. Explicit queries always outrank recency. Duplicate labels remain separate commands through their unique IDs.

Use Up/Down to change the selected result while retaining text-field focus, Page Up/Page Down to move by eight results, Enter or the Run button to invoke, and Escape to dismiss. Home/End remain normal text-editing keys in the search field. The native text field supplies its normal clear action rather than adding a second competing clear button. A search with no results clears selection, disables Run, and consumes Enter without invoking an earlier result.

Choosing an action dismisses the palette before invoking the existing workspace command dispatcher. Actions that open a dialog therefore do not compete with the command palette's modal lifetime. Searching is read-only metadata browsing; command validation, locking, transactions and error reporting stay in the controller/workspace.

## App shortcuts and native focus

The workspace registers native `KeyboardAccelerator` entries for Ctrl+K, Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S, Ctrl+E and Ctrl+P. Application actions must not depend exclusively on a focused child's bubbling KeyDown handler after a panel switcher closes or a scroll arrow reaches its limit. Accelerator tooltips are suppressed on the workspace itself; shortcuts remain displayed on their actual commands and controls.

The accelerator handler respects modal dialogs, closed/disabled/unloaded workspaces and focus in another embedded workspace. It marks an invocation handled before awaiting execution. Text-editing shortcuts such as Ctrl+C, Ctrl+V and Ctrl+Z, and canvas-only keys such as arrows, Space, Enter and Tab, are not added to this global set. Their existing context-sensitive routing remains in place.

## Reusable layers

`SvgCommandCatalogEntry` contains only immutable ID, label, category and shortcut text. `SvgCommandSearchIndex` snapshots and indexes these values independently of Uno. It has no execution delegates or document references. The host decides what command metadata to expose and how to execute a returned ID.

`StudioCommandSearch` owns the native text/list interaction and empty state. It publishes `SelectedCommandChanged` and `CommandInvoked` without invoking application commands itself. Its visual row template inherits `StudioThemeResources`, keeps long labels bounded and gives shortcut badges their own column. The workspace wrapper supplies the modal host, recent choices and the existing command dispatcher.

The catalog is bounded to 4,096 entries, the query to 256 characters, and the visible result count to 100. Core callers may request 1–256 results. Duplicate IDs and oversized metadata fail validation. Input enumeration is bounded before allocating the index; returned result collections are read-only. Normalization is deterministic token matching, not a fuzzy spelling corrector or a language model. No network search is performed by the palette.

## Validation scope

`SvgCommandSearchIndexTests` covers exact and category-qualified ranking, displayed shortcuts, unordered words, Turkish-culture stability, immutable snapshots, duplicate identity, no-result behavior, recent ordering and input budgets. Python source contracts check shared resources, bounded native controls, accelerator isolation and modal handoff ordering.

The native `command-center.sh` suite runs the existing editing, theme, segmented-paint and dock-navigation suites before checking the command center. It verifies a no-result Enter followed by a valid category query, the saved workspace's actual selected panel, real Save dispatch through shortcut search, cancellation without document edits, keyboard navigation, and 390-unit light/dark layouts. Screenshots complement saved-source/state assertions; screenshots alone are not proof of successful execution.

CI results belong to their exact commit. The Xvfb runs use software rendering. This suite does not establish physical-GPU performance, native screen-reader behavior or all mobile/browser keyboard combinations. Command search does not yet expose per-command availability explanations, user-rebindable shortcuts, persisted recent history or fuzzy typo matching.
