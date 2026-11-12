"""Exact-size device-pixel checks for the persistent/legacy bridge A/B run."""
from __future__ import annotations

import math
from pathlib import Path
from typing import Any, Callable

from PIL import Image, ImageChops


PERSISTENT_INITIALIZED = "UnoDraw canvas bridge initialized: persistent RGBA upload."
LEGACY_PREFERENCE = "UnoDraw canvas bridge preference: legacy raster texture upload."
WEBGL_PREFERENCE = "UnoDraw canvas bridge preference: WebGL document rasterization."
WEBGL_INITIALIZED = "UnoDraw canvas bridge initialized: WebGL document rasterization."
WEBGL_FALLBACK_PREFIX = "UnoDraw WebGL canvas fallback:"
FALLBACK_PREFIX = "UnoDraw canvas bridge fallback:"
MAXIMUM_CHANGED_FRACTION = 0.001
LUMINANCE_THRESHOLD = 12
STATE_NAMES = ("01-desktop", "02-actual-size", "03-refitted", "03b-brush-overlay",
               "03c-selection-overlay", "04-navigator-visible", "05-navigator-hidden",
               "06-navigator-shown-again", "07-compact", "09-desktop-restored")

# Welcome.svg's near hill, ground and distant hill, then the same fills under
# the Navigator viewport's (148,191,242,28/255) overlay. The tinted near hill has
# G-B == 5: a generic G>B+5 test rejects its actual interior and counts only a
# few incidental edge pixels. Match this known fixture, allowing only tiny
# channel-rounding differences; this does not change any native-image gate.
_WELCOME_GREENS = ((111, 157, 145), (66, 125, 115), (167, 195, 187),
                   (115, 161, 156), (75, 132, 129), (165, 195, 193))
_WELCOME_GREEN_PIXELS = frozenset((red + dr, green + dg, blue + db)
                                for red, green, blue in _WELCOME_GREENS
                                for dr in range(-2, 3) for dg in range(-2, 3) for db in range(-2, 3))


def is_welcome_green(pixel: tuple[int, int, int]) -> bool:
    return pixel in _WELCOME_GREEN_PIXELS


def compare_bridge_states(cases: dict[str, dict[str, Any]], dpr: float,
                          compare: Callable[[dict[str, Any], dict[str, Any]], dict[str, Any]],
                          state_names: tuple[str, ...] = STATE_NAMES, *,
                          candidate_bridge: str = "persistent") -> list[dict[str, Any]]:
    """Compare every completed pair; missing states remain explicit failures."""
    if candidate_bridge not in {"persistent", "webgl"}:
        raise ValueError("The candidate must use a distinct supported canvas bridge")
    states = {bridge: {state["name"]: state for state in case.get("states", [])}
              for bridge, case in cases.items()}
    rows = []
    for name in state_names:
        row: dict[str, Any] = {"state": name, "device_pixel_ratio": dpr, "passed": False}
        missing = [bridge for bridge in ("legacy", candidate_bridge) if name not in states.get(bridge, {})]
        if missing:
            row.update({"missing_bridges": missing, "failure": "State missing from " + ", ".join(missing)})
        else:
            try:
                row.update(compare(states["legacy"][name], states[candidate_bridge][name]))
                row["passed"] = True
            except Exception as error:
                row["failure"] = f"{type(error).__name__}: {error}"
        rows.append(row)
    return rows


def matrix_failures(cases: list[dict[str, Any]], comparisons: list[dict[str, Any]],
                    dprs: tuple[float, ...], state_names: tuple[str, ...] = STATE_NAMES, *,
                    candidate_bridge: str = "persistent") -> list[str]:
    """Continuation must never turn a failed or incomplete matrix into a pass."""
    if candidate_bridge not in {"persistent", "webgl"}:
        raise ValueError("The candidate must use a distinct supported canvas bridge")
    failures = []
    expected_cases = {(bridge, dpr) for bridge in ("legacy", candidate_bridge) for dpr in dprs}
    actual_cases = {(case.get("bridge"), case.get("dpr")) for case in cases}
    if len(cases) != len(expected_cases) or actual_cases != expected_cases:
        failures.append("The required bridge/DPR matrix is incomplete or contains duplicate cases")
    for case in cases:
        label = f"{case.get('bridge')} DPR {case.get('dpr')}"
        if not case.get("passed"):
            failures.append(f"{label}: {case.get('failure', 'case did not pass')}")
        names = [state["name"] for state in case.get("states", [])]
        if len(names) != len(state_names) or set(names) != set(state_names):
            failures.append(f"{label}: required states are missing or duplicated")
    expected_rows = {(dpr, name) for dpr in dprs for name in state_names}
    actual_rows = {(row.get("device_pixel_ratio"), row.get("state")) for row in comparisons}
    if len(comparisons) != len(expected_rows) or actual_rows != expected_rows:
        failures.append("The required per-state comparison matrix is incomplete or contains duplicates")
    for row in comparisons:
        if not row.get("passed"):
            failures.append(f"DPR {row.get('device_pixel_ratio')} {row.get('state')}: "
                            + row.get("failure", "comparison did not pass"))
    return failures


def verify_bridge(console: list[dict[str, Any]], bridge: str) -> None:
    messages = [entry.get("text", "") for entry in console]
    if bridge == "persistent":
        if PERSISTENT_INITIALIZED not in messages:
            raise AssertionError("The persistent bridge did not report a successful RGBA upload")
        if any(message.startswith(FALLBACK_PREFIX) for message in messages):
            raise AssertionError("The persistent bridge fell back during its quality comparison")
        if LEGACY_PREFERENCE in messages or WEBGL_PREFERENCE in messages or WEBGL_INITIALIZED in messages:
            raise AssertionError("The persistent case unexpectedly selected another bridge")
    elif bridge == "legacy":
        if (LEGACY_PREFERENCE not in messages or PERSISTENT_INITIALIZED in messages
                or WEBGL_PREFERENCE in messages or WEBGL_INITIALIZED in messages):
            raise AssertionError("The legacy case did not prove exclusive legacy bridge selection")
    elif bridge == "webgl":
        if WEBGL_PREFERENCE not in messages or WEBGL_INITIALIZED not in messages:
            raise AssertionError("The WebGL document bridge did not prove selection and a successful copy")
        if LEGACY_PREFERENCE in messages or any(message.startswith((FALLBACK_PREFIX, WEBGL_FALLBACK_PREFIX))
                                                for message in messages):
            raise AssertionError("The WebGL document bridge fell back during its comparison")
        # Auxiliary ruler, Navigator and interaction surfaces intentionally keep
        # the persistent CPU upload. Their initialization is not a document fallback.
    else:
        raise ValueError(f"Unknown canvas bridge: {bridge}")


def bridge_query(bridge: str) -> str:
    if bridge not in {"legacy", "persistent", "webgl"}:
        raise ValueError(f"Unknown canvas bridge: {bridge}")
    return "canvasBridge=" + bridge


def assert_native_size(size: tuple[int, int], viewport: dict[str, int], dpr: float) -> None:
    # A fractional CSS dimension can round by one physical edge pixel in Chrome.
    # A CSS-scaled screenshot is never accepted as the DPR>1 quality artifact.
    for actual, css in zip(size, (viewport["width"], viewport["height"])):
        expected = css * dpr
        if actual not in {math.floor(expected), math.ceil(expected)}:
            raise AssertionError(f"Expected native pixels for {viewport} at DPR {dpr}, got {size}")


def raw_comparison(before: Path, after: Path, viewport: dict[str, int], dpr: float,
                   *, navigator: bool = False) -> dict[str, Any]:
    with Image.open(before) as first, Image.open(after) as second:
        assert_native_size(first.size, viewport, dpr)
        assert_native_size(second.size, viewport, dpr)
        if first.size != second.size:
            raise AssertionError("Bridge A/B screenshots must have identical physical dimensions")
        regions: dict[str, tuple[int, int, int, int]] = {"whole_frame": (0, 0, first.width, first.height)}
        if viewport == {"width": 1440, "height": 960}:
            css_regions = {"document": (100, 160, 1050, 880),
                           "horizontal_ruler": (92, 102, 1088, 125),
                           "vertical_ruler": (68, 125, 91, 932)}
            if navigator:
                css_regions["navigator"] = (1100, 120, 1410, 420)
            regions.update({name: tuple(round(value * dpr) for value in bounds)
                            for name, bounds in css_regions.items()})
        original, current = first.convert("RGB"), second.convert("RGB")
        fractions = {}
        for name, bounds in regions.items():
            difference = ImageChops.difference(original.crop(bounds), current.crop(bounds)).convert("L")
            fractions[name] = sum(count for level, count in enumerate(difference.histogram())
                                  if level > LUMINANCE_THRESHOLD) / (difference.width * difference.height)
        failing = {name: value for name, value in fractions.items() if value > MAXIMUM_CHANGED_FRACTION}
        if failing:
            raise AssertionError(f"Native-resolution bridge pixels differ: {failing}")
        return {"physical_size": list(first.size), "device_pixel_ratio": dpr,
                "changed_pixel_fractions": fractions,
                "maximum_changed_fraction": MAXIMUM_CHANGED_FRACTION,
                "luminance_threshold": LUMINANCE_THRESHOLD, "resized_for_comparison": False}
