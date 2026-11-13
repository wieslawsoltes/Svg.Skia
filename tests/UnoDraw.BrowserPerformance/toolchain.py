#!/usr/bin/env python3
"""Apply and verify the benchmark's exact SDK/workload selection in a checkout."""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import subprocess
from pathlib import Path
from typing import Any

DOTNET_SDK = "10.0.403"
WORKLOAD_SET = "10.0.403"


def original_configuration(source: Path) -> tuple[dict[str, Any], bytes]:
    # Read the committed bytes, not the benchmark's temporary build override.
    raw = subprocess.check_output(["git", "show", "HEAD:global.json"], cwd=source)
    return json.loads(raw), raw


def pinned_configuration(original: dict[str, Any]) -> dict[str, Any]:
    effective = copy.deepcopy(original)
    effective.setdefault("sdk", {}).update({
        "version": DOTNET_SDK, "rollForward": "disable", "allowPrerelease": False,
        "workloadVersion": WORKLOAD_SET,
    })
    return effective


def pin(source: Path) -> None:
    original, _ = original_configuration(source)
    effective = pinned_configuration(original)
    path = source / "global.json"
    current = json.loads(path.read_bytes())
    if current != original and current != effective:
        raise ValueError("Refusing to replace unrelated local global.json changes")
    path.write_text(json.dumps(effective, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"source_global_json": original, "effective_global_json": effective}, indent=2))


def validate_versions(sdk: str, workload: str) -> None:
    if sdk != DOTNET_SDK:
        raise ValueError(f"Expected .NET SDK {DOTNET_SDK}, actually selected {sdk}")
    if workload != WORKLOAD_SET:
        raise ValueError(f"Expected workload set {WORKLOAD_SET}, actually selected {workload}")


def metadata(source: Path) -> dict[str, Any]:
    original, original_raw = original_configuration(source)
    effective_raw = (source / "global.json").read_bytes()
    effective = json.loads(effective_raw)
    if effective != pinned_configuration(original):
        raise ValueError("The checkout must use the benchmark's exact SDK/workload override")
    sdk = subprocess.check_output(["dotnet", "--version"], cwd=source, text=True).strip()
    workload = subprocess.check_output(["dotnet", "workload", "--version"], cwd=source, text=True).strip()
    validate_versions(sdk, workload)
    return {
        "dotnet_sdk": sdk, "dotnet_workload_version": workload,
        "source_global_json": original,
        "source_global_json_sha256": hashlib.sha256(original_raw).hexdigest(),
        "effective_global_json": effective,
        "effective_global_json_sha256": hashlib.sha256(effective_raw).hexdigest(),
        "source_configuration_overridden": effective != original,
        "configuration_override_reason": "Identical SDK and workload set across benchmark revisions",
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--pin", action="store_true", help="Change only SDK/workload selection in this checkout")
    parser.add_argument("--github-output", type=Path, help="Verify and append exact cache identities")
    args = parser.parse_args()
    source = args.source.resolve()
    if args.pin:
        pin(source)
    else:
        result = metadata(source)
        print(json.dumps(result, indent=2))
        if args.github_output:
            sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=source, text=True).strip()
            with args.github_output.open("a", encoding="utf-8") as output:
                output.write(f"sha={sha}\nsdk={result['dotnet_sdk']}\nworkload={result['dotnet_workload_version']}\n")


if __name__ == "__main__":
    main()
