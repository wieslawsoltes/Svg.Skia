#!/usr/bin/env python3
"""Observe real document ownership through the native editor's SVG clipboard."""
from __future__ import annotations

import argparse
import json
import math
import pathlib
import subprocess
import time
import xml.etree.ElementTree as ET

from assert_editor import view_box_values
from assert_svg import SVG, semantic


def read_clipboard_svg() -> str:
    deadline = time.monotonic() + 10
    while time.monotonic() < deadline:
        try:
            result = subprocess.run(
                ["xclip", "-selection", "clipboard", "-out"],
                text=True, capture_output=True, timeout=2, check=False,
            )
            if result.returncode == 0 and ET.fromstring(result.stdout).tag == SVG + "svg":
                return result.stdout
        except (subprocess.TimeoutExpired, ET.ParseError):
            pass
        time.sleep(0.1)
    raise AssertionError("The production editor did not copy a current SVG document.")


def assert_blank(root: ET.Element) -> None:
    assert root.tag == SVG + "svg"
    assert float(root.get("width", "nan")) == 1200
    assert float(root.get("height", "nan")) == 800
    assert view_box_values(root.get("viewBox", "")) == [0, 0, 1200, 800]
    assert len(root) == 0, "Closing the final tab must create a new empty document."


def assert_rectangle(root: ET.Element) -> None:
    assert root.tag == SVG + "svg"
    # A newly created document contains only the shape drawn by the harness.
    # Copying stale fixture artwork or an old clipboard cannot satisfy this.
    children = list(root)
    assert len(children) == 1 and children[0].tag == SVG + "rect", "Expected one live rectangle."
    assert children[0].get("id"), "The drawn rectangle must retain its object identity."
    for key in ("width", "height"):
        value = float(children[0].get(key, "nan"))
        assert math.isfinite(value) and value > 0, ("Invalid drawn geometry", key, value)


def assert_retained(root: ET.Element, expected: ET.Element) -> None:
    assert root.tag == SVG + "svg"
    assert semantic(root) == semantic(expected), "The close operation changed the active document or its artwork."


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=pathlib.Path)
    parser.add_argument("name")
    parser.add_argument("mode", choices=("snapshot", "blank", "rectangle", "retained"))
    parser.add_argument("--expected", type=pathlib.Path)
    args = parser.parse_args()
    source = read_clipboard_svg()
    root = ET.fromstring(source)
    (args.output / (args.name + ".svg")).write_text(source)
    if args.mode == "snapshot":
        return
    if args.mode == "blank":
        assert_blank(root)
    elif args.mode == "rectangle":
        assert_rectangle(root)
    else:
        assert args.expected is not None
        assert_retained(root, ET.parse(args.expected).getroot())
    path = args.output / "checks.json"
    checks = json.loads(path.read_text())
    checks.append({"check": args.name, "passed": True})
    path.write_text(json.dumps(checks, indent=2))
    print("Verified:", args.name)


if __name__ == "__main__":
    main()
