#!/usr/bin/env python3
"""Verify native node-keyboard edits without accepting whole-object mutations."""
from __future__ import annotations

import copy
import json
import math
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

from assert_svg import SVG, fixture_objects, semantic


NUMBER = r"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?"


def points(element: ET.Element) -> list[tuple[float, float]]:
    if element.tag == SVG + "path":
        source = element.get("d", "")
        tokens = re.findall(r"[ML]|" + NUMBER, source)
        assert not re.sub(r"[ML]|" + NUMBER + r"|[\s,]", "", source), ("Unexpected straight-path data", source)
        assert tokens and tokens[0] == "M" and tokens.count("M") == 1, ("Expected one absolute subpath", tokens)
        values: list[float] = []
        command_values = 0
        for token in tokens[1:]:
            if token == "L":
                assert command_values >= 2 and command_values % 2 == 0, ("Incomplete path command", source)
                command_values = 0
            else:
                values.append(float(token))
                command_values += 1
        assert command_values >= 2 and command_values % 2 == 0, ("Incomplete path command", source)
    else:
        source = element.get("points", "")
        assert not re.sub(NUMBER + r"|[\s,]", "", source), ("Unexpected vertex data", source)
        values = [float(value) for value in re.findall(NUMBER, source)]
    assert values and len(values) % 2 == 0 and all(math.isfinite(value) for value in values), ("Invalid points", source)
    return list(zip(values[::2], values[1::2]))


def added_element(root: ET.Element, baseline: ET.Element, kind: str) -> ET.Element:
    expected = {element.get("id") for element in baseline}
    added = [element for element in root if element.get("id") not in expected]
    assert len(added) == 1, ("The node edit must retain exactly one new object", len(added))
    element = added[0]
    assert element.tag == SVG + kind and element.get("id"), ("Wrong node-editing object", element.tag, element.attrib)
    return element


def assert_node_edit(root: ET.Element, baseline: ET.Element, created: ET.Element, kind: str, operation: str) -> None:
    fixture_objects(root)
    current = added_element(root, baseline, kind)
    original = added_element(created, baseline, kind)
    before, after = points(original), points(current)
    assert len(before) == 4, ("The native fixture must start with four nodes", before)
    geometry_attribute = "d" if kind == "path" else "points"
    assert {key: value for key, value in current.attrib.items() if key != geometry_attribute} == {
        key: value for key, value in original.attrib.items() if key != geometry_attribute
    }, "Editing a node must preserve object identity, paint and transforms"
    assert [semantic(child) for child in current] == [semantic(child) for child in original], "Editing a node changed object children"
    if operation == "created":
        assert after == before
    elif operation == "nudge":
        assert len(after) == len(before), "Nudging a node must not change the point count"
        assert math.isclose(after[0][0], before[0][0] + 1, abs_tol=0.0001)
        assert math.isclose(after[0][1], before[0][1] + 10, abs_tol=0.0001)
        assert after[1:] == before[1:], "Arrow keys moved additional nodes"
    elif operation == "delete":
        assert after == before[1:], ("Delete must remove only the active first node", before, after)
    else:
        raise AssertionError("Unknown node operation: " + operation)
    normalized = copy.deepcopy(root)
    normalized.remove(next(element for element in normalized if element.get("id") == current.get("id")))
    assert semantic(normalized) == semantic(baseline), "The node edit changed unrelated artwork or document properties"


if __name__ == "__main__":
    document, output = map(pathlib.Path, sys.argv[1:3])
    kind, operation = sys.argv[3:5]
    assert kind in {"path", "polygon", "polyline"}
    assert operation in {"created", "nudge", "delete"}
    root, baseline = ET.parse(document).getroot(), ET.parse(output / "baseline.svg").getroot()
    created = root if operation == "created" else ET.parse(output / f"editor-node-{kind}-created.svg").getroot()
    assert_node_edit(root, baseline, created, kind, operation)
    name = f"editor-node-{kind}-{operation}"
    (output / (name + ".svg")).write_bytes(document.read_bytes())
    record = output / "checks.json"
    checks = json.loads(record.read_text())
    checks.append({"check": name, "passed": True})
    record.write_text(json.dumps(checks, indent=2))
    print("Verified: " + name)
