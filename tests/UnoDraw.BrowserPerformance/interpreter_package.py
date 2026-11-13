#!/usr/bin/env python3
"""Bind shared Interpreter packages to their producing job and complete file inventory."""
from __future__ import annotations

import argparse
import json
import os
import re
from pathlib import Path
from typing import Any

from pinned_package import digest, inventory


INVENTORY_NAME = "interpreter-package-inventory.json"
ROLES = frozenset({"candidate", "baseline"})
IDENTITY_FIELDS = ("role", "source_sha", "configuration", "execution_mode",
                   "dotnet_sdk", "dotnet_workload_version")


def package_identity(role: str, source_sha: str) -> dict[str, str]:
    if role not in ROLES or not re.fullmatch(r"[0-9a-f]{40}", source_sha):
        raise ValueError("A supported Interpreter role and exact source SHA are required")
    return {"role": role, "source_sha": source_sha, "configuration": "Release",
            "execution_mode": "Interpreter", "dotnet_sdk": "10.0.403",
            "dotnet_workload_version": "10.0.403"}


def verify_manifest(package: Path, files: dict[str, str], identity: dict[str, Any]) -> dict[str, Any]:
    manifest = json.loads((package / "benchmark-package.json").read_text(encoding="utf-8"))
    if (manifest.get("schema") != 2
            or any(manifest.get(key) != identity[key] for key in IDENTITY_FIELDS)):
        raise ValueError("The packaged source manifest differs from its producer identity")
    if (files.get("wwwroot/index.html") != manifest["index_sha256"]
            or not manifest["font_assets_sha256"]
            or any(files.get("wwwroot/" + name) != value
                   for name, value in manifest["font_assets_sha256"].items())):
        raise ValueError("The packaged index or fonts differ from the source manifest")
    return manifest


def create_inventory(package: Path, role: str, source_sha: str, repository: str,
                     run_id: str, run_attempt: str) -> Path:
    """Run after publishing and creating the existing source/fixture manifest."""
    identity = package_identity(role, source_sha)
    files = inventory(package)
    verify_manifest(package, files, identity)
    record = {"schema": 1, "repository": repository, "workflow_run_id": run_id,
              "producer_run_attempt": run_attempt, **identity,
              "manifest_sha256": files["benchmark-package.json"],
              "package_files_sha256": files, "package_file_count": len(files)}
    target = package / INVENTORY_NAME
    # The inventory is the only addition after fingerprinting the published files.
    # Refuse an existing inventory instead of silently rebinding a modified package.
    with target.open("x", encoding="utf-8") as output:
        output.write(json.dumps(record, sort_keys=True, indent=2) + "\n")
    return target


def verify_packages(bindings: dict[str, Any], expected_roles: set[str], output: Path,
                    repository: str, run_id: str) -> dict[str, Any]:
    """Check the producer-bound bytes before or after the unchanged browser suite.

    Artifact IDs select immutable same-run downloads. The archive digest is kept
    as producer provenance; this function verifies extracted files against the
    producer's complete inventory, not by independently downloading the ZIP again.
    """
    if not expected_roles or not expected_roles <= ROLES or set(bindings) != expected_roles:
        raise ValueError("The required package-role set differs from this consumer")
    output = output.resolve()
    verified = {}
    for role, binding in bindings.items():
        package = Path(binding["path"]).resolve()
        if package == output or package in output.parents:
            raise ValueError("Verification results must stay outside the application package")
        artifact_id = binding["artifact_id"]
        artifact_digest = binding["artifact_digest"].removeprefix("sha256:")
        if (not isinstance(artifact_id, str) or not artifact_id.isdecimal() or int(artifact_id) <= 0
                or not re.fullmatch(r"[0-9a-f]{64}", artifact_digest)
                or not re.fullmatch(r"[0-9a-f]{64}", binding["inventory_sha256"])):
            raise ValueError("The successful producer did not expose immutable artifact identity")
        identity = package_identity(role, binding["source_sha"])
        inventory_path = package / INVENTORY_NAME
        if digest(inventory_path) != binding["inventory_sha256"]:
            raise ValueError("The downloaded inventory differs from the producer output")
        record = json.loads(inventory_path.read_text(encoding="utf-8"))
        expected = {"schema": 1, "repository": repository, "workflow_run_id": run_id, **identity}
        if any(record.get(key) != value for key, value in expected.items()):
            raise ValueError("The artifact came from a different run, source, role or execution mode")
        actual = inventory(package)
        del actual[INVENTORY_NAME]
        if actual != record["package_files_sha256"] or len(actual) != record["package_file_count"]:
            raise ValueError("A packaged file was added, removed or changed after publication")
        if actual["benchmark-package.json"] != record["manifest_sha256"]:
            raise ValueError("The packaged source manifest differs from the producer")
        manifest = verify_manifest(package, actual, identity)
        verified[role] = {"artifact_id": int(artifact_id), "producer_artifact_digest": artifact_digest,
                          "artifact_name": "unodraw-interpreter-package-" + role,
                          "inventory_sha256": binding["inventory_sha256"], "inventory": record,
                          "manifest": manifest, "all_packaged_bytes_match_producer": True}
    return {"schema": 1, "packages": verified}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("create", help="Fingerprint the newly published package")
    create.add_argument("--package", type=Path, required=True)
    create.add_argument("--role", choices=sorted(ROLES), required=True)
    commands.add_parser("verify", help="Verify PACKAGE_BINDINGS and write VERIFICATION_OUTPUT")
    args = parser.parse_args()
    repository, run_id = os.environ["GITHUB_REPOSITORY"], os.environ["GITHUB_RUN_ID"]
    if args.command == "create":
        target = create_inventory(args.package, args.role, os.environ["SOURCE_SHA"], repository,
                                  run_id, os.environ["GITHUB_RUN_ATTEMPT"])
        with Path(os.environ["GITHUB_OUTPUT"]).open("a", encoding="utf-8") as output:
            output.write("sha256=" + digest(target) + "\n")
    else:
        output = Path(os.environ["VERIFICATION_OUTPUT"])
        result = verify_packages(json.loads(os.environ["PACKAGE_BINDINGS"]),
                                 set(os.environ["EXPECTED_ROLES"].split(",")), output, repository, run_id)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        print("Verified exact same-run Interpreter package bytes:", ", ".join(sorted(result["packages"])))


if __name__ == "__main__":
    main()
