#!/usr/bin/env bash
# Sourced into the existing native application and isolated SVG fixture.

read_inspector_x() {
  local expected="$1"
  printf '%s' 'waiting-for-inspector-value' | xclip -selection clipboard
  click_editor_control 1180 270 InspectorX TextBox
  xdotool key --clearmodifiers ctrl+a ctrl+c
  python3 - "$expected" "$output" <<'PY'
import json, pathlib, subprocess, sys, time
expected, output = float(sys.argv[1]), pathlib.Path(sys.argv[2])
for _ in range(100):
    value = subprocess.check_output(['xclip', '-selection', 'clipboard', '-o'], text=True).strip()
    try:
        if abs(float(value) - expected) < 0.001:
            break
    except ValueError:
        pass
    time.sleep(0.05)
else:
    raise SystemExit(f'Retained Properties X did not synchronize: expected {expected}, clipboard {value!r}')
path = output / 'checks.json'
checks = json.loads(path.read_text())
checks.append({'check': 'retained-inspector-x-' + str(expected), 'passed': True})
path.write_text(json.dumps(checks, indent=2))
PY
}

command 'Reset workspace'
command 'Select all'
command 'Properties panel'
read_inspector_x 0

# Commit the actual native NumberBox input. Selection churn must retain the
# controls while rebinding values to the current model, including after Undo.
xdotool type --clearmodifiers '17'
xdotool key --clearmodifiers Return
command 'Deselect'
command 'Select all'
read_inspector_x 17

# Geometry edits while Properties is hidden must appear when its tab reopens.
command 'Transform panel'
xdotool key --clearmodifiers Right Right Right
command 'Properties panel'
read_inspector_x 20

# Expanding the current tab is also a visibility transition, without ActiveId
# changing. The inspector must catch up before the next native edit is possible.
click_editor_control 1390 88
command 'Select all'
xdotool key --clearmodifiers Right
click_editor_control 1390 88
read_inspector_x 21
capture editor-retained-inspector-after-hidden-edits
save
for step in 1 2 3 4 5; do command 'Undo'; done
command 'Select all'
command 'Properties panel'
read_inspector_x 0
save
verify restored-retained-inspector-edits

python3 - "$output" <<'PY'
import json, pathlib, re, sys
output = pathlib.Path(sys.argv[1])
created = len(re.findall(r'\binspector-properties-created\b', (output / 'application.log').read_text()))
assert created == 1, f'The same workspace rebuilt its Properties form {created} times'
path = output / 'checks.json'
checks = json.loads(path.read_text())
checks.append({'check': 'one-retained-properties-form', 'passed': True, 'forms': created})
path.write_text(json.dumps(checks, indent=2))
PY
command 'Deselect'
