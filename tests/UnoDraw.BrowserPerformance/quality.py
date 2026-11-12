"""Exact-size device-pixel checks and evidence for canvas bridge A/B runs."""
from __future__ import annotations

import math
from pathlib import Path
from typing import Any, Callable

from PIL import Image, ImageChops


PERSISTENT_INITIALIZED = "UnoDraw canvas bridge initialized: persistent RGBA upload."
LEGACY_PREFERENCE = "UnoDraw canvas bridge preference: legacy raster texture upload."
LEGACY_COMPOSITE_PREFERENCE = "UnoDraw canvas bridge preference: legacy-composite raster texture upload."
LEGACY_COMPOSITE_INITIALIZED = "UnoDraw canvas bridge initialized: legacy-composite raster texture upload."
LEGACY_COMPOSITE_REUSED = "UnoDraw legacy-composite canvas bridge reused cached document pixels."
LEGACY_COMPOSITE_FALLBACK_PREFIX = "UnoDraw canvas bridge legacy-composite fallback:"
WEBGL_PREFERENCE = "UnoDraw canvas bridge preference: WebGL document rasterization."
WEBGL_INITIALIZED = "UnoDraw canvas bridge initialized: WebGL document rasterization."
WEBGL_FALLBACK_PREFIX = "UnoDraw WebGL canvas fallback:"
GRAPHITE_PREFERENCE = "UnoDraw canvas bridge preference: Graphite document rasterization."
GRAPHITE_INITIALIZED = "UnoDraw canvas bridge initialized: Graphite document rasterization."
GRAPHITE_FALLBACK_PREFIX = "UnoDraw Graphite canvas fallback:"
GRAPHITE_COVERAGES = ("default", "raster")
GRAPHITE_COVERAGE_PREFIX = "UnoDraw Graphite coverage initialized:"
GRAPHITE_COVERAGE_INITIALIZED = {
    "default": "UnoDraw Graphite coverage initialized: default (InternalMultisampleCount=0).",
    "raster": "UnoDraw Graphite coverage initialized: raster (InternalMultisampleCount=1).",
}
COMPOSITE_PREFERENCE = "UnoDraw canvas bridge preference: cached document composition."
COMPOSITE_INITIALIZED = "UnoDraw canvas bridge initialized: cached document composition."
COMPOSITE_REUSED = "UnoDraw canvas bridge reused cached document pixels."
COMPOSITE_FALLBACK_PREFIX = "UnoDraw canvas bridge composite fallback:"
FALLBACK_PREFIX = "UnoDraw canvas bridge fallback:"
LIVE_BRIDGES = ("legacy", "persistent", "composite", "legacy-composite", "graphite")
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
    if candidate_bridge not in {"persistent", "webgl", "composite", "legacy-composite", "graphite"}:
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
    if candidate_bridge not in {"persistent", "webgl", "composite", "legacy-composite", "graphite"}:
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


def validate_graphite_coverage(bridge: str | None, coverage: str | None) -> None:
    if coverage is not None and (bridge != "graphite" or coverage not in GRAPHITE_COVERAGES):
        raise ValueError("Explicit Graphite coverage requires the Graphite bridge and default or raster coverage")


def verify_bridge(console: list[dict[str, Any]], bridge: str, *, require_reuse: bool = True,
                  graphite_coverage: str | None = None) -> None:
    validate_graphite_coverage(bridge, graphite_coverage)
    messages = [entry.get("text", "") for entry in console]
    coverage_messages = {message for message in messages if message.startswith(GRAPHITE_COVERAGE_PREFIX)}
    if bridge != "graphite" and coverage_messages:
        raise AssertionError("A non-Graphite bridge unexpectedly initialized a Graphite coverage context")
    if bridge == "graphite":
        # Older artifacts have no context-coverage log. An explicit request must
        # prove the actual native context option, including every recreated context.
        if graphite_coverage is not None:
            if coverage_messages != {GRAPHITE_COVERAGE_INITIALIZED[graphite_coverage]}:
                raise AssertionError("The Graphite context did not prove the requested coverage option exclusively")
        elif (len(coverage_messages) > 1
              or not coverage_messages.issubset(GRAPHITE_COVERAGE_INITIALIZED.values())):
            raise AssertionError("The Graphite context reported conflicting or unknown coverage options")
    composite_messages = {COMPOSITE_PREFERENCE, COMPOSITE_INITIALIZED, COMPOSITE_REUSED}
    legacy_composite_messages = {LEGACY_COMPOSITE_PREFERENCE, LEGACY_COMPOSITE_INITIALIZED,
                                 LEGACY_COMPOSITE_REUSED}
    graphite_messages = {GRAPHITE_PREFERENCE, GRAPHITE_INITIALIZED}
    fallback_prefixes = (FALLBACK_PREFIX, WEBGL_FALLBACK_PREFIX, COMPOSITE_FALLBACK_PREFIX,
                         LEGACY_COMPOSITE_FALLBACK_PREFIX, GRAPHITE_FALLBACK_PREFIX)
    if bridge != "legacy-composite" and legacy_composite_messages.intersection(messages):
        raise AssertionError("Another bridge unexpectedly selected legacy cached composition")
    if bridge == "persistent":
        if PERSISTENT_INITIALIZED not in messages:
            raise AssertionError("The persistent bridge did not report a successful RGBA upload")
        if any(message.startswith(fallback_prefixes) for message in messages):
            raise AssertionError("The persistent bridge fell back during its quality comparison")
        if (LEGACY_PREFERENCE in messages or WEBGL_PREFERENCE in messages or WEBGL_INITIALIZED in messages
                or composite_messages.intersection(messages) or graphite_messages.intersection(messages)):
            raise AssertionError("The persistent case unexpectedly selected another bridge")
    elif bridge == "legacy":
        if (LEGACY_PREFERENCE not in messages or PERSISTENT_INITIALIZED in messages
                or WEBGL_PREFERENCE in messages or WEBGL_INITIALIZED in messages
                or composite_messages.intersection(messages) or graphite_messages.intersection(messages)
                or any(message.startswith(fallback_prefixes) for message in messages)):
            raise AssertionError("The legacy case did not prove exclusive legacy bridge selection")
    elif bridge == "webgl":
        if WEBGL_PREFERENCE not in messages or WEBGL_INITIALIZED not in messages:
            raise AssertionError("The WebGL document bridge did not prove selection and a successful copy")
        if (LEGACY_PREFERENCE in messages or composite_messages.intersection(messages)
                or graphite_messages.intersection(messages)
                or any(message.startswith(fallback_prefixes) for message in messages)):
            raise AssertionError("The WebGL document bridge fell back during its comparison")
        # Auxiliary ruler, Navigator and interaction surfaces intentionally keep
        # the persistent CPU upload. Their initialization is not a document fallback.
    elif bridge == "composite":
        if COMPOSITE_PREFERENCE not in messages or COMPOSITE_INITIALIZED not in messages:
            raise AssertionError("The composite bridge did not prove selection and a successful combined upload")
        if PERSISTENT_INITIALIZED not in messages:
            raise AssertionError("The composite bridge did not prove its persistent RGBA upload")
        if (LEGACY_PREFERENCE in messages or WEBGL_PREFERENCE in messages or WEBGL_INITIALIZED in messages
                or graphite_messages.intersection(messages)
                or any(message.startswith(fallback_prefixes) for message in messages)):
            raise AssertionError("The composite bridge fell back during its comparison")
        if require_reuse and COMPOSITE_REUSED not in messages:
            raise AssertionError("The composite bridge never restored cached document pixels")
    elif bridge == "legacy-composite":
        if LEGACY_COMPOSITE_PREFERENCE not in messages or LEGACY_COMPOSITE_INITIALIZED not in messages:
            raise AssertionError("The legacy-composite bridge did not prove selection and a completed original-host copy")
        if (LEGACY_PREFERENCE in messages or PERSISTENT_INITIALIZED in messages
                or WEBGL_PREFERENCE in messages or WEBGL_INITIALIZED in messages
                or composite_messages.intersection(messages) or graphite_messages.intersection(messages)
                or any(message.startswith(fallback_prefixes) for message in messages)):
            raise AssertionError("The legacy-composite bridge fell back or initialized another document transport")
        if require_reuse and LEGACY_COMPOSITE_REUSED not in messages:
            raise AssertionError("The legacy-composite bridge never copied reused cached document pixels")
    elif bridge == "graphite":
        if GRAPHITE_PREFERENCE not in messages or GRAPHITE_INITIALIZED not in messages:
            raise AssertionError("The Graphite document bridge did not prove selection and validated queue completion")
        if (LEGACY_PREFERENCE in messages or WEBGL_PREFERENCE in messages or WEBGL_INITIALIZED in messages
                or composite_messages.intersection(messages)
                or any(message.startswith(fallback_prefixes) for message in messages)):
            raise AssertionError("The Graphite document bridge fell back during its comparison")
        # Auxiliary CPU surfaces may legitimately report persistent initialization.
    else:
        raise ValueError(f"Unknown canvas bridge: {bridge}")


def bridge_query(bridge: str, *, graphite_coverage: str | None = None) -> str:
    # Historical WebGL logs and reports remain verifiable above. The retired
    # document transport is no longer an executable browser-harness option.
    if bridge not in LIVE_BRIDGES:
        raise ValueError(f"Unknown or retired canvas bridge: {bridge}")
    validate_graphite_coverage(bridge, graphite_coverage)
    return "canvasBridge=" + bridge + ("&graphiteCoverage=" + graphite_coverage if graphite_coverage is not None else "")


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
