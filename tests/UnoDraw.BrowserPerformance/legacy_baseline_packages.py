#!/usr/bin/env python3
"""Verify existing workflow artifacts for an explicit baseline/opt-in comparison."""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import zipfile
from pathlib import Path
from typing import Any

from pinned_package import (FIXTURE_SHA256, PINS, REPOSITORY, archived_site_inventory,
                            canonical_digest, digest, extract_verified, inventory)


BASELINE_SHA = PINS["baseline"]["source_sha"]
ROLES = ("baseline", "candidate")
RECEIPT = "baseline-comparison-package-receipt.json"


def validate_descriptors(run_id: int, run: dict[str, Any], artifacts: list[dict[str, Any]]) -> dict[str, Any]:
    if (type(run_id) is not int or run_id <= 0 or run.get("id") != run_id
            or run.get("path") != ".github/workflows/unodraw-performance.yml"
            or run.get("repository", {}).get("full_name") != REPOSITORY
            or not re.fullmatch(r"[0-9a-f]{40}", run.get("head_sha", ""))):
        raise ValueError("Use an identified UnoDraw performance run from this repository")
    identified = {}
    for role in ROLES:
        matches = [artifact for artifact in artifacts if artifact.get("name") == f"unodraw-performance-package-{role}"]
        if len(matches) != 1:
            raise ValueError(f"The run must contain exactly one completed {role} package artifact")
        artifact = matches[0]
        origin = artifact.get("workflow_run", {})
        if (type(artifact.get("id")) is not int or artifact["id"] <= 0
                or type(artifact.get("size_in_bytes")) is not int or artifact["size_in_bytes"] <= 0
                or artifact.get("expired") is not False
                or not re.fullmatch(r"sha256:[0-9a-f]{64}", artifact.get("digest", ""))
                or origin.get("id") != run_id or origin.get("head_sha") != run["head_sha"]):
            raise ValueError("A package artifact lacks immutable digest or matching workflow provenance")
        identified[role] = artifact
    return {"repository": REPOSITORY, "run_id": run_id, "run_head_sha": run["head_sha"],
            "workflow_path": run["path"], "artifacts": identified}


def fetch_descriptors(run_id: int) -> dict[str, Any]:
    prefix = f"repos/{REPOSITORY}/actions/runs/{run_id}"
    run = json.loads(subprocess.check_output(["gh", "api", prefix], text=True))
    pages = json.loads(subprocess.check_output(
        ["gh", "api", prefix + "/artifacts?per_page=100", "--paginate", "--slurp"], text=True))
    return validate_descriptors(run_id, run, [artifact for page in pages for artifact in page["artifacts"]])


def verify_package(directory: Path, role: str, descriptors: dict[str, Any]) -> dict[str, Any]:
    if role not in ROLES:
        raise ValueError("Use the baseline or candidate package role")
    directory = directory.resolve()
    artifact = descriptors["artifacts"][role]
    receipt = json.loads((directory / RECEIPT).read_text(encoding="utf-8"))
    if (receipt.get("descriptor") != descriptors or receipt.get("role") != role
            or digest(directory / "artifact.zip") != artifact["digest"].removeprefix("sha256:")
            or (directory / "artifact.zip").stat().st_size != artifact["size_in_bytes"]):
        raise ValueError("The package receipt or archive differs from the verified GitHub artifact")
    with zipfile.ZipFile(directory / "artifact.zip") as archive:
        archived_manifest = json.loads(archive.read("benchmark-package.json"))
    package = json.loads((directory / "benchmark-package.json").read_text(encoding="utf-8"))
    if package != archived_manifest or canonical_digest(package) != receipt.get("manifest_sha256"):
        raise ValueError("The package manifest differs from the immutable archive")
    expected = {"schema": 2, "role": role, "fixture_sha256": FIXTURE_SHA256, "configuration": "Release",
                "execution_mode": "InterpreterAndAOT", "runtime_generation": "uno7", "uno_sdk": "7.0.5",
                "dotnet_sdk": "10.0.403", "dotnet_workload_version": "10.0.403"}
    if (any(package.get(key) != value for key, value in expected.items())
            or not re.fullmatch(r"[0-9a-f]{40}", package.get("source_sha", ""))
            or not re.fullmatch(r"[0-9a-f]{64}", package.get("index_sha256", ""))):
        raise ValueError("The package does not identify the controlled Uno 7 Release AOT build")
    if role == "baseline" and canonical_digest(package) != PINS["baseline"]["manifest_sha256"]:
        raise ValueError("The baseline must retain the exact 96e source, index, toolchain and font manifest")
    if role == "candidate" and package.get("supports_render_diagnostics") is not True:
        raise ValueError("The opt-in candidate must expose separate managed paint diagnostics")
    actual = inventory(directory / "wwwroot")
    if (actual != archived_site_inventory(directory / "artifact.zip") or actual != receipt.get("wwwroot_sha256")
            or actual.get("index.html") != package["index_sha256"]
            or any(actual.get(name) != value for name, value in package["font_assets_sha256"].items())):
        raise ValueError("A served binary, runtime, font or other application asset differs from the archive")
    return {**package, "path": str(directory)}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-id", type=int, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.run_id <= 0:
        parser.error("Use a positive immutable workflow run ID")
    output = args.output.resolve()
    if output.exists() and any(output.iterdir()):
        parser.error("The package destination must be empty")
    descriptors = fetch_descriptors(args.run_id)
    for role in ROLES:
        directory = output / role
        directory.mkdir(parents=True, exist_ok=True)
        artifact = descriptors["artifacts"][role]
        archive = directory / "artifact.zip"
        with archive.open("xb") as stream:
            subprocess.run(["gh", "api", f"repos/{REPOSITORY}/actions/artifacts/{artifact['id']}/zip"],
                           stdout=stream, check=True)
        extract_verified(archive, directory, artifact["digest"].removeprefix("sha256:"))
        manifest = json.loads((directory / "benchmark-package.json").read_text(encoding="utf-8"))
        receipt = {"schema": 1, "descriptor": descriptors, "role": role,
                   "manifest_sha256": canonical_digest(manifest), "wwwroot_sha256": inventory(directory / "wwwroot")}
        (directory / RECEIPT).write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
        package = verify_package(directory, role, descriptors)
        print(f"Verified {role} artifact {artifact['id']} at {package['source_sha']}", flush=True)


if __name__ == "__main__":
    main()
