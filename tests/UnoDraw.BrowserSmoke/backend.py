"""Renderer evidence for the published browser runtime, independent of Playwright."""
from __future__ import annotations

import re
from typing import Any
from urllib.parse import urlsplit


BACKENDS = ("webgpu", "skia-fallback", "software-fallback", "skia-preferred")
SMOKE_CANVAS_BRIDGES = ("default", "single-canvas")
NO_ADAPTER_ERROR = "WebGpuInit: navigator.gpu.requestAdapter returned null"
NO_WEBGL_ERROR = "Failed to create WebGL context: Failed to create WebGL context: err 0"
NEGOTIATION = re.compile(
    r"Graphics backend '(?P<provider>\w+)' won negotiation on context kind '(?P<context>\w+)'\."
)
SINGLE_CANVAS_PREFERENCE = "UnoDraw canvas bridge preference: single-canvas original raster upload."
SINGLE_CANVAS_INITIALIZED = "UnoDraw canvas bridge initialized: single-canvas original topology."
SINGLE_CANVAS_OBSERVATION_PREFIX = "UnoDraw single-canvas graphics observation: "
SINGLE_CANVAS_OBSERVATION = re.compile(
    re.escape(SINGLE_CANVAS_OBSERVATION_PREFIX)
    + r"provider='(?P<provider>\w+)'; factory='(?P<factory>\w+)'; context='(?P<context>\w+)'; "
      r"kind='(?P<kind>\w+)'; topology='(?P<topology>[\w-]+)'\."
)


def single_canvas_provider_evidence(console: list[dict[str, Any]], backend: str,
                                    negotiations: list[dict[str, str]], first_winner: int,
                                    *, allowed: bool) -> dict[str, Any] | None:
    """Bind an explicitly permitted decorator winner to its actual native objects.

    The factory record is emitted by App after host negotiation. It proves the
    observed native factory/context and chosen topology, not a completed paint,
    upload or presentation. Historical native-provider reports stay unchanged.
    """
    decorated = any(row["provider"] in {"SingleCanvasWebGpuProvider", "SingleCanvasSkiaProvider"}
                    for row in negotiations)
    observations = [(index, entry) for index, entry in enumerate(console)
                    if entry.get("text", "").startswith(SINGLE_CANVAS_OBSERVATION_PREFIX)]
    if not decorated:
        if observations:
            raise AssertionError("A native-provider winner cannot borrow a decorated factory observation")
        return None
    if allowed is not True or backend == "skia-preferred":
        raise AssertionError("This renderer proof does not permit a single-canvas provider decorator")
    expected = {
        "webgpu": ("SingleCanvasWebGpuProvider", "WebGpuGraphicsProvider", "WebGpuDrawingFactory",
                   "WebGpuBrowserGraphicsContext", "WebGpu", "single-canvas"),
        "skia-fallback": ("SingleCanvasSkiaProvider", "SkiaGraphicsProvider", "SkiaDrawingFactory",
                          "WasmGLGraphicsContext", "WebGL", "retained"),
        "software-fallback": ("SingleCanvasSkiaProvider", "SkiaGraphicsProvider", "SkiaDrawingFactory",
                              "WasmSoftwareGraphicsContext", "Software", "retained"),
    }[backend]
    wrapper, provider, factory, context, kind, topology = expected
    if negotiations != [{"provider": wrapper, "context": kind}]:
        raise AssertionError("The single-canvas decorator did not exclusively win the required native context")
    preferences = [index for index, entry in enumerate(console)
                   if entry.get("type") in {"log", "info", "debug"}
                   and entry.get("text") == SINGLE_CANVAS_PREFERENCE]
    if len(preferences) != 1 or preferences[0] >= first_winner:
        raise AssertionError("The decorated provider requires its exclusive preference before negotiation")
    if len(observations) != 1:
        raise AssertionError("The decorated provider requires exactly one native factory observation")
    index, entry = observations[0]
    match = SINGLE_CANVAS_OBSERVATION.fullmatch(entry.get("text", ""))
    native = {"provider": provider, "factory": factory, "context": context, "kind": kind, "topology": topology}
    if (entry.get("type") not in {"log", "info", "debug"} or index <= first_winner
            or match is None or match.groupdict() != native):
        raise AssertionError("The native factory/context observation does not match the completed negotiation")
    competing = [entry.get("text", "") for entry in console if (
        entry.get("text", "").startswith("UnoDraw canvas bridge preference:")
        and entry.get("text") != SINGLE_CANVAS_PREFERENCE) or (
        entry.get("text", "").startswith("UnoDraw canvas bridge initialized:")
        and (topology != "single-canvas" or entry.get("text") != SINGLE_CANVAS_INITIALIZED))]
    if competing:
        raise AssertionError("The native factory observation was followed by a competing canvas topology")
    return {**native, "decorator": wrapper, "console_slice_index": index}


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


def smoke_query(backend: str, canvas_bridge: str = "default") -> str:
    """Keep renderer preference intact when adding the explicit smoke control."""
    if canvas_bridge not in SMOKE_CANVAS_BRIDGES:
        raise ValueError(f"Unknown smoke canvas bridge: {canvas_bridge}")
    query = renderer_query(backend)
    if canvas_bridge == "single-canvas":
        query += ("&" if query else "?") + "canvasBridge=single-canvas"
    return query


def verify_smoke_navigation(console: list[dict[str, Any]], capabilities: dict[str, Any],
                            backend: str, canvas_bridge: str,
                            requested_url: str, actual_url: str) -> dict[str, Any]:
    """Bind each smoke navigation to its query, native provider and painter proof.

    This validates console evidence and actual URL state. The independent native
    image gates still establish rendered output; no canvas probe or log proves
    upload completion, presentation or a newly displayed state for each input.
    """
    expected_query = smoke_query(backend, canvas_bridge).removeprefix("?")
    requested, actual = urlsplit(requested_url), urlsplit(actual_url)
    if (requested.scheme not in {"http", "https"} or not requested.netloc
            or not requested.path.startswith("/") or requested.fragment or actual.fragment
            or requested.query != expected_query or actual.query != expected_query
            or (actual.scheme, actual.netloc, actual.path)
            != (requested.scheme, requested.netloc, requested.path)):
        raise AssertionError("The smoke navigation must preserve its exact renderer and canvas-bridge query")

    explicit = canvas_bridge == "single-canvas"
    backend_proof = verify_backend(console, capabilities, backend,
                                   allow_single_canvas_provider=explicit)
    observation = backend_proof.get("single_canvas_native_factory")
    initialized = [(index, entry) for index, entry in enumerate(console)
                   if entry.get("text", "").startswith("UnoDraw canvas bridge initialized:")]
    bridge_messages = [entry.get("text", "") for entry in console
                       if entry.get("text", "").startswith("UnoDraw canvas bridge ")]
    painter_index = None
    if not explicit:
        if (observation is not None or SINGLE_CANVAS_PREFERENCE in bridge_messages
                or any(entry.get("text") == SINGLE_CANVAS_INITIALIZED for _, entry in initialized)):
            raise AssertionError("Ordinary smoke navigation cannot use the single-canvas control")
    elif backend == "skia-preferred":
        if observation is not None or bridge_messages:
            raise AssertionError("Explicit Skia preference must keep its original native provider and retained topology")
    else:
        if observation is None:
            raise AssertionError("Explicit single-canvas smoke requires the actual native factory observation")
        allowed_messages = {SINGLE_CANVAS_PREFERENCE}
        if backend == "webgpu":
            allowed_messages.add(SINGLE_CANVAS_INITIALIZED)
            if (len(initialized) != 1 or initialized[0][1].get("text") != SINGLE_CANVAS_INITIALIZED
                    or initialized[0][1].get("type") not in {"log", "info", "debug"}
                    or initialized[0][0] <= observation["console_slice_index"]):
                raise AssertionError("The selected single-canvas WebGPU provider requires one later completed-painter initialization")
            painter_index = initialized[0][0]
        elif initialized:
            raise AssertionError("Automatic Skia/software fallback must retain its topology without single-canvas painter initialization")
        if any(message not in allowed_messages for message in bridge_messages):
            raise AssertionError("Single-canvas smoke cannot accept a competing bridge or abandoned topology")
    return {
        "canvas_bridge_mode": canvas_bridge,
        "navigation": {"requested_url": requested_url, "actual_url": actual_url},
        "native_factory_observation": observation,
        "single_canvas_painter_initialization_index": painter_index,
        "painter_evidence_scope": "Original document and interaction painters completed; not an upload or presentation proof"
            if painter_index is not None else "No single-canvas painter initialization",
    }


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


def verify_backend(console: list[dict[str, Any]], capabilities: dict[str, Any], backend: str,
                   *, allow_single_canvas_provider: bool = False) -> dict[str, Any]:
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
    observation = single_canvas_provider_evidence(console, backend, negotiations, first_winner,
                                                  allowed=allow_single_canvas_provider)
    allowed = {
        "webgpu": {("WebGpuGraphicsProvider", "WebGpu")},
        "skia-fallback": {("SkiaGraphicsProvider", "WebGL")},
        "software-fallback": {("SkiaGraphicsProvider", "Software")},
        "skia-preferred": {("SkiaGraphicsProvider", "WebGL")},
    }[backend]
    if observation is not None:
        allowed = {(observation["decorator"], observation["kind"])}
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
    result = {"capabilities": capabilities, "negotiations": negotiations,
              "expected_console_error_indices": expected_errors}
    if observation is not None:
        result["single_canvas_native_factory"] = observation
    return result
