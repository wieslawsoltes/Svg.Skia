#!/usr/bin/env bash
set -euo pipefail

# Keep all established editing, layout and theme checks. The functions and EXIT
# cleanup belong to the same live production application, not a synthetic test UI.
source "$(cd "$(dirname "$0")" && pwd)/themes.sh" "$@"

command 'Reset workspace'
command 'Select all'
command 'Gradient editor'
capture controls-01-gradient-segments
# Deterministic 1440x960 native fixture: the two paint segments are in the top
# Appearance section, whose reveal command pins its header to the viewport top.
xdotool mousemove --window "$window" 1210 228 click 1
xdotool key --clearmodifiers End Home
sleep 0.3
save
verify restored-segment-keyboard-navigation

command 'Reverse gradient draft'
xdotool mousemove --window "$window" 1355 228 click 1
sleep 0.4
capture controls-02-dirty-target-change-blocked
save
verify restored-segment-unapplied-draft
# Applying after the rejected target change must still edit Fill and retain the
# reversed colors. The checker also rejects unrelated source/geometry changes.
xdotool mousemove --window "$window" 1210 346 click 1
sleep 0.5
save
python3 "$source_dir/assert_gradient_target.py" "$document" "$output" fill
capture controls-03-fill-draft-applied
command 'Undo'
save
verify restored-segment-fill-undo

command 'Gradient editor'
xdotool mousemove --window "$window" 1210 228 click 1
xdotool key --clearmodifiers End
sleep 0.3
save
verify restored-segment-stroke-target
capture controls-04-keyboard-stroke-target
command 'Reverse gradient draft'
# Refocus the already-selected Stroke segment, then use the containing editor's
# apply shortcut. A segmented-control handler must not swallow Ctrl+Enter.
xdotool mousemove --window "$window" 1355 228 click 1
xdotool key --clearmodifiers ctrl+Return
sleep 0.5
save
python3 "$source_dir/assert_gradient_target.py" "$document" "$output" stroke
capture controls-05-stroke-keyboard-apply
command 'Undo'
save
verify restored-segment-stroke-undo

command 'Reset workspace'
command 'Properties panel'
xdotool mousemove --window "$window" 1425 125 click 1
sleep 0.4
capture controls-06-dock-panel-switcher
xdotool key --clearmodifiers End Return
sleep 0.5
capture controls-07-hidden-panel-activated
# Scroll the strip without switching its active live panel.
xdotool mousemove --window "$window" 1140 125 click --repeat 3 --delay 150 1
sleep 0.4
capture controls-08-tab-overflow-navigation
command 'Save workspace as'
xdotool type --clearmodifiers --delay 30 'Control navigation review'
xdotool key --clearmodifiers Return
python3 - "$XDG_DATA_HOME/UnoDraw/Workspaces.xml" "$output" <<'PY'
import json, pathlib, sys, time, xml.etree.ElementTree as ET
path, output = map(pathlib.Path, sys.argv[1:])
for _ in range(100):
    try:
        root = ET.parse(path).getroot()
        entry = next((item for item in root.findall('entry') if item.get('name') == 'Control navigation review'), None)
        if entry is not None:
            break
    except (OSError, ET.ParseError):
        pass
    time.sleep(0.1)
else:
    raise SystemExit('The panel navigation workspace was not saved.')
layout = entry.find('workspace')
assert layout is not None
active = {item.get('dock'): item.get('id') for item in layout.findall('active')}
assert active.get('RightTop') == 'effects', ('The picker did not activate its last panel, or scrolling changed selection.', active)
placements = {item.get('id'): item.get('dock') for item in layout.findall('panel')}
assert placements['effects'] == 'RightTop'
assert placements['properties'] == 'RightTop'
assert placements['attributes'] == 'Hidden', 'Switching panels must not unhide unrelated panels.'
checks_path = output / 'checks.json'
checks = json.loads(checks_path.read_text())
checks.append({'check': 'dock-picker-activation-and-scroll-selection', 'passed': True})
checks_path.write_text(json.dumps(checks, indent=2))
(output / 'control-navigation-workspace.xml').write_bytes(path.read_bytes())
PY
save
verify restored-control-panel-navigation
command 'Reset workspace'
command 'Gradient editor'
command 'Fit artboard'
capture controls-09-final-workspace
save
verify restored-after-control-review

if grep -Eiq 'Unhandled exception|DllNotFoundException|EntryPointNotFoundException|XamlParseException|StackOverflowException' "$output/application.log"; then
  cat "$output/application.log"; exit 1
fi
printf '%s\n' 'Segmented target navigation, pending-draft guard, fill/stroke source application, Ctrl+Enter, document undo and dock-picker activation passed. Screenshots require separate visual review; software graphics only.' >"$output/controls-result.txt"
