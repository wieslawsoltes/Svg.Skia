# EditorTheme

`EditorTheme` is the reusable Uno Platform resource dictionary used by UnoDraw.
It supplies compact control templates, semantic colors, typography, spacing,
focus treatment and editor chrome. `SvgEditorWorkspace` combines those resources
with the existing SVG controller, retained renderer, document history, tool
engine and dock layout. Hosts can use the controls independently or embed the
complete workspace.

## Add the theme to a host

Reference `Svg.Editor.Skia.Uno` and merge `EditorTheme` after
`XamlControlsResources`. The sample uses this application resource setup:

```xml
<Application x:Class="MyEditor.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:editor="using:Svg.Editor.Skia.Uno">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
        <editor:EditorTheme />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

An editor window can create and own the workspace directly:

```csharp
using Microsoft.UI.Xaml;
using Svg.Editor.Skia.Uno;

public sealed class EditorWindow : Window
{
    private readonly SvgEditorWorkspace _workspace;

    public EditorWindow()
    {
        _workspace = new SvgEditorWorkspace { HostWindow = this };
        Content = _workspace;
        _workspace.NewDocument(1200, 800);
        Closed += (_, _) => _workspace.Dispose();
    }
}
```

`HostWindow` supplies the platform window context used by host interactions.
`Documents`, `CurrentDocument`, `Controller` and `Surface` expose the workspace's
document collection, active document, controller and canvas. The workspace owns
its documents and disposes their controllers when it is disposed. Hosts should
dispose the workspace when its owning window or view is permanently closed.

The workspace also merges `EditorTheme` locally so that embedding it does not
require modifying the application's global appearance. Existing hosts can keep
merging `StudioThemeResources`: it is a compatibility dictionary that merges
`EditorTheme`. Existing `Studio*` control names and resource keys remain public.

The implementation entry points are
[EditorTheme.xaml](../src/Svg.Editor.Skia.Uno/Themes/EditorTheme.xaml),
[Generic.xaml](../src/Svg.Editor.Skia.Uno/Themes/Generic.xaml) and
[SvgEditorWorkspace.xaml](../src/Svg.Editor.Skia.Uno/SvgEditorWorkspace.xaml).

## Palette, high contrast and resource overrides

[StudioPaletteResources.xaml](../src/Svg.Editor.Skia.Uno/Themes/StudioPaletteResources.xaml)
defines matching `Light`, `Dark` and `HighContrast` dictionaries. Templates use
semantic brushes such as `StudioSurface`, `StudioRaised`, `StudioForeground`,
`StudioMuted`, `StudioBorder`, `StudioSelection`, `StudioSelectionForeground`,
`StudioAccent`, `StudioOnAccent` and `StudioDisabled`. Each brush has a corresponding
`Color` key, for example `StudioAccentColor`.

The light and dark palettes use neutral surfaces and clear selection, pressed,
disabled and focus states. The high contrast palette resolves system window,
window text, highlight, highlight text and gray text colors. Decorative canvas
shadows become transparent in that variant. The native theme system selects the
appropriate dictionary; `RequestedTheme` can choose light, dark or the system
setting for a workspace.

Colors use `ThemeResource`, so the control tree responds to theme changes.
Popups and dialogs receive the owning workspace's theme. Skia editor chrome uses
the same semantic contract: canvas, rulers, guide previews, smart guides, grid,
selection handles, artboard outlines and transparency checker colors are exposed
as brush dependency properties. Those controls subscribe to brush color and
opacity changes as well as actual-theme changes and invalidate their surfaces.
Cached checker shaders are recreated only when their colors change. These
properties accept `SolidColorBrush` values; other brush types use the control's
fallback palette. SVG artwork colors and the document's white artboard paper
represent document content.

`StudioNavigator` exposes `BackgroundBrush`, `OutlineBrush` and `AccentBrush`.
Its implicit theme style supplies the same scoped semantic brushes as the canvas.
Replacing a brush or changing its `Color`/`Opacity` updates the overview; unloading
detaches brush subscriptions and reloading observes the current values. A host
can use the Navigator independently without a local palette overriding its own
resources.

For a host palette, merge an override dictionary after `EditorTheme` in the
resource scope that owns the controls. Override paired foreground/background
roles together. For example, `HostPalette.xaml` can define a different primary
action color while retaining system high contrast colors:

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
      <Color x:Key="StudioAccentColor">#155E75</Color>
      <Color x:Key="StudioOnAccentColor">#FFFFFF</Color>
    </ResourceDictionary>
    <ResourceDictionary x:Key="Dark">
      <Color x:Key="StudioAccentColor">#67D5EC</Color>
      <Color x:Key="StudioOnAccentColor">#082F49</Color>
    </ResourceDictionary>
    <ResourceDictionary x:Key="HighContrast">
      <StaticResource x:Key="StudioAccentColor"
                      ResourceKey="SystemColorHighlightColor" />
      <StaticResource x:Key="StudioOnAccentColor"
                      ResourceKey="SystemColorHighlightTextColor" />
    </ResourceDictionary>
  </ResourceDictionary.ThemeDictionaries>
  <SolidColorBrush x:Key="StudioAccent"
                   Color="{ThemeResource StudioAccentColor}" />
  <SolidColorBrush x:Key="StudioOnAccent"
                   Color="{ThemeResource StudioOnAccentColor}" />
</ResourceDictionary>
```

For an embedded workspace, append the host dictionary to its existing resource
scope before displaying it:

```csharp
_workspace.Resources.MergedDictionaries.Add(new ResourceDictionary
{
    Source = new Uri("ms-appx:///HostPalette.xaml")
});
```

The workspace's local resources include its field, hierarchy and dock styles, so
append overrides to that collection. An application-level override alone may be
shadowed by an embedded workspace's local theme dictionary.

The Skia roles have independent names for hosts that need different chrome
contrast: `StudioCanvas`, `StudioCanvasLabel`, `StudioCanvasOutline`,
`StudioCanvasShadow`, `StudioCanvasAccent`, `StudioCanvasHandle`,
`StudioCanvasGrid`, `StudioGuide`, `StudioGuidePreview`, `StudioSmartGuide`,
`StudioCanvasCheckerBackground`, `StudioCanvasCheckerForeground`,
`StudioRulerForeground` and `StudioRulerPointer`. Their corresponding public
canvas properties are declared in
[SvgEditorCanvas.Theme.cs](../src/Svg.Editor.Skia.Uno/Controls/SvgEditorCanvas.Theme.cs).

Metrics use `StaticResource` and should be customized before constructing the
affected controls:

| Resource | Default | Purpose |
| --- | ---: | --- |
| `StudioBodySize` / `StudioLabelSize` / `StudioTitleSize` | 12 / 11 / 14 | Body text, field labels and dialog titles |
| `StudioControlHeight` / `StudioCompactHeight` | 28 / 24 | Standard and compact control targets |
| `StudioPanelRailMinWidth` | 34 | Minimum panel-rail width; intrinsic layout adds room for a visible scrollbar |
| `StudioGap` / `StudioSectionGap` | 8 / 12 | Related fields and section separation |
| `StudioPanelPadding` | 10 | Panel content inset |
| `StudioControlRadius` | 2 | Compact control corners |

The workspace's **View > Interface > Larger Tool Targets** setting adjusts its
interactive density without changing document geometry.

## Native control templates

`EditorTheme` supplies real `ControlTemplate` resources and implicit styles for
the following surface. The native control classes continue to provide value
parsing, selection, text composition, accessibility peers and input semantics.
Named parts and native visual-state names are preserved where those classes
depend on them.

| Family | Retemplated controls | Preserved behavior |
| --- | --- | --- |
| Actions | `Button`, `ToggleButton`, helper repeat buttons | Normal, hover, pressed, checked, indeterminate and disabled states; system keyboard focus visuals |
| Text and numbers | `TextBox`, `NumberBox`, editable `ComboBox` | Native text presentation and IME, clear button, placeholders, expression parsing, spin buttons and popup parts |
| Choices | `CheckBox`, `RadioButton`, `ToggleSwitch` | Checked and mixed states, native toggling and group behavior |
| Menus and overlays | `MenuBar`, `MenuBarItem`, `MenuFlyoutPresenter`, menu items, submenu items, toggle items, separators, `FlyoutPresenter`, `ToolTip` | Native menu navigation, nested flyouts, icons, shortcut labels, dismissal and focus handling |
| Collections | `ListView`, `ListViewItem`, `ComboBoxItem`, `TreeView`, `TreeViewItem` | Selection, focus, tree indentation, expansion, check states and scrolling |
| Tabs and content | `TabView`, `TabViewItem`, internal tab list, `Expander`, `ContentControl` | Close buttons, add button, tab overflow, expanded content and content templates |
| Scrolling and ranges | `ScrollViewer`, `ScrollBar`, `Slider` | Native scroll presenter, scroll commands, horizontal/vertical parts, thumbs and range input |
| Progress | `ProgressBar`, `ProgressRing` | Determinate/indeterminate states and the native animated ring source |
| Dialogs | `ContentDialog` | Native opening and closing lifecycle, title visibility, scrolling content, default action and primary/secondary/close button combinations |
| Color | `ColorPicker`, `ColorSpectrum`, `ColorPickerSlider` | RGB/HSV conversion, alpha and hex fields, previous-color preview, spectrum selection, orientation and visibility states |

The implementation is split across `EditorInputTemplates`,
`EditorMenuTemplates`, `EditorNavigationTemplates`, `EditorDialogTemplates`,
`EditorRangeTemplates` and `EditorColorTemplates`. This table defines the theme's
native control surface; other control types use the host's resources.

Explicit item styles should derive from `EditorListViewItemStyle` or
`EditorTreeViewItemStyle` so that an application-specific container style keeps
the editor template. Buttons can select `StudioButtonStyle`,
`StudioPrimaryButtonStyle`, `StudioQuietButtonStyle` or `StudioDangerButtonStyle`.
For controls constructed in code, use the scope-aware helper:

```csharp
var apply = new Button { Content = "Apply" };
StudioTheme.ApplyRole(apply, StudioActionRole.Primary, _workspace);
```

`ApplyRole` resolves the owning scope before application resources. A standalone
button receives a weakly owned fallback dictionary when necessary. Reapply the
role after intentionally moving a button to a different style scope.

## Reusable editor controls

The controls in
[Controls](../src/Svg.Editor.Skia.Uno/Controls)
share the same palette and metrics:

| Controls | Responsibility |
| --- | --- |
| `StudioIconButton`, `StudioToolButton`, `StudioTabButton` | Compact actions, active tool state, alternate-tool indicators and panel tabs |
| `StudioToolIcon`, `StudioGlyph` | Vector tool artwork and compact indicators with inherited foregrounds; legacy paint/tool mark strings remain supported |
| `StudioSection`, `StudioDockGroup`, `StudioSplitter` | Panel sections, retained dock groups and pointer/keyboard resizing |
| `StudioNumericField`, `StudioTextField` | Validated inspector drafts, commit/cancel behavior and target validation |
| `StudioPaintChip`, `StudioReferencePoint`, `StudioChoiceStrip` | Fill/stroke targeting, the transform reference grid and compact choices |
| `StudioFieldHeader`, `StudioFieldGrid`, `StudioWrapPanel` | Consistent field labels and responsive form arrangement |
| `StudioColorEditor`, `StudioGradientEditor`, `StudioGradientStopBar`, `StudioArrangeEditor` | Existing paint, gradient-stop and arrangement services with transactional editor UI |
| `StudioLayerRow`, `StudioCommandSearch` | Hierarchy presentation and ranked command discovery |
| `StudioSvgPreview`, `StudioNavigator`, `StudioRuler` | SVG previews, shared-renderer navigation and viewport rulers |

Controls with custom `DefaultStyleKey` values have templates in `Generic.xaml`.
Composite editors use those controls and the themed native controls above.
**Help > EditorTheme Control Gallery** exposes representative input, selection,
range, progress, navigation and paint controls for runtime inspection.

## Menus, toolbox and document navigation

The application menu has **File, Edit, Object, Type, Select, Effect, View, Window
and Help**. The desktop menu, compact menu, canvas context menu, command search
and panel actions invoke the same command catalog. Command availability follows
the current document, selection, lock state and operation requirements. Menu
subgroups organize transforms, stacking, alignment, paths, masks, paint
resources, artboards, guides and workspace operations.

The grouped toolbox provides selection and node tools; pen variants; type and
area type; line, shape, spiral and grid creation; pencil, paintbrush and Blob
Brush; smoothing, path erasing and joining; eraser, scissors and knife;
transforms, shear and measurement; deformation brushes; shape building; gradient
and eyedropper; symbol placement and symbol brushes; artboard, hand and zoom.
Right-click, press-and-hold, or **Alt+Down** on a group reveals its alternatives.
Double-click opens applicable tool options. The toolbar supports one or two
columns, and its fill/stroke chips share the active paint target with the Color,
Swatches and Gradient panels.

Paint-target changes validate every retained Color and Gradient draft before
updating any editor. Pending native field text is part of that draft, including
text not yet committed by a `NumberBox`. A rejected switch retains the target
and identifies the editor requiring Apply or Reset. Gradient draft commands act
on the last activated Gradient editor, including the dedicated panel and the
Appearance section. Apply validates visible numeric input through the native
formatter/expression parser before moving focus; invalid or empty values remain
available for correction. Accepted unchanged input does not create a redundant
document edit.

Entries with an ellipsis open the existing focused workflows: width profiles,
blend construction, gradient mesh, type on a path, symbol placement and image
placement. The corresponding canvas tools and dialogs retain their actual
geometry and SVG service behavior. Tool definitions and group membership are in
[SvgEditorWorkspace.EditorTools.cs](../src/Svg.Editor.Skia.Uno/SvgEditorWorkspace.EditorTools.cs).
Action-only entries follow their underlying command's availability. A group
with another usable tool stays accessible, and tool-option dialogs use the same
error-handling path as menus and command search.

Document tabs retain each controller and its undo history. The contextual control
bar reflects the selection. Rulers, status coordinates, zoom controls, outline
display, transparency display and the Navigator share the active canvas viewport.

The browser host awaits the configured native text fonts (regular, semibold and
bold) and symbol font before releasing its first rendered frame. It uses Uno's
public Skia font-preload API, so startup follows actual typeface readiness. Font
load failures are logged and release the startup gate so fallback rendering can
remain available.

Control indicators use vector geometry instead of text-font fallback. This
includes paint states and dropdowns, alternate-tool corners, tool-column and
paint actions, number spinners and dock menus. Their visibility does not depend
on the operating system supplying arrow, triangle or mathematical-symbol
glyphs. Compact geometry targets a one-DIP stroke after internal scaling, with
the native renderer's display-scale rounding, so seven-DIP dropdowns and small
spinners retain their contrast. Text labels use characters available in the
bundled text font; keyboard hints name the arrow keys directly.

## Retained panels and workspace persistence

Panel IDs are stable layout identities. Menu captions can differ from the
underlying ID, for example **Character** opens `typography` and **Pathfinder**
opens `geometry`.

| Panel ID | Window menu caption | Content |
| --- | --- | --- |
| `properties` | Properties | Existing selection inspector and editable SVG properties |
| `layers` | Layers | Document hierarchy, selection, grouping and visibility/lock controls |
| `attributes` | SVG Attributes | Direct SVG attribute inspection and editing |
| `resources` | Document Resources | Document definitions and resource-management commands |
| `transform` | Transform | Position, size, reference point, proportional scaling, relative rotation and reflection |
| `appearance` | Appearance | Fill, stroke, opacity, paint resources, filters, clipping and masks |
| `stroke` | Stroke | Weight, cap, join, miter, dash pattern, width profile and brush access |
| `align` | Align | Selection alignment, distribution and stacking order |
| `geometry` | Pathfinder | Boolean operations, path conversion, simplify, offset, blend and node commands |
| `typography` | Character | Font family, size, plain text and rich-text editing access |
| `effects` | Effects | SVG filter graph, shadow, animation timeline and frame baking |
| `color` | Color | Color editing for the active fill or stroke |
| `gradient` | Gradient | Gradient stops, stop colors, interpolation and geometry controls |
| `swatches` | Swatches | Apply document swatches and open the swatch editor |
| `brushes` | Brushes | Select a document brush for new strokes or apply it to selected paths |
| `symbols` | Symbols | Insert shared symbol instances and create a symbol from selection |
| `transparency` | Transparency | Opacity, alpha/luminance masks and transparency-grid access |
| `paragraph` | Paragraph | Text anchor, letter/word spacing and linked text-frame workflow |
| `artboards` | Artboards | Ordered named regions, precise bounds, create, rename, duplicate, delete and fitting |
| `links` | Links | Linked/embedded image sources, select and locate an image, place an image |
| `graphic-styles` | Graphic Styles | Capture, apply and remove named fill/stroke/opacity styles |
| `navigator` | Navigator | Live overview, viewport navigation, zoom and fit controls |
| `document-info` | Document Info | Root dimensions, source, DOM/selection counts and selected-object metadata |
| `history` | History | Labeled undo/redo operations and navigation through several operations |

Panel content instances survive moves, hiding, collapsing and floating. Layout
rebuilds reparent the retained controls; they do not recreate the SVG document or
discard inspector state. Narrow layouts expose panel access through the same
panel registry. Animation Timeline is a separate editor dialog.
The compact panel rail retains full icon targets when its vertical scrollbar is
visible. Links panel selection and source details follow canvas selection and
clear when the active document has no images.

`SvgWorkspaceLayout` stores dock placement, active tabs, dimensions, visibility
and workspace settings separately from SVG history. The workspace offers
Essentials, Drawing and Typography presets, named saved workspaces, import/export
and reset. Hosts can persist their own copy with `CaptureWorkspaceLayout()` and
restore it with `RestoreWorkspaceLayout(xml)`. Restoring commits a valid pending
inspector edit before applying the new layout.

## Keyboard conventions and profiles

[SvgEditorShortcutCatalog](../src/Svg.Editor.Core/SvgEditorShortcutCatalog.cs)
defines logical gestures. `Primary` means Control on Windows/Linux and Command
on Apple platforms; Alt is displayed as Option on Apple platforms. Document tab
cycling deliberately uses physical **Control+Tab** and **Control+Shift+Tab** on
every desktop platform. Display text is shared by menus, tooltips, command search
and shortcut help.

Gestures match their exact modifier set. Shortcut scope remains part of the
command contract:

| Scope | Dispatch context |
| --- | --- |
| `Application` | Host commands such as saving and command search, subject to dialog and text-composition guards |
| `Document` | Document actions when a native text editor is not consuming the gesture |
| `Canvas` | Tool letters, nudge, temporary navigation and canvas display actions |
| `CanvasOrDocumentTree` | Selection actions explicitly supported by both canvas and hierarchy |

Text and numeric inputs keep typing, selection, composition and local undo.
Popup lists, tree rows, reference-point controls, sliders and the Navigator keep
their own navigation keys. AltGr and unrelated system modifiers are rejected by
normalization, and handled child input is not dispatched again by the workspace.
Native dialogs block document shortcuts. F10 and Alt menu mnemonics open the
native menu interaction on supported desktop hosts.

Representative defaults are **Primary+S** for Save, **Primary+Z** and
**Primary+Shift+Z** for undo/redo, **V** for selection, **A** for direct selection,
**P** for pen, **T** for type, **M** for rectangle, **L** for ellipse, **B** for
paintbrush and **Shift+O** for artboards. **Primary+0** fits the active artboard,
**Primary+Alt+0** fits all artboards and **Primary+1** uses actual size.
**Primary+Shift+F12** opens command search. Canvas arrows nudge; Shift increases
the nudge step. Space temporarily pans, Primary+Space temporarily zooms in and
adding Alt zooms out. Releasing the navigation chord or losing workspace focus
restores the previous tool. Tab and Shift+Tab control panel visibility while the
canvas owns the input. The full default table is in [UnoDraw](unodraw.md).

When a path point, direction handle, polygon vertex or polyline vertex is active,
arrows move only that point and Delete removes the active node. Keyboard movement
is expressed in picture coordinates and converted through the complete scene
transform, including ancestor groups and the document's `viewBox`. Other points,
object identity and transforms remain unchanged. Each effective nudge is one undo
step; stale targets, locked objects and singular transforms cannot mutate geometry.

**Shift+B** selects Blob Brush and **Shift+J** selects Join. Smooth, Path Eraser,
Shear and Measure can be selected from their toolbox groups or command search.
Measure reports root SVG user units without changing artwork or history.
Deformation brushes integrate elapsed hold time, so a stationary pointer keeps
affecting the preview at the same rate as a moving pointer. Warp follows pointer
displacement. A completed brush gesture is one undo entry; cancellation and
window deactivation stop its timer and discard provisional geometry.
The [deformation brush guide](unodraw-deformation.md) describes the shared
geometry APIs, sampling model and processing limits.

**Edit > Keyboard Shortcuts** edits a draft profile, reports conflicts and applies
the accepted changes to routing and displayed hints together. It supports clear,
reset and XML import/export. `SvgEditorShortcutProfile` also exposes `Clone`,
`TryAssign`, `Clear`, `TryResetCommand`, `ResetDefaults`, `Serialize` and `Parse`
for hosts. Profiles override gestures while preserving each command's scope,
repeat policy and availability checks. The versioned XML format accepts up to
512 overrides and 65,536 characters; profile names contain 1–64 printable
characters. Profile settings belong to the application, separately from the SVG
document and its undo stack.

## Document model, coordinates and undo

### Named artboards

Artboards are standard SVG `view` elements in the existing root document:

```xml
<view id="artboard-2"
      viewBox="1240 0 1200 800"
      data-unodraw-artboard="true"
      data-unodraw-name="Alternate" />
```

The metadata marker identifies regions owned by the editor; unrelated imported
`view` elements remain separate. The `viewBox` values are root SVG user units and
the ordered elements define panel order. The renderer still draws the same SVG
document once. These views describe regions and do not add painted objects.

An ordinary imported SVG initially has one implicit artboard derived from its
root viewport. Creating a named collection retains that root region as its first
entry. Renaming the implicit region can materialize it without adding another
region. The panel exposes precise root-unit bounds and explicit options; File >
Document Setup edits the root SVG page and grid settings.

| Controller API | Coordinate/operation contract |
| --- | --- |
| `GetArtboards()` / `ActiveArtboard` | Regions in renderer picture coordinates for canvas interaction |
| `GetArtboardsInRootCoordinates()` | Persisted root-user-unit rectangles for precise numeric editing |
| `AddArtboard(...)` / `SetArtboardBounds(id, ...)` | Picture-coordinate authoring boundary |
| `AddArtboardInRootCoordinates(...)` / `SetArtboardBoundsInRootCoordinates(...)` | Root-coordinate authoring boundary |
| `ActivateArtboard(id)` / `ActivateArtboardAt(point)` | Active UI region, without a document undo entry |
| `RenameArtboard`, `DuplicateArtboard`, `RemoveArtboard`, `ReorderArtboard` | Transactional named-region editing |
| `GetArtboardsBounds()` | Picture-coordinate union used for fitting and the overview |
| `GetArtboardSvg(id)` | An editable SVG snapshot cropped to a region, leaving the source document and history intact |

The overload `SetArtboardBounds(SKRect)` retains its root-viewport editing
contract. Named-region geometry edits enlarge that root envelope when necessary.
The controller compensates the root transform so existing artwork keeps its
mapping relative to the regions, including imported `viewBox`, letterboxing,
percentage root sizes and supported root translation/scaling. Geometry edits
reject a rotated, skewed or singular root transform before modifying the
document. Removing a region keeps artwork and the existing root envelope.
Collections retain at least one region and allow at most 1,000. Malformed,
foreign-namespace and unrelated imported elements remain in their original DOM
positions during edits. An imported collection exceeding the editing limit is
retained, and editing is rejected before mutation.

The artboard tool selects existing regions, moves or resizes the active region,
creates a region by dragging in empty space, and duplicates with Alt-drag.
Preview geometry remains transient until release. Region edits and any required
root-envelope expansion join one controller transaction and produce one undo
entry. Numeric and dialog edits validate that their captured document and target
are still current before applying.

### Guides

`SvgEditorGuideService` stores versioned guide metadata in the root's
`data-unodraw-guides` attribute. Origins and normalized direction vectors are
stored in root user units; `SvgEditorController.GetGuides()` exposes picture
coordinates. Add and move operations invert the current root transform at that
boundary, so imported viewport transforms are not applied twice. The service
validates finite geometry, unique IDs and a maximum of 512 guides.

Ruler dragging previews a guide; Alt changes its orientation. Unlocked guides
can move or be removed by dragging outside the canvas. Escape cancels a preview.
New Guide and Guides from Selection Bounds expose precise and selection-based
creation. Persistent guide edits use controller transactions. Visibility,
locking, snapping and temporary smart-guide indicators are workspace interaction
state.

### Graphic styles, history and Navigator ownership

Graphic Styles uses the existing `AppearanceService` and `StyleEntry` schema:
named fill, stroke and opacity values. Capture, apply and remove operate on that
schema through controller transactions. Applying a style validates its paints,
opacity and the editability of the selected objects.

`Session.UndoLabels` and `Session.RedoLabels` expose operation labels with the
nearest operation first. History rows can undo or redo through an operation;
the panel verifies its controller and document revision after pending inspector
edits are committed. A stale row cannot replay against a different history.
Document edits, style changes, guide metadata and artboard changes belong in
undo history. Theme, docking, active-region selection and viewport navigation
are UI state.

`StudioNavigator.Bind(controller, viewport)` borrows the active controller's
renderer and the canvas viewport. It draws the shared scene with artboard
backgrounds and an overview viewport rectangle. Click/drag pans, the wheel
zooms, arrow keys navigate, Home fits the overview and Escape restores a
canceled drag. Its `NavigationStarting`, `ViewChanged` and `NavigationFailed`
events let a host coordinate pending edits and errors.

The Navigator owns only its rendering surface and paint resources. It detaches
the exact subscribed renderer when the controller replaces that renderer during
undo or reload, then subscribes to the new instance's animation invalidation.
Disposing the Navigator never disposes the borrowed controller or viewport.
`StudioSvgPreview`, by comparison, owns the renderer it creates for its preview
content. This ownership distinction allows live navigation without reparsing a
second document for each frame.

## Validation commands

Use the repository's pinned SDK and initialize external dependencies before
building. The repository-wide gates are:

```sh
git submodule update --init --recursive
dotnet format Svg.Skia.slnx --no-restore
dotnet build Svg.Skia.slnx -c Release
dotnet test Svg.Skia.slnx -c Release
```

The editor and theme checks can also run directly:

```sh
dotnet build samples/UnoDraw/UnoDraw.csproj -f net10.0-desktop -c Release
dotnet test tests/Svg.Editor.Skia.UnitTests/Svg.Editor.Skia.UnitTests.csproj -c Release
python3 -m unittest discover -s tests/UnoDraw.StudioSmoke -p 'test_*.py' -v
```

The native interaction harness requires Xvfb, xdotool, ImageMagick, D-Bus,
X11 utilities and `x11-xkb-utils` on Linux. It initializes an isolated US/pc105
keyboard with plain function keys and records its XKB maps, so older xdotool
versions cannot inject additional modifiers from server-switching key types:

```sh
xvfb-run --auto-servernum \
  --server-args='-screen 0 1600x1100x24 -nolisten tcp' \
  dbus-run-session -- bash tests/UnoDraw.StudioSmoke/editor.sh \
  samples/UnoDraw/bin/Release/net10.0-desktop/UnoDraw.dll \
  artifacts/unodraw-studio
```

The harness exercises real desktop controls and writes screenshots and saved-SVG
assertion records. Its workflow is
[unodraw-studio.yml](../.github/workflows/unodraw-studio.yml). Static resource and
template-contract checks complement those runtime interactions. Use the control
gallery on each target platform to inspect high contrast, keyboard focus,
popup behavior and native text composition.

For the optional browser target, install the matching WebAssembly workload and
opt into its framework explicitly:

```sh
dotnet build samples/UnoDraw/UnoDraw.csproj -c Release \
  -p:UnoDrawTargetFrameworks=net10.0-browserwasm
```

The published-browser workflow runs
[smoke.py](../tests/UnoDraw.BrowserSmoke/smoke.py) against the exact optimized
package under the documentation site's application subfolder. It verifies
startup, command execution, light/dark transitions, reload and PWA scope, then
checks rendered chrome at desktop and compact sizes. Indicator-presence checks
are separate from initial-font stability: two identically blank frames must not
satisfy the presence of paint actions, alternate-tool marks, dropdowns, spinners
or dock controls. Compact rail checks compare the actual desktop and narrow
glyph masks to detect clipping when the scrollbar appears.
