from __future__ import annotations

import copy
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from PIL import Image, ImageDraw

from merge_svg_shards import (AGGREGATE_KIND, SHARD_KIND, canonical_digest, identity, merge,
                              package_receipt, qualify_result, safe_file, source_proof, validate_case, validate_header)
from svg_bridge_quality import compare_cases, digest, source_hashes
from svg_fixture_contracts import (DESKTOP, DPRS, SCENES, STATES, anchor_colors, inspect_scene,
                                   load_fixtures, matrix_dprs, matrix_failures, probes,
                                   require_source_readback, scene_bridges)
from backend import launch_arguments, verify_backend
from quality import LEGACY_COMPOSITE_INITIALIZED, LEGACY_COMPOSITE_PREFERENCE, LEGACY_PREFERENCE
from verify_source import ALLOWED_PREFIXES
from candidate_package import ARTIFACT_NAME, RECEIPT_KIND, WORKFLOW_PATH
from pinned_package import FIXTURE_SHA256, REPOSITORY


class SvgShardContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest, cls.fixtures = load_fixtures()
        cls.hashes = source_hashes()
        cls.harness = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=Path(__file__).parent, text=True).strip()

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)

    def report(self, dpr=1, candidate="legacy-composite", coverage=None):
        package = {"schema": 2, "role": "candidate", "source_sha": "a" * 40, "runtime_generation": "uno7",
                   "configuration": "Release", "execution_mode": "InterpreterAndAOT", "dotnet_sdk": "10.0.403",
                   "dotnet_workload_version": "10.0.403", "index_sha256": "b" * 64,
                   "uno_sdk": "7.0.5", "fixture_sha256": FIXTURE_SHA256, "supports_render_diagnostics": True,
                   "font_assets_sha256": {"fonts/original.ttf": "c" * 64}, "path": "/original/package"}
        cases = [{"scene": scene, "bridge": bridge, "dpr": dpr, "passed": True,
                  "source_edit": {"semantic_body_verified": True, "exact_inserted_readback": True, "reopened_after_apply": True},
                  "states": [{"name": state} for state in STATES]}
                 for bridge in scene_bridges(candidate) for scene in SCENES]
        rows = [{"scene": scene, "dpr": dpr, "state": state, "passed": True} for scene in SCENES for state in STATES]
        return {"schema": 1, "report_kind": SHARD_KIND, "full_matrix": False, "full_matrix_passed": False,
                "package_verified_before_browser": True, "package_verified_after_browser": True,
                "passed": True, "failures": [], "candidate_bridge": candidate, "bridges": list(scene_bridges(candidate)),
                "graphite_coverage": coverage, "device_pixel_ratios": [dpr], "expected_session_count": 2,
                "expected_case_count": 16, "expected_comparison_count": 24, "expected_scenes": list(SCENES),
                "expected_states": list(STATES), "fixture_manifest": copy.deepcopy(self.manifest),
                "test_file_sha256": self.hashes, "harness_source_sha": self.harness,
                "browser_launch_arguments": launch_arguments("webgpu"), "browser_version": "143.0.7499.4",
                "execution_mode": "InterpreterAndAOT", "package": package, "cases": cases, "comparisons": rows,
                "graphics": "SwiftShader browser graphics API correctness; not hardware performance"}

    def proof(self):
        return {"package_run_id": "12345", "package_source_sha": "a" * 40, "harness_head_sha": self.harness,
                "allowed_difference_prefixes": list(ALLOWED_PREFIXES),
                "changed_paths": ["tests/UnoDraw.BrowserRenderQuality/merge_svg_shards.py"], "production_changes": [],
                "production_source_equivalent": True, "execution_mode": "InterpreterAndAOT"}

    def receipt(self, report):
        manifest = {key: value for key, value in report["package"].items() if key != "path"}
        files = {"index.html": manifest["index_sha256"], **manifest["font_assets_sha256"]}
        return {"schema": 1, "kind": RECEIPT_KIND, "repository": REPOSITORY, "workflow_run_id": 12345,
                "workflow_path": WORKFLOW_PATH, "workflow_head_sha": "d" * 40, "package_source_sha": manifest["source_sha"],
                "artifact": {"id": 67890, "name": ARTIFACT_NAME, "archive_sha256": "e" * 64, "archive_bytes": 100},
                "manifest": manifest, "manifest_sha256": canonical_digest(manifest), "wwwroot_sha256": files,
                "wwwroot_inventory_sha256": canonical_digest(files), "served_file_count": len(files), "verified": True}

    def write_shard(self, root, report):
        self.write_json(root / "source-equivalence.json", self.proof())
        self.write_json(root / "package-artifact.json", self.receipt(report))
        report["package_artifact_sha256"] = digest(root / "package-artifact.json")
        self.write_json(root / "report.json", report)

    @staticmethod
    def write_json(path, value):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")

    def native_case(self, root, report, fixture, bridge):
        dpr = report["device_pixel_ratios"][0]
        relative = f"dpr-{dpr}/{bridge}/{fixture.name}"
        directory = root / relative
        directory.mkdir(parents=True, exist_ok=True)
        for name, source in (("canonical.svg", fixture.source), ("browser-input.svg", fixture.browser_source),
                             ("source-inserted.svg", fixture.browser_source), ("source-committed.svg", fixture.browser_source)):
            (directory / name).write_text(source, encoding="utf-8")
        source_edit = {**require_source_readback(fixture.browser_source, fixture), "exact_inserted_readback": True,
                       "reopened_after_apply": True, "input_sha256": fixture.browser_sha256, "input_tag": "TEXTAREA"}
        messages = ([LEGACY_PREFERENCE] if bridge == "legacy" else [LEGACY_COMPOSITE_PREFERENCE, LEGACY_COMPOSITE_INITIALIZED])
        console = [{"type": "info", "text": "Graphics backend 'WebGpuGraphicsProvider' won negotiation on context kind 'WebGpu'.", "location": {}}]
        console += [{"type": "info", "text": text, "location": {}} for text in messages]
        capabilities = {"secure_context": True, "api_available": True, "adapter_available": True}
        renderer = {**verify_backend(console, capabilities, "webgpu"), "application_contexts": [
            {"kind": "webgpu", "connected": True, "target": True, "width": round(1440 * dpr), "height": round(960 * dpr),
             "css_width": 1440, "css_height": 960, "draw_calls": 0, "presentations": 20}]}
        case = {"scene": fixture.name, "bridge": bridge, "dpr": dpr, "passed": True, "graphite_coverage": None,
                "source_sha": report["package"]["source_sha"], "execution_mode": report["execution_mode"],
                "browser_version": report["browser_version"], "canonical_sha256": fixture.sha256,
                "input_sha256": fixture.browser_sha256, "source_edit": source_edit, "console": console,
                "page_errors": [], "resource_errors": [], "renderer": renderer,
                "environment": {"viewport": DESKTOP, "device_pixel_ratio": dpr, "cross_origin_isolated": False,
                                "user_agent": "HeadlessChrome/143.0.7499.4", "browser_fonts": []}, "states": []}
        for name, (origin_x, origin_y, scale) in zip(STATES, ((180, 240, 4), (217.25, 221.5, 4), (210, 210, 4.46))):
            origin_x, origin_y, scale = origin_x * dpr, origin_y * dpr, scale * dpr
            image = Image.new("RGB", (round(1440 * dpr), round(960 * dpr)), (30, 30, 30))
            draw = ImageDraw.Draw(image)

            def rectangle(x, y, width, height, color):
                draw.rectangle((round(origin_x + x * scale), round(origin_y + y * scale),
                                round(origin_x + (x + width) * scale) - 1,
                                round(origin_y + (y + height) * scale) - 1), fill=color)

            rectangle(0, 0, 200, 140, (255, 255, 255))
            for stripe in range(8):
                rectangle(20 * stripe, 0, 20, 110, (30 + 25 * stripe, 120 + 10 * stripe, 40 + 3 * stripe))
            for y, color in zip((4, 128), anchor_colors(fixture.name)):
                rectangle(170, y, 24, 8, color)
            for probe in probes():
                rectangle(probe["x"], probe["y"], probe["width"], probe["height"], probe["expected"])
            raw, body = directory / (name + ".png"), directory / (name + "-body.png")
            image.save(raw)
            readiness = inspect_scene(raw, fixture, dpr)
            image.crop(readiness["body_physical_bounds"]).save(body)
            case["states"].append({"name": name, "scene": fixture.name, "viewport": DESKTOP, "dpr": dpr,
                                   "raw": raw.name, "body_raw": body.name, "readiness": readiness})
        self.write_json(directory / "report.json", case)
        case["artifact_directory"] = relative
        return case

    def update_case(self, root, case):
        self.write_json(root / case["artifact_directory"] / "report.json",
                        {key: value for key, value in case.items() if key != "artifact_directory"})

    def test_only_full_or_one_device_scale_can_be_requested(self):
        self.assertEqual(matrix_dprs(), DPRS)
        self.assertEqual(matrix_dprs((1.0,)), (1,))
        for invalid in ((), (True,), (1, 2), (1, 1), (2, 1.25, 1), (float("nan"),), (float("inf"),), (1.5,), "1"):
            with self.subTest(value=invalid), self.assertRaises(ValueError):
                matrix_dprs(invalid)
        report = self.report()
        self.assertEqual(matrix_failures(report["cases"], report["comparisons"], candidate_bridge="legacy-composite", dprs=(1,)), [])
        self.assertTrue(matrix_failures(report["cases"], report["comparisons"], candidate_bridge="legacy-composite"))
        self.assertEqual(len(compare_cases([], self.fixtures, self.directory, candidate_bridge="legacy-composite", dprs=(1,))), 24)
        self.assertEqual(len(compare_cases([], self.fixtures, self.directory, candidate_bridge="legacy-composite")), 72)

    def test_partial_failed_or_relabelled_reports_cannot_claim_complete_shards(self):
        report = self.report()
        self.assertEqual(validate_header(report, self.manifest, self.hashes, self.harness), 1)
        mutations = [lambda r: r.update(report_kind="svg_document_quality"), lambda r: r.update(full_matrix=True),
                     lambda r: r.update(full_matrix_passed=True), lambda r: r.update(passed=False),
                     lambda r: r.update(failures=["cancelled"]), lambda r: r.update(device_pixel_ratios=[1, 1.25]),
                     lambda r: r.update(package_verified_before_browser=False), lambda r: r.update(package_verified_after_browser=False),
                     lambda r: r.update(expected_case_count=48), lambda r: r["cases"].pop(),
                     lambda r: r["cases"].append(copy.deepcopy(r["cases"][0])), lambda r: r["comparisons"].pop(),
                     lambda r: r["comparisons"].append(copy.deepcopy(r["comparisons"][0])),
                     lambda r: r["cases"][0]["states"].pop(), lambda r: r["cases"][0].update(passed=False),
                     lambda r: r["comparisons"][0].update(passed=False)]
        for mutate in mutations:
            changed = copy.deepcopy(report)
            mutate(changed)
            with self.subTest(mutation=mutate), self.assertRaises(ValueError):
                validate_header(changed, self.manifest, self.hashes, self.harness)

    def test_mode_fixture_browser_and_harness_identity_are_not_interchangeable(self):
        report = self.report()
        mutations = [lambda r: r.update(bridges=["persistent", "legacy-composite"]),
                     lambda r: r.update(candidate_bridge="webgl"), lambda r: r.update(graphite_coverage="raster"),
                     lambda r: r.update(harness_source_sha="b" * 40), lambda r: r.update(test_file_sha256={}),
                     lambda r: r.update(fixture_manifest={}), lambda r: r.update(browser_version=""),
                     lambda r: r["browser_launch_arguments"].append("--disable-webgl"),
                     lambda r: r["package"].update(source_sha="wrong"),
                     lambda r: r["package"].update(execution_mode="Interpreter"),
                     lambda r: r["package"].update(font_assets_sha256={})]
        for mutate in mutations:
            changed = copy.deepcopy(report)
            mutate(changed)
            with self.subTest(mutation=mutate), self.assertRaises(ValueError):
                validate_header(changed, self.manifest, self.hashes, self.harness)
        graphite = self.report(candidate="graphite", coverage="raster")
        self.assertEqual(validate_header(graphite, self.manifest, self.hashes, self.harness), 1)
        self.assertNotEqual(identity(report, self.proof(), None), identity(graphite, self.proof(), None))
        for key, value in (("index_sha256", "d" * 64), ("dotnet_sdk", "different"), ("font_assets_sha256", {"fonts/other.ttf": "c" * 64})):
            changed = copy.deepcopy(report)
            changed["package"][key] = value
            self.assertNotEqual(identity(report, self.proof(), None), identity(changed, self.proof(), None))
        changed = copy.deepcopy(report)
        changed["package"]["path"] = "/other/runner/package"
        self.assertEqual(identity(report, self.proof(), None), identity(changed, self.proof(), None))

    def test_source_proof_cannot_hide_a_different_harness_or_production_change(self):
        report = self.report()
        for patch in ({"harness_head_sha": "b" * 40}, {"package_source_sha": "c" * 40}, {"production_source_equivalent": False},
                      {"production_changes": ["samples/UnoDraw/MainPage.xaml"]}, {"changed_paths": ["samples/UnoDraw/MainPage.xaml"]},
                      {"changed_paths": ["tests/../../samples/UnoDraw/MainPage.xaml"]}, {"allowed_difference_prefixes": [""]},
                      {"package_run_id": ""}):
            self.write_json(self.directory / "source-equivalence.json", {**self.proof(), **patch})
            with self.subTest(patch=patch), self.assertRaises(ValueError):
                source_proof(self.directory, report)
        self.write_json(self.directory / "source-equivalence.json", self.proof())
        self.assertEqual(source_proof(self.directory, report), self.proof())

    def test_receipt_binds_the_complete_manifest_source_run_and_original_bytes(self):
        report = self.report()
        self.write_shard(self.directory, report)
        expected = self.receipt(report)
        self.assertEqual(package_receipt(self.directory, report, self.proof()), expected)
        for change in ({"package_run_id": "12346"}, {"package_source_sha": "f" * 40}):
            with self.subTest(proof=change), self.assertRaisesRegex(ValueError, "immutable package proof"):
                package_receipt(self.directory, report, {**self.proof(), **change})
        changed = copy.deepcopy(report)
        changed["package"]["source_global_json"] = {"unrecorded": "override"}
        with self.assertRaisesRegex(ValueError, "immutable package proof"):
            package_receipt(self.directory, changed, self.proof())
        (self.directory / "package-artifact.json").write_text(json.dumps(expected, separators=(",", ":")), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "retained receipt digest"):
            package_receipt(self.directory, report, self.proof())

    def test_missing_duplicate_and_mixed_shards_fail_without_full_matrix_claims(self):
        shards = self.directory / "shards"
        shards.mkdir()
        result = merge(shards, self.directory / "empty-aggregate")
        self.assertFalse(result["passed"])
        self.assertFalse(result["full_matrix"])
        for index in range(3):
            root = shards / str(index)
            self.write_shard(root, self.report())
        result = merge(shards, self.directory / "duplicate-aggregate")
        self.assertFalse(result["full_matrix_passed"])
        self.assertIn("duplicated", result["failures"][0])
        for index, dpr in enumerate(DPRS):
            report = self.report(dpr)
            if index == 2:
                report["package"]["index_sha256"] = "e" * 64
            self.write_shard(shards / str(index), report)
        result = merge(shards, self.directory / "mixed-aggregate")
        self.assertFalse(result["passed"])
        self.assertIn("differs across shards", result["failures"][0])
        self.assertTrue((self.directory / "mixed-aggregate/shards/2/report.json").is_file())

    def test_case_recomputes_source_native_pixels_camera_and_body_crop(self):
        report = self.report(1.25)
        fixture = self.fixtures[0]
        case = self.native_case(self.directory, report, fixture, "legacy-composite")
        validated, environment = validate_case(case, report, self.directory, fixture, None)
        self.assertEqual(len(validated["native_recalculation"]["states"]), 3)
        self.assertEqual(environment["browser_fonts"], [])
        raw = self.directory / case["artifact_directory"] / case["states"][0]["raw"]
        original = raw.read_bytes()
        with Image.open(raw) as image:
            draw = ImageDraw.Draw(image)
            draw.rectangle(case["states"][0]["readiness"]["body_physical_bounds"], fill=(30, 30, 30))
            image.save(raw)
        with self.assertRaisesRegex(AssertionError, "body is blank/incomplete"):
            validate_case(case, report, self.directory, fixture, None)
        raw.write_bytes(original)
        committed = self.directory / case["artifact_directory"] / "source-committed.svg"
        committed.write_text(self.fixtures[1].browser_source, encoding="utf-8")
        with self.assertRaises(AssertionError):
            validate_case(case, report, self.directory, fixture, None)
        committed.write_text(fixture.browser_source, encoding="utf-8")
        changed = copy.deepcopy(case)
        changed["states"][1]["readiness"] = copy.deepcopy(changed["states"][0]["readiness"])
        self.update_case(self.directory, changed)
        with self.assertRaisesRegex(ValueError, "actual PNG"):
            validate_case(changed, report, self.directory, fixture, None)
        self.update_case(self.directory, case)
        body = self.directory / case["artifact_directory"] / case["states"][0]["body_raw"]
        with Image.open(body) as image:
            image.putpixel((0, 0), (255, 0, 255))
            image.save(body)
        with self.assertRaisesRegex(ValueError, "exact crop"):
            validate_case(case, report, self.directory, fixture, None)

    def test_case_cannot_borrow_renderer_success_or_a_different_font_environment(self):
        report = self.report()
        fixture = self.fixtures[0]
        case = self.native_case(self.directory, report, fixture, "legacy-composite")
        _, environment = validate_case(case, report, self.directory, fixture, None)
        for mutation in (lambda c: c["renderer"]["application_contexts"][0].update(presentations=0),
                         lambda c: c["renderer"]["application_contexts"][0].update(connected=False),
                         lambda c: c["console"].pop(),
                         lambda c: c["environment"]["browser_fonts"].append({"family": "Substitute"}),
                         lambda c: c["page_errors"].append("runtime failure"),
                         lambda c: c["states"].reverse()):
            changed = copy.deepcopy(case)
            mutation(changed)
            self.update_case(self.directory, changed)
            with self.subTest(mutation=mutation), self.assertRaises((AssertionError, ValueError)):
                validate_case(changed, report, self.directory, fixture, environment)

    def test_artifact_paths_and_output_cannot_escape_or_overwrite_inputs(self):
        (self.directory / "evidence.json").write_text("{}")
        for invalid in ("../evidence.json", "/evidence.json", "./evidence.json", "other/../evidence.json", "other\\evidence.json"):
            with self.subTest(path=invalid), self.assertRaises(ValueError):
                safe_file(self.directory, invalid)
        (self.directory / "link.json").symlink_to(self.directory / "evidence.json")
        with self.assertRaisesRegex(ValueError, "symbolic"):
            safe_file(self.directory, "link.json")
        root = self.directory / "shards"
        root.mkdir()
        for output in (root, root / "child", self.directory):
            with self.subTest(output=output), self.assertRaises(ValueError):
                merge(root, output)

    def test_complete_aggregate_reopens_all_144_native_states_and_72_pairs(self):
        shards = self.directory / "shards"
        for dpr in DPRS:
            root = shards / f"artifact-dpr-{dpr}"
            report = self.report(dpr)
            report["cases"] = [self.native_case(root, report, fixture, bridge)
                               for bridge in scene_bridges("legacy-composite") for fixture in self.fixtures]
            report["comparisons"] = compare_cases(report["cases"], self.fixtures, root,
                                                   candidate_bridge="legacy-composite", dprs=(dpr,))
            self.write_shard(root, report)
        result = merge(shards, self.directory / "aggregate", shard_jobs_result="success")
        self.assertTrue(result["passed"], result["failures"])
        self.assertEqual(result["report_kind"], AGGREGATE_KIND)
        self.assertTrue(result["full_matrix"] and result["full_matrix_passed"])
        self.assertEqual((len(result["cases"]), result["native_recalculation"]["state_count"], len(result["comparisons"])), (48, 144, 72))
        self.assertEqual(result["package_manifest_sha256"], canonical_digest(result["package"]))
        for case in result["cases"]:
            self.assertTrue((self.directory / "aggregate" / case["artifact_directory"] / case["states"][0]["raw"]).is_file())
        self.assertTrue(all(row["scene_body_changed_pixel_fraction"] == 0 for row in result["comparisons"]))
        # These outcomes can occur after the browser saved passing reports. They
        # must preserve all recomputed evidence without qualifying the matrix.
        for status in ("failure", "cancelled", "skipped", "unknown"):
            changed = copy.deepcopy(result)
            changed["shard_jobs_result"] = status
            qualify_result(changed)
            with self.subTest(job_result=status):
                self.assertFalse(changed["passed"] or changed["full_matrix"] or changed["full_matrix_passed"])
                self.assertEqual(changed["native_recalculation"]["state_count"], 144)
                self.assertIn(status, changed["failures"][0])

        # Coordinating an altered image with its new readiness/body crop and all
        # passing case flags must still fail the original cross-mode comparison.
        root = shards / "artifact-dpr-1"
        report = json.loads((root / "report.json").read_text())
        case = next(case for case in report["cases"] if case["scene"] == "gradients" and case["bridge"] == "legacy-composite")
        state = case["states"][0]
        raw = root / case["artifact_directory"] / state["raw"]
        with Image.open(raw) as image:
            ImageDraw.Draw(image).rectangle((230, 280, 330, 380), fill=(240, 30, 120))
            image.save(raw)
        state["readiness"] = inspect_scene(raw, self.fixtures[0], 1)
        with Image.open(raw) as image:
            image.crop(state["readiness"]["body_physical_bounds"]).save(root / case["artifact_directory"] / state["body_raw"])
        self.update_case(root, case)
        self.write_json(root / "report.json", report)
        rejected = merge(shards, self.directory / "altered-image-aggregate", shard_jobs_result="success")
        self.assertFalse(rejected["full_matrix_passed"])
        self.assertIn("Native-resolution bridge pixels differ", rejected["failures"][0])


if __name__ == "__main__":
    unittest.main()
