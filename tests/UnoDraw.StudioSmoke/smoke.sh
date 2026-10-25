#!/usr/bin/env bash
set -euo pipefail

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
startup_stamp="$(python3 -c 'import os,sys; print(os.stat(sys.argv[1]).st_mtime_ns)' "$document")"
window=""
dotnet "$app" "$document" >"$output/application.log" 2>&1 &
app_pid=$!

window_diagnostics() {
  {
    date -u
    xwininfo -root -tree || true
    while read -r candidate; do
      [[ -n "$candidate" ]] || continue
      printf '\nWindow %s\n' "$candidate"
      xdotool getwindowname "$candidate" || true
      xprop -id "$candidate" WM_NAME _NET_WM_NAME WM_CLASS _NET_WM_PID || true
    done < <(xdotool search --onlyvisible --name '.*' 2>/dev/null || true)
  } >>"$output/windows.log" 2>&1
}
cleanup() {
  local status=$?
  trap - EXIT
  if [[ "$status" -ne 0 ]]; then
    window_diagnostics
    timeout 8s import -window root "$output/failure-root.png" 2>/dev/null || true
    if [[ -n "$window" ]]; then timeout 8s import -window "$window" "$output/failure-client.png" 2>/dev/null || true; fi
  fi
  cp "$document" "$output/final-document.svg" 2>/dev/null || true
  if [[ -f "$XDG_DATA_HOME/UnoDraw/Workspaces.xml" ]]; then cp "$XDG_DATA_HOME/UnoDraw/Workspaces.xml" "$output/workspaces.xml"; fi
  kill "$app_pid" 2>/dev/null || true
  wait "$app_pid" 2>/dev/null || true
  rm -rf "$work"
  exit "$status"
}
trap cleanup EXIT

# A mapped native client may keep the initial window caption. Prove the intended
# document is open by saving through its real command path instead of trusting titles.
ready=false
focused=""
for attempt in $(seq 1 180); do
  kill -0 "$app_pid" 2>/dev/null || { cat "$output/application.log"; exit 1; }
  while read -r candidate; do
    [[ -n "$candidate" ]] || continue
    if [[ -z "$window" ]]; then window="$candidate"; fi
    if [[ "$focused" != "$candidate" ]]; then
      xdotool windowsize "$candidate" 1440 960 2>/dev/null || true
      xdotool windowfocus "$candidate" 2>/dev/null || true
      focused="$candidate"
    fi
    if [[ "$attempt" -gt 8 && $((attempt % 8)) -eq 0 ]]; then
      xdotool key --clearmodifiers ctrl+s 2>/dev/null || true
    fi
    if python3 - "$document" "$startup_stamp" <<'PY'
import os, sys, xml.etree.ElementTree as ET
try:
    root = ET.parse(sys.argv[1]).getroot()
    ids = {node.get('id') for node in root.iter()}
    ok = os.stat(sys.argv[1]).st_mtime_ns != int(sys.argv[2]) and {'paper','card','arch','sun'}.issubset(ids)
except (OSError, ET.ParseError):
    ok = False
raise SystemExit(0 if ok else 1)
PY
    then window="$candidate"; ready=true; break; fi
  done < <(xdotool search --onlyvisible --name 'UnoDraw|StudioFixture' 2>/dev/null || true)
  [[ "$ready" == true ]] && break
  if [[ "$attempt" == 10 || "$attempt" == 80 ]]; then window_diagnostics; fi
  sleep 0.25
done
[[ "$ready" == true ]] || { echo 'The application did not save the intended fixture; inspect failure captures and windows.log.' >&2; cat "$output/application.log"; exit 1; }
xdotool windowsize "$window" 1440 960
xdotool windowfocus --sync "$window"
sleep 1
window_diagnostics

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
    if os.stat(path).st_mtime_ns != before: break
    time.sleep(0.1)
else: raise SystemExit('The production Save command did not write the fixture.')
PY
  sleep 0.2
}
verify() {
  python3 "$source_dir/assert_svg.py" "$document" "$output" "$1"
}

command 'Fit artboard'
save
verify baseline
capture 01-essentials-light
command 'Select all'
command 'Appearance panel'
capture 02-mixed-appearance
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

command 'Save workspace as'
xdotool type --clearmodifiers --delay 40 'Drawing desk test'
xdotool key --clearmodifiers Return
python3 - "$XDG_DATA_HOME/UnoDraw/Workspaces.xml" "$output" <<'PY'
import json, pathlib, sys, time, xml.etree.ElementTree as ET
path, output = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
for _ in range(100):
    try:
        root = ET.parse(path).getroot()
        entry = root.find('entry')
        if entry is not None and entry.get('name') == 'Drawing desk test': break
    except (OSError, ET.ParseError): pass
    time.sleep(0.1)
else: raise SystemExit('Save workspace did not persist the named snapshot.')
layout = entry.find('workspace')
assert layout is not None
assert any(p.get('id') == 'layers' and p.get('dock') == 'Left' for p in layout.findall('panel'))
(output / 'saved-workspace.xml').write_bytes(path.read_bytes())
checks = json.loads((output / 'checks.json').read_text())
checks.append({'check':'named-workspace-persistence', 'passed':True})
(output / 'checks.json').write_text(json.dumps(checks, indent=2))
PY
sleep 0.5
command 'Appearance panel'
command 'Restore saved workspace'
capture 06-named-workspace-restored
cmp "$XDG_DATA_HOME/UnoDraw/Workspaces.xml" "$output/saved-workspace.xml"
command 'Color mixer'
capture 07-inline-color-mixer
save
verify restored-color-mixer-open
command 'Typography workspace'
command 'Fit artboard'
capture 08-typography-layout
command 'Switch theme'
command 'Appearance panel'
capture 09-appearance-dark

xdotool mousemove --window "$window" 1426 91 click 1
sleep 0.4
xdotool key --clearmodifiers Home Down Down Down Down Return
sleep 0.7
capture 10-floating-panel
xdotool mousemove --window "$window" 230 190 mousedown 1
xdotool mousemove --window "$window" 390 240
xdotool mouseup 1
sleep 0.5
capture 11-moved-panel
command 'Reset workspace'
xdotool windowsize "$window" 480 850
sleep 0.7
command 'Layers panel'
capture 12-compact-layers
xdotool key --clearmodifiers Escape
sleep 0.3
command 'Properties panel'
capture 13-compact-properties
xdotool key --clearmodifiers Escape
sleep 0.3
xdotool windowsize "$window" 1440 960
sleep 0.7
command 'Geometry panel'
command 'Fit artboard'
capture 14-restored-geometry
command 'Reset workspace'
save
verify restored-after-layouts
capture 15-restored-essentials

if grep -Eiq 'Unhandled exception|DllNotFoundException|EntryPointNotFoundException|XamlParseException|StackOverflowException' "$output/application.log"; then cat "$output/application.log"; exit 1; fi
kill -0 "$app_pid"
printf '%s\n' 'Native studio smoke passed file-readiness, numeric editing, paints, undo, named-workspace persistence and layout-only SVG checks. Screenshot review remains separate; software graphics is not a physical GPU benchmark.' >"$output/result.txt"
