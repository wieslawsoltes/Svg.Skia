"""Renderer evidence for the published browser runtime, independent of Playwright."""
from __future__ import annotations

import re
from typing import Any


BACKENDS = ("webgpu", "skia-fallback", "software-fallback", "skia-preferred")
NO_ADAPTER_ERROR = "WebGpuInit: navigator.gpu.requestAdapter returned null"
NO_WEBGL_ERROR = "Failed to create WebGL context: Failed to create WebGL context: err 0"
NEGOTIATION = re.compile(
    r"Graphics backend '(?P<provider>\w+)' won negotiation on context kind '(?P<context>\w+)'\."
)


def launch_arguments(backend: str) -> list[str]:
    # Chromium documents SwANGLE and SwiftShader Vulkan in docs/gpu/swiftshader.md.
    # Initialize Vulkan as well as ANGLE: an adapter-only probe does not prove that
    # the browser can present WebGPU canvas textures through its compositor.
    arguments = ["--enable-unsafe-swiftshader", "--use-gl=angle", "--use-angle=swiftshader"]
    if backend in {"webgpu", "skia-preferred"}:
        return arguments + ["--enable-unsafe-webgpu", "--use-vulkan=swiftshader", "--enable-features=Vulkan"]
    if backend in {"skia-fallback", "software-fallback"}:
        # Chromium's own WebGPUKillSwitchPages uses this feature switch. Keep
        # --enable-unsafe-webgpu out of this mode because it bypasses the switch.
        arguments += ["--disable-features=WebGPUService"]
        if backend == "software-fallback":
            # Chromium's switch disables both WebGL versions while retaining Canvas2D.
            arguments.append("--disable-webgl")
        return arguments
    raise ValueError(f"Unknown graphics backend: {backend}")


def renderer_query(backend: str) -> str:
    if backend not in BACKENDS:
        raise ValueError(f"Unknown graphics backend: {backend}")
    return "?renderer=skia" if backend == "skia-preferred" else ""


def is_no_webgl_error(entry: dict[str, Any]) -> bool:
    if entry.get("type") != "error":
        return False
    text = entry.get("text", "")
    if text == NO_WEBGL_ERROR:
        return True
    lines = text.splitlines()
    return len(lines) == 2 and lines[0] in {
        "fail: Uno.UI.Runtime.WebGlBrowserRenderer[0]",
        "error: Uno.UI.Runtime.WebGlBrowserRenderer[0]",
    } and lines[1].strip() == NO_WEBGL_ERROR


def verify_backend(console: list[dict[str, Any]], capabilities: dict[str, Any], backend: str) -> dict[str, Any]:
    """Require a usable app renderer, with capabilities only corroborating its log.

    Call with a fresh console slice after each navigation. A previous startup's
    negotiation must never count as evidence that a reload selected its renderer.
    """
    if backend not in BACKENDS:
        raise ValueError(f"Unknown graphics backend: {backend}")
    if capabilities.get("secure_context") is not True or capabilities.get("api_available") is not True:
        raise AssertionError(f"The secure smoke origin must expose the browser's WebGPU API: {capabilities}")
    expected_adapter = backend in {"webgpu", "skia-preferred"}
    if capabilities.get("adapter_available") is not expected_adapter:
        raise AssertionError(f"{backend} must {'provide' if expected_adapter else 'disable'} a WebGPU adapter: {capabilities}")
    if backend == "software-fallback" and (
        capabilities.get("webgl1_available") is not False
        or capabilities.get("webgl2_available") is not False
        or capabilities.get("canvas2d_available") is not True
    ):
        raise AssertionError(f"Software fallback requires unavailable WebGL1/2 and usable Canvas2D: {capabilities}")
    if backend in {"skia-fallback", "skia-preferred"} and not (
        capabilities.get("webgl1_available") is True or capabilities.get("webgl2_available") is True
    ):
        raise AssertionError(f"{backend} requires a usable WebGL context: {capabilities}")

    negotiations = []
    first_winner = None
    for index, entry in enumerate(console):
        if entry.get("type") not in {"log", "info", "debug"}:
            continue
        winners = [match.groupdict() for match in NEGOTIATION.finditer(entry.get("text", ""))]
        if winners and first_winner is None:
            first_winner = index
        negotiations.extend(winners)
    if not negotiations:
        raise AssertionError("No successful graphics-backend negotiation was recorded for this navigation")
    allowed = {
        "webgpu": {("WebGpuGraphicsProvider", "WebGpu")},
        "skia-fallback": {("SkiaGraphicsProvider", "WebGL")},
        "software-fallback": {("SkiaGraphicsProvider", "Software")},
        "skia-preferred": {("SkiaGraphicsProvider", "WebGL")},
    }[backend]
    unexpected = [entry for entry in negotiations if (entry["provider"], entry["context"]) not in allowed]
    if unexpected:
        raise AssertionError(f"{backend} selected an unexpected graphics backend: {unexpected}")

    # Expected failures belong only to initialization, before this navigation's
    # first successful negotiation. The same error during editing is a failure.
    # All other errors remain fatal, including API-unavailable and device errors.
    initialization = console[:first_winner]
    fallback = backend in {"skia-fallback", "software-fallback"}
    expected_errors = [index for index, entry in enumerate(initialization)
                       if fallback and entry.get("type") == "error"
                       and entry.get("text") == NO_ADAPTER_ERROR]
    if fallback and not expected_errors:
        raise AssertionError("Skia fallback did not record the expected WebGPU no-adapter negotiation")
    if backend == "software-fallback":
        webgl_errors = [index for index, entry in enumerate(initialization) if is_no_webgl_error(entry)]
        if not webgl_errors:
            raise AssertionError("Software fallback did not record the expected unavailable-WebGL negotiation")
        expected_errors = sorted(expected_errors + webgl_errors)
    return {"capabilities": capabilities, "negotiations": negotiations,
            "expected_console_error_indices": expected_errors}
