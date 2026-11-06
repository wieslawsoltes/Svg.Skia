#!/usr/bin/env bash
set -euo pipefail

# Keep every existing editing, theme, segmented-control and dock-navigation check.
source "$(cd "$(dirname "$0")" && pwd)/controls.sh" "$@"

command 'Use dark interface'
xdotool key --clearmodifiers ctrl+k
sleep 0.4
capture commands-01-recent-dark
xdotool type --clearmodifiers --delay 20 'zzzz no matching command'
sleep 0.3
xdotool key --clearmodifiers Return
sleep 0.3
capture commands-02-no-results
# Enter on no matches must keep search usable, not execute a stale/default result.
xdotool key --clearmodifiers ctrl+a
xdotool type --clearmodifiers --delay 20 'window transform'
sleep 0.3
capture commands-03-category-query
xdotool key --clearmodifiers Return
sleep 0.7
capture commands-04-transform-opened
save
verify restored-command-empty-and-category-query

# Persist UI state through the normal dialog and prove the category query selected
# the intended panel. Do not infer successful command dispatch from a screenshot.
command 'Save workspace as'
xdotool type --clearmodifiers --delay 25 'Command center review'
xdotool key --clearmodifiers Return
python3 - "$XDG_DATA_HOME/UnoDraw/Workspaces.xml" "$output" <<'PY'
import json, pathlib, sys, time, xml.etree.ElementTree as ET
path, output = map(pathlib.Path, sys.argv[1:])
for _ in range(100):
    try:
        root = ET.parse(path).getroot()
        entry = next((item for item in root.findall('entry') if item.get('name') == 'Command center review'), None)
        if entry is not None:
            break
    except (OSError, ET.ParseError):
        pass
    time.sleep(0.1)
else:
    raise SystemExit('The command-center workspace was not saved.')
layout = entry.find('workspace')
assert layout is not None
active = {item.get('dock'): item.get('id') for item in layout.findall('active')}
assert active.get('RightTop') == 'transform', ('Category query selected the wrong panel', active)
checks = json.loads((output / 'checks.json').read_text())
checks.append({'check': 'command-empty-results-and-category-query', 'passed': True})
(output / 'checks.json').write_text(json.dumps(checks, indent=2))
PY

# Search by the displayed shortcut, then invoke the selected command with Enter.
before="$(python3 -c 'import os,sys; print(os.stat(sys.argv[1]).st_mtime_ns)' "$document")"
xdotool key --clearmodifiers ctrl+k
sleep 0.4
xdotool type --clearmodifiers --delay 30 'Ctrl+S'
sleep 0.3
capture commands-05-shortcut-query
xdotool key --clearmodifiers Return
python3 - "$document" "$before" <<'PY'
import os, sys, time
for _ in range(100):
    if os.stat(sys.argv[1]).st_mtime_ns != int(sys.argv[2]):
        break
    time.sleep(0.1)
else:
    raise SystemExit('Shortcut search did not invoke the real Save command.')
PY
verify restored-command-shortcut-query

command 'Use light interface'
xdotool key --clearmodifiers ctrl+k
sleep 0.4
xdotool type --clearmodifiers --delay 25 'gradient'
sleep 0.3
capture commands-06-light-results
xdotool key --clearmodifiers Down Up Page_Down Page_Up
sleep 0.2
capture commands-07-keyboard-selection
xdotool key --clearmodifiers Escape
sleep 0.4
save
verify restored-command-search-cancel

xdotool windowsize "$window" 390 850
sleep 0.5
xdotool key --clearmodifiers ctrl+k
sleep 0.4
capture commands-08-compact-light
xdotool key --clearmodifiers Escape
sleep 0.3
command 'Use dark interface'
xdotool key --clearmodifiers ctrl+k
sleep 0.4
xdotool type --clearmodifiers --delay 25 'align'
sleep 0.3
capture commands-09-compact-dark
xdotool key --clearmodifiers Escape
sleep 0.4
xdotool windowsize "$window" 1440 960
command 'Reset workspace'
command 'Gradient editor'
command 'Fit artboard'
capture commands-10-final-workspace
save
verify restored-after-command-center

if grep -Eiq 'Unhandled exception|DllNotFoundException|EntryPointNotFoundException|XamlParseException|StackOverflowException' "$output/application.log"; then
  cat "$output/application.log"; exit 1
fi
printf '%s\n' 'Command-center no-result Enter, unordered category search, real Save by shortcut, keyboard navigation, cancel and compact themes passed. Software graphics; visual review remains separate.' >"$output/command-center-result.txt"
