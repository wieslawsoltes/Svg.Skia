"""Strict contracts for old/new native-resolution browser rendering comparisons."""
from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any, Callable

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "UnoDraw.BrowserPerformance"))
from metrics import validate_packages  # noqa: E402
from quality import assert_native_size  # noqa: E402


ROLES = ("baseline", "candidate")
BACKENDS = ("webgpu", "skia-preferred")
DPRS = (1.25, 2)
DESKTOP = {"width": 1440, "height": 960}
STATE_NAMES = ("01-desktop", "02-actual-size", "03-pan", "04-refitted", "05-selection")
RULER_REGIONS = {"horizontal": (92, 102, 1088, 125), "vertical": (68, 125, 91, 932)}


def load_packages(directory: Path) -> dict[str, dict[str, Any]]:
    packages = []
    for path in sorted(directory.rglob("benchmark-package.json")):
        package = json.loads(path.read_text(encoding="utf-8"))
        package["path"] = str(path.parent.resolve())
        packages.append(package)
    validate_packages(packages)
    if len(packages) != 2 or {package["role"] for package in packages} != set(ROLES):
        raise ValueError("Render quality requires exactly one baseline and one candidate package")
    for package in packages:
        if package.get("runtime_generation") != "uno7":
            raise ValueError("Old/new UI quality must compare the Uno 7 baseline and candidate")
        if not re.fullmatch(r"[0-9a-f]{40}", package.get("source_sha", "")):
            raise ValueError("Each package must identify its exact source commit")
        index = Path(package["path"]) / "wwwroot/index.html"
        if not index.is_file() or hashlib.sha256(index.read_bytes()).hexdigest() != package.get("index_sha256"):
            raise ValueError("The published index does not match its package manifest")
    if len({package["source_sha"] for package in packages}) != 2:
        raise ValueError("The baseline and candidate must identify different source commits")
    return {package["role"]: package for package in packages}


def save_ruler_strips(raw: Path, dpr: float) -> dict[str, str]:
    """Copy native pixel regions into inspectable artifacts without resampling."""
    strips = {}
    with Image.open(raw) as image:
        assert_native_size(image.size, DESKTOP, dpr)
        for name, bounds in RULER_REGIONS.items():
            target = raw.with_name(f"{raw.stem}-{name}-ruler.png")
            image.crop(tuple(round(value * dpr) for value in bounds)).save(target)
            strips[name] = target.name
    return strips


def compare_states(cases: dict[str, dict[str, Any]], backend: str, dpr: float,
                   compare: Callable[[dict[str, Any], dict[str, Any]], dict[str, Any]]) -> list[dict[str, Any]]:
    states = {role: {state["name"]: state for state in case.get("states", [])}
              for role, case in cases.items()}
    rows = []
    for name in STATE_NAMES:
        row: dict[str, Any] = {"backend": backend, "device_pixel_ratio": dpr, "state": name, "passed": False}
        try:
            for role in ROLES:
                case = cases.get(role, {})
                if (case.get("role"), case.get("backend"), case.get("dpr")) != (role, backend, dpr):
                    raise AssertionError(f"Missing or mismatched {role} backend/DPR case")
                if name not in states.get(role, {}):
                    raise AssertionError(f"Required state {name} is missing from {role}")
            before, after = states["baseline"][name], states["candidate"][name]
            for state in (before, after):
                if state.get("viewport") != DESKTOP or state.get("dpr") != dpr:
                    raise AssertionError("Matched screenshots must use the required desktop viewport and DPR")
            row.update(compare(before, after))
            row["passed"] = True
        except Exception as error:
            row["failure"] = f"{type(error).__name__}: {error}"
        rows.append(row)
    return rows


def matrix_failures(cases: list[dict[str, Any]], comparisons: list[dict[str, Any]]) -> list[str]:
    failures = []
    expected_cases = {(role, backend, dpr) for role in ROLES for backend in BACKENDS for dpr in DPRS}
    actual_cases = {(case.get("role"), case.get("backend"), case.get("dpr")) for case in cases}
    if len(cases) != len(expected_cases) or actual_cases != expected_cases:
        failures.append("The required build/backend/DPR matrix is incomplete or contains duplicate cases")
    for case in cases:
        label = f"{case.get('role')} {case.get('backend')} DPR {case.get('dpr')}"
        if case.get("passed") is not True:
            failures.append(f"{label}: {case.get('failure', 'case did not pass')}")
        names = [state["name"] for state in case.get("states", [])]
        if len(names) != len(STATE_NAMES) or set(names) != set(STATE_NAMES):
            failures.append(f"{label}: required states are missing or duplicated")
    expected_rows = {(backend, dpr, name) for backend in BACKENDS for dpr in DPRS for name in STATE_NAMES}
    actual_rows = {(row.get("backend"), row.get("device_pixel_ratio"), row.get("state")) for row in comparisons}
    if len(comparisons) != len(expected_rows) or actual_rows != expected_rows:
        failures.append("The required matched comparison matrix is incomplete or contains duplicates")
    for row in comparisons:
        if row.get("passed") is not True:
            failures.append(f"{row.get('backend')} DPR {row.get('device_pixel_ratio')} {row.get('state')}: "
                            + row.get("failure", "comparison did not pass"))
    return failures
