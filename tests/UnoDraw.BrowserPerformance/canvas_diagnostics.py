"""Post-timing painter requirements for the topology that actually initialized."""
from __future__ import annotations

from typing import Any

from metrics import verify_hover_isolation
from quality import SINGLE_CANVAS_INITIALIZED, SINGLE_CANVAS_PREFERENCE, verify_bridge


def verify_hover_rendering(snapshot: dict[str, Any], console: list[dict[str, Any]], *,
                           backend: str, requested_bridge: str | None) -> dict[str, str]:
    """The caller has already verified the selected renderer and native context.

    A default selection has no canvasBridge query argument. Require completed
    topology evidence from this navigation before accepting combined painting,
    even for an explicit query. These full-console observations do not establish
    when initialization occurred relative to the first capture.

    A verified Skia run must retain its layers even if its navigation includes a
    single-canvas preference. This is a validator requirement, not evidence that
    an application implements that fallback policy. A preference alone never
    proves that the interaction surface was removed.
    """
    messages = {entry.get("text", "") for entry in console}
    single_painted = SINGLE_CANVAS_INITIALIZED in messages
    single_requested = requested_bridge == "single-canvas" or SINGLE_CANVAS_PREFERENCE in messages
    if backend == "webgpu" and (single_requested or single_painted):
        if requested_bridge not in (None, "single-canvas"):
            raise AssertionError("The observed single-canvas topology conflicts with the explicit bridge")
        verify_bridge(console, "single-canvas")
        for name in ("Document", "Interaction"):
            counts = [row.get("count") for row in snapshot.get("phases", []) if row.get("phase") == name]
            if len(counts) != 1 or type(counts[0]) is not int or counts[0] <= 0:
                raise AssertionError("Single-canvas hover must actually paint both original layers")
        return {"canvas_render_topology": "single-canvas",
                "hover_rendering": "Document and Interaction counts both increased"}

    if single_painted or requested_bridge == "single-canvas":
        raise AssertionError("Single-canvas painting requires the verified WebGPU renderer")
    verify_hover_isolation(snapshot)
    return {"canvas_render_topology": "retained",
            "hover_layer_isolation": "Document count is zero; Interaction count increased"}
