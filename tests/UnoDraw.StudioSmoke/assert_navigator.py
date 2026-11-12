#!/usr/bin/env python3
"""Verify camera navigation retains the raster overview from opt-in native traces."""
from __future__ import annotations

import argparse
import json
import math
import pathlib
import re
import time


EVENT = re.compile(
    r"navigator-(?:(overview-render) id=(\d+) count=(\d+)"
    r"|(viewport-update) id=(\d+) overviewFrames=(\d+) visible=(True|False))"
)
GEOMETRY = re.compile(
    r" camera=(\S+) viewport=(\S+) overview=(\S+) artboards=(\S+) indicator=(\S+)"
)


def verify_geometry(text: str) -> tuple[float, ...]:
    match = GEOMETRY.search(text)
    assert match, "Navigator camera update is missing overlay geometry"
    camera, viewport, overview, artboards, indicator = (
        tuple(map(float, values.split(","))) for values in match.groups())
    assert tuple(map(len, (camera, viewport, overview, artboards, indicator))) == (3, 2, 2, 4, 4), "Invalid overlay geometry"
    assert all(math.isfinite(value) for values in (camera, viewport, overview, artboards, indicator)
               for value in values), "Non-finite overlay geometry"
    offset_x, offset_y, zoom = camera
    width, height = viewport
    preview_width, preview_height = overview
    left, top, right, bottom = artboards
    assert zoom > 0 and right > left and bottom > top and preview_width > 16 and preview_height > 16
    scale = min((preview_width - 16) / (right - left), (preview_height - 16) / (bottom - top))
    origin_x = (preview_width - (right - left) * scale) / 2
    origin_y = (preview_height - (bottom - top) * scale) / 2
    visible = (max(left, -offset_x / zoom), max(top, -offset_y / zoom),
               min(right, (width - offset_x) / zoom), min(bottom, (height - offset_y) / zoom))
    assert visible[2] > visible[0] and visible[3] > visible[1], "Visible overlay has no document intersection"
    # Native rectangle strokes are inset. Layout grows by half the 1.5 DIP stroke
    # on each edge to retain the former centered Skia outline and clipped fill.
    expected = (origin_x + (visible[0] - left) * scale - 0.75,
                origin_y + (visible[1] - top) * scale - 0.75,
                (visible[2] - visible[0]) * scale + 1.5,
                (visible[3] - visible[1]) * scale + 1.5)
    assert all(math.isclose(actual, wanted, rel_tol=1e-5, abs_tol=0.001)
               for actual, wanted in zip(indicator, expected)), ("Viewport indicator does not match camera", indicator, expected)
    return indicator


def verify_navigation(text: str, minimum_updates: int = 3) -> dict:
    events = list(EVENT.finditer(text))
    updates = [event for event in events if event.group(4)]
    assert len(updates) >= minimum_updates, ("Missing fresh Navigator camera updates", len(updates))
    identity = updates[0].group(5)
    updates = [event for event in updates if event.group(5) == identity]
    assert len(updates) >= minimum_updates, "Camera updates belong to different Navigator instances"
    frames = [int(event.group(6)) for event in updates]
    assert frames[0] > 0, "Navigator overview was never rendered before navigation"
    assert len(set(frames)) == 1, ("Camera navigation rerendered the unchanged SVG overview", frames)
    assert all(event.group(7) == "True" for event in updates), "The viewport overlay disappeared during navigation"
    for event in updates:
        line_end = text.find("\n", event.end())
        verify_geometry(text[event.end():line_end if line_end >= 0 else len(text)])
    assert not any(event.group(1) and event.group(2) == identity and event.start() > updates[0].start()
                   for event in events), "The overview repainted after a camera update"
    return {"check": "navigator-camera-retains-overview", "passed": True,
            "camera_updates": len(updates), "overview_frames": frames[0], "overlay_geometry_verified": True}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("log", type=pathlib.Path)
    parser.add_argument("offset", type=int, help="Byte checkpoint taken after opening and focusing Navigator")
    parser.add_argument("--minimum-updates", type=int, default=3)
    parser.add_argument("--timeout", type=float, default=10)
    args = parser.parse_args()
    deadline = time.monotonic() + args.timeout
    while True:
        with args.log.open("rb") as stream:
            stream.seek(args.offset)
            text = stream.read().decode("utf-8", errors="replace")
        if sum(bool(event.group(4)) for event in EVENT.finditer(text)) >= args.minimum_updates:
            break
        if time.monotonic() >= deadline:
            raise TimeoutError("Native Navigator camera updates did not reach the checkpoint")
        time.sleep(0.05)
    result = verify_navigation(text, args.minimum_updates)
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
