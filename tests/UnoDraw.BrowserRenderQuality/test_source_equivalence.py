from __future__ import annotations

import json
import subprocess
import sys
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

    def run_cli(self, *options: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(Path(__file__).with_name("verify_source.py")),
             "--repository", str(self.repository), "--package", str(self.package),
             "--output", str(self.root / "evidence"), "--package-run-id", "123",
             "--github-output", str(self.root / "github-output"), *options],
            capture_output=True, text=True)

    def change_production(self) -> None:
        (self.repository / "src/application.cs").write_text("class Application { public int Value; }\n")
        self.commit()

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

    def test_report_only_writes_stale_proof_without_waiving_default_verification(self) -> None:
        self.change_production()
        evidence = verify_source(self.repository, self.package, self.root / "evidence", "123",
                                 report_only=True)
        self.assertIs(evidence["production_source_equivalent"], False)
        self.assertEqual(evidence["production_changes"], ["src/application.cs"])
        self.assertEqual(evidence["harness_head_sha"], self.git("rev-parse", "HEAD").strip())
        self.assertEqual(evidence, json.loads((self.root / "evidence/source-equivalence.json").read_text()))
        summary = (self.root / "evidence/summary.md").read_text()
        self.assertIn("Stale artifact; fresh pipeline required.", summary)
        self.assertIn("quality, performance, diagnostic and aggregate jobs must be skipped", summary)
        self.assertIn("does not claim a quality or performance pass", summary)
        with self.assertRaisesRegex(ValueError, "src/application.cs"):
            self.audit()

    def test_cli_report_only_exports_false_and_keeps_the_full_stale_evidence(self) -> None:
        self.change_production()
        result = self.run_cli("--report-only")
        self.assertEqual(result.returncode, 0, result.stderr)
        evidence = json.loads(result.stdout)
        self.assertIs(evidence["production_source_equivalent"], False)
        self.assertEqual(evidence["package_source_sha"], self.source)
        self.assertEqual(evidence["production_changes"], ["src/application.cs"])
        self.assertEqual((self.root / "github-output").read_text(), "eligible=false\n")
        self.assertFalse((self.root / "evidence/report.json").exists())

    def test_cli_default_fails_stale_manual_or_worker_requests_but_preserves_proof(self) -> None:
        self.change_production()
        result = self.run_cli()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Published candidate differs in production source", result.stderr)
        self.assertEqual((self.root / "github-output").read_text(), "eligible=false\n")
        evidence = json.loads((self.root / "evidence/source-equivalence.json").read_text())
        self.assertIs(evidence["production_source_equivalent"], False)
        self.assertIn("Stale artifact; fresh pipeline required.",
                      (self.root / "evidence/summary.md").read_text())

    def test_cli_equivalent_head_runs_in_both_modes_and_appends_outputs(self) -> None:
        path = self.repository / "tests/quality.py"
        path.parent.mkdir()
        path.write_text("# improved quality harness\n")
        self.commit()
        output = self.root / "github-output"
        output.write_text("prior=preserved\n")
        for options in ((), ("--report-only",)):
            with self.subTest(options=options):
                result = self.run_cli(*options)
                self.assertEqual(result.returncode, 0, result.stderr)
                evidence = json.loads(result.stdout)
                self.assertIs(evidence["production_source_equivalent"], True)
                self.assertEqual(evidence["production_changes"], [])
                self.assertEqual(evidence["changed_paths"], ["tests/quality.py"])
        self.assertEqual(output.read_text(), "prior=preserved\neligible=true\neligible=true\n")
        summary = (self.root / "evidence/summary.md").read_text()
        self.assertIn("Cached artifact is eligible for verification.", summary)
        self.assertIn("does not claim a quality or performance pass", summary)

    def test_cli_report_only_does_not_suppress_invalid_manifests(self) -> None:
        for key, value in (("role", "baseline"), ("configuration", "Debug"),
                           ("execution_mode", "Interpreter"), ("source_sha", "unknown")):
            original = self.manifest[key]
            self.manifest[key] = value
            self.write_manifest()
            with self.subTest(key=key):
                result = self.run_cli("--report-only")
                self.assertNotEqual(result.returncode, 0)
                self.assertFalse((self.root / "github-output").exists())
                self.assertFalse((self.root / "evidence/source-equivalence.json").exists())
            self.manifest[key] = original

    def test_cli_report_only_cannot_classify_an_unavailable_source_as_eligible(self) -> None:
        self.manifest["source_sha"] = "0" * 40
        self.write_manifest()
        result = self.run_cli("--report-only")
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.root / "github-output").exists())
        self.assertFalse((self.root / "evidence/source-equivalence.json").exists())

    def test_interpreter_and_unidentified_packages_cannot_supply_aot_evidence(self) -> None:
        for key, value in (("execution_mode", "Interpreter"), ("source_sha", "unknown")):
            original = self.manifest[key]
            self.manifest[key] = value
            self.write_manifest()
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.audit()
            self.manifest[key] = original
