"""Native-pixel and source-integrity contracts for the SVG bridge scene matrix."""
from __future__ import annotations

import base64
import copy
import hashlib
import json
import math
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PIL import Image, ImageChops, ImageColor

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "UnoDraw.BrowserPerformance"))
from quality import (LUMINANCE_THRESHOLD, MAXIMUM_CHANGED_FRACTION, assert_native_size,
                     raw_comparison)


SCENES = ("gradients", "filters", "mask", "clip", "pattern", "text", "image", "opacity-and-strokes")
# These historical defaults preserve validation of the archived rejected WebGL
# experiments. New browser sessions select only LIVE_CANDIDATE_BRIDGES.
BRIDGES = ("persistent", "webgl")
CANDIDATE_BRIDGES = ("webgl", "composite", "graphite", "legacy-composite")
LIVE_CANDIDATE_BRIDGES = ("composite", "graphite", "legacy-composite")
DPRS = (1, 1.25, 2)
STATES = ("01-fit", "02-fractional-pan", "03-fractional-zoom")
DESKTOP = {"width": 1440, "height": 960}
SVG = "http://www.w3.org/2000/svg"
XLINK = "http://www.w3.org/1999/xlink"
BODY = (0, 0, 160, 110)
FIXTURES = Path(__file__).with_name("fixtures") / "svg-bridge"
ET.register_namespace("", SVG)
ET.register_namespace("xlink", XLINK)


@dataclass(frozen=True)
class Fixture:
    name: str
    source: str
    sha256: str
    browser_source: str

    @property
    def browser_sha256(self) -> str:
        return hashlib.sha256(self.browser_source.encode()).hexdigest()


def anchor_colors(name: str) -> tuple[tuple[int, int, int], tuple[int, int, int]]:
    index = SCENES.index(name)
    return (17 + 11 * index, 203 - 7 * index, 241 - 3 * index), (239 - 5 * index, 53 + 9 * index, 173 - 4 * index)


def probes() -> list[dict[str, Any]]:
    result = []
    colors = ((225, 59, 117), (29, 201, 173), (36, 87, 231), (201, 155, 19))
    # Two-decimal inputs survive Svg.NET's fill-opacity serialization exactly.
    # The independent RGB oracle still checks both premultiplied-matte results.
    for row, (color, opacity) in enumerate(zip(colors, (.25, .5, .75, .15))):
        for matte, x, background in (("white", 172, (255, 255, 255)), ("dark", 184, (23, 43, 63))):
            result.append({"name": f"opacity-{opacity}-{matte}", "x": x, "y": 24 + 22 * row,
                           "width": 9, "height": 12, "color": color, "opacity": opacity,
                           "background": background, "group_opacity": row >= 2,
                           "expected": tuple(round(foreground * opacity + backdrop * (1 - opacity))
                                             for foreground, backdrop in zip(color, background))})
    return result


def _color(value: tuple[int, int, int]) -> str:
    return "#" + "".join(f"{channel:02x}" for channel in value)


def browser_document(name: str, source: str, sha256: str) -> str:
    canonical = ET.fromstring(source)
    if canonical.tag != f"{{{SVG}}}svg" or canonical.get("viewBox") != "0 0 160 110":
        raise ValueError("Canonical scenes must retain the native fixture's 160×110 viewBox")
    root = ET.Element(f"{{{SVG}}}svg", {"width": "200", "height": "140", "viewBox": "0 0 200 140",
                                      "id": "bridge-fixture-" + name, "data-fixture-sha256": sha256})
    body = ET.SubElement(root, f"{{{SVG}}}g", {"id": "bridge-scene-" + name})
    for element in canonical:
        body.append(copy.deepcopy(element))
    calibration = ET.SubElement(root, f"{{{SVG}}}g", {"id": "bridge-calibration"})
    for label, y, color in zip(("top", "bottom"), (4, 128), anchor_colors(name)):
        ET.SubElement(calibration, f"{{{SVG}}}rect", {"id": "bridge-anchor-" + label,
                      "x": "170", "y": str(y), "width": "24", "height": "8", "fill": _color(color)})
    for probe in probes():
        geometry = {key: str(probe[key]) for key in ("x", "y", "width", "height")}
        ET.SubElement(calibration, f"{{{SVG}}}rect", {**geometry, "fill": _color(probe["background"])})
        opacity = str(probe["opacity"])
        if probe["group_opacity"]:
            group = ET.SubElement(calibration, f"{{{SVG}}}g", {"opacity": opacity})
            ET.SubElement(group, f"{{{SVG}}}rect", {**geometry, "fill": _color(probe["color"])})
        else:
            ET.SubElement(calibration, f"{{{SVG}}}rect", {**geometry, "fill": _color(probe["color"]),
                                                        "fill-opacity": opacity})
    return ET.tostring(root, encoding="unicode") + "\n"


def load_fixtures(directory: Path = FIXTURES) -> tuple[dict[str, Any], list[Fixture]]:
    manifest = json.loads((directory / "manifest.json").read_text(encoding="utf-8"))
    entries = manifest["fixtures"]
    if [entry["name"] for entry in entries] != list(SCENES):
        raise ValueError("The eight canonical scenes must be complete and uniquely ordered")
    if {path.name for path in directory.glob("*.svg")} != {name + ".svg" for name in SCENES}:
        raise ValueError("Unexpected, missing or renamed canonical scene")
    fixtures = []
    for entry in entries:
        name = entry["name"]
        if entry["file"] != name + ".svg":
            raise ValueError("The canonical fixture path must match its scene")
        raw = (directory / entry["file"]).read_bytes()
        digest = hashlib.sha256(raw).hexdigest()
        if digest != entry["sha256"]:
            raise ValueError(f"Canonical fixture hash changed: {name}")
        source = raw.decode("utf-8")
        fixtures.append(Fixture(name, source, digest, browser_document(name, source, digest)))
    return manifest, fixtures


def normalize_newlines(value: str) -> str:
    return value.replace("\r\n", "\n").replace("\r", "\n")


_NUMBER = r"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?"
_TOKENS = re.compile(_NUMBER + r"|[A-Za-z]+")
_NUMERIC = {"x", "y", "x1", "y1", "x2", "y2", "cx", "cy", "fx", "fy", "r", "rx", "ry",
            "width", "height", "dx", "dy", "opacity", "fill-opacity", "stroke-opacity", "stop-opacity",
            "stroke-width", "stroke-dashoffset", "stroke-dasharray", "font-size", "letter-spacing",
            "offset", "stdDeviation", "values", "viewBox", "d", "transform", "patternTransform"}
_DEFAULTS = {"x": "0", "y": "0", "dx": "0", "dy": "0", "offset": "0", "opacity": "1",
             "fill-opacity": "1", "stroke-opacity": "1", "stop-opacity": "1", "stroke-width": "1",
             "stroke-dashoffset": "0"}


def semantic_attribute(tag: str, key: str, value: str) -> Any:
    value = value.strip()
    if key in {"fill", "stroke", "color", "stop-color", "flood-color"}:
        try:
            return ImageColor.getrgb(value.lower())
        except ValueError:
            return re.sub(r"\s+", "", value)
    if key == f"{{{XLINK}}}href" and value.startswith("data:image/png;base64,"):
        return hashlib.sha256(base64.b64decode(value.split(",", 1)[1], validate=True)).hexdigest()
    if key in {"transform", "patternTransform"}:
        transforms = []
        for operation, arguments in re.findall(r"([A-Za-z]+)\s*\(([^)]*)\)", value):
            numbers = tuple(round(float(token), 6) for token in re.findall(_NUMBER, arguments))
            if operation == "rotate" and len(numbers) == 1:
                numbers += (0, 0)
            elif operation == "translate" and len(numbers) == 1:
                numbers += (0,)
            elif operation == "scale" and len(numbers) == 1:
                numbers *= 2
            transforms.append((operation, numbers))
        return tuple(transforms)
    if key in _NUMERIC:
        if value.endswith("%") and (key == "offset" or tag in {"linearGradient", "radialGradient"}):
            value = str(float(value[:-1]) / 100)
        value = re.sub(r"(?<=\d)px\b", "", value)
        return tuple(round(float(token), 6) if re.fullmatch(_NUMBER, token) else token
                     for token in _TOKENS.findall(value))
    if key == "font-family":
        return value.strip("\"'")
    return value


def _attributes(element: ET.Element) -> dict[str, str]:
    attributes = dict(element.attrib)
    for declaration in attributes.pop("style", "").split(";"):
        if declaration.strip():
            key, value = declaration.split(":", 1)
            attributes[key.strip()] = value.strip()
    return attributes


def _require_tree(actual: ET.Element, expected: ET.Element, location: str) -> None:
    if actual.tag != expected.tag or len(actual) != len(expected):
        raise AssertionError(f"Committed SVG structure differs at {location}")
    tag = expected.tag.rsplit("}", 1)[-1]
    actual_attributes, expected_attributes = _attributes(actual), _attributes(expected)
    for key, value in expected_attributes.items():
        found = actual_attributes.get(key, _DEFAULTS.get(key))
        if found is None or semantic_attribute(tag, key, found) != semantic_attribute(tag, key, value):
            raise AssertionError(f"Committed SVG attribute {key} differs at {location}")
    for key in actual_attributes.keys() - expected_attributes.keys():
        default = "1.1" if tag == "svg" and key == "version" else _DEFAULTS.get(key)
        if default is None or semantic_attribute(tag, key, actual_attributes[key]) != semantic_attribute(tag, key, default):
            raise AssertionError(f"Committed SVG has an unexpected {key} attribute at {location}")
    if tag in {"text", "tspan", "title", "desc"}:
        if normalize_newlines("".join(actual.itertext())).strip() != normalize_newlines("".join(expected.itertext())).strip():
            raise AssertionError(f"Committed SVG text differs at {location}")
    for index, (child, reference) in enumerate(zip(actual, expected)):
        _require_tree(child, reference, f"{location}/{index}")


def require_source_readback(value: str, fixture: Fixture) -> dict[str, Any]:
    actual = ET.fromstring(normalize_newlines(value))
    expected = ET.fromstring(fixture.browser_source)
    if actual.tag != expected.tag:
        raise AssertionError("The reopened source is not an SVG document")
    for key in ("id", "data-fixture-sha256", "width", "height", "viewBox"):
        found = actual.get(key)
        if found is None or semantic_attribute("svg", key, found) != semantic_attribute("svg", key, expected.attrib[key]):
            raise AssertionError(f"The committed scene identity/dimensions differ: {key}")
    for identifier in ("bridge-scene-" + fixture.name, "bridge-calibration"):
        matches = [node for node in actual.iter() if node.get("id") == identifier]
        references = [node for node in expected.iter() if node.get("id") == identifier]
        if len(matches) != 1 or len(references) != 1:
            raise AssertionError("The committed source is missing or duplicating " + identifier)
    _require_tree(actual, expected, "svg")
    return {"scene": fixture.name, "canonical_sha256": fixture.sha256, "semantic_body_verified": True,
            "readback_sha256": hashlib.sha256(normalize_newlines(value).encode()).hexdigest()}


def _color_mask(image: Image.Image, color: tuple[int, int, int], tolerance: int = 2) -> Image.Image:
    channels = image.split()
    masks = [channel.point([255 if abs(value - expected) <= tolerance else 0 for value in range(256)])
             for channel, expected in zip(channels, color)]
    return ImageChops.multiply(ImageChops.multiply(masks[0], masks[1]), masks[2])


def _physical_bounds(geometry: dict[str, float], bounds: tuple[float, float, float, float]) -> tuple[int, int, int, int]:
    x, y, scale = geometry["origin_x"], geometry["origin_y"], geometry["scale"]
    return (math.floor(x + bounds[0] * scale), math.floor(y + bounds[1] * scale),
            math.ceil(x + bounds[2] * scale), math.ceil(y + bounds[3] * scale))


def inspect_scene(path: Path, fixture: Fixture, dpr: float) -> dict[str, Any]:
    with Image.open(path) as original:
        assert_native_size(original.size, DESKTOP, dpr)
        image = original.convert("RGB")
    anchors = []
    for color in anchor_colors(fixture.name):
        mask = _color_mask(image, color)
        bounds = mask.getbbox()
        if bounds is None:
            raise AssertionError("The current scene's opaque calibration anchors are missing")
        width, height = bounds[2] - bounds[0], bounds[3] - bounds[1]
        count = mask.histogram()[255]
        if width < 48 * dpr or height < 16 * dpr or not 2.8 <= width / height <= 3.2 or count < .98 * width * height:
            raise AssertionError(f"The current scene's calibration anchor is incomplete or ambiguous: {bounds}, {count}")
        anchors.append({"bounds": list(bounds), "center_x": (bounds[0] + bounds[2]) / 2,
                        "center_y": (bounds[1] + bounds[3]) / 2, "pixels": count})
    if abs(anchors[0]["center_x"] - anchors[1]["center_x"]) > 1:
        raise AssertionError("Calibration anchors do not share the expected vertical axis")
    scale = (anchors[1]["center_y"] - anchors[0]["center_y"]) / 124
    if not 3.75 * dpr <= scale <= 5.25 * dpr:
        raise AssertionError(f"The scene camera did not fit/zoom to the required native size: {scale / dpr}")
    geometry = {"origin_x": anchors[0]["center_x"] - 182 * scale,
                "origin_y": anchors[0]["center_y"] - 8 * scale, "scale": scale}
    body_bounds = _physical_bounds(geometry, BODY)
    if body_bounds[0] < 90 * dpr or body_bounds[1] < 124 * dpr or body_bounds[2] > 1088 * dpr or body_bounds[3] > 932 * dpr:
        raise AssertionError("The original SVG scene body is clipped by the editor viewport")
    body = image.crop(body_bounds)
    colored = 0
    bins = set()
    for count, pixel in body.getcolors(body.width * body.height) or []:
        if max(pixel) - min(pixel) >= 12 and min(pixel) < 248:
            colored += count
            bins.add(tuple(channel // 16 for channel in pixel))
    fraction = colored / (body.width * body.height)
    minimum = .01 if fixture.name == "text" else .1
    if fraction < minimum or len(bins) < 8:
        raise AssertionError(f"The original {fixture.name} scene body is blank/incomplete: chromatic={fraction}, bins={len(bins)}")
    observations = []
    for probe in probes():
        center_x = geometry["origin_x"] + (probe["x"] + probe["width"] / 2) * scale
        center_y = geometry["origin_y"] + (probe["y"] + probe["height"] / 2) * scale
        radius = max(1, math.floor(scale / 2))
        center = (round(center_x), round(center_y))
        patch = image.crop((center[0] - radius, center[1] - radius, center[0] + radius + 1, center[1] + radius + 1))
        delta = max(abs(channel - expected) for pixel in patch.get_flattened_data()
                    for channel, expected in zip(pixel, probe["expected"]))
        if delta > 2:
            raise AssertionError(f"Compositing/color probe {probe['name']} differs by {delta} channel levels (maximum 2)")
        observations.append({"name": probe["name"], "center": list(center), "expected_rgb": list(probe["expected"]),
                             "center_rgb": list(image.getpixel(center)), "maximum_channel_difference": delta})
    return {"geometry": geometry, "anchors": anchors, "body_physical_bounds": list(body_bounds),
            "body_chromatic_pixels": colored, "body_chromatic_fraction": fraction,
            "body_color_bins": len(bins), "compositing_probes": observations,
            "compositing_maximum_channel_difference": 2, "resized_for_inspection": False}


def compare_scene(before: Path, after: Path, fixture: Fixture, dpr: float,
                  first: dict[str, Any], second: dict[str, Any]) -> dict[str, Any]:
    result = raw_comparison(before, after, DESKTOP, dpr)
    a, b = first["body_physical_bounds"], second["body_physical_bounds"]
    if any(abs(left - right) > 1 for left, right in zip(a, b)):
        raise AssertionError("Matched scene cameras differ by more than one inferred edge pixel")
    bounds = (min(a[0], b[0]), min(a[1], b[1]), max(a[2], b[2]), max(a[3], b[3]))
    with Image.open(before) as original, Image.open(after) as current:
        difference = ImageChops.difference(original.convert("RGB").crop(bounds), current.convert("RGB").crop(bounds)).convert("L")
        fraction = sum(count for level, count in enumerate(difference.histogram()) if level > LUMINANCE_THRESHOLD) / (difference.width * difference.height)
    if fraction > MAXIMUM_CHANGED_FRACTION:
        raise AssertionError(f"Native {fixture.name} body pixels differ: {fraction} > {MAXIMUM_CHANGED_FRACTION}")
    result.update({"scene": fixture.name, "scene_body_physical_bounds": list(bounds),
                   "scene_body_changed_pixel_fraction": fraction, "resized_for_comparison": False})
    return result


def require_camera_change(previous: dict[str, Any], current: dict[str, Any], dpr: float, state: str) -> None:
    before, after = previous["geometry"], current["geometry"]
    if state == "02-fractional-pan":
        if abs(after["scale"] - before["scale"]) > .02 * dpr:
            raise AssertionError("The pan unexpectedly changed zoom")
        for axis, expected in (("origin_x", 37.25), ("origin_y", -18.5)):
            if abs((after[axis] - before[axis]) / dpr - expected) > 1:
                raise AssertionError("The fractional pan did not reach its observed device-pixel position")
    elif state == "03-fractional-zoom":
        ratio = after["scale"] / before["scale"]
        if not 1.04 < ratio < 1.3 or abs(after["scale"] / dpr - round(after["scale"] / dpr)) < .02:
            raise AssertionError("The real wheel input did not produce the required fractional zoom")
    else:
        raise ValueError(state)


def scene_bridges(candidate_bridge: str = "webgl") -> tuple[str, str]:
    if candidate_bridge not in CANDIDATE_BRIDGES:
        raise ValueError("SVG scenes require a supported candidate and its paired CPU control")
    control = "legacy" if candidate_bridge == "legacy-composite" else "persistent"
    return control, candidate_bridge


def matrix_dprs(values: tuple[float, ...] = DPRS) -> tuple[float, ...]:
    """Permit the complete matrix or one complete device-scale portion only."""
    if (not isinstance(values, (tuple, list)) or not values
            or any(isinstance(value, bool) or not isinstance(value, (int, float))
                   or not math.isfinite(value) or value not in DPRS for value in values)):
        raise ValueError("SVG device scales must be 1, 1.25 or 2")
    canonical = tuple(next(expected for expected in DPRS if value == expected) for value in values)
    if len(canonical) != 1 and canonical != DPRS:
        raise ValueError("SVG quality requires all three ordered device scales or exactly one complete DPR shard")
    return canonical


def matrix_failures(cases: list[dict[str, Any]], comparisons: list[dict[str, Any]], *,
                    candidate_bridge: str = "webgl", dprs: tuple[float, ...] = DPRS) -> list[str]:
    dprs = matrix_dprs(dprs)
    failures = []
    expected_cases = {(scene, bridge, dpr) for scene in SCENES
                      for bridge in scene_bridges(candidate_bridge) for dpr in dprs}
    actual_cases = {(case.get("scene"), case.get("bridge"), case.get("dpr")) for case in cases}
    if len(cases) != len(expected_cases) or actual_cases != expected_cases:
        failures.append(f"The required {len(expected_cases)} scene/bridge/DPR cases are missing or duplicated")
    for case in cases:
        label = f"{case.get('scene')} {case.get('bridge')} DPR {case.get('dpr')}"
        if case.get("passed") is not True:
            failures.append(label + ": " + case.get("failure", "case failed"))
        if case.get("source_edit", {}).get("semantic_body_verified") is not True:
            failures.append(label + ": committed source body was not verified")
        if case.get("source_edit", {}).get("exact_inserted_readback") is not True or case.get("source_edit", {}).get("reopened_after_apply") is not True:
            failures.append(label + ": the real source-editor Apply round trip was not verified")
        names = [state.get("name") for state in case.get("states", [])]
        if len(names) != len(STATES) or set(names) != set(STATES):
            failures.append(label + ": required states are missing or duplicated")
    expected_rows = {(scene, dpr, state) for scene in SCENES for dpr in dprs for state in STATES}
    actual_rows = {(row.get("scene"), row.get("dpr"), row.get("state")) for row in comparisons}
    if len(comparisons) != len(expected_rows) or actual_rows != expected_rows:
        failures.append(f"The required {len(expected_rows)} native scene comparisons are missing or duplicated")
    for row in comparisons:
        if row.get("passed") is not True:
            failures.append(f"{row.get('scene')} DPR {row.get('dpr')} {row.get('state')}: " + row.get("failure", "comparison failed"))
    return failures
