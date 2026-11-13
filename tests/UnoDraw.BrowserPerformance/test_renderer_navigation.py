"""Live driver navigation must select the renderer independently of app defaults."""
from __future__ import annotations

import contextlib
import io
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import MagicMock
from urllib.parse import parse_qsl, urlsplit

from bridge_quality import run_case
from performance import run_trial
from quality import LIVE_BRIDGES

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "UnoDraw.BrowserRenderQuality"))
from svg_bridge_quality import run_session  # noqa: E402


ORIGIN = "http://127.0.0.1:8123/Svg.Skia/unodraw/"
PACKAGE = {"role": "candidate", "source_sha": "a" * 40, "runtime_generation": "uno7",
           "execution_mode": "InterpreterAndAOT"}


class RendererNavigationTests(unittest.TestCase):
    def browser(self):
        browser = MagicMock()
        browser.version = "navigation-contract"
        page = browser.new_context.return_value.new_page.return_value
        # Stop at the real navigation call: no browser, input, image or timing
        # validation is simulated by these URL-contract tests.
        page.goto.side_effect = RuntimeError("Navigation captured")
        return browser, page

    def assert_query(self, page, expected):
        page.goto.assert_called_once()
        url = page.goto.call_args.args[0]
        self.assertEqual(url.count("?"), 1 if expected else 0)
        self.assertEqual(urlsplit(url)._replace(query="").geturl(), ORIGIN)
        self.assertEqual(parse_qsl(urlsplit(url).query, keep_blank_values=True), expected)
        self.assertEqual(page.goto.call_args.kwargs,
                         {"wait_until": "domcontentloaded", "timeout": 120_000})

    def test_welcome_bridge_sessions_explicitly_request_webgpu(self):
        with tempfile.TemporaryDirectory() as directory:
            for bridge in LIVE_BRIDGES:
                with self.subTest(bridge=bridge):
                    browser, page = self.browser()
                    result = run_case(browser, ORIGIN, bridge, 1, Path(directory) / bridge)
                    self.assertEqual(result["failure"], "RuntimeError: Navigation captured")
                    self.assert_query(page, [("renderer", "webgpu"), ("canvasBridge", bridge)])

    def test_svg_sessions_preserve_renderer_bridge_and_input_diagnostics(self):
        fixture = SimpleNamespace(name="navigation", sha256="b" * 64, browser_sha256="c" * 64)
        with tempfile.TemporaryDirectory() as directory, contextlib.redirect_stdout(io.StringIO()):
            for bridge in LIVE_BRIDGES:
                with self.subTest(bridge=bridge):
                    browser, page = self.browser()
                    results = run_session(browser, ORIGIN, PACKAGE, bridge, 1,
                                          [fixture], Path(directory) / bridge)
                    self.assertEqual(results[0]["failure"], "RuntimeError: Navigation captured")
                    self.assert_query(page, [("renderer", "webgpu"), ("canvasBridge", bridge),
                                             ("inputDiagnostics", "1")])

    def test_timed_driver_keeps_one_renderer_selector_when_adding_controls(self):
        with tempfile.TemporaryDirectory() as directory:
            for bridge in (None, *LIVE_BRIDGES):
                for diagnostics in (False, True):
                    with self.subTest(bridge=bridge, diagnostics=diagnostics):
                        browser, page = self.browser()
                        result = run_trial(browser, ORIGIN, PACKAGE, "webgpu", "selection", 1,
                                           SimpleNamespace(), Path(directory) / f"{bridge}-{diagnostics}",
                                           canvas_bridge=bridge, diagnostics=diagnostics)
                        self.assertEqual(result["failure"], "RuntimeError: Navigation captured")
                        expected = [("renderer", "webgpu")]
                        if bridge is not None:
                            expected.append(("canvasBridge", bridge))
                        if diagnostics:
                            expected += [("renderDiagnostics", "1"), ("perfDiagnostics", "1")]
                        self.assert_query(page, expected)

    def test_skia_trials_preserve_current_selection_and_legacy_url(self):
        with tempfile.TemporaryDirectory() as directory:
            for generation in ("uno6", "uno7"):
                with self.subTest(generation=generation):
                    browser, page = self.browser()
                    package = {**PACKAGE, "runtime_generation": generation}
                    result = run_trial(browser, ORIGIN, package, "skia-preferred", "selection", 1,
                                       SimpleNamespace(), Path(directory) / generation)
                    self.assertEqual(result["failure"], "RuntimeError: Navigation captured")
                    self.assert_query(page, [("renderer", "skia")] if generation == "uno7" else [])


if __name__ == "__main__":
    unittest.main()
