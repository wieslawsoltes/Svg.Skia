"""Exact-size device-pixel checks for the persistent/legacy bridge A/B run."""
from __future__ import annotations

import math
from pathlib import Path
from typing import Any

from PIL import Image, ImageChops


PERSISTENT_INITIALIZED = "UnoDraw canvas bridge initialized: persistent RGBA upload."
LEGACY_PREFERENCE = "UnoDraw canvas bridge preference: legacy raster texture upload."
FALLBACK_PREFIX = "UnoDraw canvas bridge fallback:"
MAXIMUM_CHANGED_FRACTION = 0.001
LUMINANCE_THRESHOLD = 12


def verify_bridge(console: list[dict[str, Any]], bridge: str) -> None:
    messages = [entry.get("text", "") for entry in console]
    if bridge == "persistent":
        if PERSISTENT_INITIALIZED not in messages:
            raise AssertionError("The persistent bridge did not report a successful RGBA upload")
        if any(message.startswith(FALLBACK_PREFIX) for message in messages):
            raise AssertionError("The persistent bridge fell back during its quality comparison")
        if LEGACY_PREFERENCE in messages:
            raise AssertionError("The persistent case unexpectedly selected the legacy bridge")
    elif bridge == "legacy":
        if LEGACY_PREFERENCE not in messages or PERSISTENT_INITIALIZED in messages:
            raise AssertionError("The legacy case did not prove exclusive legacy bridge selection")
    else:
        raise ValueError(f"Unknown canvas bridge: {bridge}")


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
