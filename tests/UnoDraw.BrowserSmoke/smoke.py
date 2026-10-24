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

from PIL import Image, ImageChops
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
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
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


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--site", type=Path, required=True, help="Published wwwroot directory")
    parser.add_argument("--output", type=Path, required=True, help="Diagnostic artifact directory")
    args = parser.parse_args()
    site, output = args.site.resolve(), args.output.resolve()
    if not (site / "index.html").is_file():
        parser.error("--site must contain the published index.html")
    output.mkdir(parents=True, exist_ok=True)
    if output == site or site in output.parents:
        parser.error("--output must be outside the served site")

    report: dict[str, Any] = {
        "passed": False,
        "started_unix": time.time(),
        "site": str(site),
        "index_sha256": hashlib.sha256((site / "index.html").read_bytes()).hexdigest(),
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
                page.goto(url, wait_until="domcontentloaded", timeout=120_000)
                wait_ready(page)
                wide = output / "01-workspace-desktop.png"
                page.screenshot(path=str(wide))
                assert_rendered(wide)
                report["checks"].append("Published runtime starts and renders a nonblank desktop workspace")

                page.keyboard.press("Control+k")
                page.wait_for_timeout(700)
                palette = output / "02-command-palette.png"
                page.screenshot(path=str(palette))
                difference = changed_pixels(wide, palette)
                if difference < 2000:
                    raise AssertionError(f"Command search did not visibly respond ({difference} changed pixels)")
                report["checks"].append(f"Ctrl+K produces a visible command-search response ({difference} changed pixels)")
                page.keyboard.press("Escape")
                page.wait_for_timeout(500)

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
