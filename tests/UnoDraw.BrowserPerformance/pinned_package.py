#!/usr/bin/env python3
"""Fetch and verify the two immutable AOT packages used by historical diagnostics."""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import stat
import subprocess
import zipfile
from pathlib import Path, PurePosixPath
from typing import Any


REPOSITORY = "wieslawsoltes/Svg.Skia"
RUN_ID = 43265614141
RUN_HEAD = "d0dafea8b705bf5e706ee8f5c008663d2d71fa12"
FIXTURE_SHA256 = "ac0841edc1a9928623c801ecf191da5ae4e3d7fb50ca7b4c64b83ec8a5f33d94"
PINS = {
    "baseline": {
        "artifact_id": 13964939029,
        "archive_sha256": "225d9d2ee21f52c868532410990c60ea6edf9bc7dbe699bf91f4e9a009d5bee6",
        "archive_bytes": 174069916,
        "source_sha": "96e5fa9d84b694e34a626b8867f2466fc231b6a7",
        "index_sha256": "fd126347cfcb1544ab572570aa6e411b3fd9fe604c737b44fb294f1e08bb6290",
        "manifest_sha256": "ff549fafca5fabb0abfdf1a52deb91540e1c40ae7071a5f521f63a59b621c266",
    },
    "candidate": {
        "artifact_id": 13967029731,
        "archive_sha256": "1c3716d3eecfb3f0a245686c09d0f876d2b2624988108483a551d93f463f7524",
        "archive_bytes": 175804274,
        "source_sha": RUN_HEAD,
        "index_sha256": "a0622ea730064ee73459f0e044fde058100bc9f8478124521a2a7a6c0091d510",
        "manifest_sha256": "1e594957d4c93c47918e8aa1e8974de9655075cbb688291490b7ce8cc4ef46a1",
    },
}
RECEIPT = "pinned-package-receipt.json"
PUBLISHED_ROOT_FILES = frozenset({"benchmark-package.json", "Uno.Fonts.Fluent.uprimarker",
                                 "Uno.Fonts.OpenSans.uprimarker", "Uno.UI.Runtime.WebAssembly.Browser.uprimarker",
                                 "UnoDraw.runtimeconfig.json", "UnoDraw.staticwebassets.endpoints.json",
                                 "dotnet.js", "web.config"})


def digest(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def canonical_digest(value: Any) -> str:
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def inventory(directory: Path) -> dict[str, str]:
    files = {}
    for path in sorted(directory.rglob("*")):
        if path.is_symlink():
            raise ValueError("A diagnostic package must not contain symbolic links")
        if path.is_file():
            files[path.relative_to(directory).as_posix()] = digest(path)
    if not files:
        raise ValueError("The diagnostic package is empty")
    return files


def verify_metadata(metadata: dict[str, Any], role: str) -> None:
    pin = PINS[role]
    expected = {"id": pin["artifact_id"], "name": f"unodraw-performance-package-{role}",
                "size_in_bytes": pin["archive_bytes"], "digest": "sha256:" + pin["archive_sha256"],
                "expired": False}
    if any(metadata.get(key) != value for key, value in expected.items()):
        raise ValueError("The artifact metadata does not match the frozen package identity")
    run = metadata.get("workflow_run", {})
    if run.get("id") != RUN_ID or run.get("head_sha") != RUN_HEAD:
        raise ValueError("The artifact came from a different workflow run or head")


def extract_verified(archive: Path, directory: Path, expected_sha256: str) -> None:
    if digest(archive) != expected_sha256:
        raise ValueError("The downloaded archive SHA256 differs from the pinned artifact")
    with zipfile.ZipFile(archive) as compressed:
        seen = set()
        members = compressed.infolist()
        for member in members:
            path = PurePosixPath(member.filename)
            if (not path.parts or path.is_absolute() or ".." in path.parts or "\\" in member.filename
                    or path.as_posix() in seen or stat.S_ISLNK(member.external_attr >> 16)
                    or not (path.parts[0] == "wwwroot"
                            or len(path.parts) == 1 and path.name in PUBLISHED_ROOT_FILES and not member.is_dir())):
                raise ValueError("The package archive contains an unexpected or unsafe entry")
            seen.add(path.as_posix())
        for member in members:
            target = directory.joinpath(*PurePosixPath(member.filename).parts)
            if member.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with compressed.open(member) as source, target.open("xb") as destination:
                    shutil.copyfileobj(source, destination)


def archived_site_inventory(archive: Path) -> dict[str, str]:
    result = {}
    with zipfile.ZipFile(archive) as compressed:
        for member in compressed.infolist():
            if not member.is_dir() and member.filename.startswith("wwwroot/"):
                with compressed.open(member) as stream:
                    result[member.filename[len("wwwroot/"):]] = hashlib.file_digest(stream, "sha256").hexdigest()
    return result


def verify_package(directory: Path, role: str = "candidate") -> dict[str, Any]:
    directory = directory.resolve()
    pin = PINS[role]
    receipt = json.loads((directory / RECEIPT).read_text(encoding="utf-8"))
    verify_metadata(receipt["artifact_metadata"], role)
    if (receipt.get("repository") != REPOSITORY or receipt.get("role") != role
            or receipt.get("archive_sha256") != pin["archive_sha256"]
            or digest(directory / "artifact.zip") != pin["archive_sha256"]):
        raise ValueError("The package archive or provenance receipt changed")
    package = json.loads((directory / "benchmark-package.json").read_text(encoding="utf-8"))
    if canonical_digest(package) != pin["manifest_sha256"]:
        raise ValueError("The complete package manifest differs from the frozen snapshot")
    expected = {"schema": 2, "role": role, "source_sha": pin["source_sha"],
                "index_sha256": pin["index_sha256"], "fixture_sha256": FIXTURE_SHA256,
                "configuration": "Release", "execution_mode": "InterpreterAndAOT",
                "runtime_generation": "uno7", "uno_sdk": "7.0.5",
                "dotnet_sdk": "10.0.403", "dotnet_workload_version": "10.0.403"}
    if any(package.get(key) != value for key, value in expected.items()):
        raise ValueError("The package source, build or fixture does not match the pinned experiment")
    actual = inventory(directory / "wwwroot")
    archived = archived_site_inventory(directory / "artifact.zip")
    if (actual != archived or actual != receipt.get("wwwroot_sha256") or actual.get("index.html") != pin["index_sha256"]
            or any(actual.get(path) != sha for path, sha in package["font_assets_sha256"].items())):
        raise ValueError("A published binary, runtime, font or other application asset changed")
    return {**package, "path": str(directory)}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--role", choices=PINS, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    if output.exists() and any(output.iterdir()):
        parser.error("The download destination must be empty")
    output.mkdir(parents=True, exist_ok=True)
    pin = PINS[args.role]
    endpoint = f"repos/{REPOSITORY}/actions/artifacts/{pin['artifact_id']}"
    # gh owns authentication and redirects; credentials are never copied into a
    # second request by this driver or retained in the artifact receipt.
    metadata = json.loads(subprocess.check_output(["gh", "api", endpoint], text=True))
    verify_metadata(metadata, args.role)
    archive = output / "artifact.zip"
    with archive.open("xb") as stream:
        subprocess.run(["gh", "api", endpoint + "/zip"], stdout=stream, check=True)
    extract_verified(archive, output, pin["archive_sha256"])
    receipt = {"schema": 1, "repository": REPOSITORY, "role": args.role,
               "artifact_metadata": metadata, "archive_sha256": digest(archive),
               "wwwroot_sha256": inventory(output / "wwwroot"),
               "scope": "Immutable historical AOT snapshot; does not qualify current production source"}
    (output / RECEIPT).write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    package = verify_package(output, args.role)
    print(f"Verified {args.role} artifact {pin['artifact_id']} at source {package['source_sha']}", flush=True)


if __name__ == "__main__":
    main()
