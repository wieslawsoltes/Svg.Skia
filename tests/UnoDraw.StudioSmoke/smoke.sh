#!/usr/bin/env bash
set -euo pipefail

# Run inside Xvfb and dbus-run-session. Use a disposable real file so native input
# can be checked through the production save/undo path without application test hooks.
app="$(realpath "${1:?Pass the built UnoDraw.dll}")"
output="${2:?Pass a diagnostic directory}"
mkdir -p "$output"
output="$(realpath "$output")"
work="$(mktemp -d)"
export XDG_DATA_HOME="$work/data"
export XDG_CACHE_HOME="$work/cache"
mkdir -p "$XDG_DATA_HOME" "$XDG_CACHE_HOME"
source_dir="$(cd "$(dirname "$0")" && pwd)"
document="$work/StudioFixture.svg"
cp "$source_dir/StudioFixture.svg" "$document"
dotnet "$app" "$document" >"$output/application.log" 2>&1 &
app_pid=$!
cleanup() {
  cp "$document" "$output/final-document.svg" 2>/dev/null || true
  kill "$app_pid" 2>/dev/null || true
  wait "$app_pid" 2>/dev/null || true
  rm -rf "$work"
}
trap cleanup EXIT

window=""
ready=false
for attempt in $(seq 1 180); do
  kill -0 "$app_pid" 2>/dev/null || { cat "$output/application.log"; exit 1; }
  window="$(xdotool search --onlyvisible --name 'UnoDraw' 2>/dev/null | head -n1 || true)"
  if [[ -n "$window" ]] && [[ "$(xdotool getwindowname "$window")" == *StudioFixture.svg* ]]; then
    ready=true
    break
  fi
  sleep 0.25
done
[[ "$ready" == true ]] || { echo 'The editable fixture did not finish loading.' >&2; cat "$output/application.log"; exit 1; }
xdotool windowsize "$window" 1440 960
xdotool windowfocus --sync "$window"
sleep 1

capture() {
  kill -0 "$app_pid" 2>/dev/null || { cat "$output/application.log"; exit 1; }
  import -window "$window" "$output/$1.png"
  identify "$output/$1.png" >>"$output/captures.txt"
}
command() {
  xdotool windowfocus --sync "$window"
  xdotool key --clearmodifiers ctrl+k
  sleep 0.5
  xdotool type --clearmodifiers --delay 25 "$1"
  sleep 0.35
  xdotool key --clearmodifiers Return
  sleep 0.7
}
save() {
  local before
  before="$(python3 -c 'import os,sys; print(os.stat(sys.argv[1]).st_mtime_ns)' "$document")"
  xdotool key --clearmodifiers ctrl+s
  python3 - "$document" "$before" <<'PY'
import os, sys, time
path, before = sys.argv[1], int(sys.argv[2])
for _ in range(100):
    if os.stat(path).st_mtime_ns != before:
        break
    time.sleep(0.1)
else:
    raise SystemExit('The production Save command did not write the fixture.')
PY
  sleep 0.2
}
verify() {
  python3 - "$document" "$output" "$1" <<'PY'
import json, pathlib, sys, xml.etree.ElementTree as ET
path, output, mode = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), sys.argv[3]
root = ET.parse(path).getroot()
objects = {element.get('id'): element for element in root.iter() if element.get('id')}
def color(value):
    return (value or '').strip().lower().replace(' ', '')
def semantic(element):
    return (element.tag, sorted(element.attrib.items()), (element.text or '').strip(), [semantic(child) for child in element])
if mode == 'baseline':
    (output / 'baseline.svg').write_bytes(path.read_bytes())
elif mode == 'opacity':
    for name in ('paper', 'card', 'arch', 'sun'):
        assert abs(float(objects[name].get('opacity', '1')) - 0.625) < 1e-6, (name, objects[name].attrib)
elif mode in ('default-paints', 'swap-paints'):
    white = {'white', '#fff', '#ffffff', 'rgb(255,255,255)'}
    black = {'black', '#000', '#000000', 'rgb(0,0,0)'}
    expected_fill, expected_stroke = (white, black) if mode == 'default-paints' else (black, white)
    for name in ('card', 'arch', 'sun'):
        assert color(objects[name].get('fill')) in expected_fill, (name, objects[name].attrib)
        assert color(objects[name].get('stroke')) in expected_stroke, (name, objects[name].attrib)
        assert float(objects[name].get('stroke-width', '0')) == 1
elif mode.startswith('restored'):
    baseline = ET.parse(output / 'baseline.svg').getroot()
    assert semantic(root) == semantic(baseline), 'Undo or workspace-only actions changed the SVG document.'
else:
    raise AssertionError('Unknown verification mode: ' + mode)
report_path = output / 'checks.json'
checks = json.loads(report_path.read_text()) if report_path.exists() else []
checks.append({'check': mode, 'passed': True})
report_path.write_text(json.dumps(checks, indent=2))
print('Verified:', mode)
PY
}

command 'Fit artboard'
save
verify baseline
capture 01-essentials-light
command 'Select all'
command 'Appearance panel'
capture 02-mixed-appearance

# The opacity field has a stable location in the 1440-DIP context bar. Ctrl+A must
# select its text, not the document; Tab must commit the field, not hide panels.
xdotool mousemove --window "$window" 480 55 click 1
xdotool key --clearmodifiers ctrl+a
xdotool type --clearmodifiers '62.5'
xdotool key --clearmodifiers Tab
sleep 0.5
save
verify opacity
capture 03-numeric-commit
command 'Undo'
save
verify restored-numeric

command 'Default fill and stroke'
save
verify default-paints
capture 04-default-paints
command 'Swap fill and stroke'
save
verify swap-paints
command 'Undo'
command 'Undo'
save
verify restored-paints

command 'Drawing workspace'
command 'Fit artboard'
command 'Transform panel'
capture 05-drawing-transform
command 'Typography workspace'
command 'Fit artboard'
capture 06-typography-layout
command 'Switch theme'
command 'Appearance panel'
capture 07-appearance-dark

# Float the active panel through its actual header menu, then move its retained
# header. Screenshots document the gesture result for visual review.
xdotool mousemove --window "$window" 1426 91 click 1
sleep 0.4
xdotool key --clearmodifiers Home Down Down Down Down Return
sleep 0.7
capture 08-floating-panel
xdotool mousemove --window "$window" 230 190 mousedown 1
xdotool mousemove --window "$window" 390 240
xdotool mouseup 1
sleep 0.5
capture 09-moved-panel

command 'Reset workspace'
xdotool windowsize "$window" 480 850
sleep 0.7
command 'Layers panel'
capture 10-compact-layers
xdotool key --clearmodifiers Escape
sleep 0.3
command 'Properties panel'
capture 11-compact-properties
xdotool key --clearmodifiers Escape
sleep 0.3
xdotool windowsize "$window" 1440 960
sleep 0.7
command 'Geometry panel'
command 'Fit artboard'
capture 12-restored-geometry
command 'Reset workspace'
save
verify restored-after-layouts
capture 13-restored-essentials

if grep -Eiq 'Unhandled exception|DllNotFoundException|EntryPointNotFoundException|XamlParseException|StackOverflowException' "$output/application.log"; then
  cat "$output/application.log"
  exit 1
fi
kill -0 "$app_pid"
printf '%s\n' 'Native studio smoke passed saved-SVG assertions for numeric editing, paint actions, undo and layout-only operations. Screenshots require visual review; software rendering is not a physical GPU benchmark.' >"$output/result.txt"
