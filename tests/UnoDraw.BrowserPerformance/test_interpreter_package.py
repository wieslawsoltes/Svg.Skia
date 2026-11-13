from __future__ import annotations

import copy
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from interpreter_package import INVENTORY_NAME, create_inventory, verify_packages
from pinned_package import digest, inventory


class InterpreterPackageTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.repository = "wieslawsoltes/Svg.Skia"
        self.run_id = "987654321"
        self.output = self.root / "results" / "verification.json"
        self.bindings = {}
        for index, role in enumerate(("candidate", "baseline")):
            package = self.root / role
            (package / "wwwroot/fonts").mkdir(parents=True)
            for relative, contents in {
                "index.html": b"<html>fixture</html>", "fonts/test.woff": b"font fixture",
                "runtime.wasm": b"runtime fixture", ".hidden-runtime": b"hidden payload",
            }.items():
                (package / "wwwroot" / relative).write_bytes(contents)
            source = ("a" if role == "candidate" else "b") * 40
            manifest = {"schema": 2, "role": role, "source_sha": source, "configuration": "Release",
                        "execution_mode": "Interpreter", "dotnet_sdk": "10.0.403",
                        "dotnet_workload_version": "10.0.403",
                        "index_sha256": digest(package / "wwwroot/index.html"),
                        "font_assets_sha256": {"fonts/test.woff": digest(package / "wwwroot/fonts/test.woff")}}
            (package / "benchmark-package.json").write_text(json.dumps(manifest), encoding="utf-8")
            record = create_inventory(package, role, source, self.repository, self.run_id, "1")
            self.bindings[role] = {"path": str(package), "source_sha": source,
                                   "inventory_sha256": digest(record), "artifact_id": str(123 + index),
                                   "artifact_digest": "sha256:" + "c" * 64}
        self.package = self.root / "candidate"

    def verify(self, bindings=None, roles=None, output=None, repository=None, run_id=None):
        return verify_packages(self.bindings if bindings is None else bindings,
                               {"candidate", "baseline"} if roles is None else roles,
                               self.output if output is None else output,
                               self.repository if repository is None else repository,
                               self.run_id if run_id is None else run_id)

    def change_record(self, **changes) -> None:
        path = self.package / INVENTORY_NAME
        record = json.loads(path.read_text())
        record.update(changes)
        path.write_text(json.dumps(record), encoding="utf-8")
        self.bindings["candidate"]["inventory_sha256"] = digest(path)

    def change_manifest(self, **changes) -> None:
        path = self.package / "benchmark-package.json"
        manifest = json.loads(path.read_text())
        manifest.update(changes)
        path.write_text(json.dumps(manifest), encoding="utf-8")

    def test_candidate_and_cross_build_consumers_preserve_all_bytes(self):
        before = inventory(self.package)
        candidate = {"candidate": self.bindings["candidate"]}
        for bindings, roles in ((candidate, {"candidate"}), (self.bindings, {"candidate", "baseline"})):
            with self.subTest(roles=roles):
                result = self.verify(bindings=bindings, roles=roles)
                self.assertEqual(set(result["packages"]), roles)
                for role in roles:
                    receipt = result["packages"][role]
                    self.assertTrue(receipt["all_packaged_bytes_match_producer"])
                    self.assertEqual(receipt["inventory"]["package_file_count"], 5)
                    self.assertIn("wwwroot/.hidden-runtime", receipt["inventory"]["package_files_sha256"])
                    self.assertEqual(receipt["producer_artifact_digest"], "c" * 64)
        self.assertEqual(inventory(self.package), before)

    def test_changed_runtime_rejected(self):
        (self.package / "wwwroot/runtime.wasm").write_bytes(b"changed")
        with self.assertRaises(ValueError):
            self.verify()

    def test_extra_file_rejected(self):
        (self.package / "wwwroot/extra.wasm").write_bytes(b"extra")
        with self.assertRaises(ValueError):
            self.verify()

    def test_removed_file_rejected(self):
        (self.package / "wwwroot/runtime.wasm").unlink()
        with self.assertRaises(ValueError):
            self.verify()

    def test_hidden_file_change_rejected(self):
        (self.package / "wwwroot/.hidden-runtime").write_bytes(b"changed")
        with self.assertRaises(ValueError):
            self.verify()

    def test_symlink_rejected(self):
        (self.package / "wwwroot/link").symlink_to(self.root / "baseline", target_is_directory=True)
        with self.assertRaises(ValueError):
            self.verify()

    def test_wrong_binding_identity_rejected(self):
        for field, value in (("source_sha", "d" * 40), ("source_sha", "HEAD"),
                             ("artifact_id", ""), ("artifact_id", "0"), ("artifact_id", 123),
                             ("artifact_digest", "unknown"), ("inventory_sha256", "e" * 64)):
            with self.subTest(field=field, value=value):
                bindings = copy.deepcopy(self.bindings)
                bindings["candidate"][field] = value
                with self.assertRaises(ValueError):
                    self.verify(bindings=bindings)

    def test_wrong_run_or_repository_rejected(self):
        for changes in ({"run_id": "other"}, {"repository": "different/repository"}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                self.verify(**changes)

    def test_required_roles_cannot_be_missing_empty_or_unknown(self):
        for bindings, roles in (({"candidate": self.bindings["candidate"]}, {"candidate", "baseline"}),
                                ({}, {"candidate"}), ({}, set()), (self.bindings, {"reference"})):
            with self.subTest(roles=roles, bindings=set(bindings)), self.assertRaises(ValueError):
                self.verify(bindings=bindings, roles=roles)

    def test_report_cannot_modify_either_package(self):
        for output in (self.package, self.package / "proof.json", self.root / "baseline" / "proof.json"):
            with self.subTest(output=output), self.assertRaises(ValueError):
                self.verify(output=output)

    def test_aot_inventory_rejected(self):
        self.change_record(execution_mode="InterpreterAndAOT")
        with self.assertRaises(ValueError):
            self.verify()

    def test_wrong_inventory_role_rejected(self):
        self.change_record(role="baseline")
        with self.assertRaises(ValueError):
            self.verify()

    def test_wrong_inventory_schema_rejected(self):
        self.change_record(schema=2)
        with self.assertRaises(ValueError):
            self.verify()

    def test_wrong_inventory_count_rejected(self):
        self.change_record(package_file_count=4)
        with self.assertRaises(ValueError):
            self.verify()

    def test_wrong_manifest_hash_rejected(self):
        self.change_record(manifest_sha256="f" * 64)
        with self.assertRaises(ValueError):
            self.verify()

    def test_producer_rejects_wrong_source(self):
        (self.package / INVENTORY_NAME).unlink()
        with self.assertRaises(ValueError):
            create_inventory(self.package, "candidate", "d" * 40, self.repository, self.run_id, "1")

    def test_producer_rejects_aot_or_invalid_manifest(self):
        (self.package / INVENTORY_NAME).unlink()
        path = self.package / "benchmark-package.json"
        original = path.read_bytes()
        for changes in ({"execution_mode": "InterpreterAndAOT"}, {"schema": 3},
                        {"index_sha256": "e" * 64}, {"font_assets_sha256": {}},
                        {"font_assets_sha256": {"fonts/test.woff": "e" * 64}}):
            with self.subTest(changes=changes):
                path.write_bytes(original)
                self.change_manifest(**changes)
                with self.assertRaises(ValueError):
                    create_inventory(self.package, "candidate", "a" * 40, self.repository, self.run_id, "1")
                self.assertFalse((self.package / INVENTORY_NAME).exists())

    def test_producer_cannot_rebind_existing_inventory(self):
        before = (self.package / INVENTORY_NAME).read_bytes()
        with self.assertRaises(FileExistsError):
            create_inventory(self.package, "candidate", "a" * 40, self.repository, self.run_id, "2")
        self.assertEqual((self.package / INVENTORY_NAME).read_bytes(), before)

    def test_cli_producer_and_before_after_consumer(self):
        module = Path(__file__).with_name("interpreter_package.py")
        producer_output = self.root / "github-output"
        producer_output.write_text("existing=value\n", encoding="utf-8")
        (self.package / INVENTORY_NAME).unlink()
        env = {**os.environ, "GITHUB_REPOSITORY": self.repository, "GITHUB_RUN_ID": self.run_id,
               "GITHUB_RUN_ATTEMPT": "1", "SOURCE_SHA": "a" * 40, "GITHUB_OUTPUT": str(producer_output)}
        result = subprocess.run([sys.executable, str(module), "create", "--package", str(self.package),
                                 "--role", "candidate"], env=env, capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        inventory_sha = digest(self.package / INVENTORY_NAME)
        self.assertEqual(producer_output.read_text(), "existing=value\nsha256=" + inventory_sha + "\n")
        self.bindings["candidate"]["inventory_sha256"] = inventory_sha
        before = inventory(self.package)
        for stage in ("before", "after"):
            output = self.root / "results" / (stage + ".json")
            env.update(PACKAGE_BINDINGS=json.dumps(self.bindings), EXPECTED_ROLES="candidate,baseline",
                       VERIFICATION_OUTPUT=str(output))
            result = subprocess.run([sys.executable, str(module), "verify"], env=env,
                                    capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(set(json.loads(output.read_text())["packages"]), {"candidate", "baseline"})
        self.assertEqual(inventory(self.package), before)


if __name__ == "__main__":
    unittest.main()
