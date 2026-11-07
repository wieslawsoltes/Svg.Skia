#!/usr/bin/env bash
set -euo pipefail

# Continue the established production workflow in the same native application.
source "$(cd "$(dirname "$0")" && pwd)/command-center.sh" "$@"

command 'Reset workspace'
command 'Fit artboard'
command 'Deselect'

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
  xdotool mousemove --window "$window" 450 340 mousedown 1
  xdotool mousemove --window "$window" 630 470
  xdotool mouseup 1
  sleep 0.35
}
check_editor() { python3 "$source_dir/assert_editor.py" "$document" "$output" "$1"; }

# Use unmodified keyboard tool selection, then verify actual saved geometry.
for entry in 'm rectangle' 'l ellipse'; do
  read -r key kind <<< "$entry"
  command 'Deselect'
  xdotool key --clearmodifiers "$key"
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

# Escape during capture must discard the complete provisional gesture.
command 'tool-rectangle'
xdotool mousemove --window "$window" 450 340 mousedown 1
xdotool mousemove --window "$window" 650 490
xdotool key --clearmodifiers Escape
xdotool mouseup 1
save
verify restored-editor-cancelled-drawing

command 'Select all'
xdotool key --clearmodifiers ctrl+5
save
check_editor bounds-guides
capture editor-guides-bounds
command 'Undo'
command 'Deselect'
command 'Fit artboard'
xdotool mousemove --window "$window" 460 111 mousedown 1
xdotool mousemove --window "$window" 460 350
xdotool mouseup 1
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
xdotool key --clearmodifiers ctrl+alt+0
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
xdotool mousemove --window "$window" 1280 225 click 1
xdotool key --clearmodifiers Right Down Home
sleep 0.3
capture editor-navigator-keyboard
save
verify restored-editor-navigator

command 'Fit artboard'
xdotool key --clearmodifiers Tab
sleep 0.3
capture editor-hidden-panels
xdotool key --clearmodifiers Tab
xdotool key --clearmodifiers shift+Tab
sleep 0.3
capture editor-hidden-side-panels
xdotool key --clearmodifiers shift+Tab
command 'Reset workspace'
save
verify restored-after-editor-workflows
printf '%s\n' 'Native menu keys, drawing tools, gesture cancellation, ruler and bounds guides, named artboards, graphic styles, history, navigation and additional panels passed.' > "$output/editor-result.txt"
