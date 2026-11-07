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
