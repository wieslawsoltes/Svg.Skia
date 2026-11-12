from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

from legacy_baseline_comparison import save_report
from legacy_baseline_contracts import (BRIDGE, REPORT_KIND, REPORT_KINDS, WORKLOADS, compare_metrics,
                                       trial_plan, verify_report_identity, verify_runs)
from legacy_baseline_packages import (BASELINE_SHA, FIXTURE_SHA256, PINS, RECEIPT, REPOSITORY,
                                      canonical_digest, digest, extract_verified, inventory,
                                      validate_descriptors, verify_package)
from merge_shards import validate_quality
from quality import (COMPOSITE_INITIALIZED, COMPOSITE_PREFERENCE, COMPOSITE_REUSED,
                     LEGACY_COMPOSITE_REUSED, LEGACY_PREFERENCE, PERSISTENT_INITIALIZED)
from test_legacy_composition import runs as same_candidate_runs


SOURCE = "a" * 40
RUN_ID = 123


def direct_runs(workload="selection", diagnostics=False, candidate_bridge=BRIDGE):
    values = same_candidate_runs(workload=workload, diagnostics=diagnostics)
    if diagnostics:
        values = [run for run in values if run["canvas_bridge"] == BRIDGE]
    for run in values:
        candidate = run["canvas_bridge"] == BRIDGE
        run["role"] = "candidate" if candidate else "baseline"
        run["source_sha"] = SOURCE if candidate else BASELINE_SHA
        if not candidate:
            run.pop("canvas_bridge")
            run.pop("graphite_coverage")
            run["console"] = []
        if diagnostics and workload in {"pan", "zoom", "edit"}:
            run["managed_render_diagnostics"]["after"]["phases"][0]["count"] = 120
            run["console"] = [entry for entry in run["console"] if entry["text"] != LEGACY_COMPOSITE_REUSED]
        if candidate:
            run["canvas_bridge"] = candidate_bridge
            if candidate_bridge == "legacy":
                run["console"] = [{"text": LEGACY_PREFERENCE}]
            elif candidate_bridge == "composite":
                run["console"] = [{"text": message} for message in
                                  (COMPOSITE_PREFERENCE, COMPOSITE_INITIALIZED,
                                   COMPOSITE_REUSED, PERSISTENT_INITIALIZED)]
    return values


def identity(workload="selection"):
    return {"report_kind": REPORT_KIND, "primary_comparison": False, "candidate_default": False,
            "candidate_bridge": BRIDGE, "baseline_source_sha": BASELINE_SHA, "candidate_source_sha": SOURCE,
            "harness_source_sha": "b" * 40, "workload": workload, "repeats": 3, "steps": 120,
            "viewport": {"width": 1440, "height": 960}, "device_scale_factor": 1,
            "expected_primary_runs": 6, "expected_candidate_diagnostics": 1}


class DirectComparisonTests(unittest.TestCase):
    def test_each_explicit_mode_keeps_its_identity_phases_and_original_workload_contract(self):
        self.assertEqual(len(set(REPORT_KINDS.values())), 3)
        for bridge, kind in REPORT_KINDS.items():
            for workload in WORKLOADS:
                report = {**identity(workload), "candidate_bridge": bridge, "report_kind": kind}
                verify_report_identity(report)
                for other in set(REPORT_KINDS) - {bridge}:
                    with self.subTest(bridge=bridge, other=other), self.assertRaises(ValueError):
                        verify_report_identity({**report, "candidate_bridge": other})
                for diagnostics in (False, True):
                    values = direct_runs(workload, diagnostics, bridge)
                    verify_runs(values, SOURCE, workload, diagnostics=diagnostics, candidate_bridge=bridge)
                    target = next(run for run in values if run["role"] == "candidate")
                    target["canvas_bridge"] = next(value for value in REPORT_KINDS if value != bridge)
                    with self.subTest(bridge=bridge, diagnostics=diagnostics), self.assertRaises(ValueError):
                        verify_runs(values, SOURCE, workload, diagnostics=diagnostics, candidate_bridge=bridge)

    def test_original_upload_cannot_claim_a_combined_cache_or_other_upload_path(self):
        for bridge in ("legacy", "composite"):
            values = direct_runs(candidate_bridge=bridge)
            target = next(run for run in values if run["role"] == "candidate")
            target["console"].append({"text": LEGACY_COMPOSITE_REUSED})
            with self.subTest(bridge=bridge), self.assertRaises(AssertionError):
                verify_runs(values, SOURCE, "selection", candidate_bridge=bridge)
        for missing in (COMPOSITE_INITIALIZED, COMPOSITE_REUSED, PERSISTENT_INITIALIZED):
            values = direct_runs(candidate_bridge="composite")
            target = next(run for run in values if run["role"] == "candidate")
            target["console"] = [entry for entry in target["console"] if entry["text"] != missing]
            with self.subTest(missing=missing), self.assertRaises(AssertionError):
                verify_runs(values, SOURCE, "selection", candidate_bridge="composite")
        values = direct_runs(candidate_bridge="legacy")
        next(run for run in values if run["role"] == "candidate")["console"] = []
        with self.assertRaises(AssertionError):
            verify_runs(values, SOURCE, "selection", candidate_bridge="legacy")

    def test_each_retained_feedback_mode_still_requires_zero_document_and_positive_interaction(self):
        for bridge in REPORT_KINDS:
            for workload in ("brush-hover", "selection"):
                for phase, count in (("Document", 1), ("Interaction", 0)):
                    values = direct_runs(workload, True, bridge)
                    next(row for row in values[0]["managed_render_diagnostics"]["after"]["phases"]
                         if row["phase"] == phase)["count"] = count
                    with self.subTest(bridge=bridge, workload=workload, phase=phase), self.assertRaises(AssertionError):
                        verify_runs(values, SOURCE, workload, diagnostics=True, candidate_bridge=bridge)

    def test_summary_names_the_exact_mode_and_excludes_unobserved_legacy_cache_reuse(self):
        for bridge, kind in REPORT_KINDS.items():
            report = {**identity(), "candidate_bridge": bridge, "report_kind": kind,
                      "started_unix": 0, "passed": False, "failure": "Incomplete", "metric_comparisons": []}
            with tempfile.TemporaryDirectory() as temporary:
                save_report(report, Path(temporary))
                summary = (Path(temporary) / "summary.md").read_text()
                self.assertIn(f"`canvasBridge={bridge}`", summary)
                self.assertIn("current candidate default is unchanged and is not measured here", summary)
                self.assertIn("Incomplete", summary)
                if bridge == "legacy":
                    self.assertIn("no combined pixel-cache reuse claim", summary)
                    self.assertNotIn("must also report actual document-pixel cache reuse", summary)

    def test_all_five_workloads_keep_three_pairs_and_separate_candidate_diagnostics(self):
        self.assertEqual(trial_plan(), [(1, "baseline"), (1, "candidate"), (2, "candidate"),
                                       (2, "baseline"), (3, "baseline"), (3, "candidate")])
        for workload in WORKLOADS:
            verify_report_identity(identity(workload))
            for diagnostics in (False, True):
                original = direct_runs(workload, diagnostics)
                verify_runs(original, SOURCE, workload, diagnostics=diagnostics)
                for defect in ("missing", "source", "role", "bridge", "input", "tail", "callbacks", "submit",
                               "warmup", "provider", "canvas", "dpr", "errors", "instrumentation"):
                    changed = copy.deepcopy(original)
                    target = changed[0]
                    if defect == "missing":
                        changed.pop()
                    elif defect in {"source", "role", "bridge", "errors"}:
                        key, value = {"source": ("source_sha", "f" * 40), "role": ("role", "reference"),
                                      "bridge": ("canvas_bridge", "persistent"), "errors": ("page_errors", ["failure"])}[defect]
                        target[key] = value
                    elif defect in {"input", "tail", "callbacks", "submit"}:
                        key, value = {"input": ("input_events", 119), "tail": ("inputs_without_completed_render_callback", 1),
                                      "callbacks": ("completed_render_callbacks", 0), "submit": ("webgpu_canvas_submissions", 0)}[defect]
                        target["metrics"][key] = value
                    elif defect == "warmup":
                        target["common_warmup"]["completed_graphics_callbacks"] = 59
                    elif defect == "provider":
                        target["renderer"]["negotiations"].clear()
                    elif defect == "canvas":
                        target["renderer"]["application_contexts"][0]["target"] = False
                    elif defect == "dpr":
                        target["environment"]["device_pixel_ratio"] = 2
                    elif diagnostics:
                        del target["managed_render_diagnostics"]
                    else:
                        target["runtime_phase_logs"] = []
                    with self.subTest(workload=workload, diagnostics=diagnostics, defect=defect), self.assertRaises((ValueError, AssertionError)):
                        verify_runs(changed, SOURCE, workload, diagnostics=diagnostics)
            for changed in (list(reversed(direct_runs(workload))), direct_runs(workload, True)):
                with self.assertRaises(ValueError):
                    verify_runs(changed, SOURCE, workload)

    def test_reuse_and_phase_expectations_follow_the_actual_workload(self):
        for workload in WORKLOADS:
            candidate = direct_runs(workload, True)
            if workload in {"brush-hover", "selection"}:
                candidate[0]["console"] = [entry for entry in candidate[0]["console"] if entry["text"] != LEGACY_COMPOSITE_REUSED]
                with self.assertRaises(AssertionError):
                    verify_runs(candidate, SOURCE, workload, diagnostics=True)
                for phase, count in (("Document", 1), ("Interaction", 0)):
                    changed = direct_runs(workload, True)
                    next(row for row in changed[0]["managed_render_diagnostics"]["after"]["phases"] if row["phase"] == phase)["count"] = count
                    with self.assertRaises(AssertionError):
                        verify_runs(changed, SOURCE, workload, diagnostics=True)
            else:
                verify_runs(candidate, SOURCE, workload, diagnostics=True)
                candidate[0]["managed_render_diagnostics"]["after"]["phases"][0]["count"] = 0
                with self.assertRaises(ValueError):
                    verify_runs(candidate, SOURCE, workload, diagnostics=True)
        changed = direct_runs()
        changed[0]["console"] = [{"text": PERSISTENT_INITIALIZED}]
        with self.assertRaises(ValueError):
            verify_runs(changed, SOURCE, "selection")

    def test_report_and_metric_names_cannot_become_current_default_evidence(self):
        original = identity()
        for key, value in (("report_kind", "same_candidate_bridge_experiment"), ("primary_comparison", True),
                           ("candidate_default", True), ("candidate_bridge", "persistent"), ("baseline_source_sha", SOURCE),
                           ("candidate_source_sha", BASELINE_SHA), ("expected_candidate_diagnostics", 2),
                           ("expected_primary_runs", 4), ("steps", 60), ("repeats", 2)):
            with self.subTest(key=key), self.assertRaises(ValueError):
                verify_report_identity({**original, key: value})
        rows = compare_metrics(direct_runs())
        self.assertEqual(rows[0]["medians"], {"baseline": 6000, "candidate": 4000})
        self.assertAlmostEqual(rows[0]["opt_in_candidate_over_baseline"], 2 / 3)
        self.assertNotIn("candidate_over_baseline", rows[0])
        for invalid in (direct_runs()[:-1], direct_runs(diagnostics=True), list(reversed(direct_runs()))):
            with self.assertRaises(ValueError):
                compare_metrics(invalid)
        changed = direct_runs()
        changed[0]["metrics"]["elapsed_ms"] = float("nan")
        with self.assertRaises(ValueError):
            compare_metrics(changed)

    def test_selection_requires_visible_feedback_and_restored_document_and_rulers(self):
        for role in ("baseline", "candidate"):
            for defect in ("no_selection", "minimum", "document", "ruler"):
                changed = direct_runs()
                visual = next(run for run in changed if run["role"] == role)["quality"]
                if defect == "no_selection":
                    visual["changed_document_pixels"] = 49
                elif defect == "minimum":
                    visual["minimum_changed_pixels"] = 0
                elif defect == "document":
                    visual["restored"]["changed_pixel_fraction"] = 0.0011
                else:
                    visual["restored"]["ruler_changed_pixel_fraction"]["vertical"] = float("nan")
                with self.subTest(role=role, defect=defect), self.assertRaises(ValueError):
                    verify_runs(changed, SOURCE, "selection")

    def test_original_document_ruler_font_gates_and_all_nine_states_remain_required(self):
        state = {"changed_pixel_fraction": 0, "ruler_changed_pixel_fraction": {"horizontal": 0, "vertical": 0},
                 "chrome_font_changed_pixel_fraction": {"menu_labels": 0, "toolbar_icons": 0, "panel_icons": 0}}
        original = [{"backend": "webgpu", "workload": "selection", "trial": trial,
                     **{name: copy.deepcopy(state) for name in ("before", "after", "restored")}} for trial in range(1, 4)]
        validate_quality(original, "webgpu", "selection", 3)
        for state_name in ("before", "after", "restored"):
            for group, key, value in ((None, "changed_pixel_fraction", 0.0011),
                                     ("ruler_changed_pixel_fraction", "horizontal", 0.0011),
                                     ("chrome_font_changed_pixel_fraction", "menu_labels", 0.0201)):
                changed = copy.deepcopy(original)
                target = changed[0][state_name] if group is None else changed[0][state_name][group]
                target[key] = value
                with self.assertRaises(ValueError):
                    validate_quality(changed, "webgpu", "selection", 3)
        with self.assertRaises(ValueError):
            validate_quality(original[:-1], "webgpu", "selection", 3)

    def test_summary_and_reusable_workflow_keep_package_reuse_automatic_and_explicit(self):
        with tempfile.TemporaryDirectory() as temporary:
            report = {**identity(), "started_unix": 0, "passed": False, "failure": "Native gate failed",
                      "metric_comparisons": []}
            save_report(report, Path(temporary))
            summary = (Path(temporary) / "summary.md").read_text()
            self.assertIn("current candidate default is unchanged and is not measured here", summary)
            self.assertIn("Native gate failed", summary)
        root = Path(__file__).resolve().parents[2]
        workflow = (root / ".github/workflows/unodraw-legacy-baseline-comparison.yml").read_text()
        caller = (root / ".github/workflows/unodraw-performance.yml").read_text().split(
            "\n  direct-legacy-baseline-comparison:\n", 1)[1]
        self.assertIn("workflow_call:", workflow)
        self.assertIn("fromJSON('[\"brush-hover\",\"selection\",\"pan\",\"zoom\",\"edit\"]')", workflow)
        self.assertIn("fromJSON('[\"brush-hover\",\"selection\"]')", workflow)
        self.assertIn("default: legacy-composite", workflow)
        self.assertIn("name: unodraw-baseline-${{ inputs.candidate_bridge }}-${{ matrix.workload }}", workflow)
        self.assertIn('--candidate-bridge "$CANDIDATE_BRIDGE"', workflow)
        self.assertIn("needs: [plan, publish]", caller)
        self.assertIn("uses: ./.github/workflows/unodraw-legacy-baseline-comparison.yml", caller)
        self.assertIn("package_run_id: ${{ format('{0}', github.run_id) }}", caller)
        self.assertNotIn("dotnet publish", workflow)


class DirectPackageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.run = {"id": RUN_ID, "path": ".github/workflows/unodraw-performance.yml", "head_sha": SOURCE,
                    "repository": {"full_name": REPOSITORY}}
        self.manifests, self.artifacts = {}, []
        for index, role in enumerate(("baseline", "candidate"), 1):
            directory = self.root / role
            directory.mkdir()
            index_bytes = ("<!doctype html>" + role).encode()
            manifest = {"schema": 2, "role": role, "source_sha": BASELINE_SHA if role == "baseline" else SOURCE,
                        "fixture_sha256": FIXTURE_SHA256, "configuration": "Release", "execution_mode": "InterpreterAndAOT",
                        "runtime_generation": "uno7", "uno_sdk": "7.0.5", "dotnet_sdk": "10.0.403",
                        "dotnet_workload_version": "10.0.403", "index_sha256": hashlib.sha256(index_bytes).hexdigest(),
                        "font_assets_sha256": {}, "supports_render_diagnostics": role == "candidate"}
            self.manifests[role] = manifest
            archive = directory / "artifact.zip"
            with zipfile.ZipFile(archive, "w") as compressed:
                compressed.writestr("benchmark-package.json", json.dumps(manifest))
                compressed.writestr("wwwroot/index.html", index_bytes)
                compressed.writestr("wwwroot/runtime.js", "original-runtime")
            self.artifacts.append({"id": index, "name": f"unodraw-performance-package-{role}",
                                   "size_in_bytes": archive.stat().st_size, "digest": "sha256:" + digest(archive),
                                   "expired": False, "workflow_run": {"id": RUN_ID, "head_sha": SOURCE}})
            extract_verified(archive, directory, digest(archive))
        self.descriptors = validate_descriptors(RUN_ID, self.run, self.artifacts)
        for role in ("baseline", "candidate"):
            directory = self.root / role
            receipt = {"schema": 1, "descriptor": self.descriptors, "role": role,
                       "manifest_sha256": canonical_digest(self.manifests[role]), "wwwroot_sha256": inventory(directory / "wwwroot")}
            (directory / RECEIPT).write_text(json.dumps(receipt))
        fixture_pin = patch.dict(PINS["baseline"], {"manifest_sha256": canonical_digest(self.manifests["baseline"])})
        fixture_pin.start()
        self.addCleanup(fixture_pin.stop)

    def test_live_metadata_must_identify_the_original_workflow_and_both_unique_artifacts(self):
        for key, value in (("id", 456), ("path", ".github/workflows/other.yml"), ("head_sha", "HEAD"),
                           ("repository", {"full_name": "other/repository"})):
            with self.subTest(key=key), self.assertRaises(ValueError):
                validate_descriptors(RUN_ID, {**self.run, key: value}, self.artifacts)
        for key, value in (("digest", ""), ("expired", True), ("size_in_bytes", 0),
                           ("workflow_run", {"id": RUN_ID + 1, "head_sha": SOURCE})):
            changed = copy.deepcopy(self.artifacts)
            changed[0][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                validate_descriptors(RUN_ID, self.run, changed)
        for changed in (self.artifacts[:1], self.artifacts + [copy.deepcopy(self.artifacts[0])]):
            with self.assertRaises(ValueError):
                validate_descriptors(RUN_ID, self.run, changed)

    def test_both_real_archive_inventories_and_source_manifests_are_bound_to_live_descriptors(self):
        for role in ("baseline", "candidate"):
            value = verify_package(self.root / role, role, self.descriptors)
            self.assertEqual(value["source_sha"], self.manifests[role]["source_sha"])
        path = self.root / "candidate/benchmark-package.json"
        changed = dict(self.manifests["candidate"], source_sha="f" * 40)
        path.write_text(json.dumps(changed))
        with self.assertRaises(ValueError):
            verify_package(self.root / "candidate", "candidate", self.descriptors)

    def test_even_a_correctly_hashed_candidate_archive_must_match_the_required_build_and_fixture(self):
        directory = self.root / "candidate"
        for key, value in (("source_sha", "HEAD"), ("fixture_sha256", "f" * 64),
                           ("execution_mode", "Interpreter"), ("configuration", "Debug"),
                           ("supports_render_diagnostics", False), ("dotnet_workload_version", "other")):
            manifest = {**self.manifests["candidate"], key: value}
            (directory / "benchmark-package.json").write_text(json.dumps(manifest))
            with zipfile.ZipFile(directory / "artifact.zip", "w") as compressed:
                compressed.writestr("benchmark-package.json", json.dumps(manifest))
                for path in (directory / "wwwroot").iterdir():
                    compressed.write(path, "wwwroot/" + path.name)
            descriptors = copy.deepcopy(self.descriptors)
            descriptors["artifacts"]["candidate"].update({"digest": "sha256:" + digest(directory / "artifact.zip"),
                                                         "size_in_bytes": (directory / "artifact.zip").stat().st_size})
            receipt = {"schema": 1, "descriptor": descriptors, "role": "candidate",
                       "manifest_sha256": canonical_digest(manifest), "wwwroot_sha256": inventory(directory / "wwwroot")}
            (directory / RECEIPT).write_text(json.dumps(receipt))
            with self.subTest(key=key), self.assertRaises(ValueError):
                verify_package(directory, "candidate", descriptors)

    def test_coordinated_served_asset_and_receipt_tampering_cannot_change_archive_bound_bytes(self):
        directory = self.root / "candidate"
        (directory / "wwwroot/runtime.js").write_text("changed-runtime")
        receipt = json.loads((directory / RECEIPT).read_text())
        receipt["wwwroot_sha256"] = inventory(directory / "wwwroot")
        (directory / RECEIPT).write_text(json.dumps(receipt))
        with self.assertRaises(ValueError):
            verify_package(directory, "candidate", self.descriptors)
        with zipfile.ZipFile(directory / "artifact.zip", "a") as compressed:
            compressed.writestr("wwwroot/added.js", "modified archive")
        receipt["descriptor"]["artifacts"]["candidate"]["digest"] = "sha256:" + digest(directory / "artifact.zip")
        (directory / RECEIPT).write_text(json.dumps(receipt))
        with self.assertRaises(ValueError):
            verify_package(directory, "candidate", self.descriptors)


if __name__ == "__main__":
    unittest.main()
