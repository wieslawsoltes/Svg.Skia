"""Negative evidence cases that must never satisfy the renderer release gate."""
import unittest

from backend import NO_ADAPTER_ERROR, NO_WEBGL_ERROR, launch_arguments, renderer_query, verify_backend


def winner(provider: str, context: str) -> dict[str, str]:
    return {"type": "info", "text": "info: Uno.UI.Composition.Drawing.GraphicsRegistry[0]\n"
            f"      Graphics backend '{provider}' won negotiation on context kind '{context}'."}


def capabilities(adapter: bool = True) -> dict[str, bool]:
    return {"secure_context": True, "api_available": True, "adapter_available": adapter,
            "webgl1_available": True, "webgl2_available": True, "canvas2d_available": True}


def software_capabilities() -> dict[str, bool]:
    return {**capabilities(False), "webgl1_available": False, "webgl2_available": False}


def software_logs() -> list[dict[str, str]]:
    return [{"type": "error", "text": NO_ADAPTER_ERROR},
            {"type": "error", "text": "fail: Uno.UI.Runtime.WebGlBrowserRenderer[0]\n      " + NO_WEBGL_ERROR},
            winner("SkiaGraphicsProvider", "Software")]


class BackendEvidenceTests(unittest.TestCase):
    def test_adapter_availability_alone_is_not_renderer_evidence(self) -> None:
        with self.assertRaisesRegex(AssertionError, "No successful"):
            verify_backend([], capabilities(), "webgpu")

    def test_failed_provider_probe_is_not_a_successful_negotiation(self) -> None:
        logs = [{"type": "info", "text": "Trying graphics backend 'WebGpuGraphicsProvider' on context kind 'WebGpu'."}]
        with self.assertRaisesRegex(AssertionError, "No successful"):
            verify_backend(logs, capabilities(), "webgpu")

    def test_log_prefix_and_multiline_format_are_accepted(self) -> None:
        proof = verify_backend([winner("WebGpuGraphicsProvider", "WebGpu")], capabilities(), "webgpu")
        self.assertEqual(proof["negotiations"], [{"provider": "WebGpuGraphicsProvider", "context": "WebGpu"}])
        self.assertEqual(proof["expected_console_error_indices"], [])

    def test_webgpu_mode_rejects_silent_skia_fallback(self) -> None:
        with self.assertRaisesRegex(AssertionError, "unexpected graphics backend"):
            verify_backend([winner("SkiaGraphicsProvider", "WebGL")], capabilities(), "webgpu")

    def test_a_webgpu_winner_does_not_hide_an_unexpected_second_winner(self) -> None:
        logs = [winner("WebGpuGraphicsProvider", "WebGpu"), winner("SkiaGraphicsProvider", "Software")]
        with self.assertRaisesRegex(AssertionError, "unexpected graphics backend"):
            verify_backend(logs, capabilities(), "webgpu")

    def test_reload_cannot_reuse_startup_negotiation(self) -> None:
        startup = [winner("WebGpuGraphicsProvider", "WebGpu")]
        verify_backend(startup, capabilities(), "webgpu")
        reload_start = len(startup)
        logs = startup + [{"type": "log", "text": "Application reloaded"}]
        with self.assertRaisesRegex(AssertionError, "No successful"):
            verify_backend(logs[reload_start:], capabilities(), "webgpu")

    def test_fallback_requires_webgl_and_software_has_its_own_scenario(self) -> None:
        no_adapter = {"type": "error", "text": NO_ADAPTER_ERROR}
        proof = verify_backend([no_adapter, winner("SkiaGraphicsProvider", "WebGL")], capabilities(False), "skia-fallback")
        self.assertEqual(proof["expected_console_error_indices"], [0])
        for context in ("OpenGL", "OpenGLES", "WebGpu", "Software"):
            with self.subTest(context=context), self.assertRaisesRegex(AssertionError, "unexpected graphics backend"):
                verify_backend([no_adapter, winner("SkiaGraphicsProvider", context)], capabilities(False), "skia-fallback")

    def test_fallback_requires_actual_browser_adapter_unavailability(self) -> None:
        logs = [{"type": "error", "text": NO_ADAPTER_ERROR}, winner("SkiaGraphicsProvider", "WebGL")]
        with self.assertRaisesRegex(AssertionError, "disable a WebGPU adapter"):
            verify_backend(logs, capabilities(), "skia-fallback")

    def test_fallback_requires_attempted_webgpu_negotiation(self) -> None:
        with self.assertRaisesRegex(AssertionError, "expected WebGPU no-adapter"):
            verify_backend([winner("SkiaGraphicsProvider", "WebGL")], capabilities(False), "skia-fallback")

    def test_only_exact_no_adapter_error_is_expected_in_fallback(self) -> None:
        logs = [{"type": "error", "text": message} for message in (
            NO_ADAPTER_ERROR,
            "WebGpuInit: navigator.gpu is unavailable",
            "WebGpuInit: device creation failed",
            NO_ADAPTER_ERROR + "; unexpected error",
        )] + [winner("SkiaGraphicsProvider", "WebGL")]
        proof = verify_backend(logs, capabilities(False), "skia-fallback")
        self.assertEqual(proof["expected_console_error_indices"], [0])

    def test_webgpu_mode_does_not_exempt_negotiation_errors(self) -> None:
        logs = [{"type": "error", "text": NO_ADAPTER_ERROR}, winner("WebGpuGraphicsProvider", "WebGpu")]
        proof = verify_backend(logs, capabilities(), "webgpu")
        self.assertEqual(proof["expected_console_error_indices"], [])

    def test_fallback_profile_cannot_bypass_its_browser_kill_switch(self) -> None:
        arguments = launch_arguments("skia-fallback")
        self.assertIn("--disable-features=WebGPUService", arguments)
        self.assertNotIn("--enable-unsafe-webgpu", arguments)
        self.assertFalse(any(argument.startswith("--use-webgpu-adapter") for argument in arguments))

    def test_unknown_profiles_fail_instead_of_using_a_default_backend(self) -> None:
        with self.assertRaises(ValueError):
            launch_arguments("auto")
        with self.assertRaises(ValueError):
            verify_backend([], capabilities(), "auto")
        with self.assertRaises(ValueError):
            renderer_query("auto")

    def test_explicit_skia_preference_bypasses_an_available_webgpu_adapter(self) -> None:
        proof = verify_backend([winner("SkiaGraphicsProvider", "WebGL")], capabilities(), "skia-preferred")
        self.assertEqual(proof["expected_console_error_indices"], [])
        self.assertEqual(renderer_query("skia-preferred"), "?renderer=skia")
        for mode in ("webgpu", "skia-fallback", "software-fallback"):
            self.assertEqual(renderer_query(mode), "")

    def test_explicit_skia_preference_rejects_ignored_override_or_software(self) -> None:
        for provider, context in (("WebGpuGraphicsProvider", "WebGpu"), ("SkiaGraphicsProvider", "Software")):
            with self.subTest(context=context), self.assertRaisesRegex(AssertionError, "unexpected graphics backend"):
                verify_backend([winner(provider, context)], capabilities(), "skia-preferred")

    def test_software_fallback_requires_both_gpu_apis_to_be_unavailable(self) -> None:
        for field in ("adapter_available", "webgl1_available", "webgl2_available"):
            with self.subTest(field=field), self.assertRaises(AssertionError):
                verify_backend(software_logs(), {**software_capabilities(), field: True}, "software-fallback")
        for value in (False, None):
            with self.subTest(canvas2d=value), self.assertRaisesRegex(AssertionError, "usable Canvas2D"):
                verify_backend(software_logs(), {**software_capabilities(), "canvas2d_available": value}, "software-fallback")

    def test_software_fallback_requires_fresh_failed_webgl_negotiation(self) -> None:
        logs = [software_logs()[0], winner("SkiaGraphicsProvider", "Software")]
        with self.assertRaisesRegex(AssertionError, "expected unavailable-WebGL"):
            verify_backend(logs, software_capabilities(), "software-fallback")

    def test_software_fallback_accepts_only_exact_known_initialization_errors(self) -> None:
        logs = software_logs() + [
            {"type": "error", "text": NO_WEBGL_ERROR + "; device failure"},
            {"type": "error", "text": "fail: AnotherRenderer[0]\n      " + NO_WEBGL_ERROR},
            {"type": "error", "text": NO_WEBGL_ERROR.replace("err 0", "err 1")},
        ]
        proof = verify_backend(logs, software_capabilities(), "software-fallback")
        self.assertEqual(proof["expected_console_error_indices"], [0, 1])

    def test_unavailable_webgl_error_is_never_exempt_in_other_modes(self) -> None:
        for mode, adapter, renderer in (
            ("webgpu", True, winner("WebGpuGraphicsProvider", "WebGpu")),
            ("skia-preferred", True, winner("SkiaGraphicsProvider", "WebGL")),
            ("skia-fallback", False, winner("SkiaGraphicsProvider", "WebGL")),
        ):
            logs = [software_logs()[0], software_logs()[1], renderer]
            with self.subTest(mode=mode):
                proof = verify_backend(logs, capabilities(adapter), mode)
                self.assertNotIn(1, proof["expected_console_error_indices"])

    def test_software_fallback_rejects_a_webgl_winner(self) -> None:
        logs = software_logs()[:2] + [winner("SkiaGraphicsProvider", "WebGL")]
        with self.assertRaisesRegex(AssertionError, "unexpected graphics backend"):
            verify_backend(logs, software_capabilities(), "software-fallback")

    def test_browser_profiles_disable_only_the_requested_capabilities(self) -> None:
        arguments = launch_arguments("software-fallback")
        self.assertIn("--disable-webgl", arguments)
        self.assertIn("--disable-features=WebGPUService", arguments)
        self.assertNotIn("--enable-unsafe-webgpu", arguments)
        for mode in ("webgpu", "skia-fallback", "skia-preferred"):
            self.assertNotIn("--disable-webgl", launch_arguments(mode))

    def test_initialization_errors_are_not_exempt_after_the_renderer_wins(self) -> None:
        no_adapter = {"type": "error", "text": NO_ADAPTER_ERROR}
        logs = [no_adapter, winner("SkiaGraphicsProvider", "WebGL"), no_adapter]
        proof = verify_backend(logs, capabilities(False), "skia-fallback")
        self.assertEqual(proof["expected_console_error_indices"], [0])
        logs = software_logs() + software_logs()[:2]
        proof = verify_backend(logs, software_capabilities(), "software-fallback")
        self.assertEqual(proof["expected_console_error_indices"], [0, 1])

    def test_webgl_scenarios_require_an_independently_available_context(self) -> None:
        for mode, adapter in (("skia-fallback", False), ("skia-preferred", True)):
            with self.subTest(mode=mode), self.assertRaisesRegex(AssertionError, "usable WebGL"):
                verify_backend([winner("SkiaGraphicsProvider", "WebGL")],
                               {**software_capabilities(), "adapter_available": adapter}, mode)


if __name__ == "__main__":
    unittest.main()
