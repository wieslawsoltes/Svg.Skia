#!/usr/bin/env bash
set -euo pipefail

# Continue the established production workflow in the same native application.
source "$(cd "$(dirname "$0")" && pwd)/command-center.sh" "$@"

command 'Reset workspace'
command 'Fit artboard'
command 'Deselect'

# Keyboard acceptance must reject a visibly empty required numeric field before
# a new document is created. The first Enter is the regression boundary.
command 'New document'
checkpoint="$(input_checkpoint)"
xdotool key --clearmodifiers ctrl+a BackSpace
wait_input "$checkpoint" 'modal-up key=Back '
checkpoint="$(input_checkpoint)"
xdotool key --clearmodifiers Return
wait_input "$checkpoint" 'modal-numeric-invalid'
capture editor-invalid-document-width
checkpoint="$(input_checkpoint)"
xdotool key --clearmodifiers Escape
wait_input "$checkpoint" 'command-completed id=new$'
save
verify restored-editor-invalid-new-document

# Native menu activation must work even though AccessKey is not implemented by
# the Skia host. Escape returns to the unchanged document.
xdotool key --clearmodifiers alt+f
sleep 0.3
capture editor-menu-file
xdotool key --clearmodifiers Escape
xdotool key --clearmodifiers F10
sleep 0.3
capture editor-menu-keyboard
xdotool key --clearmodifiers Escape

draw() {
  begin_editor_drag 450 340
  xdotool mousemove --window "$window" 630 470
  end_editor_drag
  sleep 0.35
}
check_editor() { python3 "$source_dir/assert_editor.py" "$document" "$output" "$1"; }

# Use unmodified keyboard tool selection, then verify actual saved geometry.
for entry in 'm rectangle' 'l ellipse'; do
  read -r key kind <<< "$entry"
  command 'Deselect'
  key_command "$key" "tool-$kind"
  draw
  save
  check_editor "$kind"
  capture "editor-tool-$kind"
  command 'Undo'
  save
  verify "restored-editor-$kind"
done
command 'tool-star'
draw
save
check_editor star
capture editor-tool-star
command 'Undo'
save
verify restored-editor-star

# A white creation fill cannot merge with this fixture's existing colored fills.
# Test the actual shortcut and saved filled outline, then restore its one edit.
command 'Deselect'
key_command d defaultpaints
key_command shift+b tool-blob-brush
draw
save
check_editor blob-brush
capture editor-tool-blob-brush
command 'Undo'
save
verify restored-editor-blob-brush

# Measurement draws an overlay and updates its result without editing the SVG.
command 'tool-measure'
draw
capture editor-tool-measure
save
verify restored-editor-measure

# Escape during capture must discard the complete provisional gesture.
command 'tool-rectangle'
begin_editor_drag 450 340
xdotool mousemove --window "$window" 650 490
# --clearmodifiers also releases held mouse buttons, which would commit this
# gesture before Escape. Keep the button down until cancellation has completed.
checkpoint="$(input_checkpoint)"
xdotool key Escape
wait_input "$checkpoint" 'command-completed id=cancel-action$'
# Cancellation releases pointer capture, so a later physical release need not
# produce a routed PointerUp. The saved SVG below must still match exactly.
xdotool mouseup 1
save
verify restored-editor-cancelled-drawing

command 'Select all'
key_command ctrl+5 guide-bounds
save
check_editor bounds-guides
capture editor-guides-bounds
command 'Undo'
command 'Deselect'
command 'Fit artboard'
begin_editor_drag 460 111
xdotool mousemove --window "$window" 460 350
end_editor_drag
sleep 0.3
save
check_editor ruler-guide
capture editor-guide-ruler-drag
command 'Undo'
save
verify restored-editor-guides

command 'New artboard'
xdotool type --clearmodifiers --delay 25 'Native board'
xdotool key --clearmodifiers Return
sleep 0.4
save
check_editor artboard
command 'Artboards panel'
capture editor-artboard-created
command 'Duplicate artboard'
save
check_editor artboard-duplicate
key_command ctrl+alt+0 fitall
sleep 0.3
capture editor-artboards-fit-all
command 'Undo'
command 'Undo'
command 'Fit artboard'
save
verify restored-editor-artboards

command 'tool-rectangle'
draw
command 'New graphic style'
xdotool key --clearmodifiers Return
sleep 0.4
save
check_editor style
command 'Graphic Styles panel'
capture editor-graphic-style-created
command 'History panel'
capture editor-history-labels
command 'Undo'
command 'Undo'
save
verify restored-editor-style-history

# Inspect every additional retained panel with real documents and both palettes.
panels=('Color' 'Swatches' 'Gradient' 'Brushes' 'Symbols' 'Transparency' 'Paragraph' 'Artboards' 'Links' 'Graphic Styles' 'Navigator' 'Document Info' 'History')
for appearance in light dark; do
  command "Use $appearance interface"
  for panel in "${panels[@]}"; do
    command "$panel panel"
    name="$(printf '%s' "$panel" | tr ' A-Z' '-a-z')"
    capture "editor-$appearance-$name"
  done
done
command 'Navigator panel'
click_editor_control 1280 225 '' StudioNavigator
xdotool key --clearmodifiers Right Down Home
sleep 0.3
capture editor-navigator-keyboard
save
verify restored-editor-navigator

command 'Fit artboard'
key_command Tab zen
sleep 0.3
capture editor-hidden-panels
key_command Tab zen
key_command shift+Tab workspace-hidepanels
sleep 0.3
capture editor-hidden-side-panels
key_command shift+Tab workspace-hidepanels
command 'Reset workspace'
save
verify restored-after-editor-workflows
# Keep two clean production captures for the workspace documentation, after the
# editing assertions have already proved that the fixture is fully restored.
command 'Reset workspace'
command 'Deselect'
command 'Properties panel'
command 'Fit artboard'
command 'Use light interface'
capture editor-workspace-light
command 'Use dark interface'
capture editor-workspace-dark
printf '%s\n' 'Native menu keys, drawing tools, filled brush, measurement, gesture cancellation, ruler and bounds guides, named artboards, graphic styles, history, navigation and additional panels passed.' > "$output/editor-result.txt"
