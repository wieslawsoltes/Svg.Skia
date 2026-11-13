"""Explicit topology smoke must prove its own query, provider and painter state."""
import copy
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from backend import (BACKENDS, SINGLE_CANVAS_INITIALIZED, SINGLE_CANVAS_PREFERENCE,
                     smoke_query, verify_smoke_navigation)
from test_backend import (capabilities, observed_single_canvas, software_capabilities,
                          software_logs, winner)
from smoke import (SINGLE_CANVAS_INITIALIZATION_TIMEOUT_MS, record_smoke_navigation,
                   wait_for_single_canvas_initialization)


ORIGIN = "http://127.0.0.1:8123/Svg.Skia/unodraw/"


def native_logs(backend: str) -> list[dict[str, str]]:
    if backend == "webgpu":
        return [winner("WebGpuGraphicsProvider", "WebGpu")]
    if backend == "software-fallback":
        return software_logs()
    if backend == "skia-fallback":
        return software_logs()[:1] + [winner("SkiaGraphicsProvider", "WebGL")]
    return [winner("SkiaGraphicsProvider", "WebGL")]


def profile_capabilities(backend: str) -> dict[str, bool]:
    if backend == "software-fallback":
        return software_capabilities()
    return capabilities(backend in {"webgpu", "skia-preferred"})


def explicit_logs(backend: str) -> list[dict[str, str]]:
    if backend == "skia-preferred":
        return native_logs(backend)
    logs = observed_single_canvas(backend)
    if backend == "webgpu":
        logs.append({"type": "log", "text": SINGLE_CANVAS_INITIALIZED})
    return logs


def verify(logs: list[dict[str, str]], backend: str = "webgpu", mode: str = "single-canvas",
           *, requested: str | None = None, actual: str | None = None) -> dict:
    url = ORIGIN + smoke_query(backend, mode)
    return verify_smoke_navigation(logs, profile_capabilities(backend), backend, mode,
                                   requested or url, actual or url)


class SmokeBridgeTests(unittest.TestCase):
    def test_all_eight_queries_preserve_renderer_preference(self) -> None:
        for backend in BACKENDS:
            with self.subTest(backend=backend):
                ordinary = "?renderer=skia" if backend == "skia-preferred" else ""
                self.assertEqual(smoke_query(backend), ordinary)
                self.assertEqual(smoke_query(backend, "default"), ordinary)
                self.assertEqual(smoke_query(backend, "single-canvas"),
                                 ordinary + ("&" if ordinary else "?") + "canvasBridge=single-canvas")

    def test_unknown_modes_or_backends_do_not_silently_use_defaults(self) -> None:
        for backend, mode in (("auto", "default"), ("webgpu", "legacy"),
                              ("webgpu", ""), ("skia-preferred", "Single-Canvas")):
            with self.subTest(backend=backend, mode=mode), self.assertRaises(ValueError):
                smoke_query(backend, mode)

    def test_ordinary_four_profiles_keep_native_provider_proof(self) -> None:
        for backend in BACKENDS:
            with self.subTest(backend=backend):
                proof = verify(native_logs(backend), backend, "default")
                self.assertEqual(proof["canvas_bridge_mode"], "default")
                self.assertIsNone(proof["native_factory_observation"])
                self.assertIsNone(proof["single_canvas_painter_initialization_index"])

    def test_explicit_four_profiles_bind_actual_topology(self) -> None:
        for backend in BACKENDS:
            with self.subTest(backend=backend):
                proof = verify(explicit_logs(backend), backend)
                observed = proof["native_factory_observation"]
                if backend == "webgpu":
                    self.assertEqual(observed["topology"], "single-canvas")
                    self.assertEqual(proof["single_canvas_painter_initialization_index"], 3)
                elif backend == "skia-preferred":
                    self.assertIsNone(observed)
                    self.assertIsNone(proof["single_canvas_painter_initialization_index"])
                else:
                    self.assertEqual(observed["topology"], "retained")
                    self.assertEqual(observed["factory"], "SkiaDrawingFactory")
                    self.assertIsNone(proof["single_canvas_painter_initialization_index"])

    def test_default_cannot_borrow_explicit_provider_or_painter_proof(self) -> None:
        for backend in BACKENDS[:3]:
            with self.subTest(backend=backend), self.assertRaises(AssertionError):
                verify(explicit_logs(backend), backend, "default")
        for message in (SINGLE_CANVAS_PREFERENCE, SINGLE_CANVAS_INITIALIZED):
            with self.subTest(message=message), self.assertRaises(AssertionError):
                verify(native_logs("webgpu") + [{"type": "log", "text": message}], mode="default")

    def test_webgpu_first_explicit_modes_require_real_factory_observation(self) -> None:
        for backend in BACKENDS[:3]:
            with self.subTest(backend=backend), self.assertRaisesRegex(AssertionError, "actual native factory"):
                verify(native_logs(backend), backend)

    def test_completed_painters_cannot_replace_the_selected_factory(self) -> None:
        logs = native_logs("webgpu") + [{"type": "log", "text": SINGLE_CANVAS_INITIALIZED}]
        with self.assertRaisesRegex(AssertionError, "actual native factory"):
            verify(logs)

    def test_webgpu_requires_one_later_completed_painter_initialization(self) -> None:
        logs = explicit_logs("webgpu")
        variants = [logs[:-1], logs + [logs[-1]], [logs[-1], *logs[:-1]],
                    logs[:-1] + [{**logs[-1], "type": "error"}],
                    logs[:-1] + [{**logs[-1], "text": SINGLE_CANVAS_INITIALIZED + " extra"}]]
        for changed in variants:
            with self.subTest(logs=changed), self.assertRaises(AssertionError):
                verify(changed)

    def test_automatic_fallback_rejects_single_canvas_painter_initialization(self) -> None:
        for backend in ("skia-fallback", "software-fallback"):
            for kind in ("log", "error"):
                with self.subTest(backend=backend, kind=kind), self.assertRaises(AssertionError):
                    verify(explicit_logs(backend) + [{"type": kind, "text": SINGLE_CANVAS_INITIALIZED}], backend)

    def test_skia_preference_rejects_registration_observation_and_painter_init(self) -> None:
        messages = [SINGLE_CANVAS_PREFERENCE, SINGLE_CANVAS_INITIALIZED,
                    "UnoDraw canvas bridge preference: persistent RGBA upload.",
                    observed_single_canvas("skia-fallback")[-1]["text"]]
        for message in messages:
            with self.subTest(message=message), self.assertRaises(AssertionError):
                verify(native_logs("skia-preferred") + [{"type": "log", "text": message}], "skia-preferred")
        with self.assertRaises(AssertionError):
            verify(observed_single_canvas("skia-fallback"), "skia-preferred")

    def test_late_competing_bridge_or_provider_cannot_hide_after_startup(self) -> None:
        initial = explicit_logs("webgpu")
        verify(initial)
        additions = [winner("SkiaGraphicsProvider", "Software"),
                     {"type": "log", "text": "UnoDraw canvas bridge preference: persistent RGBA upload."},
                     {"type": "log", "text": "UnoDraw canvas bridge initialized: persistent RGBA upload."},
                     {"type": "log", "text": "UnoDraw canvas bridge fallback: unavailable."}]
        for entry in additions:
            with self.subTest(entry=entry), self.assertRaises(AssertionError):
                verify(initial + [entry])

    def test_reload_requires_its_own_preference_factory_and_painter_events(self) -> None:
        for backend in BACKENDS:
            initial = explicit_logs(backend)
            combined = initial + copy.deepcopy(initial)
            boundary = len(initial)
            with self.subTest(backend=backend):
                verify(combined[:boundary], backend)
                verify(combined[boundary:], backend)
                with self.assertRaises(AssertionError):
                    verify([{"type": "log", "text": "reloaded without renderer evidence"}], backend)
                if backend != "skia-preferred":
                    with self.assertRaises(AssertionError):
                        verify(combined, backend)

    def test_selector_cannot_be_removed_duplicated_or_replaced(self) -> None:
        for query in ("", "?canvasBridge=legacy", "?canvasBridge=single-canvas&canvasBridge=single-canvas",
                      "?canvasBridge=single-canvas&renderer=skia", "?canvasBridge=Single-Canvas"):
            for field in ("requested", "actual"):
                with self.subTest(query=query, field=field), self.assertRaisesRegex(AssertionError, "exact renderer"):
                    verify(explicit_logs("webgpu"), **{field: ORIGIN + query})

    def test_skia_selector_append_cannot_drop_renderer_preference(self) -> None:
        for query in ("?canvasBridge=single-canvas", "?renderer=skia", "?renderer=webgpu&canvasBridge=single-canvas"):
            with self.subTest(query=query), self.assertRaisesRegex(AssertionError, "exact renderer"):
                verify(native_logs("skia-preferred"), "skia-preferred", actual=ORIGIN + query)

    def test_default_navigation_cannot_accept_an_explicit_selector(self) -> None:
        with self.assertRaisesRegex(AssertionError, "exact renderer"):
            verify(native_logs("webgpu"), mode="default", actual=ORIGIN + "?canvasBridge=single-canvas")

    def test_observed_navigation_cannot_move_origin_path_or_fragment(self) -> None:
        query = smoke_query("webgpu", "single-canvas")
        for url in ("http://127.0.0.1:9123/Svg.Skia/unodraw/" + query,
                    "http://127.0.0.1:8123/other/" + query, ORIGIN + query + "#other",
                    "file:///Svg.Skia/unodraw/" + query):
            with self.subTest(url=url), self.assertRaisesRegex(AssertionError, "exact renderer"):
                verify(explicit_logs("webgpu"), actual=url)


class PainterReadinessTests(unittest.TestCase):
    class ConsolePage:
        def __init__(self, report: dict, events: list[dict[str, str]], error: Exception | None = None) -> None:
            self.report, self.events, self.error = report, events, error
            self.waits = []

        def wait_for_event(self, event: str, *, timeout: int, predicate) -> None:
            self.waits.append((event, timeout))
            for entry in self.events:
                self.report["console"].append(entry)
                if predicate(SimpleNamespace(**entry)):
                    return
            raise self.error or TimeoutError("No matching console event")

    def test_an_already_recorded_fresh_completion_does_not_wait_again(self) -> None:
        report = {"console": explicit_logs("webgpu")}
        page = self.ConsolePage(report, [])
        wait_for_single_canvas_initialization(page, report, "startup", 0)
        self.assertEqual(page.waits, [])
        proof = report["canvas_bridge_readiness"]["startup"]
        self.assertTrue(proof["passed"])
        self.assertEqual(proof["timeout_ms"], 30_000)
        self.assertEqual(proof["observed_console_indices"], [3])

    def test_later_completion_is_observed_with_a_bounded_wait(self) -> None:
        report = {"console": observed_single_canvas()}
        page = self.ConsolePage(report, [{"type": "log", "text": "unrelated progress"},
                                        {"type": "info", "text": SINGLE_CANVAS_INITIALIZED}])
        wait_for_single_canvas_initialization(page, report, "startup", 0)
        self.assertEqual(page.waits, [("console", SINGLE_CANVAS_INITIALIZATION_TIMEOUT_MS)])
        self.assertEqual(report["canvas_bridge_readiness"]["startup"]["observed_console_indices"], [4])
        verify(report["console"])

    def test_reload_cannot_reuse_the_first_navigation_completion(self) -> None:
        report = {"console": explicit_logs("webgpu")}
        boundary = len(report["console"])
        page = self.ConsolePage(report, explicit_logs("webgpu"))
        wait_for_single_canvas_initialization(page, report, "reload", boundary)
        self.assertEqual(len(page.waits), 1)
        proof = report["canvas_bridge_readiness"]["reload"]
        self.assertEqual(proof["console_start_index"], boundary)
        self.assertEqual(proof["observed_console_indices"], [boundary + 3])

    def test_timeout_propagates_and_preserves_the_failed_wait_evidence(self) -> None:
        report = {"console": observed_single_canvas()}
        error = TimeoutError("Exact original wait failure")
        page = self.ConsolePage(report, [], error)
        with self.assertRaises(TimeoutError) as caught:
            wait_for_single_canvas_initialization(page, report, "startup", 0)
        self.assertIs(caught.exception, error)
        proof = report["canvas_bridge_readiness"]["startup"]
        self.assertFalse(proof["passed"])
        self.assertEqual(proof["observed_console_indices"], [])
        self.assertEqual(proof["timeout_ms"], 30_000)
        self.assertGreaterEqual(proof["duration_ms"], 0)

    def test_wrong_severity_or_partial_marker_does_not_satisfy_readiness(self) -> None:
        for entry in ({"type": "error", "text": SINGLE_CANVAS_INITIALIZED},
                      {"type": "log", "text": SINGLE_CANVAS_INITIALIZED + " extra"},
                      {"type": "log", "text": SINGLE_CANVAS_PREFERENCE}):
            report = {"console": observed_single_canvas()}
            with self.subTest(entry=entry), self.assertRaises(TimeoutError):
                wait_for_single_canvas_initialization(self.ConsolePage(report, [entry]), report, "startup", 0)

    def test_only_explicit_webgpu_adds_the_wait_before_freezing_backend_evidence(self) -> None:
        for backend in BACKENDS:
            for mode in ("default", "single-canvas"):
                logs = native_logs(backend) if mode == "default" else explicit_logs(backend)
                report = {"console": logs, "backend_mode": backend, "canvas_bridge_mode": mode,
                          "backend_selection": {}, "navigation_url": ORIGIN + smoke_query(backend, mode)}
                order = []

                def record(page, report, phase, console_start, **kwargs):
                    order.append("record")
                    report["backend_selection"][phase] = {"console_end_index": len(logs),
                                                          "capabilities": profile_capabilities(backend)}

                with self.subTest(backend=backend, mode=mode), \
                        patch("smoke.wait_for_single_canvas_initialization", side_effect=lambda *args: order.append("wait")), \
                        patch("smoke.record_backend", side_effect=record):
                    record_smoke_navigation(SimpleNamespace(url=report["navigation_url"]), report, "startup", 0)
                    self.assertEqual(order, ["wait", "record"] if mode == "single-canvas" and backend == "webgpu"
                                     else ["record"])


if __name__ == "__main__":
    unittest.main()
