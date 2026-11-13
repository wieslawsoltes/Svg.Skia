"""Negative evidence cases that must never satisfy the renderer release gate."""
import unittest

from backend import (DEFAULT_SKIA_BACKENDS, NO_ADAPTER_ERROR, NO_WEBGL_ERROR,
                     SINGLE_CANVAS_INITIALIZED, SINGLE_CANVAS_PREFERENCE, SKIA_RENDERER_PREFERENCE,
                     launch_arguments, renderer_query, verify_backend)


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


def default_skia_logs(backend: str = "skia-default") -> list[dict[str, str]]:
    return [{"type": "log", "text": SKIA_RENDERER_PREFERENCE}] + (
        software_logs()[1:] if backend == "software-default"
        else [winner("SkiaGraphicsProvider", "WebGL")])


def default_software_capabilities() -> dict[str, bool]:
    return {**software_capabilities(), "adapter_available": True}


def observed_single_canvas(backend: str = "webgpu") -> list[dict[str, str]]:
    native = {
        "webgpu": ("SingleCanvasWebGpuProvider", "WebGpuGraphicsProvider", "WebGpuDrawingFactory",
                   "WebGpuBrowserGraphicsContext", "WebGpu", "single-canvas"),
        "skia-fallback": ("SingleCanvasSkiaProvider", "SkiaGraphicsProvider", "SkiaDrawingFactory",
                          "WasmGLGraphicsContext", "WebGL", "retained"),
        "software-fallback": ("SingleCanvasSkiaProvider", "SkiaGraphicsProvider", "SkiaDrawingFactory",
                              "WasmSoftwareGraphicsContext", "Software", "retained"),
    }[backend]
    wrapper, provider, factory, context, kind, topology = native
    errors = ([] if backend == "webgpu" else [{"type": "error", "text": NO_ADAPTER_ERROR}])
    if backend == "software-fallback":
        errors.append({"type": "error", "text": NO_WEBGL_ERROR})
    return [{"type": "log", "text": SINGLE_CANVAS_PREFERENCE}, *errors, winner(wrapper, kind),
            {"type": "log", "text": f"UnoDraw single-canvas graphics observation: provider='{provider}'; "
             f"factory='{factory}'; context='{context}'; kind='{kind}'; topology='{topology}'."}]


class BackendEvidenceTests(unittest.TestCase):
    def test_decorated_provider_requires_explicit_permission_and_actual_native_identity(self) -> None:
        logs = observed_single_canvas()
        with self.assertRaisesRegex(AssertionError, "does not permit"):
            verify_backend(logs, capabilities(), "webgpu")
        proof = verify_backend(logs, capabilities(), "webgpu", allow_single_canvas_provider=True)
        self.assertEqual(proof["negotiations"], [{"provider": "SingleCanvasWebGpuProvider", "context": "WebGpu"}])
        self.assertEqual(proof["single_canvas_native_factory"], {
            "provider": "WebGpuGraphicsProvider", "factory": "WebGpuDrawingFactory",
            "context": "WebGpuBrowserGraphicsContext", "kind": "WebGpu", "topology": "single-canvas",
            "decorator": "SingleCanvasWebGpuProvider", "console_slice_index": 2})
        self.assertEqual(proof["expected_console_error_indices"], [])

    def test_decorated_winner_alone_does_not_prove_its_underlying_factory(self) -> None:
        logs = observed_single_canvas()
        for changed in (logs[:-1], logs + [logs[-1]], [logs[1], logs[0], logs[-1]],
                        [logs[0], logs[-1], logs[1]], logs + [logs[1]], logs[1:]):
            with self.subTest(logs=changed), self.assertRaises(AssertionError):
                verify_backend(changed, capabilities(), "webgpu", allow_single_canvas_provider=True)

    def test_native_observation_must_match_every_context_factory_and_topology_field(self) -> None:
        logs = observed_single_canvas()
        for before, after in (("WebGpuGraphicsProvider", "SkiaGraphicsProvider"),
                              ("WebGpuDrawingFactory", "SkiaDrawingFactory"),
                              ("WebGpuBrowserGraphicsContext", "WebGpuSwapChainContext"),
                              ("kind='WebGpu'", "kind='WebGL'"),
                              ("topology='single-canvas'", "topology='retained'"),
                              ("factory='", "factory = '")):
            changed = logs[:-1] + [{**logs[-1], "text": logs[-1]["text"].replace(before, after)}]
            with self.subTest(field=before), self.assertRaisesRegex(AssertionError, "does not match"):
                verify_backend(changed, capabilities(), "webgpu", allow_single_canvas_provider=True)

    def test_observation_cannot_masquerade_as_a_native_provider_winner(self) -> None:
        logs = observed_single_canvas()
        logs[1] = winner("WebGpuGraphicsProvider", "WebGpu")
        with self.assertRaisesRegex(AssertionError, "cannot borrow"):
            verify_backend(logs, capabilities(), "webgpu", allow_single_canvas_provider=True)

    def test_fallback_decorators_prove_retained_topology_and_keep_original_error_limits(self) -> None:
        for backend, caps in (("skia-fallback", capabilities(False)),
                              ("software-fallback", software_capabilities())):
            logs = observed_single_canvas(backend)
            with self.subTest(backend=backend):
                proof = verify_backend(logs, caps, backend, allow_single_canvas_provider=True)
                self.assertEqual(proof["single_canvas_native_factory"]["topology"], "retained")
                self.assertEqual(proof["expected_console_error_indices"],
                                 [1] if backend == "skia-fallback" else [1, 2])
                for changed in (logs + [{"type": "log", "text": SINGLE_CANVAS_INITIALIZED}],
                                logs[:-1] + [{**logs[-1], "text": logs[-1]["text"].replace("'retained'", "'single-canvas'")}],
                                [entry for entry in logs if entry.get("type") != "error"]):
                    with self.assertRaises(AssertionError):
                        verify_backend(changed, caps, backend, allow_single_canvas_provider=True)

    def test_skia_preference_keeps_its_original_undecorated_provider_contract(self) -> None:
        with self.assertRaisesRegex(AssertionError, "does not permit"):
            verify_backend(observed_single_canvas("skia-fallback"), capabilities(), "skia-preferred",
                           allow_single_canvas_provider=True)

    def test_decorator_permission_does_not_weaken_other_bridge_or_reload_proof(self) -> None:
        logs = observed_single_canvas()
        for message in ("UnoDraw canvas bridge preference: persistent RGBA upload.",
                        "UnoDraw canvas bridge initialized: persistent RGBA upload."):
            with self.subTest(message=message), self.assertRaisesRegex(AssertionError, "competing"):
                verify_backend(logs + [{"type": "log", "text": message}], capabilities(), "webgpu",
                               allow_single_canvas_provider=True)
        for entry in ({**logs[-1], "type": "error"}, {**logs[-1], "text": logs[-1]["text"] + " extra"}):
            with self.subTest(entry=entry), self.assertRaises(AssertionError):
                verify_backend(logs[:-1] + [entry], capabilities(), "webgpu", allow_single_canvas_provider=True)
        with self.assertRaisesRegex(AssertionError, "exactly one"):
            verify_backend(logs[:2], capabilities(), "webgpu", allow_single_canvas_provider=True)

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
            self.assertEqual(renderer_query(mode), "?renderer=webgpu")

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


class DefaultSkiaEvidenceTests(unittest.TestCase):
    def test_default_profiles_use_the_bare_url_with_webgpu_available(self) -> None:
        for backend in DEFAULT_SKIA_BACKENDS:
            with self.subTest(backend=backend):
                self.assertEqual(renderer_query(backend), "")
                arguments = launch_arguments(backend)
                self.assertIn("--enable-unsafe-webgpu", arguments)
                self.assertIn("--use-vulkan=swiftshader", arguments)
                self.assertNotIn("--disable-features=WebGPUService", arguments)
                self.assertEqual("--disable-webgl" in arguments, backend == "software-default")

    def test_default_profiles_prove_fresh_preference_and_native_winner(self) -> None:
        for backend, caps, kind, expected in (
            ("skia-default", capabilities(), "WebGL", []),
            ("software-default", default_software_capabilities(), "Software", [1]),
        ):
            with self.subTest(backend=backend):
                proof = verify_backend(default_skia_logs(backend), caps, backend)
                self.assertEqual(proof["negotiations"], [{"provider": "SkiaGraphicsProvider", "context": kind}])
                self.assertEqual(proof["default_skia_preference_index"], 0)
                self.assertEqual(proof["expected_console_error_indices"], expected)
                self.assertNotIn("single_canvas_native_factory", proof)

    def test_default_preference_cannot_be_missing_duplicate_late_or_wrong(self) -> None:
        original = default_skia_logs()
        variants = [original[1:], original + [original[0]], list(reversed(original)),
                    [{"type": "error", "text": SKIA_RENDERER_PREFERENCE}, original[1]],
                    [{"type": "log", "text": SKIA_RENDERER_PREFERENCE + " extra"}, original[1]],
                    [{"type": "log", "text": "UnoDraw browser renderer preference: WebGPU, then Skia (WebGL, then software)."}, original[1]]]
        for logs in variants:
            with self.subTest(logs=logs), self.assertRaisesRegex(AssertionError, "fresh Skia preference"):
                verify_backend(logs, capabilities(), "skia-default")

    def test_default_never_attempts_webgpu_or_registers_a_bridge(self) -> None:
        messages = ["Trying graphics backend 'WebGpuGraphicsProvider' on context kind 'WebGpu'.",
                    "Trying graphics backend 'SingleCanvasWebGpuProvider' on context kind 'WebGpu'.",
                    NO_ADAPTER_ERROR, "WebGpuInit: device creation failed", SINGLE_CANVAS_PREFERENCE,
                    SINGLE_CANVAS_INITIALIZED, "UnoDraw canvas bridge preference: persistent RGBA upload.",
                    observed_single_canvas()[-1]["text"]]
        for backend in DEFAULT_SKIA_BACKENDS:
            caps = default_software_capabilities() if backend == "software-default" else capabilities()
            for message in messages:
                for severity in ("info", "error"):
                    with self.subTest(backend=backend, message=message, severity=severity), self.assertRaises(AssertionError):
                        verify_backend(default_skia_logs(backend) + [{"type": severity, "text": message}], caps, backend)

    def test_default_profiles_cannot_silently_select_another_context(self) -> None:
        for backend in DEFAULT_SKIA_BACKENDS:
            caps = default_software_capabilities() if backend == "software-default" else capabilities()
            wrong_kind = "WebGL" if backend == "software-default" else "Software"
            logs = default_skia_logs(backend)[:-1] + [winner("SkiaGraphicsProvider", wrong_kind)]
            with self.subTest(backend=backend), self.assertRaisesRegex(AssertionError, "unexpected graphics backend"):
                verify_backend(logs, caps, backend)

    def test_software_default_requires_webgl_failure_but_keeps_webgpu_available(self) -> None:
        logs = default_skia_logs("software-default")
        for field, value in (("adapter_available", False), ("webgl1_available", True),
                             ("webgl2_available", True), ("canvas2d_available", False)):
            with self.subTest(field=field), self.assertRaises(AssertionError):
                verify_backend(logs, {**default_software_capabilities(), field: value}, "software-default")
        with self.assertRaisesRegex(AssertionError, "expected unavailable-WebGL"):
            verify_backend([logs[0], logs[-1]], default_software_capabilities(), "software-default")
        with self.assertRaisesRegex(AssertionError, "usable WebGL"):
            verify_backend(default_skia_logs(), default_software_capabilities(), "skia-default")

    def test_default_software_exempts_only_exact_initial_webgl_errors(self) -> None:
        logs = default_skia_logs("software-default") + [
            {"type": "error", "text": NO_WEBGL_ERROR},
            {"type": "error", "text": NO_WEBGL_ERROR + "; device failure"},
        ]
        proof = verify_backend(logs, default_software_capabilities(), "software-default")
        self.assertEqual(proof["expected_console_error_indices"], [1])

    def test_default_reload_requires_a_separate_preference_and_winner(self) -> None:
        for backend in DEFAULT_SKIA_BACKENDS:
            caps = default_software_capabilities() if backend == "software-default" else capabilities()
            logs = default_skia_logs(backend)
            with self.subTest(backend=backend):
                verify_backend(logs, caps, backend)
                with self.assertRaises(AssertionError):
                    verify_backend(logs + logs, caps, backend)
                with self.assertRaises(AssertionError):
                    verify_backend([logs[-1]], caps, backend)


if __name__ == "__main__":
    unittest.main()
