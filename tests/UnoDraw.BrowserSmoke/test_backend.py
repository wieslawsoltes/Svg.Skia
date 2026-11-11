"""Negative evidence cases that must never satisfy the renderer release gate."""
import unittest

from backend import NO_ADAPTER_ERROR, launch_arguments, verify_backend


def winner(provider: str, context: str) -> dict[str, str]:
    return {"type": "info", "text": "info: Uno.UI.Composition.Drawing.GraphicsRegistry[0]\n"
            f"      Graphics backend '{provider}' won negotiation on context kind '{context}'."}


def capabilities(adapter: bool = True) -> dict[str, bool]:
    return {"secure_context": True, "api_available": True, "adapter_available": adapter}


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

    def test_fallback_accepts_only_browser_skia_contexts(self) -> None:
        no_adapter = {"type": "error", "text": NO_ADAPTER_ERROR}
        for context in ("WebGL", "Software"):
            with self.subTest(context=context):
                proof = verify_backend([no_adapter, winner("SkiaGraphicsProvider", context)], capabilities(False), "skia-fallback")
                self.assertEqual(proof["expected_console_error_indices"], [0])
        for context in ("OpenGL", "OpenGLES", "WebGpu"):
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


if __name__ == "__main__":
    unittest.main()
