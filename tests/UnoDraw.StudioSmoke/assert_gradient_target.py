"""Strict target assertions for the stylesheet-free native Studio fixture."""
from __future__ import annotations
import argparse
import json
import math
import pathlib
import re
import xml.etree.ElementTree as ET
from assert_svg import SVG, PAINT_TARGETS, fixture_objects, semantic


def local_properties(element: ET.Element) -> dict[str, str]:
    values = dict(element.attrib)
    important = set()
    for declaration in values.pop('style', '').split(';'):
        if not declaration.strip():
            continue
        key, separator, value = declaration.partition(':')
        key, value = key.strip().lower(), value.strip()
        if not separator or not re.fullmatch(r'[a-z-]+', key) or re.search(r'[{};\\]', value):
            raise AssertionError('Unsupported fixture declaration: ' + declaration)
        marked = bool(re.search(r'\s*!\s*important\s*$', value, re.I))
        value = re.sub(r'\s*!\s*important\s*$', '', value, flags=re.I).strip()
        if not value or '!' in value or re.search(r'\bvar\s*\(', value, re.I):
            raise AssertionError('Unsupported fixture value: ' + value)
        if key not in important or marked:
            values[key] = value
            if marked:
                important.add(key)
    return values


def assert_source_stops(stops: list[ET.Element]) -> None:
    if len(stops) != 3:
        raise AssertionError('The multiline source workflow must preserve all three default stops.')
    expected_colors = (('#7061dc', 'rgb(112,97,220)'),
                       ('#b2a5ef', 'rgb(178,165,239)'),
                       ('#f2b99e', 'rgb(242,185,158)'))
    for stop, expected, colors in zip(stops, (0.0, 0.5, 1.0), expected_colors):
        offset = stop.get('offset', '')
        number = float(offset[:-1]) / 100 if offset.endswith('%') else float(offset)
        if not math.isfinite(number) or number != expected:
            raise AssertionError('The multiline source changed a default stop position.')
        properties = local_properties(stop)
        color = re.sub(r'\s+', '', properties.get('stop-color', '')).lower()
        if color not in colors:
            raise AssertionError('The multiline source changed a default stop color: ' + color)
        opacity = float(properties.get('stop-opacity', '1'))
        if not math.isfinite(opacity) or opacity != 1:
            raise AssertionError('The multiline source changed a default stop opacity.')


def assert_target(actual: ET.Element, baseline: ET.Element, target: str, mode: str = 'reversed-draft') -> None:
    if target not in ('fill', 'stroke'):
        raise AssertionError('Expected a fill or stroke target.')
    if mode not in ('reversed-draft', 'source-defaults'):
        raise AssertionError('Unknown gradient workflow.')
    objects, originals = fixture_objects(actual), fixture_objects(baseline)
    references = []
    for identifier in PAINT_TARGETS:
        item, original = objects[identifier], originals[identifier]
        properties, before = local_properties(item), local_properties(original)
        reference = properties.pop(target, '')
        before.pop(target, None)
        match = re.fullmatch(r'url\(#([^\s()]+)\)', reference)
        if not match:
            raise AssertionError(identifier + ': the requested ' + target + ' was not a local gradient: ' + reference)
        if properties != before or item.tag != original.tag or [semantic(n) for n in item] != [semantic(n) for n in original]:
            raise AssertionError(identifier + ': editing ' + target + ' changed unrelated source properties.')
        references.append(match.group(1))
    if len(set(references)) != 1:
        raise AssertionError('Selected objects must share the newly authored gradient.')
    gradient = objects.get(references[0])
    if gradient is None or gradient.tag != SVG + 'linearGradient':
        raise AssertionError('The selected target did not resolve to an authored linear gradient.')
    stops = list(gradient.findall(SVG + 'stop'))
    if mode == 'source-defaults':
        assert_source_stops(stops)
        return
    if len(stops) != 2:
        raise AssertionError('The native reverse workflow must preserve its two-stop draft.')
    for stop, expected in zip(stops, (0.0, 1.0)):
        offset = stop.get('offset', '')
        number = float(offset[:-1]) / 100 if offset.endswith('%') else float(offset)
        if not math.isfinite(number) or number != expected:
            raise AssertionError('Unexpected gradient endpoint position.')
    # These are the default mixed-paint draft colors, reversed through a real command.
    colors = [re.sub(r'\s+', '', local_properties(stop).get('stop-color', '')).lower() for stop in stops]
    if colors[0] not in ('#f2b99e', 'rgb(242,185,158)') or colors[1] not in ('#7061dc', 'rgb(112,97,220)'):
        raise AssertionError('A target change lost or reset the reversed draft: ' + repr(colors))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('document', type=pathlib.Path)
    parser.add_argument('output', type=pathlib.Path)
    parser.add_argument('target', choices=('fill', 'stroke'))
    parser.add_argument('--mode', choices=('reversed-draft', 'source-defaults'), default='reversed-draft')
    parser.add_argument('--name', help='Unique check and artifact name for another native workflow.')
    args = parser.parse_args()
    if args.name and not re.fullmatch(r'[a-z0-9]+(?:-[a-z0-9]+)*', args.name):
        parser.error('--name must contain lowercase words or numbers separated by hyphens')
    assert_target(ET.parse(args.document).getroot(), ET.parse(args.output / 'baseline.svg').getroot(), args.target, args.mode)
    name = args.name or 'segmented-' + args.target + '-target-and-draft'
    (args.output / (name + '.svg')).write_bytes(args.document.read_bytes())
    report = args.output / 'checks.json'
    checks = json.loads(report.read_text())
    checks.append({'check': name, 'passed': True})
    report.write_text(json.dumps(checks, indent=2))
    print('Verified:', name)
