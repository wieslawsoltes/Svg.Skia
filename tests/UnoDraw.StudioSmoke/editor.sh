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

# The dedicated Gradient panel, Appearance editor, paint chips and canvas share
# one target. Draft commands must act on the most recently activated editor,
# including after command search temporarily owns keyboard focus.
command 'Select all'
command 'Gradient editor'
click_editor_control 1210 218 StudioGradientTarget RadioButton
xdotool key --clearmodifiers Home
command 'Gradient panel'
command 'activepaint'
command 'gradient-reverse-draft'
command 'activepaint'
command 'gradient-apply-draft'
save
python3 "$source_dir/assert_gradient_target.py" "$document" "$output" stroke --name editor-shared-paint-target-and-draft-owner
capture editor-shared-paint-target
command 'Undo'
save
verify restored-editor-shared-paint-target

# A native NumberBox can contain raw text that has not reached its Value yet.
# Ctrl+Enter must preserve/reject an empty required field before focus transfer;
# a subsequent global target change must also preserve the pending draft.
command 'Gradient panel'
click_editor_control 1210 443 StudioGradientPosition TextBox
xdotool key --clearmodifiers ctrl+a BackSpace
checkpoint="$(input_checkpoint)"
xdotool key --clearmodifiers ctrl+Return
wait_input "$checkpoint" 'gradient-numeric-invalid'
capture editor-gradient-invalid-position
save
verify restored-editor-invalid-gradient-position
command 'activepaint'
command 'gradient-reset-draft'
command 'gradient-reverse-draft'
command 'gradient-apply-draft'
save
python3 "$source_dir/assert_gradient_target.py" "$document" "$output" stroke --name editor-raw-gradient-input-target-guard
command 'Undo'
command 'Deselect'
command 'Reset workspace'
save
verify restored-editor-raw-gradient-input

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

# Node keys must preserve the object and every unrelated vertex. Create native
# polygon/polyline and straight-path geometry through normal canvas input, then
# assert the actual saved points after small/large nudges and Delete.
for entry in 'polygon polygon-path polygon-select' 'polyline polyline polyline-select' 'path straight-pen direct'; do
  read -r kind creation selection <<< "$entry"
  command 'Deselect'
  command "tool-$creation"
  for position in '450 340' '630 340' '630 470' '450 470'; do
    read -r x y <<< "$position"
    click_editor_control "$x" "$y"
  done
  key_command Return finishpath
  save
  python3 "$source_dir/assert_node_editing.py" "$document" "$output" "$kind" created
  command "tool-$selection"
  click_editor_control 450 340
  key_command Right nudge-right
  key_command shift+Down nudge-down-large
  save
  python3 "$source_dir/assert_node_editing.py" "$document" "$output" "$kind" nudge
  capture "editor-node-$kind-nudged"
  command 'Undo'
  command 'Undo'
  click_editor_control 450 340
  key_command Delete delete
  save
  python3 "$source_dir/assert_node_editing.py" "$document" "$output" "$kind" delete
  capture "editor-node-$kind-deleted"
  command 'Undo'
  command 'Undo'
  save
  verify "restored-editor-node-$kind"
done

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
accept_dialog artboard-new
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
accept_dialog style-capture
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
capture editor-navigator-ready
navigator_checkpoint="$(input_checkpoint)"
xdotool key --clearmodifiers KP_Add Right Down Home
sleep 0.3
capture editor-navigator-keyboard
python3 "$source_dir/assert_navigator.py" "$output/application.log" "$navigator_checkpoint" --minimum-updates 4 > "$output/navigator-performance.json"
python3 - "$output" <<'PY'
import json, pathlib, sys
output = pathlib.Path(sys.argv[1])
path = output / 'checks.json'
checks = json.loads(path.read_text())
checks.append(json.loads((output / 'navigator-performance.json').read_text()))
path.write_text(json.dumps(checks, indent=2))
PY
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
command 'tool-select'
command 'Deselect'
command 'Properties panel'
command 'Fit artboard'
command 'Use light interface'
capture editor-workspace-light
command 'Use dark interface'
capture editor-workspace-dark

# A numeric character reference keeps the model's text line break as LF even
# though the native multiline editor normalizes its displayed value to CR.
# Merely visiting Typography must not create a second undo transaction.
command 'Edit SVG source'
xdotool mousemove --window "$window" 720 420 click 1
xdotool key --clearmodifiers ctrl+a
xdotool type --clearmodifiers --delay 25 '<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100" viewBox="0 0 200 100"><text id="multiline" x="20" y="40">First&#10;Second</text></svg>'
capture editor-multiline-source
checkpoint="$(input_checkpoint)"
xdotool mousemove --window "$window" 580 742 click 1
wait_input "$checkpoint" 'modal-closed result=Primary$'
wait_input "$checkpoint" 'command-completed id=source$'
command 'Select all'
command 'Typography panel'
command 'Deselect'
command 'Undo'
save
verify restored-editor-multiline-focus

# Native multiline TextBox input normalizes line endings. Author the complete
# default stop list through the source dialog, then restore the saved artwork.
command 'Select all'
command 'gradient-source-create'
capture editor-gradient-source-stops
accept_dialog gradient-source-create
save
python3 "$source_dir/assert_gradient_target.py" "$document" "$output" fill --mode source-defaults --name editor-gradient-source-default-stops
command 'Undo'
command 'Deselect'
save
verify restored-editor-gradient-source

# Exercise close ownership after the existing fixture checks and documentation
# captures. Clipboard SVG observes the real controller through production Copy
# and source-editor commands; each capture clears old clipboard content first.
copy_document() {
  printf '%s' 'waiting-for-current-svg' | xclip -selection clipboard
  command 'Select all'
  command 'Copy'
  python3 "$source_dir/assert_document_lifecycle.py" "$output" "$@"
}
copy_source_document() {
  printf '%s' 'waiting-for-current-svg' | xclip -selection clipboard
  command 'Edit SVG source'
  # The source editor is the central native TextBox in the modal content.
  xdotool mousemove --window "$window" 720 420 click 1
  xdotool key --clearmodifiers ctrl+a ctrl+c
  python3 "$source_dir/assert_document_lifecycle.py" "$output" "$@"
  local checkpoint
  checkpoint="$(input_checkpoint)"
  xdotool key --clearmodifiers Escape
  wait_input "$checkpoint" 'command-completed id=source$'
}

# Opening the multiline source editor must preserve the complete saved fixture.
copy_source_document editor-source-retains-document retained --expected "$document"

# A failed save during close must preserve the live dirty document. A directory
# at the isolated fixture's output path deterministically rejects atomic replace
# without changing filesystem permissions or depending on a native file picker.
command 'tool-rectangle'
draw
copy_document editor-close-before-failed-save snapshot
mv "$document" "$work/close-save-backup.svg"
mkdir "$document"
command 'Close document'
checkpoint="$(input_checkpoint)"
xdotool key --clearmodifiers Return
wait_input "$checkpoint" 'modal-closed result=Primary$'
wait_input "$checkpoint" 'modal-opened'
capture editor-close-save-error
checkpoint="$(input_checkpoint)"
xdotool key --clearmodifiers Escape
wait_input "$checkpoint" 'command-completed id=close$'
rmdir "$document"
mv "$work/close-save-backup.svg" "$document"
copy_document editor-close-failed-save-retains-document retained --expected "$output/editor-close-before-failed-save.svg"
command 'Undo'
save
verify restored-editor-failed-close-save

command 'New document'
accept_dialog new
command 'Fit artboard'
command 'tool-rectangle'
draw
copy_document editor-close-new-document-live rectangle

# Uno's native TabView supports middle-click close without activating the tab.
# Remove the saved fixture's inactive first tab while the new drawing is active.
checkpoint="$(input_checkpoint)"
xdotool mousemove --window "$window" 110 86 click 2
wait_input "$checkpoint" 'workspace-pointer-up-focus '
capture editor-close-inactive-tab
xdotool key --clearmodifiers ctrl+Tab
copy_document editor-close-inactive-retains-active retained --expected "$output/editor-close-new-document-live.svg"

command 'Close document'
checkpoint="$(input_checkpoint)"
xdotool key --clearmodifiers Escape
wait_input "$checkpoint" 'command-completed id=close$'
copy_document editor-close-cancel-retains-document retained --expected "$output/editor-close-new-document-live.svg"

# The default Save button initially owns focus. Tab reaches Discard changes;
# require the real Secondary result before inspecting the replacement document.
command 'Close document'
checkpoint="$(input_checkpoint)"
xdotool key --clearmodifiers Tab Return
wait_input "$checkpoint" 'modal-closed result=Secondary$'
wait_input "$checkpoint" 'command-completed id=close$'
copy_source_document editor-close-last-dirty-creates-document blank
command 'Fit artboard'
command 'tool-rectangle'
draw
copy_document editor-close-replacement-live rectangle
command 'Undo'
command 'Close document'
copy_source_document editor-close-last-clean-creates-document blank
capture editor-close-final-workspace

if grep -Eiq 'Unhandled exception|DllNotFoundException|EntryPointNotFoundException|XamlParseException|StackOverflowException' "$output/application.log"; then
  cat "$output/application.log"; exit 1
fi
printf '%s\n' 'Native menus, editing tools, shared controls, retained panels, Navigator, and active/inactive/final document close ownership passed, including canceled close and failed save.' > "$output/editor-result.txt"
