#!/usr/bin/env bash
set -euo pipefail

# Run inside Xvfb and dbus-run-session. A window manager is not required: focus is
# assigned directly to the client and all captures target the actual app window.
app="${1:?Pass the built UnoDraw.dll}"
output="${2:?Pass a diagnostic directory}"
mkdir -p "$output"
output="$(realpath "$output")"
export XDG_DATA_HOME="$(mktemp -d)"
export XDG_CACHE_HOME="$(mktemp -d)"
dotnet "$app" >"$output/application.log" 2>&1 &
app_pid=$!
cleanup() {
  kill "$app_pid" 2>/dev/null || true
  wait "$app_pid" 2>/dev/null || true
  rm -rf "$XDG_DATA_HOME" "$XDG_CACHE_HOME"
}
trap cleanup EXIT

window=""
for attempt in $(seq 1 100); do
  kill -0 "$app_pid" 2>/dev/null || { cat "$output/application.log"; exit 1; }
  window="$(xdotool search --onlyvisible --name 'UnoDraw' 2>/dev/null | head -n1 || true)"
  [[ -n "$window" ]] && break
  sleep 0.2
done
[[ -n "$window" ]] || { echo 'The workspace window did not appear.' >&2; exit 1; }
xdotool windowsize "$window" 1440 960
xdotool windowfocus --sync "$window"
sleep 3

capture() {
  kill -0 "$app_pid" 2>/dev/null || { cat "$output/application.log"; exit 1; }
  import -window "$window" "$output/$1.png"
  identify "$output/$1.png" >>"$output/captures.txt"
}
command() {
  xdotool windowfocus --sync "$window"
  xdotool key --clearmodifiers ctrl+k
  sleep 0.5
  xdotool type --clearmodifiers --delay 40 "$1"
  sleep 0.4
  xdotool key --clearmodifiers Return
  sleep 1
}

capture 01-essentials-light
command 'Drawing workspace'
capture 02-drawing-layout
command 'Transform panel'
capture 03-transform-panel
command 'Typography workspace'
capture 04-typography-layout
command 'Switch theme'
capture 05-typography-dark
command 'Appearance panel'
capture 06-appearance-dark
command 'Reset workspace'
xdotool windowsize "$window" 480 850
sleep 1
command 'Layers panel'
capture 07-compact-layers
xdotool key --clearmodifiers Escape
sleep 0.5
command 'Properties panel'
capture 08-compact-properties
xdotool key --clearmodifiers Escape
sleep 0.5
xdotool windowsize "$window" 1440 960
sleep 1
command 'Geometry panel'
capture 09-restored-geometry
command 'Reset workspace'
capture 10-restored-essentials

# These are startup/interaction checks, not a pixel-parity or physical GPU gate.
# The screenshots are retained for actual review. Errors must not be silently hidden.
if grep -Eiq 'Unhandled exception|DllNotFoundException|EntryPointNotFoundException|XamlParseException|StackOverflowException' "$output/application.log"; then
  cat "$output/application.log"
  exit 1
fi
kill -0 "$app_pid"
printf '%s\n' 'Native studio interaction smoke completed. Inspect screenshots for visual layout correctness.' >"$output/result.txt"
