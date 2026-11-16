#!/usr/bin/env python3
"""Exercise MCP against the real desktop workspace or published browser package."""
from __future__ import annotations

import argparse
import base64
import contextlib
from concurrent.futures import ThreadPoolExecutor
import io
import json
import os
from pathlib import Path
import queue
import secrets
import socket
import subprocess
import threading
import time
from typing import Any, Iterator
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET

from PIL import Image


class Rpc:
    def __init__(self, endpoint: str, token: str) -> None:
        self.endpoint = endpoint
        self.token = token
        self.session: str | None = None
        self.counter = 0

    def request(self, method: str, params: dict[str, Any] | None = None,
                *, notification: bool = False) -> dict[str, Any]:
        self.counter += 1
        body: dict[str, Any] = {"jsonrpc": "2.0", "method": method}
        if not notification:
            body["id"] = self.counter
        if params is not None:
            body["params"] = params
        headers = {"Authorization": "Bearer " + self.token, "Content-Type": "application/json",
                   "Accept": "application/json, text/event-stream"}
        if self.session:
            headers["Mcp-Session-Id"] = self.session
        if method != "initialize":
            headers["MCP-Protocol-Version"] = "2025-11-25"
        request = urllib.request.Request(self.endpoint, json.dumps(body).encode(), headers=headers)
        with urllib.request.urlopen(request, timeout=45) as response:
            self.session = response.headers.get("Mcp-Session-Id", self.session)
            data = response.read().decode()
            if notification:
                assert response.status == 202, response.status
                return {}
            if "text/event-stream" in response.headers.get("Content-Type", ""):
                messages = [json.loads(line[5:].strip()) for line in data.splitlines() if line.startswith("data:")]
                reply = next(item for item in messages if item.get("id") == self.counter)
            else:
                reply = json.loads(data)
        assert reply.get("id") == self.counter, reply
        assert "error" not in reply, reply
        return reply["result"]

    def initialize(self) -> None:
        result = self.request("initialize", {"protocolVersion": "2025-11-25", "capabilities": {},
                                            "clientInfo": {"name": "unodraw-live-smoke", "version": "1.0"}})
        assert result["protocolVersion"] == "2025-11-25", result
        assert result["serverInfo"]["name"] == "UnoDraw", result
        self.request("notifications/initialized", notification=True)

    def tool(self, name: str, arguments: dict[str, Any] | None = None,
             *, fail: bool = False) -> dict[str, Any]:
        result = self.request("tools/call", {"name": "unodraw_" + name, "arguments": arguments or {}})
        if fail:
            assert result.get("isError") is True, result
            return result
        assert result.get("isError") is not True, result
        content = result.get("structuredContent")
        assert isinstance(content, dict), result
        if name == "document_export" and content.get("mimeType", "").startswith("image/") and "base64" not in content:
            images = [item for item in result.get("content", []) if item.get("type") == "image"]
            if images:
                assert len(images) == 1 and images[0]["mimeType"] == content["mimeType"], result
                content = {**content, "base64": images[0]["data"]}
        return content


class StdioRpc(Rpc):
    """Read the actual native host's stdout from process startup through shutdown."""
    def __init__(self, process: subprocess.Popen[bytes], output: Any) -> None:
        super().__init__("", "")
        self.process = process
        self.output = output
        self.deadline = time.monotonic() + 180
        self.messages: queue.Queue[Any] = queue.Queue(maxsize=64)
        self.failure: Exception | None = None
        self.reader = threading.Thread(target=self.read_stdout, name="unodraw-mcp-stdout", daemon=True)
        self.reader.start()

    def read_stdout(self) -> None:
        assert self.process.stdout is not None
        try:
            while True:
                line = self.process.stdout.readline(32 * 1024 * 1024 + 1)
                if not line:
                    self.messages.put(None, timeout=5)
                    return
                self.output.write(line)
                self.output.flush()
                assert line.endswith(b"\n") and len(line) <= 32 * 1024 * 1024, "Incomplete or oversized MCP stdout frame"
                try:
                    message = json.loads(line.decode("utf-8"))
                except (ValueError, UnicodeError) as error:
                    raise AssertionError(f"Non-JSON native stdout: {line[:240]!r}") from error
                assert isinstance(message, dict) and message.get("jsonrpc") == "2.0", message
                assert "result" in message or "error" in message or isinstance(message.get("method"), str), message
                self.messages.put(message, timeout=5)
        except Exception as error:
            self.failure = error
            with contextlib.suppress(queue.Full):
                self.messages.put_nowait(error)

    def check_stdout(self) -> None:
        if self.failure is not None:
            raise AssertionError("Native MCP stdout was not a clean JSON-RPC stream") from self.failure

    def request(self, method: str, params: dict[str, Any] | None = None,
                *, notification: bool = False) -> dict[str, Any]:
        self.check_stdout()
        self.counter += 1
        body: dict[str, Any] = {"jsonrpc": "2.0", "method": method}
        if not notification:
            body["id"] = self.counter
        if params is not None:
            body["params"] = params
        assert self.process.stdin is not None
        self.process.stdin.write((json.dumps(body) + "\n").encode())
        self.process.stdin.flush()
        if notification:
            return {}
        deadline = min(self.deadline, time.monotonic() + (90 if method == "initialize" else 45))
        while True:
            self.check_stdout()
            remaining = deadline - time.monotonic()
            assert remaining > 0, f"Native MCP stdio timed out waiting for {method}"
            try:
                reply = self.messages.get(timeout=remaining)
            except queue.Empty as error:
                raise AssertionError(f"Native MCP stdio timed out waiting for {method}") from error
            assert reply is not None, f"Native MCP stdout ended while waiting for {method} (exit {self.process.poll()})"
            if isinstance(reply, Exception):
                raise AssertionError("Native MCP stdout reader failed") from reply
            if "method" in reply and "id" not in reply:
                continue
            assert reply.get("id") == self.counter and "method" not in reply, reply
            assert "error" not in reply, reply
            return reply["result"]


def free_port() -> int:
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        return int(probe.getsockname()[1])


def wait_endpoint(endpoint: str, process: subprocess.Popen[Any]) -> None:
    deadline = time.monotonic() + 120
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise AssertionError(f"MCP host exited early ({process.returncode})")
        try:
            urllib.request.urlopen(endpoint, timeout=1).close()
        except urllib.error.HTTPError as error:
            assert error.code == 401, f"Unauthenticated MCP returned {error.code}"
            return
        except (urllib.error.URLError, TimeoutError):
            time.sleep(0.2)
    raise AssertionError("MCP endpoint did not start")


@contextlib.contextmanager
def launch(command: list[str], log: Path, environment: dict[str, str]) -> Iterator[subprocess.Popen[Any]]:
    with log.open("wb") as output:
        process = subprocess.Popen(command, stdin=subprocess.DEVNULL, stdout=output, stderr=subprocess.STDOUT,
                                   env={**os.environ, **environment})
        try:
            yield process
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)


@contextlib.contextmanager
def launch_stdio(command: list[str], output: Path, environment: dict[str, str]) -> Iterator[StdioRpc]:
    with (output / "desktop-stderr.log").open("wb") as errors, (output / "desktop-stdout.jsonl").open("wb") as messages:
        process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=errors,
                                   env={**os.environ, **environment})
        rpc = StdioRpc(process, messages)
        try:
            yield rpc
        finally:
            # The native editor can retain its visible window after the MCP input
            # closes. Give the protocol time to stop, then bound process cleanup.
            if process.stdin is not None:
                with contextlib.suppress(BrokenPipeError, OSError):
                    process.stdin.close()
            try:
                process.wait(timeout=2)
            except subprocess.TimeoutExpired:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)
            rpc.reader.join(timeout=5)
            assert not rpc.reader.is_alive(), "Native MCP stdout did not close after process shutdown"
            rpc.check_stdout()
            if process.stdout is not None:
                process.stdout.close()


def export_png(rpc: Rpc, document: str, output: Path) -> bytes:
    result = rpc.tool("document_export", {"documentId": document, "format": "png", "scale": 1})
    assert result["mimeType"] == "image/png", result
    data = base64.b64decode(result["base64"], validate=True)
    with Image.open(io.BytesIO(data)) as image:
        assert image.size == (640, 480), image.size
        image.convert("RGBA").save(output)
        return image.convert("RGBA").tobytes()


def exercise(rpc: Rpc, output: Path) -> dict[str, Any]:
    rpc.initialize()
    tools: list[dict[str, Any]] = []
    cursor: str | None = None
    while True:
        result = rpc.request("tools/list", {"cursor": cursor} if cursor else {})
        tools.extend(result["tools"])
        cursor = result.get("nextCursor")
        if not cursor:
            break
    names = [tool["name"] for tool in tools]
    assert len(names) >= 52 and len(names) == len(set(names)), names
    (output / "tool-catalog.json").write_text(json.dumps(tools, indent=2) + "\n")
    before = rpc.tool("document_list")
    assert before["documents"], before
    opened = rpc.tool("document_new", {"width": 640, "height": 480, "name": "MCP smoke.svg"})
    document = opened["documentId"]
    arguments = {"documentId": document}
    rpc.tool("element_insert", {**arguments, "xml": '''<defs>
      <linearGradient id="mcpGradient"><stop stop-color="#513cff"/><stop offset="1" stop-color="#00b8a9"/></linearGradient>
    </defs><g id="mcpGroup"><rect id="mcpRect" x="40" y="40" width="240" height="160" rx="18" fill="url(#mcpGradient)"/>
    <circle id="mcpCircle" cx="390" cy="130" r="70" fill="#f4b942" stroke="#252535" stroke-width="7"/></g>'''})
    state = rpc.tool("document_get", arguments)
    root = ET.fromstring(state["svg"])
    assert root.find(".//*[@id='mcpRect']") is not None
    baseline = export_png(rpc, document, output / "before.png")
    rpc.tool("selection_set", {**arguments, "elements": ["mcpRect"]})
    rpc.tool("paint", {**arguments, "fill": "#ef476f"})
    changed = export_png(rpc, document, output / "after.png")
    assert baseline != changed, "The native rendered export did not change after painting"
    rpc.tool("edit", {**arguments, "action": "undo"})
    undone = export_png(rpc, document, output / "undo.png")
    assert baseline == undone, "Undo changed native pixels relative to the original document"
    rpc.tool("edit", {**arguments, "action": "redo"})
    redone = export_png(rpc, document, output / "redo.png")
    assert changed == redone, "Redo did not restore the same native pixels"
    current = rpc.tool("document_get", arguments)
    rpc.tool("transform", {**arguments, "dx": 30, "dy": 15, "expectedRevision": current["revision"]})
    transformed = rpc.tool("document_get", arguments)
    rpc.tool("paint", {**arguments, "fill": "#000000", "expectedRevision": current["revision"]}, fail=True)
    final = rpc.tool("document_get", arguments)
    assert final["svg"] == transformed["svg"], "A stale mutation was applied"
    rpc.tool("element_list", {**arguments, "limit": 2})
    rpc.tool("artboard_list", arguments)
    rpc.tool("guide_list", arguments)
    rpc.tool("resource_list", arguments)
    rpc.tool("preflight", arguments)
    commands = rpc.tool("commands_list")
    assert "mcp-connection" in json.dumps(commands)
    rpc.tool("commands_run", {"id": "mcp-connection", "allowInteraction": True}, fail=True)
    rpc.tool("tools_list")
    rpc.tool("view_get")
    rpc.tool("workspace_get")
    rpc.tool("shortcuts_get")
    resources = rpc.request("resources/list")
    assert resources["resources"], resources
    rpc.request("resources/read", {"uri": resources["resources"][0]["uri"]})
    prompts = rpc.request("prompts/list")
    assert prompts["prompts"], prompts
    rpc.tool("document_close", arguments, fail=True)
    rpc.tool("document_close", {**arguments, "discardChanges": True})
    assert len(rpc.tool("document_list")["documents"]) == len(before["documents"])
    return {"passed": True, "tool_count": len(names), "native_export_size": [640, 480],
            "undo_pixels_equal": True, "redo_pixels_equal": True, "stale_revision_rejected": True,
            "dirty_close_rejected": True, "document_closed": document}


def desktop(args: argparse.Namespace) -> dict[str, Any]:
    port = free_port()
    endpoint = f"http://127.0.0.1:{port}/mcp"
    token = secrets.token_hex(32)
    command = [args.dotnet, str(args.desktop), "--mcp-http", "--mcp-port", str(port)]
    environment = {"UNODRAW_MCP_TOKEN": token,
                   "XDG_DATA_HOME": str(args.output / "data"), "XDG_CACHE_HOME": str(args.output / "cache")}
    with launch(command, args.output / "desktop.log", environment) as process:
        wait_endpoint(endpoint, process)
        result = exercise(Rpc(endpoint, token), args.output)
    stdio_output = args.output / "stdio"
    stdio_output.mkdir(parents=True, exist_ok=True)
    stdio_environment = {"XDG_DATA_HOME": str(stdio_output / "data"),
                         "XDG_CACHE_HOME": str(stdio_output / "cache")}
    with launch_stdio([args.dotnet, str(args.desktop), "--mcp-stdio"], stdio_output, stdio_environment) as rpc:
        stdio_result = exercise(rpc, stdio_output)
    result.update({"stdio": stdio_result, "stdio_stdout_json_only": True,
                   "transports": ["streamable-http", "stdio"]})
    return result


def browser(args: argparse.Namespace) -> dict[str, Any]:
    from playwright.sync_api import sync_playwright
    from backend import launch_arguments
    from smoke import serve, wait_ready

    with serve(args.site) as origin, sync_playwright() as playwright:
        port = free_port()
        token, browser_token = secrets.token_hex(32), secrets.token_hex(32)
        endpoint = f"http://127.0.0.1:{port}/mcp"
        command = [args.dotnet, str(args.bridge), "--http", "--origin", origin.rstrip("/"), "--port", str(port)]
        with launch(command, args.output / "bridge.log", {"UNODRAW_MCP_TOKEN": token,
                    "UNODRAW_MCP_BROWSER_TOKEN": browser_token}) as process:
            wait_endpoint(endpoint, process)
            engine = playwright.chromium.launch(headless=True, args=launch_arguments("skia-default"))
            try:
                page = engine.new_page(viewport={"width": 1440, "height": 960}, device_scale_factor=1)
                errors: list[str] = []
                console: list[str] = []
                page.on("pageerror", lambda error: errors.append(str(error)))
                page.on("console", lambda message: console.append(message.text))
                page.goto(origin.rstrip("/") + args.base_path + "?inputDiagnostics=1", wait_until="domcontentloaded")
                wait_ready(page)
                initial = page.evaluate("""async () => {
                    globalThis.mcpExports = (await Module.getAssemblyExports('UnoDraw')).UnoDraw.BrowserMcpConnection;
                    return JSON.parse(mcpExports.Status());
                }""")
                assert initial["connected"] is False, initial
                attached = page.evaluate("""async ({endpoint, token}) => JSON.parse(
                    await mcpExports.Connect(endpoint, token, false))""",
                    {"endpoint": f"ws://127.0.0.1:{port}/editor", "token": browser_token})
                assert attached["connected"] is True, attached
                result = exercise(Rpc(endpoint, token), args.output)
                page.screenshot(path=str(args.output / "browser.png"))
                interactive = Rpc(endpoint, token)
                interactive.initialize()
                documents_before_dialog = sorted(item["documentId"] for item in interactive.tool("document_list")["documents"])
                console_start = len(console)
                with ThreadPoolExecutor(max_workers=1) as executor:
                    pending = executor.submit(interactive.tool, "commands_run",
                                              {"id": "new", "allowInteraction": True}, fail=True)
                    deadline = time.monotonic() + 15
                    while not any("modal-opened" in message for message in console[console_start:]):
                        assert not pending.done(), "The interactive MCP request ended before opening its native dialog"
                        assert time.monotonic() < deadline, "The MCP command did not open its native dialog"
                        page.wait_for_timeout(100)
                    detached = page.evaluate("async () => JSON.parse(await mcpExports.Disconnect())")
                    assert detached["connected"] is False, detached
                    try:
                        pending.result(timeout=15)
                    except (AssertionError, urllib.error.URLError):
                        # Cancellation can be a JSON-RPC error or a tool error, depending
                        # on whether the HTTP client or attachment closes first.
                        pass
                assert any("modal-closed" in message for message in console[console_start:]), console[console_start:]
                disconnected = Rpc(endpoint, token)
                disconnected.initialize()
                disconnected.tool("document_list", fail=True)
                attached = page.evaluate("""async ({endpoint, token}) => JSON.parse(
                    await mcpExports.Connect(endpoint, token, true))""",
                    {"endpoint": f"ws://127.0.0.1:{port}/editor", "token": browser_token})
                assert attached["connected"] and attached["readOnly"], attached
                readonly = Rpc(endpoint, token)
                readonly.initialize()
                documents_after_dialog = sorted(item["documentId"] for item in readonly.tool("document_list")["documents"])
                assert documents_after_dialog == documents_before_dialog, "Canceling the MCP dialog created a document"
                readonly.tool("document_new", fail=True)
                page.evaluate("async () => await mcpExports.Disconnect()")
                assert not errors, errors
                result.update({"read_only_rejected_mutation": True, "disconnect_rejected_calls": True,
                               "owned_dialog_cancelled_on_disconnect": True,
                               "page_errors": errors, "renderer": "skia-default"})
                return result
            finally:
                engine.close()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group(required=True)
    modes.add_argument("--desktop", type=Path, help="Built UnoDraw.dll; run inside Xvfb on Linux CI")
    modes.add_argument("--site", type=Path, help="Root containing the published browser assets")
    parser.add_argument("--bridge", type=Path, help="Built UnoDraw.Mcp.dll for browser mode")
    parser.add_argument("--base-path", default="/Svg.Skia/unodraw/")
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.site and not args.bridge:
        parser.error("--site requires --bridge")
    args.output = args.output.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
    result: dict[str, Any] = {"passed": False}
    try:
        result = desktop(args) if args.desktop else browser(args)
    finally:
        (args.output / "mcp-report.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
