#!/usr/bin/env python3
"""Strict saved-SVG assertions for the deterministic native Studio fixture.

This is not a general CSS cascade engine: the fixture has no stylesheets or
inherited paint declarations. Accept either presentation attributes or the inline
style declarations emitted by Svg.Custom, and fail rather than guess when that
fixture contract changes. Actual rendering is inspected separately.
"""
from __future__ import annotations

import argparse
import json
import math
import pathlib
import re
import xml.etree.ElementTree as ET

SVG = "{http://www.w3.org/2000/svg}"
PAINT_TARGETS = ("paper", "card", "arch", "sun")


def presentation_value(element: ET.Element, name: str, default: str | None = None) -> str | None:
    """Read a simple local property, honoring inline precedence and !important.

    The deterministic fixture uses only plain property values. Unsupported CSS
    fails closed, rather than returning an attribute that CSS could override.
    This helper deliberately does not evaluate stylesheets or inheritance.
    """
    value = element.get(name, default)
    important = False
    for declaration in element.get("style", "").split(";"):
        if not declaration.strip():
            continue
        match = re.fullmatch(r"\s*([a-zA-Z-]+)\s*:\s*([^:;{}\\'\"]+)\s*", declaration)
        if not match:
            raise AssertionError(f"Unsupported fixture declaration: {declaration!r}")
        key, candidate = match.groups()
        candidate = candidate.strip()
        marked = re.search(r"\s*!\s*important\s*$", candidate, re.IGNORECASE)
        if marked:
            candidate = candidate[:marked.start()].strip()
        if "!" in candidate or re.search(r"\b(?:var|url)\s*\(", candidate, re.IGNORECASE):
            raise AssertionError(f"Unsupported fixture value: {candidate!r}")
        if key.lower() == name.lower() and (not important or marked):
            value, important = candidate, bool(marked)
    return value


def fixture_objects(root: ET.Element) -> dict[str, ET.Element]:
    if root.tag != SVG + "svg":
        raise AssertionError("The saved fixture is not an SVG document.")
    if any(node.tag in (SVG + "style", SVG + "link") for node in root.iter()):
        raise AssertionError("This local-property fixture must not contain stylesheets.")
    objects: dict[str, ET.Element] = {}
    for element in root.iter():
        identifier = element.get("id")
        if identifier:
            if identifier in objects:
                raise AssertionError(f"Duplicate SVG ID: {identifier}")
            objects[identifier] = element
    for name in PAINT_TARGETS:
        if name not in objects:
            raise AssertionError(f"The saved fixture lost object {name!r}.")
        if objects[name] not in list(root):
            raise AssertionError(f"Fixture object {name!r} is no longer a root child; inspect inherited styles.")
    return objects


def assert_finite_number(actual: str | None, expected: float, label: str) -> None:
    try:
        number = float(actual) if actual is not None else math.nan
    except ValueError as error:
        raise AssertionError(f"{label}: expected {expected}, got {actual!r}") from error
    if not math.isfinite(number) or abs(number - expected) >= 1e-6:
        raise AssertionError(f"{label}: expected {expected}, got {actual!r}")


def assert_opacity(root: ET.Element) -> None:
    for name, element in fixture_objects(root).items():
        if name in PAINT_TARGETS:
            assert_finite_number(presentation_value(element, "opacity", "1"), 0.625, name + " opacity")


def assert_paints(root: ET.Element, swapped: bool = False) -> None:
    objects = fixture_objects(root)
    white = {"white", "#fff", "#ffffff", "rgb(255,255,255)"}
    black = {"black", "#000", "#000000", "rgb(0,0,0)"}
    expected_fill, expected_stroke = (black, white) if swapped else (white, black)
    for name in PAINT_TARGETS:
        element = objects[name]
        for property_name, expected in (("fill", expected_fill), ("stroke", expected_stroke)):
            value = presentation_value(element, property_name)
            normalized = re.sub(r"\s+", "", value or "").lower()
            if normalized not in expected:
                raise AssertionError(f"{name} {property_name}: expected {sorted(expected)}, got {value!r}; {element.attrib}")
        assert_finite_number(presentation_value(element, "stroke-width"), 1, name + " stroke-width")


def semantic(element: ET.Element) -> tuple:
    # Compare all source properties/content/order on undo/layout-only operations.
    # Do not normalize CSS here: those actions must restore the original source.
    return (element.tag, sorted(element.attrib.items()), (element.text or "").strip(),
            (element.tail or "").strip(), [semantic(child) for child in element])


def verify(path: pathlib.Path, output: pathlib.Path, mode: str) -> None:
    root = ET.parse(path).getroot()
    fixture_objects(root)
    if mode == "baseline":
        (output / "baseline.svg").write_bytes(path.read_bytes())
    elif mode == "opacity":
        assert_opacity(root)
    elif mode in ("default-paints", "swap-paints"):
        assert_paints(root, mode == "swap-paints")
    elif mode.startswith("restored-"):
        baseline = ET.parse(output / "baseline.svg").getroot()
        if semantic(root) != semantic(baseline):
            raise AssertionError("Undo or workspace-only actions changed the SVG document.")
    else:
        raise AssertionError("Unknown verification mode: " + mode)
    report_path = output / "checks.json"
    checks = json.loads(report_path.read_text()) if report_path.exists() else []
    checks.append({"check": mode, "passed": True})
    report_path.write_text(json.dumps(checks, indent=2))
    print("Verified:", mode)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("document", type=pathlib.Path)
    parser.add_argument("output", type=pathlib.Path)
    parser.add_argument("mode")
    args = parser.parse_args()
    verify(args.document, args.output, args.mode)
