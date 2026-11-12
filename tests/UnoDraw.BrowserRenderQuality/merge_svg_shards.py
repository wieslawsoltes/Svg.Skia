#!/usr/bin/env python3
"""Recompute a complete SVG quality matrix from three native-DPR artifacts."""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import re
import shutil
import subprocess
import time
from pathlib import Path, PurePosixPath
from typing import Any

from PIL import Image

from svg_bridge_quality import (EXECUTION_MODES, compare_cases, digest, source_hashes,
                                verify_scene_bridge, write_summary)
from svg_fixture_contracts import (DESKTOP, DPRS, SCENES, STATES, Fixture,
                                   inspect_scene, load_fixtures, matrix_dprs, matrix_failures,
                                   normalize_newlines, require_camera_change,
                                   require_source_readback, scene_bridges)
from backend import launch_arguments, verify_backend
from quality import validate_graphite_coverage
from verify_source import ALLOWED_PREFIXES
from candidate_package import validate_receipt


SHARD_KIND = "svg_document_quality_dpr_shard"
AGGREGATE_KIND = "svg_document_quality_dpr_aggregate"
SOURCE_PROOF = "source-equivalence.json"
PACKAGE_RECEIPT = "package-artifact.json"
# Passive revalidation retains the original shard schema's renderer pairs.
# Browser entry points separately reject the retired direct Graphite path.
REPORT_CANDIDATE_BRIDGES = ("composite", "graphite", "legacy-composite", "single-canvas")
JOB_RESULTS = ("success", "failure", "cancelled", "skipped", "unknown")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def canonical_digest(value: Any) -> str:
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False).encode()).hexdigest()


def read_json(path: Path) -> Any:
    def unique_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        value = {}
        for key, item in pairs:
            require(key not in value, f"Duplicate JSON key in {path.name}: {key}")
            value[key] = item
        return value
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_pairs)


def safe_file(root: Path, relative: str) -> Path:
    require(isinstance(relative, str) and bool(relative) and "\\" not in relative,
            "Artifact paths must be nonempty relative POSIX paths")
    path = PurePosixPath(relative)
    require(not path.is_absolute() and relative == path.as_posix()
            and all(part not in {".", ".."} for part in path.parts), "Artifact path escapes its shard")
    current = root
    for part in path.parts:
        current /= part
        require(not current.is_symlink(), "Artifact paths must not traverse symbolic links")
    require(current.is_file() and root.resolve() in current.resolve().parents,
            f"Required artifact file is missing: {relative}")
    return current


def validate_header(report: dict[str, Any], manifest: dict[str, Any], hashes: dict[str, str],
                    harness_sha: str) -> float:
    require(report.get("schema") == 1 and report.get("report_kind") == SHARD_KIND
            and report.get("full_matrix") is False and report.get("full_matrix_passed") is False,
            "Only explicitly incomplete DPR shard reports may enter this aggregate")
    dprs = matrix_dprs(report.get("device_pixel_ratios", []))
    require(len(dprs) == 1, "Each SVG shard must contain exactly one supported DPR")
    require(report.get("passed") is True and report.get("failures") == [],
            "A failed or incomplete shard cannot qualify the complete SVG matrix")
    require(report.get("package_verified_before_browser") is True and report.get("package_verified_after_browser") is True,
            "The complete immutable package must be verified before and after each browser shard")
    candidate = report.get("candidate_bridge")
    require(candidate in REPORT_CANDIDATE_BRIDGES, "A supported SVG shard report mode is required")
    validate_graphite_coverage(candidate, report.get("graphite_coverage"))
    require(report.get("bridges") == list(scene_bridges(candidate)), "The declared SVG control/candidate pair changed")
    expected = {"expected_session_count": 2, "expected_case_count": 16, "expected_comparison_count": 24,
                "expected_scenes": list(SCENES), "expected_states": list(STATES)}
    require(all(report.get(key) == value for key, value in expected.items()),
            "A DPR shard must retain both complete eight-scene, three-state sessions")
    require(report.get("fixture_manifest") == manifest, "The original SVG fixture identities differ")
    require(report.get("test_file_sha256") == hashes, "The shard and merger must use the identical actual validation files")
    require(re.fullmatch(r"[0-9a-f]{40}", harness_sha) is not None
            and report.get("harness_source_sha") == harness_sha, "The shard and merger must use the same immutable harness commit")
    require(report.get("browser_launch_arguments") == launch_arguments("webgpu")
            and isinstance(report.get("browser_version"), str) and bool(report["browser_version"]),
            "The original browser launch configuration and exact version are required")
    package = report.get("package", {})
    require(package.get("role") == "candidate" and package.get("runtime_generation") == "uno7"
            and package.get("configuration") == "Release"
            and report.get("execution_mode") in EXECUTION_MODES
            and package.get("execution_mode") == report["execution_mode"]
            and re.fullmatch(r"[0-9a-f]{40}", package.get("source_sha", "")) is not None
            and re.fullmatch(r"[0-9a-f]{64}", package.get("index_sha256", "")) is not None
            and bool(package.get("dotnet_sdk")) and bool(package.get("dotnet_workload_version")),
            "The complete Release candidate package identity is required")
    fonts = package.get("font_assets_sha256")
    require(isinstance(fonts, dict) and bool(fonts)
            and all(isinstance(name, str) and bool(name) and isinstance(value, str)
                    and re.fullmatch(r"[0-9a-f]{64}", value) is not None for name, value in fonts.items()),
            "The package must retain the original font asset identities")
    failures = matrix_failures(report.get("cases", []), report.get("comparisons", []),
                               candidate_bridge=candidate, dprs=dprs)
    require(not failures, "; ".join(failures))
    return dprs[0]


def source_proof(root: Path, report: dict[str, Any]) -> dict[str, Any]:
    proof = read_json(safe_file(root, SOURCE_PROOF))
    require(proof.get("package_source_sha") == report["package"]["source_sha"]
            and proof.get("harness_head_sha") == report["harness_source_sha"]
            and proof.get("execution_mode") == report["execution_mode"]
            and proof.get("production_source_equivalent") is True
            and proof.get("production_changes") == []
            and proof.get("allowed_difference_prefixes") == list(ALLOWED_PREFIXES)
            and re.fullmatch(r"[1-9][0-9]*", str(proof.get("package_run_id", ""))) is not None,
            "The shard lacks a matching successful production-source equivalence proof")
    paths = proof.get("changed_paths")
    require(isinstance(paths, list) and len(paths) == len(set(paths))
            and all(isinstance(name, str) and name.startswith(ALLOWED_PREFIXES)
                    and ".." not in PurePosixPath(name).parts and "\\" not in name for name in paths),
            "The source-equivalence proof contains unsupported production differences")
    return proof


def identity(report: dict[str, Any], proof: dict[str, Any], receipt: Any) -> dict[str, Any]:
    keys = ("execution_mode", "candidate_bridge", "bridges", "graphite_coverage", "graphics",
            "fixture_manifest", "test_file_sha256", "harness_source_sha", "browser_version", "browser_launch_arguments")
    return {**{key: report.get(key) for key in keys},
            "package": {key: value for key, value in report["package"].items() if key != "path"},
            "source_equivalence": proof, "package_artifact": receipt}


def package_receipt(root: Path, report: dict[str, Any], proof: dict[str, Any]) -> dict[str, Any]:
    path = safe_file(root, PACKAGE_RECEIPT)
    receipt = read_json(path)
    validate_receipt(receipt)
    require(receipt["manifest"] == {key: value for key, value in report["package"].items() if key != "path"}
            and receipt["package_source_sha"] == proof["package_source_sha"]
            and str(receipt["workflow_run_id"]) == str(proof["package_run_id"])
            and report.get("package_artifact_sha256") == digest(path),
            "The shard manifest, source, run or retained receipt digest differs from its immutable package proof")
    return receipt


def validate_case(case: dict[str, Any], report: dict[str, Any], root: Path, fixture: Fixture,
                  environment_identity: dict[str, Any] | None) -> tuple[dict[str, Any], dict[str, Any]]:
    case = copy.deepcopy(case)
    dpr, bridge = matrix_dprs((case["dpr"],))[0], case["bridge"]
    relative = f"dpr-{dpr}/{bridge}/{fixture.name}"
    require(case.get("artifact_directory") == relative, "The native case directory does not match its scene/mode/DPR")
    local = read_json(safe_file(root, relative + "/report.json"))
    require(local == {key: value for key, value in case.items() if key != "artifact_directory"},
            "The independent case report differs from the shard index")
    require(case.get("source_sha") == report["package"]["source_sha"]
            and case.get("execution_mode") == report["execution_mode"]
            and case.get("browser_version") == report["browser_version"]
            and case.get("canonical_sha256") == fixture.sha256
            and case.get("input_sha256") == fixture.browser_sha256
            and case.get("graphite_coverage") == (report.get("graphite_coverage") if bridge == "graphite" else None),
            "The case source, input, execution mode, browser or renderer coverage differs")
    environment = case.get("environment", {})
    require(environment.get("viewport") == DESKTOP and environment.get("device_pixel_ratio") == dpr
            and environment.get("cross_origin_isolated") is False
            and isinstance(environment.get("user_agent"), str) and bool(environment["user_agent"])
            and isinstance(environment.get("browser_fonts"), list), "The original native browser environment is missing")
    normalized = {key: value for key, value in environment.items() if key != "device_pixel_ratio"}
    normalized["browser_fonts"] = sorted(environment["browser_fonts"], key=lambda font: json.dumps(font, sort_keys=True))
    require(environment_identity is None or normalized == environment_identity,
            "Browser environment or font observations differ between SVG sessions")
    require(case.get("page_errors") == [] and case.get("resource_errors") == [], "The case contains runtime/resource errors")
    console = case.get("console", [])
    require(not any(entry.get("type") == "error" and not entry.get("location", {}).get("url", "").endswith("/favicon.ico")
                    for entry in console), "The case contains console errors")
    verify_scene_bridge(console, bridge, graphite_coverage=case.get("graphite_coverage"))
    renderer = case.get("renderer", {})
    backend = verify_backend(console, renderer.get("capabilities", {}), "webgpu")
    require(all(renderer.get(key) == value for key, value in backend.items()), "Stored graphics negotiation differs from its actual log")
    targets = [entry for entry in renderer.get("application_contexts", []) if entry.get("target") is True]
    require(len(targets) == 1 and targets[0].get("kind") == "webgpu" and targets[0].get("connected") is True
            and targets[0].get("width") == round(DESKTOP["width"] * dpr)
            and targets[0].get("height") == round(DESKTOP["height"] * dpr)
            and targets[0].get("css_width") == DESKTOP["width"] and targets[0].get("css_height") == DESKTOP["height"]
            and (targets[0].get("draw_calls", 0) > 0 or targets[0].get("presentations", 0) > 0),
            "The native application WebGPU canvas has not demonstrated actual graphics work")
    files = {name: safe_file(root, relative + "/" + name)
             for name in ("canonical.svg", "browser-input.svg", "source-inserted.svg", "source-committed.svg")}
    require(digest(files["canonical.svg"]) == fixture.sha256
            and files["browser-input.svg"].read_text(encoding="utf-8") == fixture.browser_source
            and normalize_newlines(files["source-inserted.svg"].read_text(encoding="utf-8")) == normalize_newlines(fixture.browser_source),
            "The retained canonical or actual inserted SVG differs from the required fixture")
    readback = require_source_readback(files["source-committed.svg"].read_text(encoding="utf-8"), fixture)
    require(all(case["source_edit"].get(key) == value for key, value in readback.items())
            and case["source_edit"].get("input_sha256") == fixture.browser_sha256
            and case["source_edit"].get("input_tag") == "TEXTAREA", "The retained committed source differs from its Apply readback proof")
    require([state["name"] for state in case["states"]] == list(STATES), "The original camera-state sequence changed")
    previous = None
    native = []
    for state in case["states"]:
        name = state["name"]
        require(state.get("scene") == fixture.name and state.get("dpr") == dpr and state.get("viewport") == DESKTOP
                and state.get("raw") == name + ".png" and state.get("body_raw") == name + "-body.png",
                "The native image state identity or path differs")
        raw = safe_file(root, relative + "/" + state["raw"])
        body = safe_file(root, relative + "/" + state["body_raw"])
        readiness = inspect_scene(raw, fixture, dpr)
        require(readiness == state.get("readiness"), "The actual PNG differs from the retained native readiness observations")
        if previous is not None:
            require_camera_change(previous, readiness, dpr, name)
        previous = state["readiness"] = readiness
        with Image.open(raw) as image, Image.open(body) as crop:
            expected = image.crop(readiness["body_physical_bounds"])
            require(crop.mode == expected.mode and crop.size == expected.size and crop.tobytes() == expected.tobytes(),
                    "The retained body crop is not an exact crop of its native PNG")
        native.append({"name": name, "raw_sha256": digest(raw), "body_sha256": digest(body),
                       "native_readiness_recomputed": True, "camera_transition_recomputed": name != STATES[0]})
    case["native_recalculation"] = {"source_file_sha256": {name: digest(path) for name, path in files.items()}, "states": native}
    return case, normalized


def qualify_result(result: dict[str, Any]) -> None:
    result["failures"].extend(matrix_failures(result["cases"], result["comparisons"], candidate_bridge=result["candidate_bridge"]))
    if result["shard_jobs_result"] != "success":
        result["failures"].append("The original shard jobs did not all complete successfully: " + result["shard_jobs_result"])
    result["full_matrix"] = result["full_matrix_passed"] = result["passed"] = not result["failures"]


def merge(shards: Path, output: Path, *, shard_jobs_result: str = "unknown") -> dict[str, Any]:
    require(shard_jobs_result in JOB_RESULTS, "Use the actual completed shard-job result")
    shards, output = shards.resolve(), output.resolve()
    require(shards != output and shards not in output.parents and output not in shards.parents,
            "The aggregate output must be separate from the original shard tree")
    require(not output.exists() or not any(output.iterdir()), "The aggregate output must be empty")
    output.mkdir(parents=True, exist_ok=True)
    result: dict[str, Any] = {"schema": 1, "report_kind": AGGREGATE_KIND, "passed": False,
                             "full_matrix": False, "full_matrix_passed": False, "started_unix": time.time(),
                             "execution_mode": "unknown", "candidate_bridge": None,
                             "shard_jobs_result": shard_jobs_result,
                             "device_pixel_ratios": list(DPRS), "expected_scenes": list(SCENES), "expected_states": list(STATES),
                             "expected_session_count": 6, "expected_case_count": 48, "expected_comparison_count": 72,
                             "cases": [], "comparisons": [], "shards": [], "failures": []}
    try:
        directories = sorted(path for path in shards.iterdir() if path.is_dir())
        require(len(directories) == len(DPRS) and all(path.is_dir() for path in shards.iterdir()),
                "Exactly three original shard artifact directories are required")
        for directory in directories:
            require(not directory.is_symlink(), "A shard artifact directory must not be a symbolic link")
            for path in directory.rglob("*"):
                require(not path.is_symlink() and (path.is_file() or path.is_dir()), "Shard artifacts must contain only ordinary files and directories")
            shutil.copytree(directory, output / "shards" / directory.name)
        manifest, fixtures = load_fixtures()
        fixture_map = {fixture.name: fixture for fixture in fixtures}
        hashes = source_hashes()
        harness_sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=Path(__file__).resolve().parent, text=True).strip()
        shared_identity = environment = None
        seen = set()
        loaded = []
        for directory in directories:
            root = output / "shards" / directory.name
            report = read_json(safe_file(root, "report.json"))
            evidence = {"directory": str(root.relative_to(output)), "report_sha256": digest(root / "report.json"),
                        "original_passed": report.get("passed"), "failures": report.get("failures", [])}
            result["shards"].append(evidence)
            dpr = validate_header(report, manifest, hashes, harness_sha)
            result.update({"candidate_bridge": report["candidate_bridge"], "execution_mode": report["execution_mode"],
                           "graphite_coverage": report.get("graphite_coverage")})
            require(dpr not in seen, "A device-scale shard is duplicated")
            seen.add(dpr)
            proof = source_proof(root, report)
            receipt = package_receipt(root, report, proof)
            current_identity = identity(report, proof, receipt)
            require(shared_identity is None or current_identity == shared_identity,
                    "The source, package, harness, fixtures, browser, renderer pair/coverage or artifact receipt differs across shards")
            shared_identity = current_identity
            evidence.update({"dpr": dpr, "source_equivalence": proof, "source_equivalence_sha256": digest(root / SOURCE_PROOF),
                             "package_artifact": receipt, "package_artifact_sha256": digest(root / PACKAGE_RECEIPT)})
            loaded.append((root, report, dpr))
        require(seen == set(DPRS), "The aggregate requires DPR 1, 1.25 and 2 exactly once")
        result.update({key: value for key, value in shared_identity.items()
                       if key not in {"source_equivalence", "package_artifact"}})
        result["package_manifest_sha256"] = canonical_digest(result["package"])
        for root, report, dpr in loaded:
            cases = []
            for case in report["cases"]:
                validated, environment = validate_case(case, report, root, fixture_map[case["scene"]], environment)
                cases.append(validated)
            comparisons = compare_cases(cases, fixtures, root, candidate_bridge=report["candidate_bridge"], dprs=(dpr,))
            failures = matrix_failures(cases, comparisons, candidate_bridge=report["candidate_bridge"], dprs=(dpr,))
            require(not failures, "; ".join(failures))
            index = lambda rows: {(row["scene"], row["dpr"], row["state"]): row for row in rows}
            require(index(comparisons) == index(report["comparisons"]), "Stored native comparison results differ from the actual PNG recalculation")
            for case in cases:
                case["artifact_directory"] = str(root.relative_to(output) / case["artifact_directory"])
            result["cases"].extend(cases)
            result["comparisons"].extend(comparisons)
        result["browser_environment"] = environment
        result["native_recalculation"] = {"case_count": len(result["cases"]), "state_count": sum(len(case["states"]) for case in result["cases"]),
                                           "comparison_count": len(result["comparisons"]), "source_readbacks_recomputed": True,
                                           "native_png_readiness_and_camera_recomputed": True,
                                           "original_compare_scene_recomputed": True, "resized_for_comparison": False}
        qualify_result(result)
    except Exception as error:
        result["failures"].append(f"{type(error).__name__}: {error}")
    finally:
        result["duration_seconds"] = time.time() - result["started_unix"]
        (output / "report.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        if result["candidate_bridge"] is None:
            (output / "summary.md").write_text("# SVG document quality aggregate\n\n"
                "**Failed before a consistent renderer pair was established; no full-matrix qualification.**\n\n"
                + "\n".join("- " + failure for failure in result["failures"]) + "\n", encoding="utf-8")
        else:
            write_summary(result, output)
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--shards", type=Path, required=True, help="Parent containing exactly three original shard artifact directories")
    parser.add_argument("--output", type=Path, required=True, help="New self-contained aggregate directory, outside the original shards")
    parser.add_argument("--shard-jobs-result", choices=JOB_RESULTS, default="unknown",
                        help="Actual completed result of all shard jobs; unknown, failed, skipped or cancelled jobs cannot qualify the matrix")
    args = parser.parse_args()
    try:
        result = merge(args.shards, args.output, shard_jobs_result=args.shard_jobs_result)
    except ValueError as error:
        parser.error(str(error))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
