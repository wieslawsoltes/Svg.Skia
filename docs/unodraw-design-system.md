# UnoDraw shared interface system

UnoDraw's workspace and reusable controls now share semantic resources rather than maintaining separate light/dark palettes in the shell and generic control templates. The system lives in `src/Svg.Editor.Skia.Uno/Themes` and is usable independently of the sample application.

## Composition and customization

`StudioPaletteResources` owns surface, input, border, divider, text, selection, accent and destructive colors. Light and Dark define the same semantic keys. HighContrast maps those keys to native system colors instead of assuming a particular black/white contrast scheme.

`StudioThemeResources` merges the palette, provides shared typography and control styles, and maps native input, popup, menu and dialog resource keys onto it. The sample merges it after `XamlControlsResources`; the workspace also scopes it locally for embedding. Custom default templates in `Generic.xaml` consume the same dictionary. Layer rows, inline editors and the control gallery use the reusable resources too.

```xml
<ResourceDictionary.MergedDictionaries>
  <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
  <studio:StudioThemeResources />
</ResourceDictionary.MergedDictionaries>
```

Here `studio` is `using:Svg.Editor.Skia.Uno`. A host can override named styles/tokens after this dictionary in its own resource scope. Do not edit generic templates to change a surface color. Hosts should merge the application dictionary as shown when they want native popups and dialogs outside the embedded workspace to share the same appearance.

The View menu/command palette offers **Use light interface**, **Use dark interface** and **Follow system appearance**. These operate on the workspace's requested theme, not on SVG paint values. They do not change the document or create history entries. The existing theme-toggle command remains available. Explicit appearance commands do not add an independent persistence format.

## Visual hierarchy

The interface uses a 4-unit spacing rhythm: 8-unit field gaps, 12-unit panel insets/section separation, 32-unit regular controls and 28-unit compact actions. These are minimum heights, not fixed text containers. Body and field text use 12 units; field headers and secondary instructions use 11 units. Header templates wrap long labels.

Surfaces are differentiated by purpose: the canvas surround, panel body, dock header, raised action and inset input each have a semantic token. Borders and section dividers have separate contrast levels. Accent is reserved for selection, focus and primary actions instead of coloring every button.

Buttons share a single template with four roles:

| Role | Resource/API | Use |
|---|---|---|
| Normal | `StudioButtonStyle` / `StudioActionRole.Normal` | Secondary commands |
| Primary | `StudioPrimaryButtonStyle` / `Primary` | Apply, Export and other explicit acceptance |
| Quiet | `StudioQuietButtonStyle` / `Quiet` | Header chrome and low-emphasis actions |
| Destructive | `StudioDangerButtonStyle` / `Destructive` | Remove/delete actions |

Code-created editors use `StudioTheme.ApplyRole(button, role)`. Roles retain system focus visuals and native Button keyboard/automation behavior. Input controls keep native templates: TextBox/NumberBox selection, expression parsing, validation and IME are not replaced by painted mock fields. Native toggle, menu, list and dialog controls likewise retain their interaction implementations.

Native resource values must preserve the platform template's data type. In particular, `ContentDialogButtonSpacing` is a `GridLength`, not a `Thickness`. ColorPicker's vertical minimum is reduced to 220 units so the native control fits the smallest supported dock interior rather than insisting on the platform's larger default.

## Responsive form layout

`StudioFieldGrid` is a reusable panel for equal-width controls with content-sized rows. It stacks fields when the requested minimum column width no longer fits. Hidden/collapsed children do not reserve space, and no controls are rebuilt or reparented when crossing a breakpoint. The pure `SvgStudioGridMetrics` value performs the column/width calculations without a UI dependency.

```csharp
var form = new StudioFieldGrid
{
    MaximumColumns = 2,
    MinimumColumnWidth = 144,
    Spacing = 8
};
form.Children.Add(widthField);
form.Children.Add(heightField);
```

With these values, a 296-unit content area permits two 144-unit fields plus the gap; narrower content uses one column. Action grids may use smaller minimums appropriate to their shorter labels. Infinite measure constraints return a finite preferred width. Invalid configuration and non-finite dimensions are rejected.

The Align editor uses a regular three-column edge/center group and equal-width pairs for center/gap distribution. Long reference labels receive the whole panel width. Explanatory spacing text is available in a disclosure rather than crowding the core actions.

The gradient editor puts Apply/Reset immediately after the ramp, before the long editing options. Selected-stop color is visible in a paint chip; clicking it opens the complete native color/alpha editor. Exact position and opacity are paired, while long geometry choices occupy a full row. Keyboard help is a separate disclosure. The solid Color mixer similarly places acceptance actions before its spectrum and no longer silently discards a pending draft when changing fill/stroke targets.

Existing registered inline panel roots acquire the same insets once during initialization. This is not a per-frame visual-tree styling pass. The SVG scene, rendering engine, editing transactions, docking persistence and file formats are unchanged by the design-system resources.

## Control review inventory

| Area | Treatment in this continuation |
|---|---|
| Shell, context bar and status bar | Shared surfaces/dividers, aligned compact actions, explicit Export role |
| Properties and native attributes | Shared input/header styles and panel insets |
| Transform and precision fields | Shared numeric-field template and native editing styles |
| Appearance and Color | Visible draft actions, shared roles, responsive fields, bounded ColorPicker width |
| Gradient | Equal-width actions, full-width geometry, stop-color disclosure, unchanged draft transactions |
| Align | Regular action grids and clearer reference/spacing grouping |
| Geometry, Typography, Effects and Resources | Shared native controls, custom sections and root spacing; existing authoring workflows retained |
| Layers | Shared icon/badge chrome and consistent row rhythm; existing selection/lock/rename behavior retained |
| Dock groups, tabs, tools and splitters | Shared default templates and theme tokens with focus visuals |
| Menus, native dialogs and flyouts | Application-scope lightweight resources rather than a separate visual language |

Custom canvas artwork previews and ruler drawing remain Skia-based. The system does not repaint document colors to match interface colors, nor does a palette audit certify all possible SVG rendering. Advanced feature dialogs inherit the shared native styling; this continuation does not redesign every large studio's internal workflow or eliminate every legacy local size value.

## Live gallery and regression coverage

**Window > Control gallery** opens actual inputs, action roles, disabled/mixed states, paint chips, precision fields and vector tool controls. These examples are detached from the SVG document and can be exercised with the keyboard. The gallery is also a reusable `StudioControlGallery` control.

`SvgStudioGridMetricsTests` verifies breakpoints, equal column alignment, finite unconstrained measurement, narrow hosts and invalid values. `test_theme_contracts.py` checks palette-key parity, high-contrast mappings, native resource types, preservation of native input templates, custom template parts and button-role/focus contracts. It calculates at least 4.5:1 contrast for normal, secondary, selected, primary and destructive text pairs in both explicit palettes. That numerical check is not screen-reader certification, native high-contrast runtime testing or a claim about every rendered text state.

The native `themes.sh` suite first runs the full existing Studio and advanced editing suites. It then captures all eleven panels in both explicit themes, color/gradient editors, gallery/focus states and 390-/480-unit compact views. Saved SVG checks verify that interface-only operations do not alter the artwork. `theme-review.json` inventories the resulting captures. Screenshot production is not equivalent to visual correctness; the images must be inspected separately.

The CI run and artifacts must be associated with their exact commit. Xvfb uses software graphics, so these checks do not establish physical GPU performance. Mobile hardware, browser interaction, screen readers, system high contrast and large accessibility text scales remain distinct validation targets.
