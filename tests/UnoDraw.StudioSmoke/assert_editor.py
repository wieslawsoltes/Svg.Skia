#!/usr/bin/env python3
"""Validate real saved artwork from the EditorTheme native interaction suite."""
from __future__ import annotations

import copy
import json
import math
import pathlib
import re
import sys
import uuid
import xml.etree.ElementTree as ET

from assert_svg import SVG, fixture_objects, presentation_value, semantic


def view_box_values(value: str) -> list[float]:
    # Both native SvgView metadata and SvgDocument serialization are valid SVG:
    # the former uses spaces, while the latter emits comma/whitespace separators.
    number = r"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?"
    separator = r"(?:[ \t\r\n]+,?[ \t\r\n]*|,[ \t\r\n]*)"
    match = re.fullmatch(r"[ \t\r\n]*(" + number + ")" + (separator + "(" + number + ")") * 3 + r"[ \t\r\n]*", value)
    assert match is not None, ("Expected four SVG viewBox numbers", value)
    values = [float(part) for part in match.groups()]
    assert all(math.isfinite(part) for part in values), ("Nonfinite viewBox", value)
    return values


def assert_editor_change(root: ET.Element, baseline: ET.Element, mode: str) -> None:
    fixture_objects(root)
    expected = {element.get("id") for element in baseline if element.get("id")}
    added = [element for element in root if element.get("id") not in expected]
    normalized = copy.deepcopy(root)
    if mode in ("rectangle", "ellipse", "star", "blob-brush", "style"):
        assert len(added) == 1, ("Expected exactly one newly drawn object", len(added))
        element = added[0]
        expected_tag = {"rectangle": "rect", "ellipse": "ellipse", "star": "path", "blob-brush": "path", "style": "rect"}[mode]
        assert element.tag == SVG + expected_tag, ("Wrong drawing tool", element.tag, expected_tag)
        if expected_tag == "rect":
            assert all(math.isfinite(float(element.get(key, "nan"))) and float(element.get(key, "0")) > 0 for key in ("width", "height"))
        elif expected_tag == "ellipse":
            assert all(math.isfinite(float(element.get(key, "nan"))) and float(element.get(key, "0")) > 0 for key in ("rx", "ry"))
        else:
            path = element.get("d", "")
            assert path and "z" in path.lower(), "The drawing tool must create a closed vector path"
        if mode == "blob-brush":
            assert element.get("id"), "A blob stroke must have a stable object identity"
            fill = re.sub(r"\s+", "", presentation_value(element, "fill") or "").lower()
            assert fill in {"white", "#fff", "#ffffff", "rgb(255,255,255)"}, ("Blob Brush must use the chosen creation fill", fill)
            assert presentation_value(element, "stroke", "none") == "none", "Blob Brush paints a fill without a separate stroke"
        normalized.remove(next(item for item in normalized if item.get("id") == element.get("id")))
    elif mode in ("artboard", "artboard-duplicate"):
        count = 2 if mode == "artboard" else 3
        assert len(added) == count, ("The artboard operation did not create named views", len(added))
        names = set()
        for view in added:
            assert view.tag == SVG + "view" and view.get("data-unodraw-artboard") == "true"
            names.add(view.get("data-unodraw-name"))
            dimensions = view_box_values(view.get("viewBox", ""))
            assert dimensions[2] == 1000 and dimensions[3] == 640
            normalized.remove(next(item for item in normalized if item.get("id") == view.get("id")))
        assert "Native board" in names
        if mode == "artboard-duplicate":
            assert "Native board copy" in names
        expected_width = 2050 if mode == "artboard" else 3074
        assert float(root.get("width", "nan")) == expected_width
        assert float(root.get("height", "nan")) == 640
        assert view_box_values(root.get("viewBox", "")) == [0, 0, expected_width, 640]
        for key in ("width", "height", "viewBox"):
            normalized.set(key, baseline.get(key))
    else:
        assert not added, "Creating guides must not add visible artwork"
    if mode == "style":
        styles = [key for key in root.attrib if key.startswith("data-style-")]
        assert len(styles) == 1, ("Graphic style was not stored", styles)
        assert all(part in root.get(styles[0], "") for part in ("fill:", "stroke:", "opacity:"))
        normalized.attrib.pop(styles[0])
    if mode in ("bounds-guides", "ruler-guide"):
        value = root.get("data-unodraw-guides", "")
        assert value.startswith("1;"), "The guide gesture did not create persistent guide metadata"
        guides = value[2:].split(";")
        assert len(guides) == (4 if mode == "bounds-guides" else 1), ("Wrong guide count", guides)
        for guide in guides:
            fields = guide.split(",")
            assert len(fields) == 5
            uuid.UUID(hex=fields[0])
            values = [float(number) for number in fields[1:]]
            assert all(math.isfinite(number) for number in values)
            assert abs(math.hypot(values[2], values[3]) - 1) < 1e-5
        normalized.attrib.pop("data-unodraw-guides")
    assert semantic(normalized) == semantic(baseline), "The operation changed unrelated artwork or document properties"


if __name__ == "__main__":
    document, output = map(pathlib.Path, sys.argv[1:3])
    mode = sys.argv[3]
    assert mode in {"rectangle", "ellipse", "star", "blob-brush", "style", "bounds-guides", "ruler-guide", "artboard", "artboard-duplicate"}
    assert_editor_change(ET.parse(document).getroot(), ET.parse(output / "baseline.svg").getroot(), mode)
    (output / ("editor-" + mode + "-applied.svg")).write_bytes(document.read_bytes())
    path = output / "checks.json"
    checks = json.loads(path.read_text())
    checks.append({"check": "editor-" + mode, "passed": True})
    path.write_text(json.dumps(checks, indent=2))
    print("Verified: editor-" + mode)
