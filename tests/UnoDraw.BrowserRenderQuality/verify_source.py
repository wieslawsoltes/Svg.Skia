#!/usr/bin/env python3
"""Audit cached application artifacts and reject stale binaries by default."""
from __future__ import annotations

import argparse
import json
import re
import subprocess
from pathlib import Path
from typing import Any


ALLOWED_PREFIXES = ("tests/", "docs/", ".github/")


def changed_paths(repository: Path, source: str, head: str) -> list[str]:
    # A production deletion must remain visible if its contents move into tests.
    output = subprocess.check_output(
        ["git", "diff", "--no-renames", "--name-only", "-z", source, head],
        cwd=repository, text=True)
    return [name for name in output.split("\0") if name]


def verify_source(repository: Path, package: Path, output: Path,
                  package_run_id: str, *, report_only: bool = False,
                  github_output: Path | None = None) -> dict[str, Any]:
    manifest = json.loads((package / "benchmark-package.json").read_text(encoding="utf-8"))
    if (manifest.get("role") != "candidate" or manifest.get("configuration") != "Release"
            or manifest.get("execution_mode") != "InterpreterAndAOT"):
        raise ValueError("Rendering verification requires a Release InterpreterAndAOT candidate artifact")
    source = manifest.get("source_sha", "")
    if not isinstance(source, str) or not re.fullmatch(r"[0-9a-f]{40}", source):
        raise ValueError("The candidate package must record its exact source commit")
    if subprocess.run(["git", "cat-file", "-e", source + "^{commit}"], cwd=repository,
                      stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode:
        subprocess.run(["git", "fetch", "--no-tags", "--depth=1", "origin", source],
                       cwd=repository, check=True)
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repository, text=True).strip()
    changed = changed_paths(repository, source, head)
    production = [name for name in changed if not name.startswith(ALLOWED_PREFIXES)]
    evidence = {"package_run_id": package_run_id, "package_source_sha": source,
                "harness_head_sha": head, "allowed_difference_prefixes": list(ALLOWED_PREFIXES),
                "changed_paths": changed, "production_changes": production,
                "production_source_equivalent": not production,
                "execution_mode": manifest["execution_mode"]}
    output.mkdir(parents=True, exist_ok=True)
    (output / "source-equivalence.json").write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    status = ("Stale artifact; fresh pipeline required." if production
              else "Cached artifact is eligible for verification.")
    summary = ["# Cached rendering artifact eligibility", "", f"**{status}**", "",
               f"- Package run: `{package_run_id}`",
               f"- Package source: `{source}`",
               f"- Current source: `{head}`", ""]
    if production:
        summary += ["Cached quality, performance, diagnostic and aggregate jobs must be skipped for this head.",
                    "Fresh production validation is required in the UnoDraw browser performance workflow.", "",
                    "Production differences:", "", "```json", json.dumps(production, indent=2), "```", ""]
    else:
        summary += ["Source equivalence permits the separate cached verification jobs to run.", ""]
    summary += ["This source audit does not claim a quality or performance pass.", ""]
    (output / "summary.md").write_text("\n".join(summary), encoding="utf-8")
    if github_output is not None:
        with github_output.open("a", encoding="utf-8") as stream:
            stream.write(f"eligible={'false' if production else 'true'}\n")
    if production and not report_only:
        raise ValueError("Published candidate differs in production source: " + ", ".join(production))
    return evidence


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", type=Path, default=Path.cwd())
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--package-run-id", required=True)
    parser.add_argument("--report-only", action="store_true",
                        help="Report stale production source without failing; invalid artifacts still fail")
    parser.add_argument("--github-output", type=Path,
                        help="Append eligible=true/false for the dependent cached-verification jobs")
    args = parser.parse_args()
    print(json.dumps(verify_source(args.repository, args.package, args.output, args.package_run_id,
                                   report_only=args.report_only, github_output=args.github_output), indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
