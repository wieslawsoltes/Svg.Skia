from __future__ import annotations

import copy
import unittest

from canvas_diagnostics import verify_hover_rendering
from quality import (COMPOSITE_INITIALIZED, LEGACY_PREFERENCE, PERSISTENT_INITIALIZED,
                     SINGLE_CANVAS_INITIALIZED, SINGLE_CANVAS_PREFERENCE)


def snapshot(document: int = 120, interaction: int = 120) -> dict:
    return {"phases": [{"phase": "Document", "count": document},
                       {"phase": "Interaction", "count": interaction}]}


def console(*messages: str) -> list[dict]:
    return [{"text": message} for message in messages]


class CanvasDiagnosticsTests(unittest.TestCase):
    def test_default_and_explicit_single_canvas_require_the_same_completed_paint(self) -> None:
        evidence = console(SINGLE_CANVAS_PREFERENCE, SINGLE_CANVAS_INITIALIZED)
        for requested in (None, "single-canvas"):
            with self.subTest(requested=requested):
                result = verify_hover_rendering(snapshot(), evidence, backend="webgpu", requested_bridge=requested)
                self.assertEqual(result["canvas_render_topology"], "single-canvas")
                self.assertNotIn("hover_layer_isolation", result)

    def test_a_query_or_a_partial_startup_log_cannot_exempt_document_retention(self) -> None:
        for requested in (None, "single-canvas"):
            for messages in ((SINGLE_CANVAS_PREFERENCE,), (SINGLE_CANVAS_INITIALIZED,)):
                with self.subTest(requested=requested, messages=messages), self.assertRaises(AssertionError):
                    verify_hover_rendering(snapshot(), console(*messages), backend="webgpu", requested_bridge=requested)
        with self.assertRaises(AssertionError):
            verify_hover_rendering(snapshot(), [], backend="webgpu", requested_bridge="single-canvas")

    def test_single_canvas_rejects_zero_missing_duplicate_and_invalid_phase_counts(self) -> None:
        evidence = console(SINGLE_CANVAS_PREFERENCE, SINGLE_CANVAS_INITIALIZED)
        for phase in ("Document", "Interaction"):
            for value in (0, -1, True, None, 120.0):
                changed = snapshot()
                next(row for row in changed["phases"] if row["phase"] == phase)["count"] = value
                with self.subTest(phase=phase, value=value), self.assertRaises(AssertionError):
                    verify_hover_rendering(changed, evidence, backend="webgpu", requested_bridge=None)
            missing = snapshot()
            missing["phases"] = [row for row in missing["phases"] if row["phase"] != phase]
            duplicate = snapshot()
            duplicate["phases"].append({"phase": phase, "count": 120})
            for changed in (missing, duplicate):
                with self.subTest(phase=phase, phases=changed), self.assertRaises(AssertionError):
                    verify_hover_rendering(changed, evidence, backend="webgpu", requested_bridge=None)

    def test_conflicting_transport_and_explicit_selection_fail(self) -> None:
        evidence = console(SINGLE_CANVAS_PREFERENCE, SINGLE_CANVAS_INITIALIZED)
        for extra in (PERSISTENT_INITIALIZED, COMPOSITE_INITIALIZED, LEGACY_PREFERENCE,
                      "UnoDraw canvas bridge fallback: unavailable"):
            with self.subTest(extra=extra), self.assertRaises(AssertionError):
                verify_hover_rendering(snapshot(), evidence + console(extra), backend="webgpu", requested_bridge=None)
        for requested in ("legacy", "persistent", "composite", "legacy-composite"):
            with self.subTest(requested=requested), self.assertRaises(AssertionError):
                verify_hover_rendering(snapshot(), evidence, backend="webgpu", requested_bridge=requested)

    def test_retained_webgpu_and_skia_still_require_zero_document_paints(self) -> None:
        for backend in ("webgpu", "skia-preferred", "skia-fallback", "software-fallback"):
            with self.subTest(backend=backend):
                result = verify_hover_rendering(snapshot(0), [], backend=backend, requested_bridge=None)
                self.assertEqual(result["canvas_render_topology"], "retained")
                self.assertIn("hover_layer_isolation", result)
                for document, interaction in ((1, 120), (0, 0)):
                    with self.assertRaises(AssertionError):
                        verify_hover_rendering(snapshot(document, interaction), [], backend=backend, requested_bridge=None)

    def test_verified_skia_still_requires_retained_painting_despite_a_preference(self) -> None:
        for backend in ("skia-preferred", "skia-fallback", "software-fallback"):
            with self.subTest(backend=backend):
                result = verify_hover_rendering(snapshot(0), console(SINGLE_CANVAS_PREFERENCE),
                                               backend=backend, requested_bridge=None)
                self.assertEqual(result["canvas_render_topology"], "retained")
                with self.assertRaises(AssertionError):
                    verify_hover_rendering(snapshot(0), console(SINGLE_CANVAS_PREFERENCE, SINGLE_CANVAS_INITIALIZED),
                                           backend=backend, requested_bridge=None)

    def test_explicit_single_canvas_requires_the_verified_webgpu_renderer(self) -> None:
        with self.assertRaises(AssertionError):
            verify_hover_rendering(snapshot(0), [], backend="skia-preferred", requested_bridge="single-canvas")

    def test_validation_does_not_modify_raw_evidence(self) -> None:
        phases = snapshot()
        evidence = console(SINGLE_CANVAS_PREFERENCE, SINGLE_CANVAS_INITIALIZED)
        original = copy.deepcopy((phases, evidence))
        verify_hover_rendering(phases, evidence, backend="webgpu", requested_bridge=None)
        self.assertEqual((phases, evidence), original)


if __name__ == "__main__":
    unittest.main()
