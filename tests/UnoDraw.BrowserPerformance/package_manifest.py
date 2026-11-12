#!/usr/bin/env python3
"""Record the source and build contract beside a published benchmark package."""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from pathlib import Path

from toolchain import metadata


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--role", choices=("reference", "baseline", "candidate"), required=True)
    args = parser.parse_args()
    source, package = args.source.resolve(), args.package.resolve()
    if not (package / "wwwroot/index.html").is_file():
        parser.error("--package must contain the published wwwroot/index.html")
    toolchain = metadata(source)
    uno_sdk = toolchain["source_global_json"]["msbuild-sdks"]["Uno.Sdk"]
    fonts = {str(path.relative_to(package / "wwwroot")): digest(path)
             for path in sorted((package / "wwwroot").rglob("*"))
             if path.is_file() and path.suffix.lower() in {".ttf", ".otf", ".woff", ".woff2"}}
    manifest = {
        "schema": 2, "role": args.role,
        "source_sha": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=source, text=True).strip(),
        **toolchain,
        "dotnet_workload_info": subprocess.check_output(["dotnet", "workload", "--info"], cwd=source, text=True).strip(),
        "uno_sdk": uno_sdk,
        "runtime_generation": "uno6" if uno_sdk.startswith("6.") else "uno7",
        "configuration": "Release", "execution_mode": "InterpreterAndAOT",
        "fixture": "samples/UnoDraw/Assets/Welcome.svg",
        "fixture_sha256": digest(source / "samples/UnoDraw/Assets/Welcome.svg"),
        "index_sha256": digest(package / "wwwroot/index.html"),
        "font_assets_sha256": fonts,
        "supports_render_diagnostics": (source / "samples/UnoDraw/Platforms/WebAssembly/BrowserRenderDiagnostics.cs").is_file(),
    }
    (package / "benchmark-package.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
