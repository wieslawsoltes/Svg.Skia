# UnoDraw control refinement: states, direct choices and dock navigation

This continuation extends the [shared design system](unodraw-design-system.md). It keeps the retained Uno workspace, native input implementation and SVG editing transactions. It does not introduce another renderer, document model or application-wide visual-tree restyling pass.

## Native controls use the intended templates and states

The shared TextBox, ComboBox, ToggleButton, CheckBox and RadioButton styles explicitly inherit their native `Default…Style` resources. Their input, selection, expression, focus and automation implementations stay native. NumberBox retains its existing native template; its embedded input receives the shared resources. Checkable menu items also inherit the native `DefaultToggleMenuFlyoutItemStyle`, with the studio font size, padding and minimum height.

`StudioNativeStateResources` supplies missing lightweight state aliases. Both the legacy disabled TextBox keys and Fluent header/placeholder keys map to the semantic palette. CheckBox checked and indeterminate glyphs use the matching on-accent foreground. ToggleButton has background, foreground and border aliases for normal, checked and indeterminate selection states combined with normal, pointer-over, pressed and disabled interaction states. Selected list/tree rows and list hover states use matched selection foreground/background pairs.

Native resource names were checked against Uno's Fluent TextBox resources and Microsoft's WinUI control resources. The implementation references these public resource contracts; it does not copy a replacement native TextBox/ComboBox editing template. Applications embedding the controls should load `XamlControlsResources` before the studio dictionaries, as described in the design-system guide.

The shared action-button template gives a pressed action a one-unit content translation. This is a visual transform, not a margin/layout mutation. Section chevrons are small vector paths with a checked-state rotation instead of font-dependent arrow characters. Pressed tool/tab/section foregrounds are paired with their selected backgrounds.

## Direct Fill / Stroke choices

`StudioChoiceStrip` replaces the two-option dropdown in both the Color mixer and Gradient editor. Both alternatives are visible and have equal-width targets. The selected segment has an underline as well as a color change. Longer labels wrap; the existing responsive field grid stacks choices when their minimum widths do not fit.

Each choice is a native RadioButton with a unique group identity. Selection changes do not recreate the child controls. Tab enters the selected choice, arrows move between choices, and Home/End reach the endpoints. Horizontal navigation accounts for right-to-left flow; modified editor shortcuts are not deliberately consumed by the strip. Full keyboard/assistive-technology behavior still requires the applicable platform runtime tests.

```csharp
var target = new StudioChoiceStrip
{
    Header = "Paint",
    ItemsSource = new[] { "Fill", "Stroke" },
    SelectedIndex = 0
};
target.SelectionChanging += (_, change) =>
{
    if (hasPendingDraft)
        change.Cancel = true;
};
```

The control snapshots up to 16 nonempty plain-text labels, each at most 128 characters, on ItemsSource assignment. It is a compact choice control, not a virtualized arbitrary-items selector. Assign a new ItemsSource to replace the choices; collection notifications are not observed. An out-of-range selected index becomes no selection. SelectionChanging covers user selection requests; programmatic SelectedIndex changes raise SelectionChanged and may be restored by the consumer.

In the actual paint editors, choosing another target with a pending draft is rejected with an explanation. The current selection and draft remain intact. The existing programmatic target guard is also retained. Apply/Reset, stale-document protection, stop identities, independent opacity and one-step SVG undo behave as before.

## Scoped action roles

`StudioTheme.ApplyRole` now checks the intended resource scope before the application. Code building an unparented button should pass its owning editor explicitly:

```csharp
StudioTheme.ApplyRole(applyButton, StudioActionRole.Primary, editor);
```

This permits an embedded editor's scoped role style to take priority. Without an explicit scope, the helper searches from the button through its parents and then the application. A standalone fallback dictionary is weakly associated with the button and created once, rather than appended again on every role change. Role application is an explicit operation; after intentionally moving a button into a different resource scope, apply the role with that new scope. It does not install a per-frame ancestor scanner.

## Discoverable dock tabs

Dock groups retain their live content while switching panels. The tab row adds three controls outside its scroll viewport:

- Backward/forward arrows appear only when the group cannot fit its tab titles. They scroll the strip without changing the active panel or issuing a document command. Existing input fields' lost-focus commit behavior still applies when focus moves away from an edited field.
- The panel picker lists every panel in that group with its complete title and marks the active one. Choosing an item uses the same guarded activation path as clicking a tab.
- The existing docking menu remains separate, with the same move/float/close commands.

The active tab is revealed inside the horizontal tab viewport without asking ancestor workspace scrollers to move. Arrow availability follows the actual scroll offset. The overflow decision compares against the width without arrow chrome, so widening a group does not leave an unnecessary arrow-induced overflow state. Retemplating detaches old handlers; queued reveal requests are versioned and invalidated on unload.

The picker explicitly focuses its checked menu entry on opening and implements Home/End navigation within its entries. A selected panel is activated after the flyout closes, not while the popup still owns input. Focus then returns to the active tab, allowing workspace shortcuts to work again. Closing or replacing the group invalidates queued picker operations; an unrelated dialog or an outside click's new focus target is not deliberately overridden.

`SetItems` snapshots its input before clearing the old collection, rejects duplicate/empty panel IDs, and accepts the group's own read-only Items view as input without losing its contents. This is not a new arbitrary-recursive docking system or a set of native detached windows.

## Review and regression coverage

The Control gallery includes the new choice strip, its disabled selected state and native checked/mixed/disabled ToggleButtons alongside the existing text, numeric, paint and tool controls. It uses actual reusable controls, not a mock illustration.

`test_control_refinements.py` checks resource-key coverage, native style inheritance, selection/focus groups, dock-template parts, nonrecursive dictionary composition, paint target guards and modifier handling. These are source contracts, not proofs of native interaction or screen-reader conformance.

`controls.sh` extends, rather than replaces, `themes.sh`, `advanced.sh` and the original smoke suite. It operates the production desktop application, navigates Fill/Stroke with the keyboard, attempts a dirty target change, applies to both paint targets, uses the editor's Ctrl+Enter shortcut, and checks undo against the original saved source. `assert_gradient_target.py` verifies the intended gradient assignment, reversed draft colors and unchanged unrelated fixture properties. Its own negative cases reject wrong targets, lost colors, nonfinite offsets, unrelated mutations and misleading inline declarations.

The native dock check selects the last panel through the picker, scrolls the tab strip, and saves a workspace snapshot. The saved active-panel identity must still be Effects, while unrelated hidden panels remain hidden and the SVG remains unchanged. Screenshots cover those gestures in addition to the existing eleven-panel light/dark capture set.

Results must be associated with the exact CI head and inspected artifacts. Screenshot production alone is not visual correctness. The native harness uses Xvfb/software graphics; it is not a physical GPU benchmark. Browser/mobile runtime checks, high-contrast interaction, screen-reader testing, touch sizing throughout the larger advanced dialogs and high text-scale review remain separate validation tasks. Not every native slider/switch state or advanced-dialog layout is redesigned by this increment.
