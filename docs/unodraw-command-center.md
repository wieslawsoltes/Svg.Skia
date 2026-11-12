# UnoDraw command center

The command center uses EditorTheme resources, a native search field and a virtualized result list. Open it with **Ctrl+Shift+F12** (Command+Shift+F12 on macOS), the top-bar Search action, or **Help > Search commands**. Every result displays its action name, category and active shortcut. **Ctrl+K** opens Preferences.

## Finding and running actions

Enter a command name, a category with an action, or a displayed shortcut. Queries are case-insensitive and culture-invariant. Words may appear in a different order: `window transform` finds the Transform panel and `editor gradient` finds the Gradient editor. `Ctrl+S` selects Save, while `Ctrl+Shift+S` selects Save as. Exact action names rank before shortcuts, prefixes and broader all-word matches.

An empty search places up to sixteen recent choices before the rest of the catalog. Recent choices are held in memory for that workspace instance; they are not saved to the SVG, included in undo history, or uploaded elsewhere. Explicit queries always outrank recency. Duplicate labels remain separate commands through their unique IDs.

Use Up/Down to change the selected result while retaining text-field focus, Page Up/Page Down to move by eight results, Enter or the Run button to invoke, and Escape to dismiss. Home/End remain normal text-editing keys in the search field. The native text field supplies its normal clear action rather than adding a second competing clear button. A search with no results clears selection, disables Run, and consumes Enter without invoking an earlier result.

Choosing an action dismisses the palette before invoking the existing workspace command dispatcher. Actions that open a dialog therefore do not compete with the command palette's modal lifetime. Searching is read-only metadata browsing; command validation, locking, transactions and error reporting stay in the controller/workspace.

## App shortcuts and native focus

The active `SvgEditorShortcutProfile` supplies native `KeyboardAccelerator` entries, bubbling key dispatch, menu hints, tooltips and search metadata. Defaults include Ctrl+Shift+F12 for search, Ctrl+K for Preferences, Ctrl+N/O/S for file commands, and Ctrl+Tab / Ctrl+Shift+Tab for document switching. Document tab navigation retains physical Control on macOS; Command+Tab remains owned by the system. Accelerator tooltips are suppressed on the workspace itself; shortcuts remain displayed on their actual commands and controls.

The accelerator handler respects modal dialogs, closed/disabled/unloaded workspaces and focus in another embedded workspace. It marks an invocation handled before awaiting execution. Text editors retain native clipboard, selection and undo keys. Canvas-only arrows, Space, Enter and unmodified Tab are excluded from the accelerator set and resolve only in their intended focus scope. **Edit > Keyboard Shortcuts** provides validated draft profiles, conflict feedback, import/export and reset to defaults; applying a profile updates every displayed shortcut.

## Reusable layers

`SvgCommandCatalogEntry` contains only immutable ID, label, category and shortcut text. `SvgCommandSearchIndex` snapshots and indexes these values independently of Uno. It has no execution delegates or document references. The host decides what command metadata to expose and how to execute a returned ID.

`StudioCommandSearch` owns the native text/list interaction and empty state. It publishes `SelectedCommandChanged` and `CommandInvoked` without invoking application commands itself. Its visual row template inherits `StudioThemeResources`, keeps long labels bounded and gives shortcut badges their own column. The workspace wrapper supplies the modal host, recent choices and the existing command dispatcher.

The catalog is bounded to 4,096 entries, the query to 256 characters, and the visible result count to 100. Core callers may request 1–256 results. Duplicate IDs and oversized metadata fail validation. Input enumeration is bounded before allocating the index; returned result collections are read-only. Normalization is deterministic token matching, not a fuzzy spelling corrector or a language model. No network search is performed by the palette.

## Validation scope

`SvgCommandSearchIndexTests` covers exact and category-qualified ranking, displayed shortcuts, unordered words, Turkish-culture stability, immutable snapshots, duplicate identity, no-result behavior, recent ordering and input budgets. Python source contracts check shared resources, bounded native controls, accelerator isolation and modal handoff ordering.

The native `command-center.sh` suite runs the existing editing, theme, segmented-paint and dock-navigation suites before checking the command center. It verifies a no-result Enter followed by a valid category query, the saved workspace's actual selected panel, real Save dispatch through shortcut search, cancellation without document edits, keyboard navigation, and 390-unit light/dark layouts. Screenshots complement saved-source/state assertions; screenshots alone are not proof of successful execution.

CI results belong to their exact commit. The Xvfb runs use software rendering. This suite does not establish physical-GPU performance, native screen-reader behavior or all mobile/browser keyboard combinations. Command search does not yet expose per-command availability explanations, persisted recent history or fuzzy typo matching.
