#!/usr/bin/env python3
"""Smoke-test the published UnoDraw site, not a development or interpreter-only build."""
from __future__ import annotations

import argparse
import contextlib
import functools
import hashlib
import http.server
import json
import threading
import time
from pathlib import Path
from typing import Any, Iterator
from urllib.parse import urlsplit

from PIL import Image, ImageChops, ImageStat
from playwright.sync_api import Page, sync_playwright


class SiteHandler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {
        **http.server.SimpleHTTPRequestHandler.extensions_map,
        ".wasm": "application/wasm",
        ".js": "text/javascript",
        ".mjs": "text/javascript",
        ".json": "application/json",
        ".webmanifest": "application/manifest+json",
    }

    def end_headers(self) -> None:
        # Match static GitHub Pages hosting, which has no COOP/COEP headers.
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, format: str, *args: Any) -> None:
        pass


@contextlib.contextmanager
def serve(site: Path) -> Iterator[str]:
    handler = functools.partial(SiteHandler, directory=str(site))
    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler)
    server.daemon_threads = True
    thread = threading.Thread(target=server.serve_forever, name="unodraw-smoke-http", daemon=True)
    thread.start()
    try:
        yield f"http://127.0.0.1:{server.server_port}/"
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=5)


READY = """() => {
    const canvases = [...document.querySelectorAll('canvas')];
    const visible = canvases.some(canvas => {
        const bounds = canvas.getBoundingClientRect();
        return canvas.width > 100 && canvas.height > 100 && bounds.width > 100 && bounds.height > 100;
    });
    const loader = document.querySelector('.uno-loader');
    const loading = loader && loader.getBoundingClientRect().width > 0 &&
        loader.getBoundingClientRect().height > 0 && getComputedStyle(loader).visibility !== 'hidden' &&
        getComputedStyle(loader).display !== 'none';
    return typeof globalThis.Module?.getAssemblyExports === 'function' && visible && !loading &&
        !globalThis.__unoDrawReleaseInitialFrame;
}"""


def wait_ready(page: Page) -> None:
    page.wait_for_function(READY, timeout=120_000)
    # Readiness proves runtime/canvas startup; allow asynchronous welcome-file loading
    # and the first composition frames to finish before inspecting rendered pixels.
    page.wait_for_timeout(1500)


def assert_rendered(path: Path) -> None:
    with Image.open(path) as image:
        colors = image.convert("RGB").resize((180, 120)).quantize(colors=32).getcolors()
        if not colors or len(colors) < 8:
            raise AssertionError(f"The captured workspace appears blank: {path.name}")


def changed_pixels(before: Path, after: Path) -> int:
    with Image.open(before) as first, Image.open(after) as second:
        if first.size != second.size:
            raise AssertionError("Cannot compare screenshots with different viewport sizes.")
        diff = ImageChops.difference(first.convert("RGB"), second.convert("RGB")).convert("L")
        return sum(count for level, count in enumerate(diff.histogram()) if level > 12)


def chrome_brightness(path: Path) -> dict[str, float]:
    # Empty chrome regions in the 1440x960 desktop layout. Artwork colors,
    # command-palette rows and caret/focus changes cannot satisfy this assertion.
    regions = {"menu_strip": (700, 8, 1000, 26), "control_bar": (1040, 42, 1120, 62)}
    with Image.open(path) as image:
        if image.size != (1440, 960):
            raise AssertionError("Theme dispatch must be inspected in the desktop viewport.")
        return {name: ImageStat.Stat(image.crop(bounds).convert("L")).mean[0] / 255
                for name, bounds in regions.items()}


def chrome_font_changes(before: Path, after: Path) -> dict[str, float]:
    # Compare first-frame dark chrome with the same restored-dark chrome. These
    # static labels and font icons exclude artwork, scrolling, input carets and
    # focus/hover targets. A delayed native font load changes their glyphs and
    # measurements even when the initial canvas already appears nonblank.
    regions = {"menu_labels": (120, 3, 592, 29), "toolbar_icons": (1259, 4, 1315, 28),
               "panel_icons": (1094, 79, 1115, 284)}
    with Image.open(before) as initial, Image.open(after) as restored:
        if initial.size != (1440, 960) or restored.size != initial.size:
            raise AssertionError("Initial font readiness must be inspected in the desktop viewport.")
        initial_rgb, restored_rgb = initial.convert("RGB"), restored.convert("RGB")
        changes: dict[str, float] = {}
        for name, bounds in regions.items():
            difference = ImageChops.difference(initial_rgb.crop(bounds), restored_rgb.crop(bounds)).convert("L")
            changed = sum(count for level, count in enumerate(difference.histogram()) if level > 12)
            changes[name] = changed / (difference.width * difference.height)
        return changes


def workspace_affordance_pixels(path: Path, light: bool = False) -> dict[str, int]:
    # A stable first frame can still omit unsupported Unicode symbols forever.
    # Inspect the actual indicator pixels separately from font-load stability.
    # These regions exclude labels, control borders, artwork and hover targets.
    regions = {
        "tool_columns": (3, 77, 23, 94),
        "swap_paints": (10, 584, 25, 601),
        "no_paint": (24, 611, 43, 628),
        "alternate_selection": (23, 122, 31, 130),
    }
    with Image.open(path) as image:
        if image.size == (1440, 960):
            regions.update({
                "fill_dropdown": (150, 47, 163, 61),
                "stroke_dropdown": (250, 47, 263, 61),
                "weight_spin": (376, 46, 389, 61),
                "dock_menu": (1411, 81, 1428, 96),
                "panel_picker": (1416, 111, 1431, 129),
                "workspace_panels": (1410, 43, 1428, 61),
            })
        elif image.size != (390, 844):
            raise AssertionError("Workspace indicators require a desktop or compact viewport.")
        luminance = image.convert("L")
        result: dict[str, int] = {}
        for name, bounds in regions.items():
            region = luminance.crop(bounds)
            foreground = [[(region.getpixel((x, y)) < 190 if light else region.getpixel((x, y)) > 112)
                           for x in range(region.width)] for y in range(region.height)]
            # A missing glyph can also shorten a text-based control. Exclude any
            # full-width/height border that consequently enters the sample; a
            # control outline cannot stand in for the indicator it should contain.
            border_columns = {x for x in range(region.width) if all(row[x] for row in foreground)}
            border_rows = {y for y, row in enumerate(foreground) if all(row)}
            result[name] = sum(value for y, row in enumerate(foreground) if y not in border_rows
                               for x, value in enumerate(row) if x not in border_columns)
        return result


def assert_workspace_affordances(pixels: dict[str, int]) -> None:
    missing = {name: count for name, count in pixels.items() if count < 4}
    if missing:
        raise AssertionError(f"Workspace indicators did not render: {missing}")


def compact_panel_rail_changes(desktop: Path, compact: Path) -> dict[str, Any]:
    # Compare the actual rendered glyphs when the rail gains a vertical scrollbar.
    # Locate its first icon in broad chrome-only bands so the assertion tolerates
    # an intrinsic rail width and does not reproduce the layout's width formula.
    def glyphs(image: Image.Image, search_bounds: tuple[int, int, int, int]) -> list[Image.Image]:
        luminance = image.convert("L")
        first = luminance.crop(search_bounds).point(lambda value: 255 if value > 112 else 0).getbbox()
        if first is None:
            raise AssertionError("Cannot locate the rendered Properties icon in the panel rail.")
        center = search_bounds[0] + (first[0] + first[2]) // 2
        result = []
        for row in range(6):
            region = luminance.crop((center - 12, 81 + row * 35, center + 12, 105 + row * 35))
            # Normalize contrast so an active panel's foreground/background colors
            # do not masquerade as a clipped glyph after responsive docking.
            background = ImageStat.Stat(region).median[0]
            threshold = background + (region.getextrema()[1] - background) * 0.45
            icon = region.point(lambda value: 255 if value > threshold else 0)
            bounds = icon.getbbox()
            if bounds is None:
                raise AssertionError(f"Panel rail icon {row + 1} did not render.")
            result.append(icon.crop(bounds))
        return result

    with Image.open(desktop) as wide, Image.open(compact) as narrow:
        if wide.size != (1440, 960) or narrow.size != (390, 844):
            raise AssertionError("Panel-rail regression requires the desktop and compact smoke viewports.")
        wide_glyphs = glyphs(wide, (wide.width - 420, 82, wide.width - 300, 105))
        narrow_glyphs = glyphs(narrow, (narrow.width - 70, 82, narrow.width, 105))
        changes: dict[str, float] = {}
        dimensions: dict[str, dict[str, tuple[int, int]]] = {}
        names = ("properties", "transform", "appearance", "stroke", "align", "pathfinder")
        for name, expected, actual in zip(names, wide_glyphs, narrow_glyphs):
            size = (max(expected.width, actual.width), max(expected.height, actual.height))
            expected_mask, actual_mask = Image.new("L", size), Image.new("L", size)
            expected_mask.paste(expected, (0, 0))
            actual_mask.paste(actual, (0, 0))
            changed = ImageChops.difference(expected_mask, actual_mask).histogram()[255]
            changes[name] = changed / expected.histogram()[255]
            dimensions[name] = {"desktop": expected.size, "compact": actual.size}
        return {"different_foreground_fraction": changes, "glyph_dimensions": dimensions,
                "maximum_different_foreground_fraction": 0.08}


def wait_for_theme(page: Page, path: Path, light: bool) -> dict[str, float]:
    deadline = time.monotonic() + 15
    samples: dict[str, float] = {}
    while time.monotonic() < deadline:
        page.screenshot(path=str(path))
        samples = chrome_brightness(path)
        if all(value > 0.7 if light else value < 0.3 for value in samples.values()):
            return samples
        page.wait_for_timeout(100)
    raise AssertionError(f"The theme command did not change desktop chrome to {'light' if light else 'dark'}: {samples}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--site", type=Path, required=True, help="Static HTTP document root")
    parser.add_argument("--base-path", default="/", help="Application path under the document root, with leading/trailing slashes")
    parser.add_argument("--output", type=Path, required=True, help="Diagnostic artifact directory")
    args = parser.parse_args()
    site, output = args.site.resolve(), args.output.resolve()
    base_path = args.base_path
    if not base_path.startswith("/") or not base_path.endswith("/") or any(
        part in {".", ".."} for part in base_path.split("/")
    ) or any(character in base_path for character in "?#%\\") or "//" in base_path:
        parser.error("--base-path must be an absolute directory URL path without traversal, query or fragment")
    app = site / base_path.strip("/")
    if not (app / "index.html").is_file():
        parser.error("--site and --base-path must locate the published index.html")
    output.mkdir(parents=True, exist_ok=True)
    if output == site or site in output.parents:
        parser.error("--output must be outside the served site")

    report: dict[str, Any] = {
        "passed": False,
        "started_unix": time.time(),
        "site": str(site),
        "base_path": base_path,
        "index_sha256": hashlib.sha256((app / "index.html").read_bytes()).hexdigest(),
        "graphics": "Headless Chromium with software WebGL; not a hardware-performance benchmark",
        "console": [], "page_errors": [], "http_errors": [], "request_failures": [], "checks": [],
    }
    page: Page | None = None
    try:
        with serve(site) as url, sync_playwright() as playwright:
            browser = playwright.chromium.launch(headless=True, args=["--enable-unsafe-swiftshader"])
            report["browser_version"] = browser.version
            context = browser.new_context(viewport={"width": 1440, "height": 960}, device_scale_factor=1)
            context.tracing.start(screenshots=True, snapshots=True, sources=False)
            page = context.new_page()
            page.on("console", lambda message: report["console"].append({"type": message.type, "text": message.text, "location": message.location}))
            page.on("pageerror", lambda error: report["page_errors"].append(str(error)))
            page.on("response", lambda response: report["http_errors"].append({"status": response.status, "url": response.url}) if response.status >= 400 else None)
            page.on("requestfailed", lambda request: report["request_failures"].append({"url": request.url, "failure": request.failure}))
            try:
                app_url = url.rstrip("/") + base_path
                page.goto(app_url, wait_until="domcontentloaded", timeout=120_000)
                wait_ready(page)
                if page.evaluate("globalThis.crossOriginIsolated"):
                    raise AssertionError("Smoke server must match GitHub Pages without cross-origin isolation")
                report["checks"].append(f"Application starts at {base_path} without cross-origin isolation")
                wide = output / "01-workspace-desktop.png"
                page.screenshot(path=str(wide))
                assert_rendered(wide)
                report["checks"].append("Published runtime starts and renders a nonblank desktop workspace")
                desktop_affordances = workspace_affordance_pixels(wide)
                report["workspace_affordances"] = {"desktop_foreground_pixels": desktop_affordances,
                    "minimum_foreground_pixels": 4}
                assert_workspace_affordances(desktop_affordances)
                report["checks"].append("Desktop paint, tool, spin and dock indicators render without system-font fallback")

                dark_brightness = chrome_brightness(wide)
                if not all(value < 0.3 for value in dark_brightness.values()):
                    raise AssertionError(f"The initial workspace chrome must use the default dark theme: {dark_brightness}")
                page.keyboard.press("Control+Shift+F12")
                page.wait_for_timeout(700)
                palette = output / "02-command-palette.png"
                page.screenshot(path=str(palette))
                difference = changed_pixels(wide, palette)
                if difference < 2000:
                    raise AssertionError(f"Command search did not visibly respond ({difference} changed pixels)")
                page.keyboard.type("Use light interface", delay=30)
                page.keyboard.press("Enter")
                light_brightness = wait_for_theme(page, output / "02a-command-light-theme.png", light=True)
                if not all(light_brightness[name] - value > 0.4 for name, value in dark_brightness.items()):
                    raise AssertionError("The selected theme command did not change both chrome surfaces.")
                report["checks"].append("Ctrl+Shift+F12 command search executes Use light interface and changes both desktop chrome surfaces from dark to light")
                light_affordances = workspace_affordance_pixels(output / "02a-command-light-theme.png", light=True)
                report["workspace_affordances"]["light_foreground_pixels"] = light_affordances
                assert_workspace_affordances(light_affordances)
                report["checks"].append("Desktop indicators retain contrasting foregrounds after switching to the light theme")

                page.keyboard.press("Control+Shift+F12")
                page.wait_for_timeout(700)
                page.keyboard.type("Use dark interface", delay=30)
                page.keyboard.press("Enter")
                restored_brightness = wait_for_theme(page, output / "02b-command-dark-theme.png", light=False)
                report["theme_command_brightness"] = {"initial_dark": dark_brightness,
                    "command_light": light_brightness, "restored_dark": restored_brightness}
                report["checks"].append("A second command-search invocation executes Use dark interface and restores dark chrome")

                font_changes = chrome_font_changes(wide, output / "02b-command-dark-theme.png")
                font_change_limit = 0.02
                report["initial_chrome_font_stability"] = {
                    "changed_pixel_fraction": font_changes, "maximum_changed_pixel_fraction": font_change_limit}
                if any(value > font_change_limit for value in font_changes.values()):
                    raise AssertionError(f"Initial chrome font/glyphs changed after startup: {font_changes}")
                report["checks"].append("Initial desktop menu labels and font icons match the settled dark workspace")

                page.set_viewport_size({"width": 390, "height": 844})
                wait_ready(page)
                compact = output / "03-workspace-compact.png"
                page.screenshot(path=str(compact))
                assert_rendered(compact)
                report["checks"].append("Workspace renders after resizing to 390x844")
                compact_affordances = workspace_affordance_pixels(compact)
                report["workspace_affordances"]["compact_foreground_pixels"] = compact_affordances
                assert_workspace_affordances(compact_affordances)
                report["checks"].append("Compact tool-column, alternate-tool and paint indicators remain visible")
                rail_changes = compact_panel_rail_changes(output / "02b-command-dark-theme.png", compact)
                report["compact_panel_rail"] = rail_changes
                if any(value > rail_changes["maximum_different_foreground_fraction"]
                       for value in rail_changes["different_foreground_fraction"].values()):
                    raise AssertionError(f"The compact panel rail clips or changes its glyphs: {rail_changes}")
                report["checks"].append("Compact scrolling panel rail preserves all six inspected desktop glyphs")

                page.reload(wait_until="domcontentloaded", timeout=120_000)
                wait_ready(page)
                reopened = output / "04-workspace-reloaded.png"
                page.screenshot(path=str(reopened))
                assert_rendered(reopened)
                report["checks"].append("Published runtime restarts after page reload")
                report["canvases"] = page.locator("canvas").evaluate_all("nodes => nodes.map(n => ({width:n.width,height:n.height}))")
                scopes = page.evaluate("async () => (await navigator.serviceWorker.getRegistrations()).map(r => r.scope)")
                report["service_worker_scopes"] = scopes
                if any(not scope.startswith(app_url) for scope in scopes):
                    raise AssertionError("The application service worker must not control the surrounding documentation")
                manifest = page.evaluate("""async () => {
                    const url = document.querySelector('link[rel="manifest"]').href;
                    const manifest = await (await fetch(url)).json();
                    return { start: new URL(manifest.start_url, url).href, scope: new URL(manifest.scope, url).href };
                }""")
                if manifest["start"] != app_url or manifest["scope"] != app_url:
                    raise AssertionError(f"PWA manifest must stay inside the application folder: {manifest}")
                report["checks"].append("PWA start URL, manifest scope and service worker scopes stay inside the application folder")

                errors = [entry for entry in report["console"] if entry["type"] == "error"
                          and urlsplit(entry["location"].get("url", "")).path != "/favicon.ico"]
                # A browser-generated optional favicon request and navigation-aborted
                # requests are not app failures. Missing scripts/Wasm/fonts remain fatal.
                http_errors = [entry for entry in report["http_errors"] if urlsplit(entry["url"]).path != "/favicon.ico"]
                failures = [entry for entry in report["request_failures"] if entry["failure"] != "net::ERR_ABORTED"]
                if report["page_errors"] or errors or http_errors or failures:
                    raise AssertionError("Browser runtime or resource errors were recorded; inspect report.json and trace.zip")
                report["passed"] = True
            finally:
                if not report["passed"] and page and not page.is_closed():
                    with contextlib.suppress(Exception):
                        page.screenshot(path=str(output / "failure.png"), timeout=5000)
                    with contextlib.suppress(Exception):
                        (output / "failure.html").write_text(page.content(), encoding="utf-8")
                with contextlib.suppress(Exception):
                    context.tracing.stop(path=str(output / "trace.zip"))
                context.close()
                browser.close()
    except Exception as error:
        report["passed"] = False
        report["failure"] = f"{type(error).__name__}: {error}"
    finally:
        report["duration_seconds"] = time.time() - report["started_unix"]
        (output / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(json.dumps(report, indent=2))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
