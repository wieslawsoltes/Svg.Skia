from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
import zipfile
from pathlib import Path

from candidate_package import (ARTIFACT_NAME, FIXTURE_SHA256, REPOSITORY, WORKFLOW_PATH,
                               canonical_digest, create_receipt, digest, extract_verified,
                               validate_descriptor, validate_receipt, verify_package)


SOURCE = "a" * 40
RUN_ID = 123


def candidate_fixture(directory: Path):
    """Small original archive and valid portable receipt for contract tests."""
    directory.mkdir(parents=True, exist_ok=True)
    index, font = b"<!doctype html>candidate", b"fixture-font"
    manifest = {"schema": 2, "role": "candidate", "source_sha": SOURCE, "fixture_sha256": FIXTURE_SHA256,
                "configuration": "Release", "execution_mode": "InterpreterAndAOT", "runtime_generation": "uno7",
                "uno_sdk": "7.0.5", "dotnet_sdk": "10.0.403", "dotnet_workload_version": "10.0.403",
                "index_sha256": hashlib.sha256(index).hexdigest(), "supports_render_diagnostics": True,
                "font_assets_sha256": {"fonts/fixture.ttf": hashlib.sha256(font).hexdigest()},
                "additional_build_identity": "preserve every manifest field"}
    archive = directory / "artifact.zip"
    with zipfile.ZipFile(archive, "w") as compressed:
        compressed.writestr("benchmark-package.json", json.dumps(manifest))
        compressed.writestr("wwwroot/index.html", index)
        compressed.writestr("wwwroot/runtime.js", b"original-runtime")
        compressed.writestr("wwwroot/fonts/fixture.ttf", font)
    run = {"id": RUN_ID, "path": WORKFLOW_PATH, "head_sha": SOURCE, "repository": {"full_name": REPOSITORY}}
    artifact = {"id": 456, "name": ARTIFACT_NAME, "size_in_bytes": archive.stat().st_size,
                "digest": "sha256:" + digest(archive), "expired": False,
                "workflow_run": {"id": RUN_ID, "head_sha": SOURCE}}
    descriptor = validate_descriptor(RUN_ID, run, [artifact])
    extract_verified(archive, directory, descriptor["artifact"]["archive_sha256"])
    return run, artifact, descriptor, create_receipt(directory, descriptor)


class CandidatePackageTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name) / "package"
        self.run, self.artifact, self.descriptor, self.receipt = candidate_fixture(self.directory)

    def test_candidate_only_descriptor_needs_no_baseline_or_completed_entire_workflow(self):
        run = {**self.run, "status": "in_progress", "conclusion": None}
        self.assertEqual(validate_descriptor(RUN_ID, run, [self.artifact]), self.descriptor)
        unrelated = {"id": 789, "name": "unrelated-quality-evidence"}
        self.assertEqual(validate_descriptor(RUN_ID, run, [unrelated, self.artifact]), self.descriptor)
        self.assertNotIn("baseline", json.dumps(self.descriptor))

    def test_metadata_requires_exact_repository_workflow_run_head_and_unique_digest(self):
        for key, value in (("id", RUN_ID + 1), ("path", ".github/workflows/other.yml"), ("head_sha", "HEAD"),
                           ("repository", {"full_name": "other/repository"})):
            with self.subTest(key=key), self.assertRaises(ValueError):
                validate_descriptor(RUN_ID, {**self.run, key: value}, [self.artifact])
        for key, value in (("id", True), ("digest", ""), ("expired", True), ("size_in_bytes", 0),
                           ("workflow_run", {"id": RUN_ID, "head_sha": "f" * 40}),
                           ("workflow_run", {"id": RUN_ID + 1, "head_sha": SOURCE})):
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                validate_descriptor(RUN_ID, self.run, [{**self.artifact, key: value}])
        for artifacts in ([], [self.artifact, copy.deepcopy(self.artifact)]):
            with self.assertRaises(ValueError):
                validate_descriptor(RUN_ID, self.run, artifacts)

    def test_receipt_is_portable_complete_and_reverifiable_against_original_zip(self):
        validate_receipt(self.receipt)
        package = verify_package(self.directory, self.receipt, descriptor=self.descriptor)
        self.assertEqual(package["source_sha"], SOURCE)
        self.assertEqual(self.receipt["served_file_count"], 3)
        self.assertEqual(self.receipt["manifest"]["additional_build_identity"], "preserve every manifest field")
        self.assertNotIn(str(self.directory), json.dumps(self.receipt))
        self.assertNotIn("source_equivalence", self.receipt)
        self.assertNotIn("harness_head_sha", self.receipt)
        changed = copy.deepcopy(self.descriptor)
        changed["artifact"]["id"] += 1
        with self.assertRaises(ValueError):
            verify_package(self.directory, self.receipt, descriptor=changed)

    def test_pure_receipt_validation_rejects_changed_manifests_inventories_and_scope(self):
        for defect in ("manifest", "inventory", "count", "source", "run", "artifact", "local_path", "scope"):
            changed = copy.deepcopy(self.receipt)
            if defect == "manifest":
                changed["manifest"]["additional_build_identity"] = "changed"
            elif defect == "inventory":
                changed["wwwroot_sha256"]["runtime.js"] = "f" * 64
            elif defect == "count":
                changed["served_file_count"] += 1
            elif defect == "source":
                changed["package_source_sha"] = "f" * 40
            elif defect == "run":
                changed["workflow_run_id"] = True
            elif defect == "artifact":
                changed["artifact"]["archive_sha256"] = "not-a-digest"
            elif defect == "local_path":
                changed["manifest"]["path"] = "/tmp/package"
                changed["manifest_sha256"] = canonical_digest(changed["manifest"])
            else:
                changed["harness_head_sha"] = "b" * 40
            with self.subTest(defect=defect), self.assertRaises(ValueError):
                validate_receipt(changed)

    def test_even_rehashed_manifest_must_retain_build_fixture_index_and_fonts(self):
        for key, value in (("execution_mode", "Interpreter"), ("configuration", "Debug"),
                           ("fixture_sha256", "f" * 64), ("role", "baseline"),
                           ("index_sha256", "f" * 64), ("supports_render_diagnostics", False),
                           ("font_assets_sha256", {}), ("font_assets_sha256", {"fonts/fixture.ttf": "f" * 64})):
            changed = copy.deepcopy(self.receipt)
            changed["manifest"][key] = value
            changed["manifest_sha256"] = canonical_digest(changed["manifest"])
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                validate_receipt(changed)

    def test_rehashed_inventory_rejects_unsafe_names_and_missing_original_index(self):
        for name in ("../runtime.js", "/runtime.js", "runtime\\file.js", "./runtime.js", "dir//file.js", ""):
            changed = copy.deepcopy(self.receipt)
            changed["wwwroot_sha256"][name] = changed["wwwroot_sha256"].pop("runtime.js")
            changed["wwwroot_inventory_sha256"] = canonical_digest(changed["wwwroot_sha256"])
            with self.subTest(name=name), self.assertRaises(ValueError):
                validate_receipt(changed)
        changed = copy.deepcopy(self.receipt)
        changed["wwwroot_sha256"].pop("index.html")
        changed["served_file_count"] -= 1
        changed["wwwroot_inventory_sha256"] = canonical_digest(changed["wwwroot_sha256"])
        with self.assertRaises(ValueError):
            validate_receipt(changed)

    def test_coordinated_served_file_and_receipt_mutation_still_fails_original_zip_binding(self):
        path = self.directory / "wwwroot/runtime.js"
        path.write_text("changed-runtime")
        changed = copy.deepcopy(self.receipt)
        changed["wwwroot_sha256"]["runtime.js"] = digest(path)
        changed["wwwroot_inventory_sha256"] = canonical_digest(changed["wwwroot_sha256"])
        validate_receipt(changed)
        with self.assertRaises(ValueError):
            verify_package(self.directory, changed, descriptor=self.descriptor)

    def test_coordinated_manifest_and_receipt_mutation_still_fails_original_zip_binding(self):
        changed = copy.deepcopy(self.receipt)
        changed["manifest"]["additional_build_identity"] = "changed"
        changed["manifest_sha256"] = canonical_digest(changed["manifest"])
        (self.directory / "benchmark-package.json").write_text(json.dumps(changed["manifest"]))
        validate_receipt(changed)
        with self.assertRaises(ValueError):
            verify_package(self.directory, changed, descriptor=self.descriptor)

    def test_modified_archive_fails_even_when_extracted_tree_is_unchanged(self):
        with (self.directory / "artifact.zip").open("ab") as stream:
            stream.write(b"modified archive")
        with self.assertRaises(ValueError):
            verify_package(self.directory, self.receipt)


if __name__ == "__main__":
    unittest.main()
