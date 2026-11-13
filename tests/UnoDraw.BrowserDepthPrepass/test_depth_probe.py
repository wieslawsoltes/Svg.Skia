import copy
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

import yaml

from depth_probe import (ADAPTER_FILES, FLAG, MODES, PINNED_ARTIFACT, PINNED_INVENTORY,
                         PINNED_MANIFEST, PINNED_PACKAGE_RUN, PINNED_SOURCE, PREFIX,
                         ObservedBrowser, ObservedContext, adapter_identity,
                         configure, snapshot_harness, source_inventory, trial_plan,
                         verify_depth_observation, verify_package_pin, verify_source_pins)


CONFIG = (b"let config = {};\n"
          b"config.environmentVariables = config.environmentVariables || {};\n"
          b"export { config };\n")


class ConfigurationTests(unittest.TestCase):
    def test_each_arm_retains_prefix_and_sets_exactly_one_startup_flag(self):
        for mode, value in zip(MODES, ("0", "1")):
            with self.subTest(mode=mode):
                served, proof = configure(CONFIG, mode)
                self.assertEqual(served[:len(CONFIG)], CONFIG)
                self.assertEqual(proof["environment_variables"], {FLAG: value})
                self.assertEqual(proof["proof_message"], PREFIX + json.dumps({FLAG: value}, sort_keys=True))
                self.assertEqual(served.count(FLAG.encode()), 2)
                self.assertFalse(proof["primary_comparison"])
                self.assertTrue(proof["served_package_modified"])
                self.assertNotEqual(proof["original_sha256"], proof["served_sha256"])

    def test_changed_or_preconfigured_modules_are_rejected(self):
        for data in (CONFIG.replace(b"let config", b"const config"), CONFIG + b"unknown();",
                     CONFIG + CONFIG, CONFIG.replace(b"export { config };", b"export default config;"),
                     CONFIG.replace(b"export", b"// " + FLAG.encode() + b"\nexport"),
                     CONFIG.replace(b"export", b"// UNO_WEBGPU_STATS\nexport"),
                     CONFIG.replace(b"export", b"// UNO_LOG_FRAME_PHASES\nexport"),
                     CONFIG.replace(b"config.environmentVariables = config.environmentVariables || {};", b"")):
            with self.subTest(data=data), self.assertRaises(ValueError):
                configure(data, MODES[0])

    def test_unknown_arm_is_rejected(self):
        with self.assertRaises(ValueError):
            configure(CONFIG, "auto")

    def test_three_pairs_keep_alternating_order_and_full_coverage(self):
        self.assertEqual(trial_plan(), [(1, MODES[0]), (1, MODES[1]), (2, MODES[1]),
                                       (2, MODES[0]), (3, MODES[0]), (3, MODES[1])])


class ActivationTests(unittest.TestCase):
    def test_both_actual_descriptor_states_are_accepted(self):
        for mode, depth in zip(MODES, (7, 0)):
            verify_depth_observation({"initialized": True, "renderPasses": 10,
                "depthPasses": depth, "colorOnlyPasses": 10 - depth, "unknownPasses": 0}, mode)

    def test_wrong_arm_unknown_or_missing_native_work_is_rejected(self):
        valid = {"initialized": True, "renderPasses": 10, "depthPasses": 7,
                 "colorOnlyPasses": 3, "unknownPasses": 0}
        cases = [("initialized", False), ("renderPasses", 0), ("depthPasses", 0),
                 ("colorOnlyPasses", 0), ("unknownPasses", 1), ("renderPasses", 11),
                 ("renderPasses", True), ("depthPasses", -1), ("depthPasses", 7.0)]
        for key, value in cases:
            snapshot = copy.deepcopy(valid)
            snapshot[key] = value
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                verify_depth_observation(snapshot, MODES[0])
        with self.assertRaises(ValueError):
            verify_depth_observation(valid, MODES[1])
        with self.assertRaises(ValueError):
            verify_depth_observation(valid, "unknown")

    def test_native_prepass_statistics_are_not_activation_evidence(self):
        with self.assertRaises(ValueError):
            verify_depth_observation({"prepass": 0}, MODES[1])


class AdapterTests(unittest.TestCase):
    def test_context_options_and_native_page_are_preserved(self):
        calls = []
        page = SimpleNamespace(evaluate=lambda code: {"initialized": True})
        real = SimpleNamespace(add_init_script=lambda **kw: calls.append(("script", kw)),
                               new_page=lambda: page, close=lambda: calls.append(("close", {})),
                               arbitrary="delegated")
        def new_context(**options):
            calls.append(("context", options))
            return real
        observed = ObservedBrowser(SimpleNamespace(new_context=new_context))
        context = observed.new_context(viewport={"width": 1440, "height": 960}, device_scale_factor=1)
        self.assertEqual(calls[0][1], {"viewport": {"width": 1440, "height": 960}, "device_scale_factor": 1})
        self.assertEqual(context.arbitrary, "delegated")
        self.assertIs(context.new_page(), page)
        context.close()
        self.assertEqual(observed.observations, [{"initialized": True}])
        self.assertEqual(calls[-1][0], "close")

    def test_snapshot_error_closes_context_and_cannot_look_like_success(self):
        calls = []
        def fail(_):
            raise RuntimeError("page closed")
        context = ObservedContext(SimpleNamespace(close=lambda: calls.append("closed")), [])
        context.page = SimpleNamespace(evaluate=fail)
        context.close()
        self.assertEqual(calls, ["closed"])
        self.assertIn("failure", context.observations[0])
        with self.assertRaises(ValueError):
            verify_depth_observation(context.observations[0], MODES[0])


class HarnessSnapshotTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.repository = self.root / "repository"
        self.repository.mkdir()
        self.git("init", "-q")
        for name in ("UnoDraw.BrowserPerformance/performance.py", "UnoDraw.BrowserPerformance/instrumentation.js",
                     "UnoDraw.BrowserSmoke/smoke.py", "UnoDraw.BrowserSmoke/backend.py"):
            path = self.repository / "tests" / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("committed source\n")
        self.git("add", ".")
        self.git("-c", "user.name=Probe Test", "-c", "user.email=probe@example.invalid", "commit", "-qm", "fixture")
        self.commit = self.git("rev-parse", "HEAD").strip()

    def git(self, *args):
        return subprocess.check_output(["git", *args], cwd=self.repository, text=True, stderr=subprocess.PIPE)

    def test_only_committed_bytes_are_importable_even_when_worktree_changes(self):
        relative = "tests/UnoDraw.BrowserPerformance/performance.py"
        (self.repository / relative).write_text("uncommitted replacement\n")
        (self.repository / "tests/UnoDraw.BrowserPerformance/shadow.py").write_text("untracked source\n")
        target = self.root / "snapshot"
        inventory = snapshot_harness(self.repository, self.commit, target)
        self.assertEqual((target / relative).read_text(), "committed source\n")
        self.assertNotIn("tests/UnoDraw.BrowserPerformance/shadow.py", inventory)
        self.assertEqual(source_inventory(target), inventory)
        (target / relative).write_text("changed during run\n")
        self.assertNotEqual(source_inventory(target), inventory)

    def test_short_sha_and_reused_snapshot_are_rejected(self):
        with self.assertRaises(ValueError):
            snapshot_harness(self.repository, self.commit[:8], self.root / "short")
        target = self.root / "snapshot"
        snapshot_harness(self.repository, self.commit, target)
        with self.assertRaises(ValueError):
            snapshot_harness(self.repository, self.commit, target)

    def test_committed_source_symlink_is_rejected(self):
        path = self.repository / "tests/UnoDraw.BrowserPerformance/indirect.py"
        path.symlink_to("performance.py")
        self.git("add", ".")
        self.git("-c", "user.name=Probe Test", "-c", "user.email=probe@example.invalid", "commit", "-qm", "symlink")
        with self.assertRaises(ValueError):
            snapshot_harness(self.repository, self.git("rev-parse", "HEAD").strip(), self.root / "snapshot")

    def test_adapter_checkout_is_recorded_separately_and_changed_bytes_reject(self):
        for name in ADAPTER_FILES:
            path = self.repository / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("committed diagnostic adapter\n")
        self.git("add", ".")
        self.git("-c", "user.name=Probe Test", "-c", "user.email=probe@example.invalid", "commit", "-qm", "adapter")
        proof = adapter_identity(self.repository)
        self.assertEqual(proof["source_sha"], self.git("rev-parse", "HEAD").strip())
        self.assertEqual(set(proof["files_sha256"]), set(ADAPTER_FILES))
        self.assertFalse(proof["application_source_equivalence_claimed"])
        (self.repository / ADAPTER_FILES[0]).write_text("uncommitted replacement\n")
        with self.assertRaises(ValueError):
            adapter_identity(self.repository)


class ImmutablePinsTests(unittest.TestCase):
    def test_only_the_exact_application_primary_harness_and_mode_are_admitted(self):
        verify_source_pins(PINNED_SOURCE, PINNED_SOURCE, "single-canvas")
        for values in (("0" * 40, PINNED_SOURCE, "single-canvas"),
                       (PINNED_SOURCE, "0" * 40, "single-canvas"),
                       (PINNED_SOURCE, PINNED_SOURCE, "persistent")):
            with self.subTest(values=values), self.assertRaises(ValueError):
                verify_source_pins(*values)

    def test_republished_or_changed_package_identity_is_rejected(self):
        # This tests the extra immutable pins; the original candidate-package
        # verifier separately checks the complete manifest, ZIP and inventory.
        receipt = {"workflow_run_id": PINNED_PACKAGE_RUN, "workflow_head_sha": PINNED_SOURCE,
                   "package_source_sha": PINNED_SOURCE, "artifact": copy.deepcopy(PINNED_ARTIFACT),
                   "manifest_sha256": PINNED_MANIFEST, "wwwroot_inventory_sha256": PINNED_INVENTORY,
                   "served_file_count": 907}
        verify_package_pin(receipt)
        for key in receipt:
            changed = copy.deepcopy(receipt)
            changed[key] = None
            with self.subTest(key=key), self.assertRaises(ValueError):
                verify_package_pin(changed)
        for key in PINNED_ARTIFACT:
            changed = copy.deepcopy(receipt)
            changed["artifact"][key] = None
            with self.subTest(artifact_key=key), self.assertRaises(ValueError):
                verify_package_pin(changed)


class WorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.path = Path(__file__).resolve().parents[2] / ".github/workflows/unodraw-depth-prepass.yml"
        cls.text = cls.path.read_text()
        # BaseLoader preserves GitHub's `on` key without YAML 1.1 bool coercion.
        cls.workflow = yaml.load(cls.text, Loader=yaml.BaseLoader)
        cls.request = yaml.load(cls.path.with_name("unodraw-depth-prepass-request.yml").read_text(),
                                Loader=yaml.BaseLoader)

    def test_automatic_scope_permissions_and_artifact_namespace_are_isolated(self):
        self.assertEqual(set(self.workflow["on"]), {"workflow_call", "workflow_dispatch"})
        self.assertEqual(self.request["on"], {"pull_request": {"types": ["labeled"]}})
        self.assertEqual(self.request["permissions"], {"contents": "read", "actions": "read"})
        self.assertEqual(self.request["jobs"], {"requested-probe": {
            "if": "github.event.label.name == 'unodraw-depth-prepass'",
            "uses": "./.github/workflows/unodraw-depth-prepass.yml"}})
        self.assertEqual(self.workflow["permissions"], {"contents": "read", "actions": "read"})
        self.assertEqual(set(self.workflow["jobs"]), {"contracts", "compare"})
        self.assertEqual(self.workflow["jobs"]["compare"]["needs"], "contracts")
        uploads = [step for step in self.workflow["jobs"]["compare"]["steps"]
                   if step.get("uses") == "actions/upload-artifact@v4"]
        self.assertEqual(len(uploads), 1)
        self.assertEqual(uploads[0]["with"]["name"], "unodraw-depth-prepass-single-canvas-selection")
        self.assertEqual(uploads[0]["if"], "always()")
        self.assertNotIn("dotnet", self.text)
        self.assertNotIn("verify_source.py", self.text)

    def test_acquisition_and_execution_use_only_pinned_inputs(self):
        environment = self.workflow["env"]
        self.assertEqual(environment["PRIMARY_HARNESS_SHA"], PINNED_SOURCE)
        self.assertEqual(environment["PACKAGE_SOURCE_SHA"], PINNED_SOURCE)
        self.assertEqual(environment["PACKAGE_RUN_ID"], str(PINNED_PACKAGE_RUN))
        self.assertEqual(environment["PYTHONDONTWRITEBYTECODE"], "1")
        steps = self.workflow["jobs"]["compare"]["steps"]
        extract = next(i for i, step in enumerate(steps) if "git archive" in step.get("run", ""))
        acquire = next(i for i, step in enumerate(steps) if "candidate_package.py" in step.get("run", ""))
        install = next(i for i, step in enumerate(steps) if "requirements.txt" in step.get("run", ""))
        execute = next(i for i, step in enumerate(steps) if "depth_probe.py" in step.get("run", ""))
        self.assertLess(extract, acquire)
        self.assertLess(acquire, install)
        self.assertLess(install, execute)
        self.assertIn('git archive --format=tar "$PRIMARY_HARNESS_SHA"', steps[extract]["run"])
        self.assertIn("artifacts/pinned-acquisition-harness/tests/UnoDraw.BrowserPerformance/candidate_package.py", steps[acquire]["run"])
        self.assertIn("artifacts/pinned-acquisition-harness/tests/UnoDraw.BrowserSmoke/requirements.txt", steps[install]["run"])
        self.assertIn('--harness-sha "$PRIMARY_HARNESS_SHA"', steps[execute]["run"])
        self.assertIn('--expected-source "$PACKAGE_SOURCE_SHA" --bridge single-canvas', steps[execute]["run"])
        self.assertNotIn("--steps", steps[execute]["run"])
        self.assertNotIn("--repeats", steps[execute]["run"])


if __name__ == "__main__":
    unittest.main()
