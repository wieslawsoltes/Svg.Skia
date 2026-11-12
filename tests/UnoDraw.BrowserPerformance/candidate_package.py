#!/usr/bin/env python3
"""Acquire one immutable candidate package without rebuilding or downloading controls.

This receipt proves package bytes and workflow provenance. Source eligibility is
deliberately a separate verify_source.py step, which can report stale production
source without treating an otherwise valid archive as a failed acquisition.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import zipfile
from pathlib import Path, PurePosixPath
from typing import Any

from pinned_package import (FIXTURE_SHA256, REPOSITORY, archived_site_inventory,
                            canonical_digest, digest, extract_verified, inventory)


WORKFLOW_PATH = ".github/workflows/unodraw-performance.yml"
ARTIFACT_NAME = "unodraw-performance-package-candidate"
RECEIPT_KIND = "verified_unodraw_candidate_package"
RECEIPT_KEYS = frozenset({
    "schema", "kind", "repository", "workflow_run_id", "workflow_path", "workflow_head_sha",
    "package_source_sha", "artifact", "manifest", "manifest_sha256", "wwwroot_sha256",
    "wwwroot_inventory_sha256", "served_file_count", "verified",
})


def _sha(value: Any, length: int = 64) -> bool:
    return isinstance(value, str) and re.fullmatch(r"[0-9a-f]{" + str(length) + r"}", value) is not None


def _positive_integer(value: Any) -> bool:
    return type(value) is int and value > 0


def _site_inventory(value: Any) -> bool:
    if not isinstance(value, dict) or not value:
        return False
    for name, fingerprint in value.items():
        if not isinstance(name, str) or not name or "\\" in name or not _sha(fingerprint):
            return False
        path = PurePosixPath(name)
        if not path.parts or path.is_absolute() or ".." in path.parts or path.as_posix() != name:
            return False
    return True


def validate_descriptor(run_id: int, run: dict[str, Any], artifacts: list[dict[str, Any]]) -> dict[str, Any]:
    """Normalize fresh GitHub metadata without retaining URLs or timestamps."""
    if (not _positive_integer(run_id) or not _positive_integer(run.get("id")) or run.get("id") != run_id
            or run.get("path") != WORKFLOW_PATH or run.get("repository", {}).get("full_name") != REPOSITORY
            or not _sha(run.get("head_sha"), 40)):
        raise ValueError("Use an identified UnoDraw performance workflow run from this repository")
    matches = [artifact for artifact in artifacts if artifact.get("name") == ARTIFACT_NAME]
    if len(matches) != 1:
        raise ValueError("The run must contain exactly one completed candidate package artifact")
    artifact = matches[0]
    origin = artifact.get("workflow_run", {})
    if (not _positive_integer(artifact.get("id")) or not _positive_integer(artifact.get("size_in_bytes"))
            or artifact.get("expired") is not False
            or not isinstance(artifact.get("digest"), str)
            or not re.fullmatch(r"sha256:[0-9a-f]{64}", artifact["digest"])
            or not _positive_integer(origin.get("id")) or origin.get("id") != run_id
            or origin.get("head_sha") != run["head_sha"]):
        raise ValueError("The candidate artifact lacks immutable digest or matching workflow provenance")
    return {"repository": REPOSITORY, "workflow_run_id": run_id, "workflow_path": WORKFLOW_PATH,
            "workflow_head_sha": run["head_sha"],
            "artifact": {"id": artifact["id"], "name": ARTIFACT_NAME,
                         "archive_sha256": artifact["digest"].removeprefix("sha256:"),
                         "archive_bytes": artifact["size_in_bytes"]}}


def fetch_descriptor(run_id: int) -> dict[str, Any]:
    prefix = f"repos/{REPOSITORY}/actions/runs/{run_id}"
    run = json.loads(subprocess.check_output(["gh", "api", prefix], text=True))
    pages = json.loads(subprocess.check_output(
        ["gh", "api", prefix + "/artifacts?per_page=100", "--paginate", "--slurp"], text=True))
    return validate_descriptor(run_id, run, [artifact for page in pages for artifact in page["artifacts"]])


def validate_receipt(receipt: dict[str, Any]) -> None:
    """Validate portable receipt structure; performs no network or browser work."""
    if (not isinstance(receipt, dict) or set(receipt) != RECEIPT_KEYS
            or type(receipt.get("schema")) is not int or receipt.get("schema") != 1 or receipt.get("kind") != RECEIPT_KIND
            or receipt.get("verified") is not True or receipt.get("repository") != REPOSITORY
            or receipt.get("workflow_path") != WORKFLOW_PATH
            or not _positive_integer(receipt.get("workflow_run_id"))
            or not _sha(receipt.get("workflow_head_sha"), 40)
            or not _sha(receipt.get("package_source_sha"), 40)):
        raise ValueError("The candidate receipt does not identify its immutable workflow and source")
    artifact = receipt.get("artifact", {})
    if (not isinstance(artifact, dict) or set(artifact) != {"id", "name", "archive_sha256", "archive_bytes"}
            or not _positive_integer(artifact.get("id")) or artifact.get("name") != ARTIFACT_NAME
            or not _positive_integer(artifact.get("archive_bytes")) or not _sha(artifact.get("archive_sha256"))):
        raise ValueError("The candidate receipt lacks the original artifact identity and archive digest")
    manifest = receipt.get("manifest")
    expected = {"schema": 2, "role": "candidate", "source_sha": receipt["package_source_sha"],
                "fixture_sha256": FIXTURE_SHA256, "configuration": "Release", "execution_mode": "InterpreterAndAOT",
                "runtime_generation": "uno7", "uno_sdk": "7.0.5", "dotnet_sdk": "10.0.403",
                "dotnet_workload_version": "10.0.403", "supports_render_diagnostics": True}
    if (not isinstance(manifest, dict) or "path" in manifest
            or type(manifest.get("schema")) is not int or manifest.get("supports_render_diagnostics") is not True
            or any(manifest.get(key) != value for key, value in expected.items())
            or not _sha(manifest.get("index_sha256"))
            or not _sha(receipt.get("manifest_sha256"))
            or canonical_digest(manifest) != receipt["manifest_sha256"]):
        raise ValueError("The complete candidate manifest changed its source, build, fixture or canonical digest")
    files = receipt.get("wwwroot_sha256")
    fonts = manifest.get("font_assets_sha256")
    if (not _site_inventory(files) or not _site_inventory(fonts)
            or not _positive_integer(receipt.get("served_file_count"))
            or receipt["served_file_count"] != len(files)
            or not _sha(receipt.get("wwwroot_inventory_sha256"))
            or canonical_digest(files) != receipt["wwwroot_inventory_sha256"]
            or files.get("index.html") != manifest["index_sha256"]
            or any(files.get(name) != fingerprint for name, fingerprint in fonts.items())):
        raise ValueError("The complete served-file inventory, packaged fonts or index differs from its receipt")


def create_receipt(directory: Path, descriptor: dict[str, Any]) -> dict[str, Any]:
    manifest = json.loads((directory / "benchmark-package.json").read_text(encoding="utf-8"))
    files = inventory(directory / "wwwroot")
    receipt = {"schema": 1, "kind": RECEIPT_KIND, **descriptor,
               "package_source_sha": manifest.get("source_sha"), "manifest": manifest,
               "manifest_sha256": canonical_digest(manifest), "wwwroot_sha256": files,
               "wwwroot_inventory_sha256": canonical_digest(files), "served_file_count": len(files), "verified": True}
    verify_package(directory, receipt, descriptor=descriptor)
    return receipt


def verify_package(directory: Path, receipt: dict[str, Any], *,
                   descriptor: dict[str, Any] | None = None) -> dict[str, Any]:
    """Rebind extracted bytes and receipt to the original archive, before or after use."""
    validate_receipt(receipt)
    if descriptor is not None and any(receipt.get(key) != value for key, value in descriptor.items()):
        raise ValueError("The candidate receipt differs from freshly fetched GitHub artifact metadata")
    directory = directory.resolve()
    archive = directory / "artifact.zip"
    if digest(archive) != receipt["artifact"]["archive_sha256"] or archive.stat().st_size != receipt["artifact"]["archive_bytes"]:
        raise ValueError("The candidate archive differs from its verified GitHub identity")
    with zipfile.ZipFile(archive) as compressed:
        archived_manifest = json.loads(compressed.read("benchmark-package.json"))
    manifest = json.loads((directory / "benchmark-package.json").read_text(encoding="utf-8"))
    if manifest != archived_manifest or manifest != receipt["manifest"]:
        raise ValueError("The extracted candidate manifest differs from the original archive")
    actual, archived = inventory(directory / "wwwroot"), archived_site_inventory(archive)
    if actual != archived or actual != receipt["wwwroot_sha256"]:
        raise ValueError("A served binary, runtime, font or other asset differs from the original candidate archive")
    return {**manifest, "path": str(directory)}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-id", type=int, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--receipt", type=Path, required=True)
    args = parser.parse_args()
    if args.run_id <= 0:
        parser.error("Use a positive immutable workflow run ID")
    output, receipt_path = args.output.resolve(), args.receipt.resolve()
    if output.exists() and any(output.iterdir()):
        parser.error("The package destination must be empty")
    if (receipt_path.exists() or receipt_path == output or output / "wwwroot" in receipt_path.parents
            or receipt_path == output / "wwwroot" or receipt_path.name in {"artifact.zip", "benchmark-package.json"}):
        parser.error("Use a new receipt file outside the served application and original package files")
    descriptor = fetch_descriptor(args.run_id)
    output.mkdir(parents=True, exist_ok=True)
    archive = output / "artifact.zip"
    # gh owns authentication and redirects; GH_TOKEN/GITHUB_TOKEN is never read,
    # copied into another request, logged, or retained in the portable receipt.
    endpoint = f"repos/{REPOSITORY}/actions/artifacts/{descriptor['artifact']['id']}/zip"
    with archive.open("xb") as stream:
        subprocess.run(["gh", "api", endpoint], stdout=stream, check=True)
    if archive.stat().st_size != descriptor["artifact"]["archive_bytes"]:
        raise ValueError("The downloaded candidate archive size differs from GitHub metadata")
    extract_verified(archive, output, descriptor["artifact"]["archive_sha256"])
    receipt = create_receipt(output, descriptor)
    receipt_path.parent.mkdir(parents=True, exist_ok=True)
    with receipt_path.open("x", encoding="utf-8") as stream:
        stream.write(json.dumps(receipt, indent=2) + "\n")
    print(f"Verified candidate artifact {receipt['artifact']['id']} at source {receipt['package_source_sha']} "
          f"with {receipt['served_file_count']} original served files", flush=True)


if __name__ == "__main__":
    main()
