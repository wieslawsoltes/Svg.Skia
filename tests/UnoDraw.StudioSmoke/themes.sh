#!/usr/bin/env bash
set -euo pipefail

# Retain every existing editing and saved-document assertion before visual inspection.
source "$(cd "$(dirname "$0")" && pwd)/advanced.sh" "$@"

panels=('Properties' 'Transform' 'Appearance' 'Stroke' 'Align' 'Geometry' 'Layers' 'Resources' 'Typography' 'Effects' 'SVG attributes')
for appearance in light dark; do
  command "Use $appearance interface"
  command 'Reset workspace'
  command 'Select all'
  index=0
  for panel in "${panels[@]}"; do
    command "$panel panel"
    index=$((index + 1))
    name="$(printf '%02d' "$index")-$(printf '%s' "$panel" | tr ' A-Z' '-a-z')"
    capture "theme-$appearance-$name"
    if [[ "$panel" == 'Transform' ]]; then
      xdotool mousemove --window "$window" 1154 164 click 1
      xdotool key --clearmodifiers Right Down
      sleep 0.3
      capture "theme-$appearance-transform-keyboard"
      save
      verify "restored-transform-reference-$appearance"
    fi
  done
  command 'Gradient editor'
  capture "theme-$appearance-gradient"
  command 'Color mixer'
  capture "theme-$appearance-color"
  command 'Control gallery'
  capture "theme-$appearance-gallery"
  xdotool key --clearmodifiers Tab Tab
  sleep 0.3
  capture "theme-$appearance-gallery-focus"
  # Navigate to the mixed ToggleButton below the enabled/disabled choice strips.
  # Native focus navigation brings the actual control states into the viewport.
  for ((step = 0; step < 21; step++)); do xdotool key --clearmodifiers Tab; done
  sleep 0.4
  capture "theme-$appearance-gallery-lower"
  xdotool key --clearmodifiers Escape
  sleep 0.4
  save
  verify "restored-theme-$appearance"
done

for width in 390 480; do
  xdotool windowsize "$window" "$width" 850
  sleep 0.5
  command 'Gradient editor'
  capture "theme-narrow-$width-gradient"
  xdotool key --clearmodifiers Escape
  sleep 0.3
  command 'Align panel'
  capture "theme-narrow-$width-align"
  xdotool key --clearmodifiers Escape
  sleep 0.3
  command 'Control gallery'
  capture "theme-narrow-$width-gallery"
  for ((step = 0; step < 23; step++)); do xdotool key --clearmodifiers Tab; done
  sleep 0.4
  capture "theme-narrow-$width-gallery-lower"
  xdotool key --clearmodifiers Escape
  sleep 0.4
done
xdotool windowsize "$window" 1440 960
command 'Use dark interface'
command 'Reset workspace'
command 'Gradient editor'
command 'Fit artboard'
capture theme-final-workspace
save
verify restored-after-theme-review

if grep -Eiq 'Unhandled exception|DllNotFoundException|EntryPointNotFoundException|XamlParseException|StackOverflowException' "$output/application.log"; then
  cat "$output/application.log"; exit 1
fi
python3 - "$output" <<'PY'
import json, pathlib, subprocess, sys
output = pathlib.Path(sys.argv[1])
captures = sorted(path.name for path in output.glob('theme-*.png'))
assert len(captures) == 43, ('Incomplete theme inspection captures', len(captures))
brightness = {}
for theme in ('light', 'dark'):
    image = output / ('theme-' + theme + '-01-properties.png')
    samples = []
    for crop in ('6x6+1132+145', '6x6+1350+50'):
        value = subprocess.check_output(['convert', str(image), '-crop', crop, '+repage',
            '-colorspace', 'gray', '-format', '%[fx:mean]', 'info:'], text=True)
        samples.append(float(value))
    brightness[theme] = samples
assert all(value > 0.7 for value in brightness['light']), ('Light surfaces did not update', brightness)
assert all(value < 0.3 for value in brightness['dark']), ('Dark surfaces did not update', brightness)
(output / 'theme-review.json').write_text(json.dumps({
    'captures': captures,
    'panels_per_theme': 11,
    'themes': ['light', 'dark'],
    'compact_widths': [390, 480],
    'document_unchanged': True,
    'live_theme_surface_brightness': brightness,
    'rendering': 'Xvfb software graphics; not physical GPU validation',
    'visual_review': 'Required separately; capture completion alone is not pixel correctness'
}, indent=2))
PY
