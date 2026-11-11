"""Renderer evidence for the published browser runtime, independent of Playwright."""
from __future__ import annotations

import re
from typing import Any


BACKENDS = ("webgpu", "skia-fallback")
NO_ADAPTER_ERROR = "WebGpuInit: navigator.gpu.requestAdapter returned null"
NEGOTIATION = re.compile(
    r"Graphics backend '(?P<provider>\w+)' won negotiation on context kind '(?P<context>\w+)'\."
)


def launch_arguments(backend: str) -> list[str]:
    # Chromium documents SwANGLE and SwiftShader Vulkan in docs/gpu/swiftshader.md.
    # Initialize Vulkan as well as ANGLE: an adapter-only probe does not prove that
    # the browser can present WebGPU canvas textures through its compositor.
    arguments = ["--enable-unsafe-swiftshader", "--use-gl=angle", "--use-angle=swiftshader"]
    if backend == "webgpu":
        return arguments + ["--enable-unsafe-webgpu", "--use-vulkan=swiftshader", "--enable-features=Vulkan"]
    if backend == "skia-fallback":
        # Chromium's own WebGPUKillSwitchPages uses this feature switch. Keep
        # --enable-unsafe-webgpu out of this mode because it bypasses the switch.
        return arguments + ["--disable-features=WebGPUService"]
    raise ValueError(f"Unknown graphics backend: {backend}")


def verify_backend(console: list[dict[str, Any]], capabilities: dict[str, Any], backend: str) -> dict[str, Any]:
    """Require a usable app renderer, with capabilities only corroborating its log.

    Call with a fresh console slice after each navigation. A previous startup's
    negotiation must never count as evidence that a reload selected its renderer.
    """
    if backend not in BACKENDS:
        raise ValueError(f"Unknown graphics backend: {backend}")
    if capabilities.get("secure_context") is not True or capabilities.get("api_available") is not True:
        raise AssertionError(f"The secure smoke origin must expose the browser's WebGPU API: {capabilities}")
    expected_adapter = backend == "webgpu"
    if capabilities.get("adapter_available") is not expected_adapter:
        raise AssertionError(f"{backend} must {'provide' if expected_adapter else 'disable'} a WebGPU adapter: {capabilities}")

    negotiations = []
    for entry in console:
        if entry.get("type") not in {"log", "info", "debug"}:
            continue
        negotiations.extend(match.groupdict() for match in NEGOTIATION.finditer(entry.get("text", "")))
    if not negotiations:
        raise AssertionError("No successful graphics-backend negotiation was recorded for this navigation")
    allowed = ({("WebGpuGraphicsProvider", "WebGpu")} if backend == "webgpu" else
               {("SkiaGraphicsProvider", "WebGL"), ("SkiaGraphicsProvider", "Software")})
    unexpected = [entry for entry in negotiations if (entry["provider"], entry["context"]) not in allowed]
    if unexpected:
        raise AssertionError(f"{backend} selected an unexpected graphics backend: {unexpected}")

    # Uno logs the normal no-adapter negotiation failure with console.error.
    # This exact sentinel is expected only when the browser service is disabled
    # and a successful Skia negotiation has also been proved. All other errors
    # remain fatal in the caller, including the API-unavailable and device errors.
    expected_errors = [index for index, entry in enumerate(console)
                       if backend == "skia-fallback" and entry.get("type") == "error"
                       and entry.get("text") == NO_ADAPTER_ERROR]
    if backend == "skia-fallback" and not expected_errors:
        raise AssertionError("Skia fallback did not record the expected WebGPU no-adapter negotiation")
    return {"capabilities": capabilities, "negotiations": negotiations,
            "expected_console_error_indices": expected_errors}
