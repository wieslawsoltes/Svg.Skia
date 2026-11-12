from __future__ import annotations

import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from verify_source import verify_source


class SourceEquivalenceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.repository = self.root / "repository"
        self.repository.mkdir()
        self.git("init", "-q")
        self.git("config", "user.name", "Source audit test")
        self.git("config", "user.email", "source-audit@example.invalid")
        (self.repository / "src").mkdir()
        (self.repository / "src/application.cs").write_text("class Application {}\n")
        self.commit()
        self.source = self.git("rev-parse", "HEAD").strip()
        self.package = self.root / "package"
        self.package.mkdir()
        self.manifest = {"role": "candidate", "configuration": "Release",
                         "execution_mode": "InterpreterAndAOT", "source_sha": self.source}
        self.write_manifest()

    def git(self, *arguments: str) -> str:
        return subprocess.check_output(["git", *arguments], cwd=self.repository, text=True,
                                       stderr=subprocess.STDOUT)

    def commit(self) -> None:
        self.git("add", "-A")
        self.git("commit", "-qm", "source audit fixture")

    def write_manifest(self) -> None:
        (self.package / "benchmark-package.json").write_text(json.dumps(self.manifest))

    def audit(self) -> dict:
        return verify_source(self.repository, self.package, self.root / "evidence", "123")

    def test_quality_and_documentation_changes_preserve_binary_applicability(self) -> None:
        for directory in ("tests", "docs", ".github/workflows"):
            path = self.repository / directory / "quality.txt"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("new verification\n")
        self.commit()
        evidence = self.audit()
        self.assertTrue(evidence["production_source_equivalent"])
        self.assertEqual(len(evidence["changed_paths"]), 3)
        self.assertEqual(evidence["package_source_sha"], self.source)
        self.assertNotEqual(evidence["harness_head_sha"], self.source)

    def test_moving_production_content_into_tests_still_rejects_old_binary(self) -> None:
        (self.repository / "tests").mkdir()
        (self.repository / "src/application.cs").rename(self.repository / "tests/application.cs")
        self.commit()
        with self.assertRaisesRegex(ValueError, "src/application.cs"):
            self.audit()
        evidence = json.loads((self.root / "evidence/source-equivalence.json").read_text())
        self.assertFalse(evidence["production_source_equivalent"])

    def test_build_configuration_changes_reject_old_binary(self) -> None:
        (self.repository / "global.json").write_text('{"sdk":{"version":"10.0.404"}}\n')
        self.commit()
        with self.assertRaisesRegex(ValueError, "global.json"):
            self.audit()

    def test_interpreter_and_unidentified_packages_cannot_supply_aot_evidence(self) -> None:
        for key, value in (("execution_mode", "Interpreter"), ("source_sha", "unknown")):
            original = self.manifest[key]
            self.manifest[key] = value
            self.write_manifest()
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.audit()
            self.manifest[key] = original
