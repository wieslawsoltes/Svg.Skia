# EditorTheme

`EditorTheme` is the reusable Uno Platform theme for UnoDraw's vector editing
workspace. It builds on the existing SVG document controller, retained canvas,
history, docking, paint editors and command system.

## Workspace contract

- A compact application menu exposes File, Edit, Object, Type, Select, Effect,
  View, Window and Help, with related operations grouped into submenus.
- The control bar follows the selection; the toolbox exposes related tools
  without replacing the existing drawing engine.
- Document tabs, rulers, the canvas, dock groups, floating panels and status bar
  share the same semantic colors, typography, spacing and focus treatment.
- Panels retain their content and editing state when moved, collapsed or hidden.
- Desktop, touch and narrow layouts expose the same commands.

## Theme contract

Merge `EditorTheme` after `XamlControlsResources`. Existing `Studio*` resource
keys and public controls remain available for applications already embedding
the editor. Theme templates preserve named input parts, accessibility,
keyboard navigation and text composition.

The theme includes neutral dark and light palettes and system high contrast
colors. Every interactive control must distinguish normal, hover, pressed,
selected, disabled and keyboard focus states where applicable. Popups and
dialogs must use the owning workspace's theme.

## Command contract

Menu labels, keyboard shortcut hints, tooltips, command search and shortcut
help use a shared command description. Unmodified drawing shortcuts are
scoped to the canvas. Text fields, numeric editors, popup lists, tree rows and
dialogs keep their native editing and navigation keys. Temporary navigation
modes restore the previous tool when released or when the workspace loses focus.

## Validation

Validation covers the actual desktop application, editor history and geometry
tests, keyboard dispatch, retained panel state, theme resources and browser
startup. Runtime visual checks cover dark and light themes, text entry, menu
navigation, tool selection, docking and narrow layouts. The change does not
replace the existing renderer or introduce a second document model.
