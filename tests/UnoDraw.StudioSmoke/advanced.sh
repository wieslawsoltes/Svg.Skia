#!/usr/bin/env bash
set -euo pipefail

# Extend (do not replace or weaken) the established native interaction suite. Sourcing
# retains its app process, production-command helpers, isolated storage and EXIT trap.
source "$(cd "$(dirname "$0")" && pwd)/smoke.sh" "$@"

command 'Select all'
command 'Gradient editor'
capture 16-inline-gradient-editor
save
verify restored-gradient-open
command 'Reverse gradient draft'
save
verify restored-gradient-draft
command 'Undo gradient draft'
command 'Redo gradient draft'
command 'Apply gradient draft'
save
python3 - "$document" "$output" <<'PY'
import json, math, pathlib, re, sys, xml.etree.ElementTree as ET
path, output = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
ns = '{http://www.w3.org/2000/svg}'
root = ET.parse(path).getroot()
ids = {}
for node in root.iter():
    identifier = node.get('id')
    if identifier:
        assert identifier not in ids, 'Duplicate SVG ID: ' + identifier
        ids[identifier] = node

def local_property(node, name):
    value = node.get(name)
    important = False
    for declaration in node.get('style', '').split(';'):
        if not declaration.strip(): continue
        key, separator, candidate = declaration.partition(':')
        assert separator and key.strip(), 'Malformed local declaration'
        candidate = candidate.strip()
        marked = bool(re.search(r'\s*!\s*important\s*$', candidate, re.I))
        candidate = re.sub(r'\s*!\s*important\s*$', '', candidate, flags=re.I).strip()
        if key.strip().lower() == name and (not important or marked):
            value, important = candidate, marked
    return value

def stop_offset(stop):
    # SVG permits unit fractions or percentages. SvgGradientStop canonicalizes its
    # serialized Offset to a percentage; compare values, not their lexical spelling.
    text = (stop.get('offset') or '').strip()
    value = float(text[:-1]) / 100 if text.endswith('%') else float(text)
    assert math.isfinite(value) and 0 <= value <= 1, ('Invalid stop offset', text)
    return value

references = []
for name in ('paper', 'card', 'arch', 'sun'):
    assert name in ids and ids[name] in list(root), 'Lost/reparented fixture object'
    reference = local_property(ids[name], 'fill')
    match = re.fullmatch(r'url\(#([^\s()]+)\)', reference or '')
    assert match, (name, reference)
    references.append(match.group(1))
assert len(set(references)) == 1, 'The selected targets must use one authored gradient'
gradient = ids[references[0]]
assert gradient.tag == ns + 'linearGradient'
stops = list(gradient.findall(ns + 'stop'))
assert len(stops) == 2
assert [stop_offset(stop) for stop in stops] == [0.0, 1.0]
expected = ({'#f2b99e', 'rgb(242,185,158)'}, {'#7061dc', 'rgb(112,97,220)'})
for stop, colors in zip(stops, expected):
    color = re.sub(r'\s+', '', local_property(stop, 'stop-color') or '').lower()
    assert color in colors, (color, colors)
    opacity = float(local_property(stop, 'stop-opacity'))
    assert math.isfinite(opacity) and opacity == 1
(output / 'gradient-applied.svg').write_bytes(path.read_bytes())
checks = json.loads((output / 'checks.json').read_text())
checks.append({'check':'inline-gradient-reverse-local-history-and-apply', 'passed':True})
(output / 'checks.json').write_text(json.dumps(checks, indent=2))
PY
capture 17-gradient-applied
command 'Undo'
save
verify restored-gradient-apply
command 'Align panel'
capture 18-advanced-align-panel
command 'Equalize horizontal gaps'
save
python3 - "$document" "$output" <<'PY'
import json, pathlib, sys, xml.etree.ElementTree as ET
path, output = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
root, original = ET.parse(path).getroot(), ET.parse(output / 'baseline.svg').getroot()
def semantic(node): return (node.tag, sorted(node.attrib.items()), (node.text or '').strip(), [semantic(child) for child in node])
assert semantic(root) != semantic(original), 'Spacing did not move the fixture objects'
actual = {node.get('id'): node for node in root.iter() if node.get('id')}
before = {node.get('id'): node for node in original.iter() if node.get('id')}
assert actual.keys() == before.keys(), 'Spacing changed document identities'
for key in actual:
    assert actual[key].tag == before[key].tag
    for name in ('d', 'points', 'width', 'height', 'rx', 'r'):
        assert actual[key].get(name) == before[key].get(name), (key, name)
(output / 'spacing-applied.svg').write_bytes(path.read_bytes())
checks = json.loads((output / 'checks.json').read_text())
checks.append({'check':'native-equal-gap-command', 'passed':True})
(output / 'checks.json').write_text(json.dumps(checks, indent=2))
PY
capture 19-equal-gap-arrangement
command 'Undo'
save
verify restored-advanced-spacing
xdotool windowsize "$window" 480 850
sleep 0.7
command 'Gradient editor'
capture 20-compact-gradient-editor
xdotool key --clearmodifiers Escape
sleep 0.3
xdotool windowsize "$window" 1440 960
command 'Reset workspace'
save
verify restored-after-advanced-panels
if grep -Eiq 'Unhandled exception|DllNotFoundException|EntryPointNotFoundException|XamlParseException|StackOverflowException' "$output/application.log"; then
  cat "$output/application.log"; exit 1
fi
printf '%s\n' 'Original Studio suite plus inline gradients, draft history, saved gradient colors, atomic undo, equal-gap arrangement and compact advanced panels passed. Software graphics only.' >"$output/advanced-result.txt"
